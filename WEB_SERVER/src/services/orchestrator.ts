import { execFile } from "child_process";
import { promisify } from "util";
import http from "node:http";
import https from "node:https";
import net from "node:net";
import dgram from "node:dgram";
import { URL } from "node:url";
import { resolveContainerRuntime, resolveRoomDockerImage } from "./containerRuntime";
import prisma from "../models/prismaClient";

const execFileAsync = promisify(execFile);

export type DsMode = "IDLE" | "MATCH";

export type DockerContainerInfo = {
  id: string;
  name: string;
  createdAt?: string;
  labels: {
    app: string;
    region: string;
    mode: DsMode;
    typeMatchGid: string;
    matchId?: string;
    sessionName?: string;
  };
};

type StartDsParams =
  | {
      mode: "IDLE";
      region: string;
      typeMatchGid: number;
    }
  | {
      mode: "MATCH";
      region: string;
      typeMatchGid: number;
      matchId: string;
      sessionName: string;
      maxPlayers: number;
      realPlayerCount: number;
      bet: number;
      maxRound?: number;
      characterSelectionsCsv?: string;
      botCharacterModelIdsCsv?: string;
    };

function safeName(s: string) {
  return s.replace(/[^a-zA-Z0-9_.-]/g, "_");
}

function stripIgnorableDockerStderr(stderr: string) {
  const lines = stderr
    .split("\n")
    .map((l) => l.trim())
    .filter(Boolean);

  return lines.join("\n");
}

function parseLabelString(rawLabels: string) {
  const normalized = rawLabels
    .trim()
    .replace(/^map\[/, "")
    .replace(/\]$/, "");

  if (!normalized) {
    return {};
  }

  return normalized
    .split(",")
    .map((entry) => entry.trim())
    .filter(Boolean)
    .reduce<Record<string, string>>((acc, entry) => {
      const [key, ...valueParts] = entry.split("=");
      if (!key) return acc;
      const value = valueParts.join("=");
      acc[key.trim()] = value.trim();
      return acc;
    }, {});
}

const DS_PORT_RANGE_START = Number(process.env.DS_PORT_START ?? process.env.ROOM_PORT_START) || 27200;
const DS_PORT_RANGE_END = Number(process.env.DS_PORT_END ?? process.env.ROOM_PORT_END) || 27299;
const DS_CONTAINER_PORT = Number(process.env.DS_CONTAINER_PORT) || 27015;

function getFirstConfiguredEnv(...names: string[]) {
  for (const name of names) {
    const value = process.env[name]?.trim();
    if (value) return value;
  }

  return "";
}

let portAllocationLock: Promise<void> = Promise.resolve();

async function withPortAllocationLock<T>(fn: () => Promise<T>): Promise<T> {
  let release: () => void;

  const ready = new Promise<void>((resolve) => {
    release = resolve;
  });

  const previous = portAllocationLock;
  portAllocationLock = portAllocationLock.then(() => ready);

  await previous;

  try {
    return await fn();
  } finally {
    release!();
  }
}

async function isPortAvailable(port: number): Promise<boolean> {
  return new Promise((resolve) => {
    const tcpTester = net.createServer();
    const udpTester = dgram.createSocket("udp4");

    let tcpReady = false;
    let udpReady = false;
    let resolved = false;

    const tryResolve = () => {
      if (resolved) return;
      if (tcpReady && udpReady) {
        resolved = true;
        resolve(true);
      }
    };

    const fail = () => {
      if (resolved) return;
      resolved = true;
      tcpTester.close();
      udpTester.close();
      resolve(false);
    };

    tcpTester.once("error", fail).once("listening", () => {
      tcpReady = true;
      tcpTester.close(() => tryResolve());
    });

    udpTester.once("error", fail).once("listening", () => {
      udpReady = true;
      udpTester.close(() => tryResolve());
    });

    tcpTester.listen(port, "0.0.0.0");
    udpTester.bind(port, "0.0.0.0");
  });
}

async function isPortFreeInPool(port: number): Promise<boolean> {
  const existing = await prisma.serverPortPool.findUnique({ where: { portNo: port } });
  return !existing;
}

