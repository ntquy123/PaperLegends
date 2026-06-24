import { RequestHandler } from "express";
import crypto from "crypto";
import { Matchmaker } from "../services/matchmaker";
import { getOnlinePlayerCount } from "../websocket/registry";
import { releaseServerPortPoolEntry } from "../services/matchmakingService";
import { getRedisClient } from "../services/redisClient";

type QueueJoinBody = {
  userId?: number;
  bet?: number;         // ví dụ: 100
  region?: string;      // ví dụ: "asia"
  typeMatchGid?: number; // bucket theo mode
  maxPlayers?: number;
};

type QueueCancelBody = {
  userId?: number;
};

type MatchReadyBody = {
  matchId?: string;
  sessionName?: string;
  region?: string;
  dsId?: string; // optional: id của container DS
  hostPort?: number;
  containerIp?: string;
};

type MatchResultBody = {
  matchId?: string;
  result?: unknown;
};

type MatchEarlyExitBody = {
  matchId?: string;
  playerId?: number;
  roomId?: number;
};

type MatchStartedBody = {
  matchId?: string;
};

type DsRegisterBody = {
  dsId?: string;       // container id / instance id
  region?: string;     // region nó phục vụ
  status?: "IDLE" | "BUSY";
};

type ReleaseServerPortBody = {
  portNo?: number;
  containerId?: string;
};

function getIO(req: any) {
  // server.ts nên set: app.set("io", io);
  return req.app.get("io");
}

// ==== CONFIG ====
// NOTE: tạm thời giảm xuống 2 người để test. Đổi lại 3 khi cần.
const DEFAULT_MATCH_SIZE = Number(process.env.DEFAULT_MATCH_SIZE || 3);
const PAPER_LEGENDS_MATCH_SIZE = Number(process.env.PAPER_LEGENDS_MATCH_SIZE || 4);
const PAPER_LEGENDS_TYPE_MATCH_GIDS = new Set(
  (process.env.PAPER_LEGENDS_TYPE_MATCH_GIDS || "10000002")
    .split(",")
    .map((value) => Number(value.trim()))
    .filter((value) => Number.isFinite(value) && value > 0),
);
const MAX_CCU = 20; // gói free Photon
const SERVER_READY_TIMEOUT_MS = 180_000; // tăng timeout lên 3 phút để tránh fail khi tải chậm
const PLAYER_JOIN_DEADLINE_MS = 180_000;
const MATCH_ACK_TIMEOUT_MS = 12_000;

const JOIN_TOKEN_SECRET = process.env.JOIN_TOKEN_SECRET || "dev_secret_change_me";
const REDIS_FAST_TIMEOUT_MS = Number(process.env.REDIS_FAST_TIMEOUT_MS || 1500);

const resolveMatchSize = (typeMatchGid?: number, requestedMaxPlayers?: number) => {
  if (PAPER_LEGENDS_TYPE_MATCH_GIDS.has(Number(typeMatchGid))) {
    return Math.max(1, PAPER_LEGENDS_MATCH_SIZE);
  }

  const requested = Number(requestedMaxPlayers);
  if (Number.isInteger(requested) && requested > 0) {
    return Math.max(1, Math.min(16, requested));
  }

  return Math.max(1, DEFAULT_MATCH_SIZE);
};

// Tạo token đơn giản (HMAC) để “ai không có ticket” không join được.
// (Nếu bạn đã dùng jose/JWT thì có thể thay bằng JWT).
function signJoinToken(payload: object) {
  const json = JSON.stringify(payload);
  const sig = crypto.createHmac("sha256", JOIN_TOKEN_SECRET).update(json).digest("hex");
  return Buffer.from(json).toString("base64url") + "." + sig;
}

export const joinQueue: RequestHandler = async (req, res) => {
  const { userId, bet, region, typeMatchGid, maxPlayers } = req.body as QueueJoinBody;

  if (!userId) {
    res.status(400).json({ error: "userId là bắt buộc" });
    return;
  }

  const io = getIO(req);
  if (!io) {
    res.status(503).json({ error: "Socket.IO instance not available" });
    return;
  }

  const result = await Matchmaker.instance.enqueue({
    userId,
    bet: bet ?? 0,
    region: region ?? "asia",
    typeMatchGid: typeMatchGid ?? 0,
  }, {
    matchSize: resolveMatchSize(typeMatchGid, maxPlayers),
    maxCCU: MAX_CCU,
    serverReadyTimeoutMs: SERVER_READY_TIMEOUT_MS,
    playerJoinDeadlineMs: PLAYER_JOIN_DEADLINE_MS,
    ackTimeoutMs: MATCH_ACK_TIMEOUT_MS,
    io,
    signJoinToken,
  });

  // enqueue luôn trả về trạng thái hiện tại của user
  res.status(202).json(result.http);
};

export const cancelQueue: RequestHandler = async (req, res) => {
  const { userId } = req.body as QueueCancelBody;
  if (!userId) {
    res.status(400).json({ error: "userId là bắt buộc" });
    return;
  }

  const io = getIO(req);
  if (!io) {
    res.status(503).json({ error: "Socket.IO instance not available" });
    return;
  }
  const out = await Matchmaker.instance.cancel(userId, io);
  res.json(out);
};

export const forceStartQueue: RequestHandler = async (req, res) => {
  const { userId, bet, region, typeMatchGid, maxPlayers } = req.body as QueueJoinBody;

  if (!userId) {
    res.status(400).json({ error: "userId là bắt buộc" });
    return;
  }

  const io = getIO(req);
  if (!io) {
    res.status(503).json({ error: "Socket.IO instance not available" });
    return;
  }

  const result = await Matchmaker.instance.forceStart(
    {
      userId,
      bet: bet ?? 0,
      region: region ?? "asia",
      typeMatchGid: typeMatchGid ?? 0,
    },
    {
      matchSize: resolveMatchSize(typeMatchGid, maxPlayers),
      maxCCU: MAX_CCU,
      serverReadyTimeoutMs: SERVER_READY_TIMEOUT_MS,
      playerJoinDeadlineMs: PLAYER_JOIN_DEADLINE_MS,
      ackTimeoutMs: MATCH_ACK_TIMEOUT_MS,
      io,
      signJoinToken,
    },
  );

  res.json(result);
};

// Dedicated Server callback: báo session đã sẵn sàng
export const matchReady: RequestHandler = async (req, res) => {
  const body = req.body as MatchReadyBody;
  const { matchId, sessionName, region, dsId } = body;
  const hostPort =
    typeof body.hostPort === "number"
      ? body.hostPort
      : typeof body.hostPort === "string"
        ? Number(body.hostPort)
        : undefined;
  const containerIp = typeof body.containerIp === "string" ? body.containerIp : undefined;

  if (!matchId || !sessionName || !region) {
    res.status(400).json({ error: "matchId, sessionName, region là bắt buộc" });
    return;
  }

  const io = getIO(req);
  if (!io) {
    res.status(503).json({ error: "Socket.IO instance not available" });
    return;
  }

  const readyDoneKey = `mm:ready:done:${matchId}`;
  const readyLockKey = `mm:ready:lock:${matchId}`;
  const readyLockToken = crypto.randomUUID();
  const readyDoneTtlSec = Math.max(120, Math.floor(PLAYER_JOIN_DEADLINE_MS / 1000) + 60);
  const readyLockTtlSec = 15;
  const withRedisTimeout = async <T>(opName: string, action: () => Promise<T>): Promise<T> => {
    let timeoutHandle: NodeJS.Timeout | null = null;

    const timeoutPromise = new Promise<never>((_, reject) => {
      timeoutHandle = setTimeout(() => {
        reject(new Error(`REDIS_${opName}_TIMEOUT_${REDIS_FAST_TIMEOUT_MS}MS`));
      }, REDIS_FAST_TIMEOUT_MS);
    });

    try {
      return await Promise.race([action(), timeoutPromise]);
    } finally {
      if (timeoutHandle) {
        clearTimeout(timeoutHandle);
      }
    }
  };

  try {
    const redis = await withRedisTimeout("GET_CLIENT", () => getRedisClient());

    const alreadyDone = await withRedisTimeout("GET_DONE", () => redis.get(readyDoneKey));
    if (alreadyDone) {
      res.json({ status: "ALREADY_PROCESSED", matchId });
      return;
    }

    const locked = await withRedisTimeout("SET_LOCK", () => redis.set(readyLockKey, readyLockToken, {
      NX: true,
      EX: readyLockTtlSec,
    }));

    if (locked !== "OK") {
      res.json({ status: "PROCESSING", matchId });
      return;
    }

    const doubleCheckDone = await withRedisTimeout("GET_DONE_DOUBLE_CHECK", () => redis.get(readyDoneKey));
    if (doubleCheckDone) {
      res.json({ status: "ALREADY_PROCESSED", matchId });
      return;
    }

    const ok = Matchmaker.instance.onServerReady({
      matchId,
      sessionName,
      region,
      io,
      signJoinToken,
      playerJoinDeadlineMs: PLAYER_JOIN_DEADLINE_MS,
      hostPort,
      containerIp,
    });

    if (ok) {
      await withRedisTimeout("SET_DONE", () =>
        redis.set(readyDoneKey, JSON.stringify({ dsId: dsId ?? null, at: Date.now() }), {
          EX: readyDoneTtlSec,
        }),
      );
    }

    const releaseScript = `
      if redis.call("GET", KEYS[1]) == ARGV[1] then
        return redis.call("DEL", KEYS[1])
      else
        return 0
      end
    `;
    await withRedisTimeout("RELEASE_LOCK", () =>
      redis.eval(releaseScript, {
        keys: [readyLockKey],
        arguments: [readyLockToken],
      }),
    );

    res.json({ status: ok ? "OK" : "IGNORED", matchId });
    return;
  } catch (error) {
    console.warn("matchReady idempotency fallback (redis unavailable)", {
      matchId,
      error: String(error),
    });
  }

  const ok = Matchmaker.instance.onServerReady({
    matchId,
    sessionName,
    region,
    io,
    signJoinToken,
    playerJoinDeadlineMs: PLAYER_JOIN_DEADLINE_MS,
    hostPort,
    containerIp,
  });

  res.json({ status: ok ? "OK" : "IGNORED", matchId });
};