async function reserveHostPort(): Promise<number> {
  return withPortAllocationLock(async () => {
    for (let port = DS_PORT_RANGE_START; port <= DS_PORT_RANGE_END; port += 1) {
      // eslint-disable-next-line no-await-in-loop
      const portIsFree = await isPortAvailable(port);
      if (!portIsFree) {
        continue;
      }
      // eslint-disable-next-line no-await-in-loop
      const poolIsFree = await isPortFreeInPool(port);
      if (poolIsFree) {
        return port;
      }
    }

    throw new Error(`NO_AVAILABLE_DS_PORT in range ${DS_PORT_RANGE_START}-${DS_PORT_RANGE_END}`);
  });
}

function postJson(urlStr: string, payload: any, timeoutMs = 5000): Promise<{ ok: boolean; status: number; body: string }> {
  const u = new URL(urlStr);
  const data = Buffer.from(JSON.stringify(payload), "utf8");
  const lib = u.protocol === "https:" ? https : http;

  return new Promise((resolve, reject) => {
    const req = lib.request(
      {
        method: "POST",
        hostname: u.hostname,
        port: u.port ? Number(u.port) : u.protocol === "https:" ? 443 : 80,
        path: u.pathname + u.search,
        headers: {
          "Content-Type": "application/json",
          "Content-Length": data.length,
        },
        timeout: timeoutMs,
      },
      (res) => {
        let body = "";
        res.setEncoding("utf8");
        res.on("data", (chunk) => (body += chunk));
        res.on("end", () =>
          resolve({
            ok: (res.statusCode ?? 0) >= 200 && (res.statusCode ?? 0) < 300,
            status: res.statusCode ?? 0,
            body,
          }),
        );
      },
    );

    req.on("timeout", () => req.destroy(new Error("HTTP_TIMEOUT")));
    req.on("error", reject);
    req.write(data);
    req.end();
  });
}

function isDnsResolutionError(err: unknown): boolean {
  if (!err) return false;
  const anyErr = err as { code?: string; message?: string };
  return anyErr.code === "ENOTFOUND" || anyErr.code === "EAI_AGAIN" || /getaddrinfo/i.test(anyErr.message ?? "");
}

function isTransientAssignError(err: unknown): boolean {
  if (!err) return false;
  const anyErr = err as { code?: string; message?: string };
  const msg = `${anyErr.message ?? ""}`.toLowerCase();
  const code = `${anyErr.code ?? ""}`.toUpperCase();
  return (
    code === "ECONNRESET" ||
    code === "ECONNREFUSED" ||
    code === "ECONNABORTED" ||
    code === "EPIPE" ||
    msg.includes("socket hang up") ||
    msg.includes("http_timeout") ||
    msg.includes("timeout")
  );
}