// Dedicated Server callback: báo kết quả trận (backend settle)
export const matchResult: RequestHandler = async (req, res) => {
  const { matchId, result } = req.body as MatchResultBody;

  if (!matchId) {
    res.status(400).json({ error: "matchId là bắt buộc" });
    return;
  }

  const io = getIO(req);
  if (!io) {
    res.status(503).json({ error: "Socket.IO instance not available" });
    return;
  }

  const out = await Matchmaker.instance.onMatchResult({
    matchId,
    result: result ?? null,
    io,
  });

  res.json(out);
};

export const matchEarlyExit: RequestHandler = async (req, res) => {
  const body = req.body as MatchEarlyExitBody;
  const matchId = typeof body.matchId === "string" ? body.matchId.trim() : "";
  const playerId = Number(body.playerId);
  const roomId = body.roomId === undefined || body.roomId === null ? undefined : Number(body.roomId);

  if (!Number.isFinite(playerId) || playerId <= 0) {
    res.status(400).json({ error: "playerId lÃ  báº¯t buá»™c" });
    return;
  }

  const io = getIO(req);
  if (!io) {
    res.status(503).json({ error: "Socket.IO instance not available" });
    return;
  }

  const out = Matchmaker.instance.markPlayerEarlyExit({
    matchId: matchId || undefined,
    playerId,
    roomId: roomId && Number.isFinite(roomId) && roomId > 0 ? roomId : undefined,
    io,
  });

  res.json(out);
};

export const matchStarted: RequestHandler = (req, res) => {
  const { matchId } = req.body as MatchStartedBody;

  if (!matchId) {
    res.status(400).json({ error: "matchId là bắt buộc" });
    return;
  }

  const io = getIO(req);
  if (!io) {
    res.status(503).json({ error: "Socket.IO instance not available" });
    return;
  }

  const out = Matchmaker.instance.onMatchStarted({ matchId, io });
  res.json(out);
};

// DS idle register (warm pool). DS container “idle” boot xong thì gọi endpoint này.
export const dsRegister: RequestHandler = async (req, res) => {
  const { dsId, region, status } = req.body as DsRegisterBody;
  if (!dsId || !region) {
    res.status(400).json({ error: "dsId, region là bắt buộc" });
    return;
  }

  Matchmaker.instance.registerDs({
    dsId,
    region,
    status: status ?? "IDLE",
  });

  res.json({ status: "REGISTERED", dsId, region });
};

export const releaseServerPort: RequestHandler = async (req, res) => {
  const { portNo, containerId } = req.body as ReleaseServerPortBody;

  if (!portNo || !containerId) {
    res.status(400).json({ error: "portNo, containerId là bắt buộc" });
    return;
  }

  try {
    const result = await releaseServerPortPoolEntry(portNo, containerId);
    res.json(result);
  } catch (error) {
    console.error("Không thể release serverPortPool:", error);
    res.status(500).json({ error: "RELEASE_SERVER_PORT_FAILED" });
  }
};

export const getOnlineCount: RequestHandler = (_req, res) => {
  res.json({ onlineCount: getOnlinePlayerCount() });
};