function sleep(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/** Check if a container is still running (not exited/dead). */
async function isContainerRunning(dockerBin: string, containerName: string): Promise<boolean> {
  try {
    const { stdout } = await execFileAsync(
      dockerBin,
      ["inspect", "-f", "{{.State.Running}}", containerName],
      { timeout: 5_000 },
    );
    return (stdout || "").trim() === "true";
  } catch {
    return false;
  }
}

/** Quick HTTP GET health probe – resolves true if the DS internal HTTP server responds. */
async function probeHttp(url: string, timeoutMs = 3000): Promise<boolean> {
  return new Promise((resolve) => {
    const u = new URL(url);
    const lib = u.protocol === "https:" ? https : http;
    const req = lib.get(
      {
        hostname: u.hostname,
        port: u.port ? Number(u.port) : 80,
        path: "/health",
        timeout: timeoutMs,
      },
      (res) => {
        res.resume();
        // Any response (even 404) means the server is listening
        resolve(true);
      },
    );
    req.on("timeout", () => { req.destroy(); resolve(false); });
    req.on("error", () => resolve(false));
  });
}

/** Fetch last N lines of a container's logs (best-effort, never throws). */
async function fetchContainerLogsSafe(dockerBin: string, containerIdOrName: string, tail = 80): Promise<string | null> {
  try {
    const { stdout, stderr } = await execFileAsync(
      dockerBin,
      ["logs", "--tail", String(tail), containerIdOrName],
      { timeout: 10_000 },
    );
    return ((stdout || "") + "\n" + (stderr || "")).trim() || null;
  } catch {
    return null;
  }
}

/** Resolve the container's IP address via docker inspect. Returns null if not found. */
async function getContainerIp(dockerBin: string, containerName: string): Promise<string | null> {
  try {
    const { stdout } = await execFileAsync(
      dockerBin,
      ["inspect", "-f", "{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}", containerName],
      { timeout: 10_000 },
    );
    return (stdout || "").trim().split(/\s+/).find(Boolean) || null;
  } catch {
    return null;
  }
}

/** Resolve the host port mapped to DS_CONTAINER_PORT/udp for this container. */
async function getContainerHostPort(dockerBin: string, containerName: string): Promise<number | null> {
  const containerPort = `${DS_CONTAINER_PORT}/udp`;
  try {
    const { stdout } = await execFileAsync(
      dockerBin,
      ["inspect", "-f", `{{ (index (index .NetworkSettings.Ports "${containerPort}") 0).HostPort }}`, containerName],
      { timeout: 10_000 },
    );
    const hostPortStr = (stdout || "").trim();
    const hostPort = Number(hostPortStr);
    if (Number.isFinite(hostPort) && hostPort > 0) return hostPort;
    return null;
  } catch {
    return null;
  }
}

const DS_AUTO_REMOVE = (process.env.DS_AUTO_REMOVE ?? "false").toLowerCase() === "true";

export class DockerOrchestrator {
  // DS callback READY/RESULT về backend theo URL này
  private static BACKEND_URL = process.env.BACKEND_URL || "http://backend:3000";

  // Backend và DS phải cùng network để gọi bằng container name
  private static DOCKER_NETWORK = process.env.DOCKER_NETWORK || "paper-legends-net";

  // Port HTTP nội bộ DS (IDLE nhận assign). DS phải listen port này trong container.
  private static DS_INTERNAL_HTTP_PORT = Number(process.env.DS_INTERNAL_HTTP_PORT) || 8080;

  private static FUSION_PUBLIC_IP = getFirstConfiguredEnv(
    "FUSION_PUBLIC_IP",
    "GAME_SERVER_PUBLIC_IP",
    "SERVER_PUBLIC_IP",
    "PUBLIC_IP",
    "HOST_IP",
  );

  // Linux: nếu BACKEND_URL dùng host.docker.internal thì cần map host
  private static ADD_HOST_LINUX = process.env.DOCKER_ADD_HOST || ""; // ví dụ "host.docker.internal:172.17.0.1"

  // label để list/cleanup
  private static APP_LABEL = process.env.DS_APP_LABEL || "paper-legends-ds";
  private static PROJECT_LABEL = process.env.DS_PROJECT_LABEL || "com.paperlegends.project=true";

  static async startDedicatedServer(p: StartDsParams) {
    const dockerBin = await resolveContainerRuntime();
    const roomImage = await resolveRoomDockerImage(dockerBin);
    const hostPort = await reserveHostPort();
    const name =
      p.mode === "IDLE"
        ? `ds_idle_${safeName(p.region)}_${p.typeMatchGid}_${Date.now()}`
        : `ds_match_${safeName(p.matchId)}_${Date.now()}`;

    const env: string[] = [
      `MODE=${p.mode}`,
      `REGION=${p.region}`,
      `TYPE_MATCH_GID=${p.typeMatchGid}`,
      `BACKEND_URL=${this.BACKEND_URL}`,
      `DS_INTERNAL_HTTP_PORT=${this.DS_INTERNAL_HTTP_PORT}`,
      `DS_ID=${name}`,
      `SERVER_PORT=${DS_CONTAINER_PORT}`,
      `HOST_PORT=${hostPort}`,
      ...(this.FUSION_PUBLIC_IP ? [`FUSION_PUBLIC_IP=${this.FUSION_PUBLIC_IP}`] : []),
      ...(process.env.JOIN_SECRET ? [`JOIN_SECRET=${process.env.JOIN_SECRET}`] : []),
      ...(process.env.TOKEN_ID ? [`TOKEN_ID=${process.env.TOKEN_ID}`] : []),
    ];

    if (p.mode === "MATCH") {
      env.push(
        `MATCH_ID=${p.matchId}`,
        `SESSION_NAME=${p.sessionName}`,
        `MAX_PLAYERS=${p.maxPlayers}`,
        `REAL_PLAYER_COUNT=${p.realPlayerCount}`,
        `BET=${p.bet}`,
        `MAX_ROUND=${p.maxRound ?? 0}`,
        ...(p.characterSelectionsCsv ? [`PAPER_LEGENDS_CHARACTER_SELECTIONS=${p.characterSelectionsCsv}`] : []),
        ...(p.botCharacterModelIdsCsv ? [`PAPER_LEGENDS_BOT_CHARACTER_MODEL_IDS=${p.botCharacterModelIdsCsv}`] : []),
      );
    }

    const labels: string[] = [
      "--label",
      `app=${this.APP_LABEL}`,
      "--label",
      this.PROJECT_LABEL,
      "--label",
      `region=${p.region}`,
      "--label",
      `mode=${p.mode}`,
      "--label",
      `typeMatchGid=${p.typeMatchGid}`,
    ];

    if (p.mode === "MATCH") {
      labels.push("--label", `matchId=${p.matchId}`, "--label", `sessionName=${p.sessionName}`);
    }

    const baseArgs: string[] = [
      "run",
      "-d",
      ...(DS_AUTO_REMOVE ? ["--rm"] : []),
      "--name",
      name,
      ...labels,
      ...env.flatMap((e) => ["-e", e]),
    ];

    const buildArgs = (useNetwork: boolean) => {
      const args = [...baseArgs];
      if (useNetwork && this.DOCKER_NETWORK) args.push("--network", this.DOCKER_NETWORK);
      if (this.ADD_HOST_LINUX) args.push("--add-host", this.ADD_HOST_LINUX);
      args.push("-p", `${hostPort}:${DS_CONTAINER_PORT}/udp`);

      // Publish UDP port ra host để client truy cập (mapping đến port nội bộ 27015).
      args.push(roomImage);
      return args;
    };

    const runContainer = async (useNetwork: boolean) => {
      // Remove stale container with the same name (stopped but not removed)
      try {
        await execFileAsync(dockerBin, ["rm", "-f", name], { timeout: 15_000 });
        console.info("Removed stale container before re-create", { name });
      } catch {
        // Container doesn't exist – nothing to remove, safe to continue
      }

      console.info('Running docker run', { name, useNetwork });
      const { stdout, stderr } = await execFileAsync(dockerBin, buildArgs(useNetwork), { timeout: 60_000 });
      const nonIgnorable = stripIgnorableDockerStderr(stderr ?? "");
      if (nonIgnorable) throw new Error(`Docker start error: ${nonIgnorable}`);

      const containerId = (stdout || "").trim();
      if (!containerId) throw new Error("Docker start error: missing container id");

      console.info('Docker run success', { containerId, name, hostPort });
      await sleep(1200);
      const running = await isContainerRunning(dockerBin, name);
      if (!running) {
        const logs = await fetchContainerLogsSafe(dockerBin, name, 120);
        throw new Error(
          `Docker start error: container exited after start. name=${name} id=${containerId}${logs ? `\nLogs:\n${logs}` : ""}`,
        );
      }
      return { containerId, name, hostPort };
    };

    try {
      return await runContainer(true);
    } catch (e: any) {
      const errorText = `${e?.message ?? ""} ${e?.stderr ?? ""}`.trim();
      if (this.DOCKER_NETWORK && /network .* not found/i.test(errorText)) {
        return await runContainer(false);
      }
      const msg = e?.message ? String(e.message) : "Unknown";
      if (msg.includes("ENOENT") || msg.includes("not found")) throw new Error("DOCKER_NOT_AVAILABLE");
      throw e;
    }
  }

  static async spawnMatchContainer(p: {
    region: string;
    typeMatchGid: number;
    matchId: string;
    sessionName: string;
    maxPlayers: number;
    realPlayerCount: number;
    bet: number;
    maxRound?: number;
    characterSelectionsCsv?: string;
    botCharacterModelIdsCsv?: string;
  }) {
    return this.startDedicatedServer({
      mode: "MATCH",
      region: p.region,
      typeMatchGid: p.typeMatchGid,
      matchId: p.matchId,
      sessionName: p.sessionName,
      maxPlayers: p.maxPlayers,
      realPlayerCount: p.realPlayerCount,
      bet: p.bet,
      maxRound: p.maxRound,
      characterSelectionsCsv: p.characterSelectionsCsv,
      botCharacterModelIdsCsv: p.botCharacterModelIdsCsv,
    });
  }

  // Warm pool đúng nghĩa: assign match vào container IDLE đang chạy sẵn
  static async assignToIdleDs(p: {
    dsContainerName: string; // container NAME để gọi nội bộ
    matchId: string;
    sessionName: string;
    maxPlayers: number;
    realPlayerCount: number;
    bet: number;
    maxRound?: number;
    region: string;
    typeMatchGid: number;
    characterSelectionsCsv?: string;
    botCharacterModelIdsCsv?: string;
  }): Promise<{ ok: true; hostPort?: number | null; containerIp?: string | null }> {
    const assignUrl = `http://${p.dsContainerName}:${this.DS_INTERNAL_HTTP_PORT}/internal/assign`;

    const payload = {
      matchId: p.matchId,
      sessionName: p.sessionName,
      maxPlayers: p.maxPlayers,
      realPlayerCount: p.realPlayerCount,
      bet: p.bet,
      maxRound: p.maxRound ?? 0,
      region: p.region,
      typeMatchGid: p.typeMatchGid,
      characterSelectionsCsv: p.characterSelectionsCsv ?? "",
      botCharacterModelIdsCsv: p.botCharacterModelIdsCsv ?? "",
    };

    const maxAttempts = 6;
    let lastError: unknown = null;

    // Pre-flight: verify container is still running before wasting retry cycles
    const dockerBin = await resolveContainerRuntime();
    const running = await isContainerRunning(dockerBin, p.dsContainerName);
    if (!running) {
      console.warn('IDLE DS container is not running, skipping assign', { dsContainerName: p.dsContainerName, matchId: p.matchId });
      const logs = await fetchContainerLogsSafe(dockerBin, p.dsContainerName);
      if (logs) console.warn('Dead IDLE DS logs (last lines):', { dsContainerName: p.dsContainerName, logs: logs.slice(-2000) });
      throw new Error('IDLE_CONTAINER_NOT_RUNNING');
    }

    // Resolve container IP – always prefer IP over DNS name because Docker network
    // may not be available (container started with useNetwork=false).
    const containerIp = await getContainerIp(dockerBin, p.dsContainerName);
    const hostPort = await getContainerHostPort(dockerBin, p.dsContainerName);
    const effectiveHost = containerIp || p.dsContainerName;
    const effectiveAssignUrl = `http://${effectiveHost}:${this.DS_INTERNAL_HTTP_PORT}/internal/assign`;

    console.info('Resolved IDLE DS address', {
      dsContainerName: p.dsContainerName,
      containerIp,
      hostPort,
      effectiveHost,
      matchId: p.matchId,
    });

    // Pre-flight: quick HTTP probe to check if DS internal server is actually listening
    const probeUrl = `http://${effectiveHost}:${this.DS_INTERNAL_HTTP_PORT}`;
    const healthy = await probeHttp(probeUrl, 3000);
    if (!healthy) {
      console.warn('IDLE DS HTTP server not responding, skipping assign', { dsContainerName: p.dsContainerName, matchId: p.matchId, probeUrl, effectiveHost });
      const logs = await fetchContainerLogsSafe(dockerBin, p.dsContainerName);
      if (logs) console.warn('Unresponsive IDLE DS logs (last lines):', { dsContainerName: p.dsContainerName, logs: logs.slice(-2000) });
      throw new Error('IDLE_CONTAINER_NOT_HEALTHY');
    }

    for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
      console.info('Assigning to idle DS', {
        dsContainerName: p.dsContainerName,
        matchId: p.matchId,
        url: effectiveAssignUrl,
        attempt,
        maxAttempts,
      });

      try {
        const resp = await postJson(effectiveAssignUrl, payload, 5000);
        if (!resp.ok) {
          console.error('Assign to idle DS failed', { dsContainerName: p.dsContainerName, effectiveHost, status: resp.status, body: resp.body, attempt });
          throw new Error(`ASSIGN_FAILED status=${resp.status} body=${resp.body}`);
        }

        console.info('Assign to idle DS succeeded', { dsContainerName: p.dsContainerName, matchId: p.matchId, effectiveHost, attempt });
        return { ok: true, hostPort, containerIp };
      } catch (err) {
        lastError = err;

        if (attempt < maxAttempts) {
          const retryDelayMs = Math.min(1000 * attempt, 4000);
          await sleep(retryDelayMs);
          continue;
        }
      }
    }

    throw lastError ?? new Error('ASSIGN_FAILED_UNKNOWN');
  }

  static async tryStopContainerById(containerIdOrName: string) {
    try {
      const dockerBin = await resolveContainerRuntime();
      await execFileAsync(dockerBin, ["stop", containerIdOrName], { timeout: 10_000 });
      return { ok: true };
    } catch {
      return { ok: false };
    }
  }

  /** Remove exited/dead containers managed by this app (those not cleaned by --rm). */
  static async cleanupDeadContainers() {
    try {
      const dockerBin = await resolveContainerRuntime();
      const { stdout } = await execFileAsync(
        dockerBin,
        [
          "ps", "-a",
          "--filter", `label=app=${this.APP_LABEL}`,
          "--filter", "status=exited",
          "--filter", "status=dead",
          "--format", "{{.ID}} {{.Names}}",
        ],
        { timeout: 15_000 },
      );

      const lines = (stdout || "").trim().split("\n").filter(Boolean);
      if (lines.length === 0) return;

      console.info("Cleaning up dead DS containers", { count: lines.length });

      for (const line of lines) {
        const [id, name] = line.split(/\s+/);
        if (!id) continue;
        try {
          // Grab last logs before removing
          const logs = await fetchContainerLogsSafe(dockerBin, id, 40);
          if (logs) {
            console.warn("Dead DS container logs before removal", { id, name, logs: logs.slice(-1500) });
          }
          await execFileAsync(dockerBin, ["rm", "-f", id], { timeout: 10_000 });
        } catch { /* ignore */ }
      }
    } catch (err) {
      console.warn("Failed to cleanup dead containers", { err: String(err) });
    }
  }

  static async listManagedContainers(params: { region?: string; mode?: DsMode }): Promise<DockerContainerInfo[]> {
    const dockerBin = await resolveContainerRuntime();
    const filters: string[] = ["--filter", `label=app=${this.APP_LABEL}`];
    if (params.region) filters.push("--filter", `label=region=${params.region}`);
    if (params.mode) filters.push("--filter", `label=mode=${params.mode}`);

    // id|name|labels|createdAt
    const format = "{{.ID}}|{{.Names}}|{{.Labels}}|{{.CreatedAt}}";

    const args = ["ps", ...filters, "--format", format];

    const { stdout } = await execFileAsync(dockerBin, args, { timeout: 15_000 });
    const lines = (stdout || "")
      .trim()
      .split("\n")
      .map((l) => l.trim())
      .filter(Boolean);

    return lines.map((line) => {
      const [id, name, rawLabels, createdAt] = line.split("|");
      const labels = parseLabelString(rawLabels || "");
      return {
        id,
        name,
        createdAt,
        labels: {
          app: labels.app || this.APP_LABEL,
          region: labels.region || "",
          mode: (labels.mode as DsMode) || "IDLE",
          typeMatchGid: labels.typeMatchGid || "0",
          matchId: labels.matchId || undefined,
          sessionName: labels.sessionName || undefined,
        },
      };
    });
  }
}
