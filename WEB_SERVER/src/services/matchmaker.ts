import crypto from "crypto";
import { DockerOrchestrator } from "./orchestrator";
import { ensureWarmIdleContainers } from "./orchestratorWarmPool";
import { findRoomByPlayerId, leaveRoomAndCleanup } from "./matchmakingService";
import { isRoomContainerPoolEnabled } from "../config/roomContainerPool";
import { getPlayerSocketState, isPlayerOnline } from "../websocket/registry";
import prisma from "../models/prismaClient";
import { getRedisClient } from "./redisClient";

const EARLY_EXIT_LUCKY_DRAW_MESSAGE_MARKER = "__MATCH_EARLY_EXIT_LUCKY_DRAW__";
const EARLY_EXIT_LUCKY_DRAW_MESSAGE_KEY = "lucky_draw_mess";
const PAPER_LEGENDS_TYPE_MATCH_GIDS = new Set(
  (process.env.PAPER_LEGENDS_TYPE_MATCH_GIDS || "10000002")
    .split(",")
    .map((value) => Number(value.trim()))
    .filter((value) => Number.isFinite(value) && value > 0),
);
const PAPER_LEGENDS_CHARACTER_SELECTION_SECONDS = Math.max(
  5,
  Number(process.env.PAPER_LEGENDS_CHARACTER_SELECTION_SECONDS || 40),
);
const PAPER_LEGENDS_BOT_SELECT_MIN_MS = Math.max(
  0,
  Number(process.env.PAPER_LEGENDS_BOT_SELECT_MIN_MS || 3000),
);
const PAPER_LEGENDS_BOT_SELECT_MAX_MS = Math.max(
  PAPER_LEGENDS_BOT_SELECT_MIN_MS,
  Number(process.env.PAPER_LEGENDS_BOT_SELECT_MAX_MS || 6000),
);

type CharacterSelectionState = {
  participantIds: number[];
  botPlayerIds: Set<number>;
  selectableModelIds: number[];
  selectionsByPlayerId: Map<number, number>;
  lockedPlayerIds: Set<number>;
  playerNamesById: Map<number, string>;
  deadlineMs: number;
  timer?: NodeJS.Timeout;
  botTimers: NodeJS.Timeout[];
};

type EnqueueParams = {
  userId: number;
  bet: number;
  region: string;
  typeMatchGid: number;
};

type EnqueueCtx = {
  matchSize: number;
  maxCCU: number;
  serverReadyTimeoutMs: number;
  playerJoinDeadlineMs: number;
  ackTimeoutMs: number;
  io: any;
  signJoinToken: (payload: object) => string;
};

type MatchState =
  | "QUEUED"
  | "MATCH_PROPOSED"
  | "MATCH_CONFIRMED"
  | "CHARACTER_SELECTING"
  | "SERVER_CREATING"
  | "READY"
  | "IN_PROGRESS"
  | "FINISHED"
  | "FAILED"
  | "CANCELLED";

type MatchRecord = {
  matchId: string;
  sessionName: string;
  region: string;
  bet: number;
  typeMatchGid: number;
  players: number[];
  createdAt: number;
  state: MatchState;
  maxPlayers: number;
  ackedPlayers: Set<number>;
  ackTimeoutMs: number;
  ackRetryCount: number;
  signJoinToken: (payload: object) => string;
  playerJoinDeadlineMs: number;
  earlyExitedPlayerIds: Set<number>;
  earlyExitRoomIds: Map<number, number>;

  // nếu assign từ warm pool: đây là container name idle được assign
  dsContainerName: string | null;
  dsHostPort: number | null;
  dsContainerIp: string | null;

  ackTimer?: NodeJS.Timeout;
  serverReadyTimer?: NodeJS.Timeout;
  playerJoinTimer?: NodeJS.Timeout;
  characterSelection?: CharacterSelectionState;
  characterSelectionsCsv?: string;
  botCharacterModelIdsCsv?: string;
  serverReadyTimeoutMs?: number;
};

type QueueSnapshotEntry = {
  playerId: number;
  bucket: string;
  region: string;
  typeMatchGid: number;
  bet: number;
  position: number;
  bucketSize: number;
  queuedAt: number | null;
  status: "QUEUED";
};

type MatchSnapshotEntry = {
  playerId: number;
  matchId: string;
  sessionName: string;
  region: string;
  typeMatchGid: number;
  bet: number;
  players: number[];
  playerCount: number;
  requiredPlayers: number;
  createdAt: number;
  status: MatchState;
  hostPort: number | null;
};

type PersistedMatchSnapshot = {
  matchId: string;
  sessionName: string;
  region: string;
  bet: number;
  typeMatchGid: number;
  players: number[];
  createdAt: number;
  state: MatchState;
  maxPlayers: number;
  playerJoinDeadlineMs: number;
  characterSelectionsCsv?: string;
  botCharacterModelIdsCsv?: string;
  dsContainerName?: string | null;
  dsHostPort?: number | null;
  dsContainerIp?: string | null;
  updatedAt: number;
};

function makeMatchId() {
  return "m_" + crypto.randomBytes(8).toString("hex");
}

function makeSessionName(region: string) {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let s = "";
  for (let i = 0; i < 7; i++) s += alphabet[Math.floor(Math.random() * alphabet.length)];
  return `${region.toUpperCase()}-${s}`;
}

function userRoom(userId: number) {
  return `user:${userId}`;
}

type DedicatedServerStartResult =
  | { kind: "WAITING_FOR_READY"; startedByThisProcess: boolean }
  | { kind: "READY_NOW"; sessionName: string; region: string; hostPort?: number | null; containerIp?: string | null };

function getSingleRoomSessionName(match: MatchRecord) {
  return (
    process.env.QUICKMATCH_SINGLE_ROOM_SESSION_NAME?.trim() ||
    process.env.SINGLE_TEST_SESSION_NAME?.trim() ||
    match.sessionName ||
    "test"
  );
}

function getOptionalEnvNumber(name: string) {
  const raw = process.env[name]?.trim();
  if (!raw) return null;

  const parsed = Number(raw);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function normalizeDedicatedServerStartFailure(error: unknown) {
  const raw = String(error ?? "");
  if (raw.includes("ROOM_CONTAINER_POOL_DISABLED")) {
    return {
      reason: "ROOM_CONTAINER_POOL_DISABLED",
      detail: "ROOM_CONTAINER_POOL_ENABLED=false, backend will not start dedicated server containers.",
    };
  }

  if (raw.includes("DOCKER_NOT_AVAILABLE") || raw.includes("ENOENT") || raw.includes("not found")) {
    return {
      reason: "DOCKER_NOT_AVAILABLE",
      detail: "Docker runtime is not available on the backend host.",
    };
  }

  if (raw.includes("DS_CONTAINER_EXITED_PREMATURELY")) {
    return {
      reason: "DS_CONTAINER_EXITED_PREMATURELY",
      detail: raw,
    };
  }

  if (raw.includes("Docker start error")) {
    return {
      reason: "DOCKER_START_FAILED",
      detail: raw,
    };
  }

  return {
    reason: "DS_START_FAILED",
    detail: raw,
  };
}

export class Matchmaker {
  static instance = new Matchmaker();

  // queue per bucket
  private queue = new Map<string, number[]>();
  private queuedUsers = new Set<number>();
  private queuedAtByUser = new Map<number, number>();

  private matches = new Map<string, MatchRecord>();

  // tránh double-assign 1 container IDLE
  private lockedIdleContainers = new Set<string>();

  // CCU approximation: reserve slots khi match allocated
  private reservedCCUSlots = 0;

  // track DS register (warm pool DS báo về)
  private dsRegistry = new Map<
    string,
    {
      region: string;
      status: "IDLE" | "BUSY";
      registeredAt: number;
    }
  >();

  private readyCleanupSuccessCount = 0;
  private readyCleanupFailureCount = 0;

  private persistedMatchTtlSeconds() {
    return Math.max(600, Number(process.env.MATCH_RESYNC_TTL_SECONDS || 600));
  }

  private persistedMatchKey(matchId: string) {
    return `mm:match:${matchId}`;
  }

  private persistedUserMatchKey(userId: number) {
    return `mm:user-match:${userId}`;
  }

  private buildPersistedMatchSnapshot(match: MatchRecord): PersistedMatchSnapshot {
    return {
      matchId: match.matchId,
      sessionName: match.sessionName,
      region: match.region,
      bet: match.bet,
      typeMatchGid: match.typeMatchGid,
      players: [...match.players],
      createdAt: match.createdAt,
      state: match.state,
      maxPlayers: match.maxPlayers,
      playerJoinDeadlineMs: match.playerJoinDeadlineMs,
      characterSelectionsCsv: match.characterSelectionsCsv,
      botCharacterModelIdsCsv: match.botCharacterModelIdsCsv,
      dsContainerName: match.dsContainerName,
      dsHostPort: match.dsHostPort,
      dsContainerIp: match.dsContainerIp,
      updatedAt: Date.now(),
    };
  }

  private async persistMatchSnapshot(match: MatchRecord) {
    if (!this.isPaperLegendMatch(match)) {
      return;
    }

    try {
      const redis = await getRedisClient();
      const snapshot = this.buildPersistedMatchSnapshot(match);
      const ttl = this.persistedMatchTtlSeconds();
      await redis.set(this.persistedMatchKey(match.matchId), JSON.stringify(snapshot), { EX: ttl });

      for (const userId of match.players) {
        if (!Number.isFinite(userId) || userId <= 0) {
          continue;
        }

        await redis.set(this.persistedUserMatchKey(userId), match.matchId, { EX: ttl });
      }
    } catch (error) {
      console.warn("Unable to persist Paper Legends match snapshot", {
        matchId: match.matchId,
        state: match.state,
        error: String(error),
      });
    }
  }

  private async readPersistedMatchByMatchId(matchId: string) {
    if (!matchId) {
      return null;
    }

    try {
      const redis = await getRedisClient();
      const raw = await redis.get(this.persistedMatchKey(matchId));
      if (!raw) {
        return null;
      }

      return JSON.parse(String(raw)) as PersistedMatchSnapshot;
    } catch (error) {
      console.warn("Unable to read persisted Paper Legends match snapshot", {
        matchId,
        error: String(error),
      });
      return null;
    }
  }

  private async readPersistedMatchForUser(userId: number) {
    if (!Number.isFinite(userId) || userId <= 0) {
      return null;
    }

    try {
      const redis = await getRedisClient();
      const matchId = await redis.get(this.persistedUserMatchKey(userId));
      if (!matchId) {
        return null;
      }

      const snapshot = await this.readPersistedMatchByMatchId(String(matchId));
      if (!snapshot || !snapshot.players.includes(userId)) {
        return null;
      }

      return snapshot;
    } catch (error) {
      console.warn("Unable to read persisted Paper Legends user match pointer", {
        userId,
        error: String(error),
      });
      return null;
    }
  }

  private async clearPersistedMatchSnapshot(match: MatchRecord) {
    if (!this.isPaperLegendMatch(match)) {
      return;
    }

    try {
      const redis = await getRedisClient();
      const keys = [
        this.persistedMatchKey(match.matchId),
        ...match.players
          .filter((userId) => Number.isFinite(userId) && userId > 0)
          .map((userId) => this.persistedUserMatchKey(userId)),
      ];

      if (keys.length > 0) {
        await redis.del(keys);
      }
    } catch (error) {
      console.warn("Unable to clear persisted Paper Legends match snapshot", {
        matchId: match.matchId,
        error: String(error),
      });
    }
  }

  private rehydrateMatchFromSnapshot(
    snapshot: PersistedMatchSnapshot,
    signJoinToken: (payload: object) => string,
    playerJoinDeadlineMs: number,
  ): MatchRecord {
    return {
      matchId: snapshot.matchId,
      sessionName: snapshot.sessionName,
      region: snapshot.region,
      bet: snapshot.bet,
      typeMatchGid: snapshot.typeMatchGid,
      players: [...snapshot.players],
      createdAt: snapshot.createdAt,
      state: snapshot.state,
      maxPlayers: snapshot.maxPlayers,
      ackedPlayers: new Set<number>(snapshot.players),
      ackTimeoutMs: 0,
      ackRetryCount: 0,
      signJoinToken,
      playerJoinDeadlineMs: snapshot.playerJoinDeadlineMs || playerJoinDeadlineMs,
      earlyExitedPlayerIds: new Set<number>(),
      earlyExitRoomIds: new Map<number, number>(),
      dsContainerName: snapshot.dsContainerName ?? null,
      dsHostPort: snapshot.dsHostPort ?? null,
      dsContainerIp: snapshot.dsContainerIp ?? null,
      characterSelectionsCsv: snapshot.characterSelectionsCsv,
      botCharacterModelIdsCsv: snapshot.botCharacterModelIdsCsv,
      serverReadyTimeoutMs: 0,
    };
  }

  /**
   * Sau khi 1 match consume phòng (assign idle hoặc spawn on-demand), luôn
   * bù lại warm pool theo type để lượt tìm trận kế tiếp khởi động nhanh.
   * Chạy best-effort, không ảnh hưởng flow chính nếu refill lỗi.
   */
  private replenishWarmPool(region: string, typeMatchGid: number) {
    if (!isRoomContainerPoolEnabled()) {
      return;
    }

    ensureWarmIdleContainers({
      region,
      types: [typeMatchGid],
      minIdlePerType: Math.max(3, Number(process.env.MIN_IDLE_DS_PER_TYPE) || 0),
    }).catch((error) => {
      console.warn("Warm pool replenish failed", {
        region,
        typeMatchGid,
        err: String(error),
      });
    });
  }

  private async tryAcquireMatchStartLock(matchId: string) {
    const lockKey = `match-start:${matchId}`;
    const rows = await prisma.$queryRaw<{ locked: boolean }[]>`
      SELECT pg_try_advisory_lock(hashtext(${lockKey})) AS locked
    `;

    return rows?.[0]?.locked ?? false;
  }

  private async releaseMatchStartLock(matchId: string) {
    const lockKey = `match-start:${matchId}`;
    await prisma.$queryRaw`
      SELECT pg_advisory_unlock(hashtext(${lockKey}))
    `;
  }

  private async clearReadyIdempotencyKeys(matchId: string) {
    const doneKey = `mm:ready:done:${matchId}`;
    const lockKey = `mm:ready:lock:${matchId}`;

    try {
      const redis = await getRedisClient();
      await redis.del([doneKey, lockKey]);
      this.readyCleanupSuccessCount += 1;

      const total = this.readyCleanupSuccessCount + this.readyCleanupFailureCount;
      if (total % 20 === 0) {
        console.info("READY idempotency cleanup metrics", {
          success: this.readyCleanupSuccessCount,
          failed: this.readyCleanupFailureCount,
          total,
          successRate: Number(((this.readyCleanupSuccessCount / total) * 100).toFixed(2)),
        });
      }
    } catch (error) {
      this.readyCleanupFailureCount += 1;
      console.warn("Unable to clear READY idempotency keys", {
        matchId,
        error: String(error),
        success: this.readyCleanupSuccessCount,
        failed: this.readyCleanupFailureCount,
      });
    }
  }

  async enqueue(p: EnqueueParams, ctx: EnqueueCtx) {
    const { io } = ctx;

    if (this.queuedUsers.has(p.userId)) {
      return { http: { status: "ALREADY_QUEUED", message: "Bạn đang ở trong hàng chờ" } };
    }

    const key = this.bucketKey(p);
    const arr = this.queue.get(key) ?? [];
    arr.push(p.userId);
    this.queue.set(key, arr);
    this.queuedUsers.add(p.userId);
    this.queuedAtByUser.set(p.userId, Date.now());

    io.to(userRoom(p.userId)).emit("queue:update", {
      bucket: key,
      current: Math.min(arr.length, ctx.matchSize),
      required: ctx.matchSize,
    });

    await this.tryAllocate(key, p, ctx);

    return { http: { status: "QUEUED", bucket: key, message: "Đã vào hàng chờ" } };
  }

  async cancel(userId: number, io: any) {
    if (!Number.isFinite(userId) || userId <= 0) {
      return { status: "INVALID_PLAYER" };
    }

    const removedFromQueue = this.removeUserFromQueue(userId);
    const pendingMatches = this.findPendingMatchesForPlayer(userId);

    for (const match of pendingMatches) {
      console.warn("Cancelling pending matchmaking state by user request", {
        userId,
        matchId: match.matchId,
        state: match.state,
        players: match.players,
      });

      if (match.state === "MATCH_PROPOSED") {
        this.cancelMatch(match.matchId, io, "PLAYER_CANCELLED_QUEUE");
      } else {
        await this.failMatch(match.matchId, io, "PLAYER_CANCELLED_BEFORE_START", `playerId=${userId}`);
      }
    }

    if (removedFromQueue || pendingMatches.length > 0) {
      io.to(userRoom(userId)).emit("queue:cancelled", { userId });
      return {
        status: "CANCELLED",
        removedFromQueue,
        cancelledMatchCount: pendingMatches.length,
      };
    }

    return { status: "NOT_IN_QUEUE", removedFromQueue: false, cancelledMatchCount: 0 };
  }

  private findPendingMatchesForPlayer(userId: number) {
    return this.findActiveMatchesForPlayer(userId).filter((match) => match.state !== "IN_PROGRESS");
  }

  private findActiveMatchesForPlayer(userId: number) {
    return Array.from(this.matches.values())
      .filter(
        (match) =>
          match.players.includes(userId) &&
          match.state !== "FINISHED" &&
          match.state !== "FAILED" &&
          match.state !== "CANCELLED",
      )
      .sort((a, b) => b.createdAt - a.createdAt);
  }

  onPlayerDisconnected(userId: number, io: any) {
    if (!Number.isFinite(userId) || userId <= 0) {
      return { status: "INVALID_PLAYER" };
    }

    const removedFromQueue = this.removeUserFromQueue(userId);
    if (removedFromQueue) {
      console.info("Removed disconnected player from matchmaking queue", { userId });
    }

    const affectedMatches = Array.from(this.matches.values()).filter(
      (match) =>
        match.players.includes(userId) &&
        match.state !== "IN_PROGRESS" &&
        match.state !== "FINISHED" &&
        match.state !== "FAILED" &&
        match.state !== "CANCELLED",
    );

    for (const match of affectedMatches) {
      if (
        this.isPaperLegendMatch(match) &&
        (
          match.state === "CHARACTER_SELECTING" ||
          match.state === "MATCH_CONFIRMED" ||
          match.state === "SERVER_CREATING" ||
          match.state === "READY"
        )
      ) {
        console.info("Preserving Paper Legends match after transient websocket disconnect", {
          userId,
          matchId: match.matchId,
          state: match.state,
          players: match.players,
        });
        continue;
      }

      console.warn("Player disconnected before match started; cleaning pending match", {
        userId,
        matchId: match.matchId,
        state: match.state,
        players: match.players,
      });

      if (match.state === "MATCH_PROPOSED") {
        this.cancelMatch(match.matchId, io, "PLAYER_DISCONNECTED");
      } else {
        void this.failMatch(match.matchId, io, "PLAYER_DISCONNECTED_BEFORE_START", `playerId=${userId}`);
      }
    }

    return {
      status: removedFromQueue || affectedMatches.length > 0 ? "CLEANED" : "NOOP",
      removedFromQueue,
      affectedMatchCount: affectedMatches.length,
    };
  }

  /**
   * Force-start a match immediately with available players in the same bucket.
   * If not enough real players, the dedicated server will fill remaining slots with bots.
   * Skips ACK flow entirely — goes straight to server creation.
   */
  async forceStart(
    p: EnqueueParams,
    ctx: EnqueueCtx & { signJoinToken: (payload: object) => string; playerJoinDeadlineMs: number },
  ) {
    const { io } = ctx;
    const key = this.bucketKey(p);
    const arr = this.queue.get(key) ?? [];
    const existingMatch = this.findPendingMatchesForPlayer(p.userId)[0];
    if (existingMatch) {
      console.info("Force-start ignored because player is already assigned to a pending match", {
        userId: p.userId,
        matchId: existingMatch.matchId,
        state: existingMatch.state,
        players: existingMatch.players,
      });
      return {
        status: "OK",
        message: "Nguoi choi dang duoc ghep tran.",
        matchId: existingMatch.matchId,
      };
    }

    // CCU gate
    if (this.reservedCCUSlots + ctx.matchSize > ctx.maxCCU) {
      return { status: "CCU_FULL", message: "Server đầy, vui lòng thử lại sau." };
    }

    // Collect all online players from this bucket (including the requester)
    const selected: number[] = [];
    const remaining: number[] = [];
    for (const uid of arr) {
      const pendingMatch = this.findPendingMatchesForPlayer(uid)[0];
      if (pendingMatch) {
        this.queuedUsers.delete(uid);
        this.queuedAtByUser.delete(uid);
        console.warn("Skipping queued player already assigned to a pending match during force-start", {
          userId: uid,
          matchId: pendingMatch.matchId,
          state: pendingMatch.state,
        });
        continue;
      }

      if (selected.length < ctx.matchSize && isPlayerOnline(uid)) {
        selected.push(uid);
      } else {
        remaining.push(uid);
      }
    }
    this.queue.set(key, remaining);

    // Make sure the requester is included
    if (!selected.includes(p.userId)) {
      if (selected.length >= ctx.matchSize) {
        // Replace last slot with the requester
        const bumped = selected.pop()!;
        remaining.unshift(bumped);
        this.queue.set(key, remaining);
        this.queuedUsers.add(bumped);
        if (!this.queuedAtByUser.has(bumped)) {
          this.queuedAtByUser.set(bumped, Date.now());
        }
      }
      selected.push(p.userId);
    }

    // Remove selected players from queue tracking
    for (const uid of selected) {
      this.queuedUsers.delete(uid);
      this.queuedAtByUser.delete(uid);
    }

    // At least the requester must be in the match
    if (selected.length === 0) {
      return { status: "ERROR", message: "Không có người chơi để tạo trận." };
    }

    const matchId = makeMatchId();
    const sessionName = makeSessionName(p.region);

    console.info("🤖 Force-start match with bots", {
      matchId,
      players: selected,
      matchSize: ctx.matchSize,
      botsNeeded: ctx.matchSize - selected.length,
    });

    const match: MatchRecord = {
      matchId,
      sessionName,
      region: p.region,
      bet: p.bet,
      typeMatchGid: p.typeMatchGid,
      players: selected,
      createdAt: Date.now(),
      state: "MATCH_CONFIRMED", // skip PROPOSED → go straight to CONFIRMED
      maxPlayers: ctx.matchSize,
      ackedPlayers: new Set<number>(selected), // auto-ack all
      ackTimeoutMs: ctx.ackTimeoutMs,
      ackRetryCount: 0,
      signJoinToken: ctx.signJoinToken,
      playerJoinDeadlineMs: ctx.playerJoinDeadlineMs,
      earlyExitedPlayerIds: new Set<number>(),
      earlyExitRoomIds: new Map<number, number>(),
      dsContainerName: null,
      dsHostPort: null,
      dsContainerIp: null,
      serverReadyTimeoutMs: ctx.serverReadyTimeoutMs,
    };
    this.matches.set(matchId, match);

    // Reserve CCU slots only for real players
    this.reservedCCUSlots += selected.length;

    // Notify players that match is confirmed
    for (const uid of selected) {
      io.to(userRoom(uid)).emit("match:confirmed", {
        matchId,
        required: ctx.matchSize,
        players: selected.length,
        forceStart: true,
      });
    }

    console.info("Force-start route resolved", {
      matchId,
      typeMatchGid: match.typeMatchGid,
      paperLegends: this.isPaperLegendMatch(match),
      paperLegendsTypeMatchGids: Array.from(PAPER_LEGENDS_TYPE_MATCH_GIDS),
    });

    // Paper Legends enters websocket character selection before server creation.
    await this.beginCharacterSelectionOrServerCreation(match, io);

    return { status: "OK", message: "Trận đấu đang được tạo với bot." };
  }

  async onServerReady(args: {
    matchId: string;
    sessionName: string;
    region: string;
    io: any;
    signJoinToken: (payload: object) => string;
    playerJoinDeadlineMs: number;
    hostPort?: number | null;
    containerIp?: string | null;
  }) {
    let m = this.matches.get(args.matchId);
    if (!m) {
      const snapshot = await this.readPersistedMatchByMatchId(args.matchId);
      if (!snapshot) {
        return false;
      }

      m = this.rehydrateMatchFromSnapshot(snapshot, args.signJoinToken, args.playerJoinDeadlineMs);
      this.matches.set(m.matchId, m);
      console.warn("Rehydrated Paper Legends match from Redis during READY callback", {
        matchId: m.matchId,
        players: m.players,
        previousState: snapshot.state,
      });
    }

    if (m.state === "FAILED" || m.state === "CANCELLED" || m.state === "FINISHED") return false;

    if (m.serverReadyTimer) clearTimeout(m.serverReadyTimer);

    m.state = "READY";
    m.sessionName = args.sessionName;
    m.region = args.region;
    if (args.hostPort != null) m.dsHostPort = args.hostPort;
    if (args.containerIp != null) m.dsContainerIp = args.containerIp;

    // phát ticket + báo loading cho toàn bộ client đã ghép đôi
    for (const uid of m.players) {
      args.io.to(userRoom(uid)).emit("match:loading", { matchId: m.matchId, stage: "MATCH_READY" });
      const token = args.signJoinToken({
        matchId: m.matchId,
        userId: uid,
        sessionName: m.sessionName,
        region: m.region,
        exp: Date.now() + args.playerJoinDeadlineMs,
      });

      args.io.to(userRoom(uid)).emit("match:ticket", {
        matchId: m.matchId,
        sessionName: m.sessionName,
        region: m.region,
        joinToken: token,
        deadlineMs: Date.now() + args.playerJoinDeadlineMs,
        hostPort: m.dsHostPort ?? 0,
      });
    }

    // fail-safe join deadline (nếu bạn chưa có ACK join)
    m.playerJoinTimer = setTimeout(() => {
      const mm = this.matches.get(m.matchId);
      if (!mm) return;
      if (mm.state === "READY" || mm.state === "SERVER_CREATING" || mm.state === "MATCH_CONFIRMED") {
        this.failMatch(mm.matchId, args.io, "PLAYER_JOIN_TIMEOUT");
      }
    }, args.playerJoinDeadlineMs);

    this.matches.set(m.matchId, m);
    await this.persistMatchSnapshot(m);
    return true;
  }

  registerDs(args: { dsId: string; region: string; status: "IDLE" | "BUSY" }) {
    this.dsRegistry.set(args.dsId, {
      region: args.region,
      status: args.status,
      registeredAt: Date.now(),
    });

    if (args.status === "IDLE") {
      this.lockedIdleContainers.delete(args.dsId);
    }

    return { status: "REGISTERED", dsId: args.dsId, region: args.region };
  }

  getSearchingPlayersSnapshot() {
    this.pruneOfflineQueuedUsers();

    const queued: QueueSnapshotEntry[] = [];
    for (const [bucket, playerIds] of this.queue.entries()) {
      const bucketInfo = this.parseBucketKey(bucket);
      playerIds.forEach((playerId, index) => {
        queued.push({
          playerId,
          bucket,
          region: bucketInfo.region,
          typeMatchGid: bucketInfo.typeMatchGid,
          bet: bucketInfo.bet,
          position: index + 1,
          bucketSize: playerIds.length,
          queuedAt: this.queuedAtByUser.get(playerId) ?? null,
          status: "QUEUED",
        });
      });
    }

    const activeMatches: MatchSnapshotEntry[] = [];
    for (const match of this.matches.values()) {
      if (match.state === "FINISHED" ||
          match.state === "FAILED" ||
          match.state === "CANCELLED" ||
          match.state === "IN_PROGRESS") {
        continue;
      }

      for (const playerId of match.players) {
        activeMatches.push({
          playerId,
          matchId: match.matchId,
          sessionName: match.sessionName,
          region: match.region,
          typeMatchGid: match.typeMatchGid,
          bet: match.bet,
          players: [...match.players],
          playerCount: match.players.length,
          requiredPlayers: match.maxPlayers,
          createdAt: match.createdAt,
          status: match.state,
          hostPort: match.dsHostPort,
        });
      }
    }

    return {
      queued,
      activeMatches,
      totalSearchingPlayers: queued.length + activeMatches.length,
      bucketCount: this.queue.size,
      activeMatchCount: activeMatches.length
        ? new Set(activeMatches.map((entry) => entry.matchId)).size
        : 0,
    };
  }

  onMatchStarted(args: { matchId: string; io: any }) {
    const m = this.matches.get(args.matchId);
    if (!m) return { status: "NOT_FOUND" };

    if (m.state === "FAILED" || m.state === "CANCELLED" || m.state === "FINISHED") {
      return { status: "IGNORED", state: m.state };
    }

    m.state = "IN_PROGRESS";

    if (m.playerJoinTimer) {
      clearTimeout(m.playerJoinTimer);
      m.playerJoinTimer = undefined;
    }

    this.matches.set(m.matchId, m);
    void this.persistMatchSnapshot(m);
    return { status: "OK", matchId: m.matchId };
  }

  markPlayerEarlyExit(args: { matchId?: string; playerId: number; roomId?: number; io: any }) {
    if (!Number.isFinite(args.playerId) || args.playerId <= 0) {
      return { status: "INVALID_PLAYER" };
    }

    const match = this.resolveMatchForEarlyExit(args.matchId, args.playerId);
    if (!match) {
      return { status: "NOT_FOUND" };
    }

    if (!match.players.includes(args.playerId)) {
      return { status: "NOT_IN_MATCH", matchId: match.matchId };
    }

    if (match.state === "FINISHED" || match.state === "FAILED" || match.state === "CANCELLED") {
      return { status: "IGNORED", matchId: match.matchId, state: match.state };
    }

    match.earlyExitedPlayerIds.add(args.playerId);
    if (args.roomId && Number.isFinite(args.roomId) && args.roomId > 0) {
      match.earlyExitRoomIds.set(args.playerId, args.roomId);
    }

    this.matches.set(match.matchId, match);

    args.io.to(userRoom(args.playerId)).emit("match:early_exit_registered", {
      matchId: match.matchId,
      playerId: args.playerId,
      roomId: args.roomId ?? null,
    });

    return {
      status: "OK",
      matchId: match.matchId,
      playerId: args.playerId,
      roomId: args.roomId ?? null,
    };
  }

  async onMatchResult(args: { matchId: string; result: unknown; io: any }) {
    const m = this.matches.get(args.matchId);
    if (!m) return { status: "NOT_FOUND" };

    m.state = "FINISHED";
    this.matches.set(m.matchId, m);

    // free CCU slots
    this.reservedCCUSlots = Math.max(0, this.reservedCCUSlots - m.players.length);

    // unlock warm DS (container sẽ exit, nhưng unlock để tránh leak)
    if (m.dsContainerName) this.lockedIdleContainers.delete(m.dsContainerName);

    await this.cleanupRoomData(m, args.result);

    const earlyExitedPlayerIds = Array.from(m.earlyExitedPlayerIds ?? new Set<number>())
      .filter((uid) => m.players.includes(uid));
    await this.createEarlyExitResultMessages(m, args.result, earlyExitedPlayerIds);

    for (const uid of m.players) {
      if (m.earlyExitedPlayerIds?.has(uid)) {
        args.io.to(userRoom(uid)).emit("match:early_exit_result_message", {
          matchId: m.matchId,
          playerId: uid,
          message: "MATCH_RESULT_SENT_TO_MESSAGES",
        });
        continue;
      }

      args.io.to(userRoom(uid)).emit("match:finished", { matchId: m.matchId, result: args.result });
    }

    this.cleanupMatch(m);

    // Bù warm pool ngay sau khi match dùng xong (best-effort)
    ensureWarmIdleContainers({
      region: m.region,
      types: [m.typeMatchGid],
      minIdlePerType: Math.max(3, Number(process.env.MIN_IDLE_DS_PER_TYPE) || 0),
    }).catch(() => {});

    return { status: "RESULT_RECEIVED", matchId: m.matchId };
  }

  // ---------------- Internals ----------------

  private resolveMatchForEarlyExit(matchId: string | undefined, playerId: number) {
    const normalizedMatchId = matchId?.trim();
    if (normalizedMatchId) {
      return this.matches.get(normalizedMatchId) ?? null;
    }

    const candidates = Array.from(this.matches.values())
      .filter((match) =>
        match.players.includes(playerId) &&
        match.state !== "FINISHED" &&
        match.state !== "FAILED" &&
        match.state !== "CANCELLED"
      )
      .sort((a, b) => b.createdAt - a.createdAt);

    return candidates[0] ?? null;
  }

  private async createEarlyExitResultMessages(match: MatchRecord, result: unknown, playerIds: number[]) {
    if (!playerIds.length) {
      return;
    }

    for (const playerId of playerIds) {
      const message = this.buildEarlyExitResultMessageLocalizationKey(match.matchId, playerId, result);

      try {
        const existing = await prisma.friendMessage.findFirst({
          where: {
            senderId: 0,
            receiverId: playerId,
            message: {
              contains: `${EARLY_EXIT_LUCKY_DRAW_MESSAGE_MARKER} matchId=${match.matchId}`,
            },
          },
          select: { seqMess: true },
        });

        if (existing) {
          continue;
        }

        await this.createSystemMessage(playerId, message);
      } catch (error) {
        console.warn("Unable to create early-exit result system message", {
          matchId: match.matchId,
          playerId,
          error: String(error),
        });
      }
    }
  }

  private async createSystemMessage(receiverId: number, message: string) {
    const maxAttempts = 3;

    for (let attempt = 0; attempt < maxAttempts; attempt++) {
      const last = await prisma.friendMessage.findFirst({
        where: { senderId: 0 },
        orderBy: { seqMess: "desc" },
        select: { seqMess: true },
      });

      const seqMess = (last?.seqMess ?? 0) + 1;

      try {
        await prisma.friendMessage.create({
          data: {
            senderId: 0,
            receiverId,
            message,
            seqMess,
          },
        });
        return;
      } catch (error: any) {
        if (error?.code === "P2002" && attempt < maxAttempts - 1) {
          continue;
        }

        throw error;
      }
    }
  }

  private buildEarlyExitResultMessageLocalizationKey(matchId: string, playerId: number, result: unknown) {
    return `${EARLY_EXIT_LUCKY_DRAW_MESSAGE_MARKER} matchId=${matchId}\n${EARLY_EXIT_LUCKY_DRAW_MESSAGE_KEY}`;
  }

  private buildEarlyExitResultMessage(matchId: string, playerId: number, result: unknown) {
    return this.buildEarlyExitResultMessageLocalizationKey(matchId, playerId, result);

    const playerResult = this.findPlayerResult(result, playerId);
    if (!playerResult) {
      return `${EARLY_EXIT_LUCKY_DRAW_MESSAGE_MARKER} matchId=${matchId}\nTráº­n Ä‘áº¥u Ä‘Ã£ káº¿t thÃºc. Káº¿t quáº£ cá»§a báº¡n Ä‘Ã£ Ä‘Æ°á»£c ghi nháº­n. Báº¥m nháº­n Ä‘á»ƒ má»Ÿ rÃºt thÄƒm may máº¯n.`;
    }

    const marblesWon = this.normalizeNumber((playerResult as any).marblesWon);
    const marblesLost = this.normalizeNumber((playerResult as any).marblesLost);
    const expGained = this.normalizeNumber((playerResult as any).expGained);
    const marbleDelta = marblesWon > 0 ? marblesWon : -Math.max(marblesLost, 0);
    const marbleText = `${marbleDelta >= 0 ? "+" : ""}${marbleDelta} bi`;
    const expText = `${expGained >= 0 ? "+" : ""}${expGained} EXP`;

    return `${EARLY_EXIT_LUCKY_DRAW_MESSAGE_MARKER} matchId=${matchId}\nTráº­n Ä‘áº¥u Ä‘Ã£ káº¿t thÃºc. Káº¿t quáº£ cá»§a báº¡n: ${marbleText}, ${expText}. Báº¥m nháº­n Ä‘á»ƒ má»Ÿ rÃºt thÄƒm may máº¯n.`;
  }

  private findPlayerResult(result: unknown, playerId: number) {
    if (!result || typeof result !== "object") {
      return null;
    }

    const maybeResults = (result as { overGameResults?: unknown }).overGameResults;
    if (!Array.isArray(maybeResults)) {
      return null;
    }

    return maybeResults.find((entry) => {
      if (!entry || typeof entry !== "object") {
        return false;
      }

      return this.normalizeNumber((entry as any).playerId) === playerId;
    }) ?? null;
  }

  private normalizeNumber(value: unknown) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  private bucketKey(p: EnqueueParams) {
    return `${p.region}|type:${p.typeMatchGid}|bet:${p.bet}`;
  }

  private parseBucketKey(bucket: string) {
    const [region = "", typePart = "", betPart = ""] = bucket.split("|");
    return {
      region,
      typeMatchGid: Number(typePart.replace("type:", "")) || 0,
      bet: Number(betPart.replace("bet:", "")) || 0,
    };
  }

  private async tryAllocate(bucketKey: string, p: EnqueueParams, ctx: EnqueueCtx) {
    const arr = this.queue.get(bucketKey) ?? [];
    if (arr.length < ctx.matchSize) return;

    // CCU gate: chỉ allocate nếu còn đủ slot (matchSize)
    if (this.reservedCCUSlots + ctx.matchSize > ctx.maxCCU) {
      for (const uid of arr.slice(0, ctx.matchSize)) {
        ctx.io.to(userRoom(uid)).emit("queue:blocked", { reason: "CCU_FULL", maxCCU: ctx.maxCCU });
      }
      return;
    }

    // Select only online players. If queued player is offline, remove them from queue.
    const selected: number[] = [];
    while (selected.length < ctx.matchSize && arr.length > 0) {
      const candidate = arr.shift() as number;
      if (isPlayerOnline(candidate)) {
        selected.push(candidate);
      } else {
        console.warn('Skipping offline queued player during allocation', { bucketKey, playerId: candidate });
        this.queuedUsers.delete(candidate);
        this.queuedAtByUser.delete(candidate);
      }
    }

    // Put back remaining queue
    this.queue.set(bucketKey, arr);

    if (selected.length < ctx.matchSize) {
      // Not enough online players to form a match now; requeue selected players at front
      this.queue.set(bucketKey, [...selected, ...arr]);
      for (const uid of selected) {
        this.queuedUsers.add(uid);
        if (!this.queuedAtByUser.has(uid)) {
          this.queuedAtByUser.set(uid, Date.now());
        }
      }
      console.info('Not enough online players to allocate match, will wait', { bucketKey, selectedCount: selected.length, required: ctx.matchSize });
      return;
    }

    const players = selected;
    for (const uid of players) {
      this.queuedUsers.delete(uid);
      this.queuedAtByUser.delete(uid);
    }

    const matchId = makeMatchId();
    const sessionName = makeSessionName(p.region);

    const match: MatchRecord = {
      matchId,
      sessionName,
      region: p.region,
      bet: p.bet,
      typeMatchGid: p.typeMatchGid,
      players,
      createdAt: Date.now(),
      state: "MATCH_PROPOSED",
      maxPlayers: ctx.matchSize,
      ackedPlayers: new Set<number>(),
      ackTimeoutMs: ctx.ackTimeoutMs,
      ackRetryCount: 0,
      signJoinToken: ctx.signJoinToken,
      playerJoinDeadlineMs: ctx.playerJoinDeadlineMs,
      earlyExitedPlayerIds: new Set<number>(),
      earlyExitRoomIds: new Map<number, number>(),
      dsContainerName: null,
      dsHostPort: null,
      dsContainerIp: null,
      serverReadyTimeoutMs: ctx.serverReadyTimeoutMs,
    };
    this.matches.set(matchId, match);

    // reserve CCU slots
    this.reservedCCUSlots += players.length;

    // emit proposal and wait for ack from both players
    for (const uid of players) {
      ctx.io.to(userRoom(uid)).emit("match:found", {
        matchId,
        required: ctx.matchSize,
        players: players.length,
      });
      console.info('Match proposal sent', { matchId, toPlayer: uid, players, ackTimeoutMs: ctx.ackTimeoutMs, isOnline: isPlayerOnline(uid) });
    }

    // immediate verification: if any player disconnected between selection and emit, abort and requeue
    for (const uid of players) {
      if (!isPlayerOnline(uid)) {
        console.warn('Player disconnected after proposal, aborting match allocation and requeueing', { matchId, disconnectedPlayer: uid });
        // put remaining players back to front of queue
        const remaining = players.filter((id) => id !== uid);
        const existing = this.queue.get(bucketKey) ?? [];
        this.queue.set(bucketKey, [...remaining, ...existing]);
        for (const id of remaining) {
          this.queuedUsers.add(id);
          if (!this.queuedAtByUser.has(id)) {
            this.queuedAtByUser.set(id, Date.now());
          }
        }
        // release reserved CCU slots
        this.reservedCCUSlots = Math.max(0, this.reservedCCUSlots - players.length);
        // cleanup match record
        this.matches.delete(matchId);
        return;
      }
    }

    console.info('Starting ACK timer', { matchId, ackTimeoutMs: ctx.ackTimeoutMs });
    match.ackTimer = setTimeout(() => {
      const missingPlayers = players.filter((uid) => !match.ackedPlayers.has(uid));
      const missingPlayerStates = missingPlayers.map((uid) => ({
        userId: uid,
        isOnline: isPlayerOnline(uid),
        socketState: getPlayerSocketState(uid),
      }));
      console.warn("Match ACK timeout", {
        matchId,
        players,
        ackedPlayers: Array.from(match.ackedPlayers),
        missingPlayers: missingPlayerStates,
        ackTimeoutMs: ctx.ackTimeoutMs,
        ackRetryCount: match.ackRetryCount,
      });

      const shouldRetry =
        missingPlayers.length > 0 &&
        match.ackRetryCount < 1 &&
        missingPlayerStates.every((state) => state.isOnline);

      if (shouldRetry) {
        match.ackRetryCount += 1;
        console.info("Retrying match proposal for missing ACKs", {
          matchId,
          missingPlayers,
          ackRetryCount: match.ackRetryCount,
        });
        for (const uid of missingPlayers) {
          ctx.io.to(userRoom(uid)).emit("match:found", {
            matchId,
            required: ctx.matchSize,
            players: players.length,
            retry: match.ackRetryCount,
          });
        }

        match.ackTimer = setTimeout(() => {
          const retryMissingPlayers = players.filter((uid) => !match.ackedPlayers.has(uid));
          const retryMissingStates = retryMissingPlayers.map((uid) => ({
            userId: uid,
            isOnline: isPlayerOnline(uid),
            socketState: getPlayerSocketState(uid),
          }));
          console.warn("Match ACK retry timeout", {
            matchId,
            players,
            ackedPlayers: Array.from(match.ackedPlayers),
            missingPlayers: retryMissingStates,
            ackTimeoutMs: ctx.ackTimeoutMs,
            ackRetryCount: match.ackRetryCount,
          });
          for (const miss of retryMissingPlayers) {
            ctx.io.to(userRoom(miss)).emit("match:missing_ack", { matchId });
          }
          this.cancelMatch(matchId, ctx.io, "ACK_TIMEOUT");
        }, ctx.ackTimeoutMs);
        this.matches.set(matchId, match);
        return;
      }

      // emit a notice to missing players to help debug client-side issues
      for (const miss of missingPlayers) {
        ctx.io.to(userRoom(miss)).emit('match:missing_ack', { matchId });
      }
      this.cancelMatch(matchId, ctx.io, "ACK_TIMEOUT");
    }, ctx.ackTimeoutMs);

    this.matches.set(matchId, match);
  }

  onMatchAck(args: { matchId: string; userId: number; io: any }) {
    const match = this.matches.get(args.matchId);
    if (!match) return { status: "NOT_FOUND" };

    if (!match.players.includes(args.userId)) {
      return { status: "NOT_IN_MATCH" };
    }

    if (match.state !== "MATCH_PROPOSED") {
      return { status: "IGNORED", state: match.state };
    }

    match.ackedPlayers.add(args.userId);
    console.info("Match ACK received", {
      matchId: match.matchId,
      userId: args.userId,
      ackedPlayers: Array.from(match.ackedPlayers),
      totalPlayers: match.players.length,
    });

    if (match.ackedPlayers.size >= match.players.length) {
      if (match.ackTimer) {
        clearTimeout(match.ackTimer);
        match.ackTimer = undefined;
      }

      match.state = "MATCH_CONFIRMED";
      for (const uid of match.players) {
        args.io.to(userRoom(uid)).emit("match:confirmed", {
          matchId: match.matchId,
          required: match.maxPlayers,
          players: match.players.length,
        });
      }

      void this.beginCharacterSelectionOrServerCreation(match, args.io);
    }

    this.matches.set(match.matchId, match);
    return { status: "OK", matchId: match.matchId, acked: match.ackedPlayers.size };
  }

  notifyPendingMatchForUser(userId: number, io: any) {
    const pendingMatches = this.findActiveMatchesForPlayer(userId);

    if (pendingMatches.length === 0) {
      console.info("No active matchmaking state to resync for user", { userId });
      return { status: "NO_MATCH", emittedCount: 0 };
    }

    let emittedCount = 0;
    for (const match of pendingMatches) {
      if (match.state === "MATCH_PROPOSED") {
        if (match.ackedPlayers.has(userId)) {
          continue;
        }

        io.to(userRoom(userId)).emit("match:found", {
          matchId: match.matchId,
          required: match.maxPlayers,
          players: match.players.length,
          retry: match.ackRetryCount,
          reason: "REGISTER_RESYNC",
        });
        console.info("Resent match proposal after register", {
          matchId: match.matchId,
          userId,
          ackRetryCount: match.ackRetryCount,
        });
        emittedCount += 1;
        continue;
      }

      if (match.state === "CHARACTER_SELECTING") {
        this.emitPaperLegendCharacterSelectionResync(match, userId, io);
        emittedCount += 1;
        continue;
      }

      if (match.state === "MATCH_CONFIRMED" || match.state === "SERVER_CREATING") {
        this.emitPaperLegendSelectionCompleteResync(match, userId, io);
        io.to(userRoom(userId)).emit("match:loading", {
          matchId: match.matchId,
          stage: "SERVER_CREATING",
          reason: "REGISTER_RESYNC",
        });
        console.info("Resent server-creating match state after register/heartbeat", {
          matchId: match.matchId,
          userId,
          state: match.state,
        });
        emittedCount += 1;
        continue;
      }

      if (match.state === "READY" || match.state === "IN_PROGRESS") {
        this.emitMatchReadyTicketForUser(match, userId, io, "REGISTER_RESYNC");
        emittedCount += 1;
      }
    }

    return { status: "OK", emittedCount };
  }

  async syncPendingMatchForUser(args: {
    userId: number;
    matchId?: string;
    io: any;
    signJoinToken: (payload: object) => string;
    playerJoinDeadlineMs: number;
  }) {
    const notifyResult = this.notifyPendingMatchForUser(args.userId, args.io);
    const requestedMatchId = typeof args.matchId === "string" ? args.matchId.trim() : "";
    const activeMatches = this.findActiveMatchesForPlayer(args.userId);
    let match = requestedMatchId
      ? activeMatches.find((candidate) => candidate.matchId === requestedMatchId)
      : activeMatches[0];

    if (!match && requestedMatchId && activeMatches.length > 0) {
      match = activeMatches[0];
      console.warn("HTTP matchmaking resync replaced stale requested matchId with active player match", {
        userId: args.userId,
        requestedMatchId,
        activeMatchId: match.matchId,
        activeState: match.state,
      });
    }

    if (!match) {
      const snapshot = requestedMatchId
        ? (await this.readPersistedMatchByMatchId(requestedMatchId)) ?? (await this.readPersistedMatchForUser(args.userId))
        : await this.readPersistedMatchForUser(args.userId);
      if (snapshot) {
        const belongsToPlayer = snapshot.players.includes(args.userId);
        if (!belongsToPlayer) {
          console.warn("HTTP matchmaking resync ignored Redis snapshot for different player", {
            userId: args.userId,
            requestedMatchId,
            snapshotPlayers: snapshot.players,
          });
          return {
            status: "NO_MATCH",
            emittedCount: notifyResult.emittedCount ?? 0,
          };
        }

        const response: any = {
          status: "OK",
          emittedCount: notifyResult.emittedCount ?? 0,
          matchId: snapshot.matchId,
          state: snapshot.state,
          sessionName: snapshot.sessionName,
          region: snapshot.region,
          hostPort: snapshot.dsHostPort ?? 0,
          characterSelections: snapshot.characterSelectionsCsv ?? "",
          recoveredFrom: "REDIS",
        };

        if (snapshot.state === "MATCH_CONFIRMED" || snapshot.state === "SERVER_CREATING") {
          response.matchLoadingStage = "SERVER_CREATING";
        } else if (snapshot.state === "READY" || snapshot.state === "IN_PROGRESS") {
          response.matchLoadingStage = "MATCH_READY";
          if (snapshot.sessionName && snapshot.region) {
            const deadlineMs = Date.now() + args.playerJoinDeadlineMs;
            response.ticket = {
              type: "match:ticket",
              matchId: snapshot.matchId,
              sessionName: snapshot.sessionName,
              region: snapshot.region,
              joinToken: args.signJoinToken({
                matchId: snapshot.matchId,
                userId: args.userId,
                sessionName: snapshot.sessionName,
                region: snapshot.region,
                exp: deadlineMs,
              }),
              deadlineMs,
              hostPort: snapshot.dsHostPort ?? 0,
              reason: "HTTP_RESYNC_REDIS",
            };
          }
        }

        console.info("HTTP matchmaking resync recovered from Redis", {
          userId: args.userId,
          matchId: snapshot.matchId,
          state: snapshot.state,
          hasTicket: !!response.ticket,
        });

        return response;
      }

      return {
        status: "NO_MATCH",
        emittedCount: notifyResult.emittedCount ?? 0,
      };
    }

    const response: any = {
      status: "OK",
      emittedCount: notifyResult.emittedCount ?? 0,
      matchId: match.matchId,
      state: match.state,
      sessionName: match.sessionName,
      region: match.region,
      hostPort: match.dsHostPort ?? 0,
      characterSelections: match.characterSelectionsCsv ?? "",
    };

    if (match.state === "MATCH_CONFIRMED" || match.state === "SERVER_CREATING") {
      response.matchLoadingStage = "SERVER_CREATING";
    } else if (match.state === "READY" || match.state === "IN_PROGRESS") {
      response.matchLoadingStage = "MATCH_READY";
      if (match.sessionName && match.region) {
        const deadlineMs = Date.now() + args.playerJoinDeadlineMs;
        response.ticket = {
          type: "match:ticket",
          matchId: match.matchId,
          sessionName: match.sessionName,
          region: match.region,
          joinToken: args.signJoinToken({
            matchId: match.matchId,
            userId: args.userId,
            sessionName: match.sessionName,
            region: match.region,
            exp: deadlineMs,
          }),
          deadlineMs,
          hostPort: match.dsHostPort ?? 0,
          reason: "HTTP_RESYNC",
        };
      }
    }

    console.info("HTTP matchmaking resync response prepared", {
      userId: args.userId,
      matchId: match.matchId,
      state: match.state,
      emittedCount: response.emittedCount,
      hasTicket: !!response.ticket,
    });

    return response;
  }

  private emitPaperLegendCharacterSelectionResync(match: MatchRecord, userId: number, io: any) {
    const selection = match.characterSelection;
    if (!selection) return;

    io.to(userRoom(userId)).emit("match:confirmed", {
      matchId: match.matchId,
      required: match.maxPlayers,
      players: match.players.length,
      forceStart: true,
      reason: "REGISTER_RESYNC",
    });

    const selectedHeroId = selection.selectionsByPlayerId.get(userId) ?? 0;
    const remainingSeconds = Math.max(0, (selection.deadlineMs - Date.now()) / 1000);
    io.to(userRoom(userId)).emit("paper_legend:character_selection_update", {
      type: "paper_legend:character_selection_update",
      matchId: match.matchId,
      playerId: userId,
      characterModelId: selectedHeroId,
      selectedModelIds: this.buildSelectedPaperLegendModelIdsCsv(selection),
      selectedCount: selection.selectionsByPlayerId.size,
      lockedCount: selection.lockedPlayerIds.size,
      totalCount: selection.participantIds.length,
      isLocked: selection.lockedPlayerIds.has(userId),
      remainingSeconds,
      reason: "REGISTER_RESYNC",
    });

    console.info("Resent Paper Legends character selection state after register/heartbeat", {
      matchId: match.matchId,
      userId,
      selectedHeroId,
      remainingSeconds,
    });
  }

  private emitPaperLegendSelectionCompleteResync(match: MatchRecord, userId: number, io: any) {
    if (!this.isPaperLegendMatch(match) || !match.characterSelectionsCsv) {
      return;
    }

    io.to(userRoom(userId)).emit("paper_legend:character_selection_complete", {
      type: "paper_legend:character_selection_complete",
      matchId: match.matchId,
      selections: match.characterSelectionsCsv,
      reason: "REGISTER_RESYNC",
    });
  }

  private emitMatchReadyTicketForUser(match: MatchRecord, userId: number, io: any, reason: string) {
    if (!match.sessionName || !match.region) {
      return;
    }

    this.emitPaperLegendSelectionCompleteResync(match, userId, io);
    io.to(userRoom(userId)).emit("match:loading", {
      matchId: match.matchId,
      stage: "MATCH_READY",
      reason,
    });

    const deadlineMs = Date.now() + match.playerJoinDeadlineMs;
    const token = match.signJoinToken({
      matchId: match.matchId,
      userId,
      sessionName: match.sessionName,
      region: match.region,
      exp: deadlineMs,
    });

    io.to(userRoom(userId)).emit("match:ticket", {
      matchId: match.matchId,
      sessionName: match.sessionName,
      region: match.region,
      joinToken: token,
      deadlineMs,
      hostPort: match.dsHostPort ?? 0,
      reason,
    });

    console.info("Resent match ticket after register/heartbeat", {
      matchId: match.matchId,
      userId,
      state: match.state,
      sessionName: match.sessionName,
      hostPort: match.dsHostPort,
      reason,
    });
  }

  private async beginCharacterSelectionOrServerCreation(match: MatchRecord, io: any) {
    if (this.isPaperLegendMatch(match)) {
      console.info("Begin Paper Legends character selection", {
        matchId: match.matchId,
        typeMatchGid: match.typeMatchGid,
        players: match.players,
        maxPlayers: match.maxPlayers,
      });
      await this.beginPaperLegendCharacterSelection(match, io);
      return;
    }

    console.info("Begin server creation without character selection", {
      matchId: match.matchId,
      typeMatchGid: match.typeMatchGid,
      paperLegendsTypeMatchGids: Array.from(PAPER_LEGENDS_TYPE_MATCH_GIDS),
    });
    await this.beginServerCreation(match, io);
  }

  private isPaperLegendMatch(match: MatchRecord) {
    const typeMatchGid = Number(match.typeMatchGid);
    return Number.isFinite(typeMatchGid) && PAPER_LEGENDS_TYPE_MATCH_GIDS.has(typeMatchGid);
  }

  private async beginPaperLegendCharacterSelection(match: MatchRecord, io: any) {
    if (match.state === "CHARACTER_SELECTING") return;
    if (match.state === "SERVER_CREATING" || match.state === "READY") return;

    const participantIds = await this.resolvePaperLegendParticipants(match);
    const selectableModelIds = await this.resolvePaperLegendSelectableModelIds();
    const playerNamesById = await this.resolvePlayerNames(participantIds);
    const botPlayerIds = new Set(participantIds.filter((id) => !match.players.includes(id)));
    const deadlineMs = Date.now() + PAPER_LEGENDS_CHARACTER_SELECTION_SECONDS * 1000;

    match.state = "CHARACTER_SELECTING";
    match.characterSelection = {
      participantIds,
      botPlayerIds,
      selectableModelIds,
      selectionsByPlayerId: new Map<number, number>(),
      lockedPlayerIds: new Set<number>(),
      playerNamesById,
      deadlineMs,
      botTimers: [],
    };
    this.matches.set(match.matchId, match);

    const startPayload = {
      type: "paper_legend:character_selection_start",
      matchId: match.matchId,
      playerIds: participantIds.join(","),
      playerNames: this.serializePlayerNames(playerNamesById),
      botPlayerIds: Array.from(botPlayerIds).join(","),
      totalPlayers: participantIds.length,
      realPlayerCount: match.players.length,
      botCount: botPlayerIds.size,
      selectableModelIds: selectableModelIds.join(","),
      countdownSeconds: PAPER_LEGENDS_CHARACTER_SELECTION_SECONDS,
      deadlineMs,
    };

    for (const uid of match.players) {
      io.to(userRoom(uid)).emit("paper_legend:character_selection_start", startPayload);
    }

    match.characterSelection.timer = setTimeout(() => {
      void this.finalizePaperLegendCharacterSelection(match.matchId, io, "TIMEOUT");
    }, PAPER_LEGENDS_CHARACTER_SELECTION_SECONDS * 1000);

    for (const botPlayerId of botPlayerIds) {
      const delay = this.randomInt(PAPER_LEGENDS_BOT_SELECT_MIN_MS, PAPER_LEGENDS_BOT_SELECT_MAX_MS);
      const timer = setTimeout(() => {
        void this.autoSelectPaperLegendBot(match.matchId, botPlayerId, io);
      }, delay);
      match.characterSelection.botTimers.push(timer);
    }

    this.matches.set(match.matchId, match);
  }

  async onPaperLegendCharacterSelect(args: {
    matchId: string;
    playerId: number;
    characterModelId: number;
    io: any;
    isBot?: boolean;
  }) {
    let match = this.matches.get(args.matchId);
    if (match &&
        this.isPaperLegendMatch(match) &&
        match.state === "MATCH_CONFIRMED" &&
        !match.characterSelection) {
      console.warn("Recovering Paper Legends character selection from MATCH_CONFIRMED state", {
        matchId: match.matchId,
        playerId: args.playerId,
        characterModelId: args.characterModelId,
      });
      await this.beginPaperLegendCharacterSelection(match, args.io);
      match = this.matches.get(args.matchId);
    }

    if (!match || !match.characterSelection || match.state !== "CHARACTER_SELECTING") {
      console.warn("Paper Legends character select rejected because selection is not active", {
        matchId: args.matchId,
        playerId: args.playerId,
        characterModelId: args.characterModelId,
        matchFound: !!match,
        state: match?.state ?? "NOT_FOUND",
        hasCharacterSelection: !!match?.characterSelection,
      });
      return { status: "REJECTED", reason: "CHARACTER_SELECTION_NOT_ACTIVE" };
    }

    const selection = match.characterSelection;
    if (!selection.participantIds.includes(args.playerId)) {
      return { status: "REJECTED", reason: "PLAYER_NOT_IN_SELECTION" };
    }

    if (selection.lockedPlayerIds.has(args.playerId)) {
      return {
        status: "REJECTED",
        reason: "CHARACTER_ALREADY_LOCKED",
        selectedModelIds: this.buildSelectedPaperLegendModelIdsCsv(selection),
      };
    }

    if (!selection.selectableModelIds.includes(args.characterModelId)) {
      this.emitPaperLegendSelectionRejected(match, args.io, args.playerId, args.characterModelId, "CHARACTER_NOT_SELECTABLE");
      return {
        status: "REJECTED",
        reason: "CHARACTER_NOT_SELECTABLE",
        selectedModelIds: this.buildSelectedPaperLegendModelIdsCsv(selection),
      };
    }

    const existingOwner = Array.from(selection.selectionsByPlayerId.entries())
      .find(([playerId, modelId]) => playerId !== args.playerId && modelId === args.characterModelId);
    if (existingOwner) {
      this.emitPaperLegendSelectionRejected(match, args.io, args.playerId, args.characterModelId, "CHARACTER_ALREADY_SELECTED");
      return {
        status: "REJECTED",
        reason: "CHARACTER_ALREADY_SELECTED",
        selectedModelIds: this.buildSelectedPaperLegendModelIdsCsv(selection),
      };
    }

    selection.selectionsByPlayerId.set(args.playerId, args.characterModelId);
    this.matches.set(match.matchId, match);

    this.emitPaperLegendSelectionUpdate(match, args.io, args.playerId, args.characterModelId);

    return { status: "OK" };
  }

  async onPaperLegendCharacterLock(args: {
    matchId: string;
    playerId: number;
    io: any;
  }) {
    let match = this.matches.get(args.matchId);
    if (match &&
        this.isPaperLegendMatch(match) &&
        match.state === "MATCH_CONFIRMED" &&
        !match.characterSelection) {
      console.warn("Recovering Paper Legends character selection before lock from MATCH_CONFIRMED state", {
        matchId: match.matchId,
        playerId: args.playerId,
      });
      await this.beginPaperLegendCharacterSelection(match, args.io);
      match = this.matches.get(args.matchId);
    }

    if (!match || !match.characterSelection || match.state !== "CHARACTER_SELECTING") {
      return { status: "REJECTED", reason: "CHARACTER_SELECTION_NOT_ACTIVE" };
    }

    const selection = match.characterSelection;
    if (!selection.participantIds.includes(args.playerId)) {
      return { status: "REJECTED", reason: "PLAYER_NOT_IN_SELECTION" };
    }

    if (selection.lockedPlayerIds.has(args.playerId)) {
      return { status: "OK", alreadyLocked: true };
    }

    let modelId = selection.selectionsByPlayerId.get(args.playerId) ?? 0;
    if (modelId <= 0) {
      if (!selection.botPlayerIds.has(args.playerId)) {
        return { status: "REJECTED", reason: "SELECT_CHARACTER_FIRST" };
      }

      modelId = this.pickAvailablePaperLegendModel(selection);
      if (modelId <= 0) {
        return { status: "REJECTED", reason: "NO_CHARACTER_AVAILABLE" };
      }

      selection.selectionsByPlayerId.set(args.playerId, modelId);
    }

    selection.lockedPlayerIds.add(args.playerId);
    this.matches.set(match.matchId, match);
    this.emitPaperLegendSelectionUpdate(match, args.io, args.playerId, modelId);

    if (selection.lockedPlayerIds.size >= selection.participantIds.length) {
      await this.finalizePaperLegendCharacterSelection(match.matchId, args.io, "ALL_LOCKED");
    }

    return { status: "OK" };
  }

  private async autoSelectPaperLegendBot(matchId: string, botPlayerId: number, io: any) {
    const match = this.matches.get(matchId);
    if (!match || !match.characterSelection || match.state !== "CHARACTER_SELECTING") return;
    if (match.characterSelection.lockedPlayerIds.has(botPlayerId)) return;

    const modelId = this.pickAvailablePaperLegendModel(match.characterSelection);
    if (modelId <= 0) return;

    const selected = await this.onPaperLegendCharacterSelect({
      matchId,
      playerId: botPlayerId,
      characterModelId: modelId,
      io,
      isBot: true,
    });

    if (selected.status === "OK") {
      await this.onPaperLegendCharacterLock({
        matchId,
        playerId: botPlayerId,
        io,
      });
    }
  }

  private async finalizePaperLegendCharacterSelection(matchId: string, io: any, reason: string) {
    const match = this.matches.get(matchId);
    if (!match || !match.characterSelection || match.state !== "CHARACTER_SELECTING") return;

    const selection = match.characterSelection;
    for (const playerId of selection.participantIds) {
      if (selection.selectionsByPlayerId.has(playerId)) continue;
      const modelId = this.pickAvailablePaperLegendModel(selection);
      if (modelId > 0) selection.selectionsByPlayerId.set(playerId, modelId);
    }

    this.clearPaperLegendCharacterSelectionTimers(match);

    const selectionsCsv = selection.participantIds
      .map((playerId) => `${playerId}:${selection.selectionsByPlayerId.get(playerId) ?? 0}`)
      .filter((pair) => !pair.endsWith(":0"))
      .join(",");
    const realPlayerIds = new Set(match.players);
    const botCharacterModelIdsCsv = selection.participantIds
      .filter((playerId) => !realPlayerIds.has(playerId))
      .map((playerId) => selection.selectionsByPlayerId.get(playerId) ?? 0)
      .filter((modelId) => modelId > 0)
      .join(",");

    for (const uid of match.players) {
      io.to(userRoom(uid)).emit("paper_legend:character_selection_complete", {
        type: "paper_legend:character_selection_complete",
        matchId: match.matchId,
        selections: selectionsCsv,
        reason,
      });
    }

    match.state = "MATCH_CONFIRMED";
    match.characterSelectionsCsv = selectionsCsv;
    match.botCharacterModelIdsCsv = botCharacterModelIdsCsv;
    this.matches.set(match.matchId, match);
    await this.persistMatchSnapshot(match);
    await this.beginServerCreation(match, io);
  }

  private emitPaperLegendSelectionUpdate(match: MatchRecord, io: any, playerId: number, characterModelId: number) {
    const selection = match.characterSelection;
    if (!selection) return;

    const remainingSeconds = Math.max(0, (selection.deadlineMs - Date.now()) / 1000);
    const payload = {
      type: "paper_legend:character_selection_update",
      matchId: match.matchId,
      playerId,
      characterModelId,
      selectedModelIds: this.buildSelectedPaperLegendModelIdsCsv(selection),
      selectedCount: selection.selectionsByPlayerId.size,
      lockedCount: selection.lockedPlayerIds.size,
      totalCount: selection.participantIds.length,
      isLocked: selection.lockedPlayerIds.has(playerId),
      remainingSeconds,
    };

    for (const uid of match.players) {
      io.to(userRoom(uid)).emit("paper_legend:character_selection_update", payload);
    }
  }

  private emitPaperLegendSelectionRejected(match: MatchRecord, io: any, playerId: number, characterModelId: number, reason: string) {
    if (!match.players.includes(playerId)) return;

    io.to(userRoom(playerId)).emit("paper_legend:character_selection_rejected", {
      type: "paper_legend:character_selection_rejected",
      matchId: match.matchId,
      playerId,
      characterModelId,
      selectedModelIds: match.characterSelection
        ? this.buildSelectedPaperLegendModelIdsCsv(match.characterSelection)
        : "",
      reason,
    });
  }

  private buildSelectedPaperLegendModelIdsCsv(selection: CharacterSelectionState) {
    return Array.from(new Set(selection.selectionsByPlayerId.values()))
      .filter((modelId) => modelId > 0)
      .join(",");
  }

  private pickAvailablePaperLegendModel(selection: CharacterSelectionState) {
    const used = new Set(selection.selectionsByPlayerId.values());
    const available = selection.selectableModelIds.filter((id) => id > 0 && !used.has(id));
    if (available.length === 0) return 0;
    return available[this.randomInt(0, available.length - 1)];
  }

  private async resolvePaperLegendParticipants(match: MatchRecord) {
    const participants = [...match.players];
    const botsNeeded = Math.max(0, match.maxPlayers - participants.length);
    if (botsNeeded <= 0) return participants;

    const bots = await prisma.player.findMany({
      where: {
        ProviderType: "BOT",
        IsActive: true,
        id: { notIn: participants },
      },
      select: { id: true },
    });

    this.shuffleArray(bots);
    for (const bot of bots.slice(0, botsNeeded)) {
      participants.push(bot.id);
    }

    const remainingBotsNeeded = Math.max(0, match.maxPlayers - participants.length);
    for (let i = 0; i < remainingBotsNeeded; i++) {
      participants.push(-100000 - i);
    }

    return participants;
  }

  private async resolvePaperLegendSelectableModelIds() {
    const heroes = await prisma.hero.findMany({
      where: { isActive: true, modelId: { not: null } },
      orderBy: [{ sortOrder: "asc" }, { code: "asc" }],
      select: { modelId: true },
    });

    const ids = heroes
      .map((hero) => Number(hero.modelId))
      .filter((modelId) => Number.isFinite(modelId) && modelId > 0);

    return ids.length > 0 ? ids : [10000001, 10000002, 10000003, 10000004, 10000005, 10000006];
  }

  private async resolvePlayerNames(playerIds: number[]) {
    const rows = await prisma.player.findMany({
      where: { id: { in: playerIds } },
      select: { id: true, PlayerName: true, ProviderType: true },
    });

    const names = new Map<number, string>();
    for (const row of rows) {
      const fallback = row.ProviderType === "BOT" ? `BOT ${row.id}` : `Player ${row.id}`;
      names.set(row.id, row.PlayerName?.trim() || fallback);
    }

    for (const playerId of playerIds) {
      if (names.has(playerId)) continue;
      names.set(playerId, playerId < 0 ? `BOT ${Math.abs(playerId + 100000) + 1}` : `Player ${playerId}`);
    }

    return names;
  }

  private serializePlayerNames(names: Map<number, string>) {
    return Array.from(names.entries())
      .map(([playerId, playerName]) => `${playerId}:${encodeURIComponent(playerName)}`)
      .join("|");
  }

  private clearPaperLegendCharacterSelectionTimers(match: MatchRecord) {
    const selection = match.characterSelection;
    if (!selection) return;

    if (selection.timer) clearTimeout(selection.timer);
    for (const timer of selection.botTimers) clearTimeout(timer);
    selection.timer = undefined;
    selection.botTimers = [];
  }

  private randomInt(min: number, max: number) {
    const low = Math.ceil(Math.min(min, max));
    const high = Math.floor(Math.max(min, max));
    return Math.floor(Math.random() * (high - low + 1)) + low;
  }

  private shuffleArray<T>(items: T[]) {
    for (let i = items.length - 1; i > 0; i--) {
      const j = this.randomInt(0, i);
      [items[i], items[j]] = [items[j], items[i]];
    }
  }
  private async beginServerCreation(match: MatchRecord, io: any) {
    if (match.state === "SERVER_CREATING" || match.state === "READY") {
      return;
    }

    const lockAcquired = await this.tryAcquireMatchStartLock(match.matchId);
    if (!lockAcquired) {
      console.info("Skip server creation because another process is handling this match", {
        matchId: match.matchId,
        sessionName: match.sessionName,
      });
      return;
    }

    try {
      match.state = "SERVER_CREATING";
      this.matches.set(match.matchId, match);
      await this.persistMatchSnapshot(match);

      for (const uid of match.players) {
        io.to(userRoom(uid)).emit("match:loading", { matchId: match.matchId, stage: "SERVER_CREATING" });
      }

      console.info("Begin server creation", {
        matchId: match.matchId,
        players: match.players,
        region: match.region,
        typeMatchGid: match.typeMatchGid,
      });

      const startResult = await this.startDedicatedServerForMatch(match);
      await this.persistMatchSnapshot(match);
      const currentAfterStart = this.matches.get(match.matchId);
      if (!currentAfterStart ||
          currentAfterStart.state === "FAILED" ||
          currentAfterStart.state === "CANCELLED" ||
          currentAfterStart.state === "FINISHED") {
        console.warn("Skip server creation completion because match was already cleaned up", {
          matchId: match.matchId,
          state: currentAfterStart?.state ?? "NOT_FOUND",
        });
        return;
      }

      if (startResult.kind === "READY_NOW") {
        const ready = await this.onServerReady({
          matchId: match.matchId,
          sessionName: startResult.sessionName,
          region: startResult.region,
          io,
          signJoinToken: match.signJoinToken,
          playerJoinDeadlineMs: match.playerJoinDeadlineMs,
          hostPort: startResult.hostPort,
          containerIp: startResult.containerIp,
        });

        if (!ready) {
          throw new Error("SINGLE_ROOM_READY_FAILED");
        }

        return;
      }

      if (!startResult.startedByThisProcess) {
        console.info("Server creation already handled elsewhere, skipping local timer setup", {
          matchId: match.matchId,
          sessionName: match.sessionName,
        });
        return;
      }

      if (!this.matches.has(match.matchId)) {
        console.warn("Skip server ready timeout setup because match was removed", { matchId: match.matchId });
        return;
      }

      match.serverReadyTimer = setTimeout(() => {
        this.failMatch(match.matchId, io, "SERVER_READY_TIMEOUT");
      }, match.serverReadyTimeoutMs ?? 0);

      this.matches.set(match.matchId, match);
    } catch (err) {
      const failure = normalizeDedicatedServerStartFailure(err);
      console.error("Server creation failed", {
        matchId: match.matchId,
        reason: failure.reason,
        detail: failure.detail,
        err: String(err),
      });
      this.failMatch(match.matchId, io, failure.reason, failure.detail);
      return;
    } finally {
      await this.releaseMatchStartLock(match.matchId).catch(() => {});
    }
  }

  private async startDedicatedServerForMatch(match: MatchRecord): Promise<DedicatedServerStartResult> {
    const poolEnabled = isRoomContainerPoolEnabled();
    console.info('Attempting to start DS for match', {
      matchId: match.matchId,
      region: match.region,
      typeMatchGid: match.typeMatchGid,
      poolEnabled,
    });

    // 1) Try pick IDLE container from warm pool
    let pickedName: string | null = null;
    try {
      const idle = await DockerOrchestrator.listManagedContainers({ region: match.region, mode: "IDLE" });
      const candidates = idle.filter(
        (c) => c.labels.typeMatchGid === String(match.typeMatchGid) && !this.lockedIdleContainers.has(c.name),
      );

      if (candidates.length > 0) {
        const picked = candidates[0];
        pickedName = picked.name;
        this.lockedIdleContainers.add(picked.name);
        match.dsContainerName = picked.name;

        console.info('Assigning to idle DS', { matchId: match.matchId, dsContainerName: picked.name });
        // assign to idle DS (this should be fast)
        const assignResult = await DockerOrchestrator.assignToIdleDs({
          dsContainerName: picked.name,
          matchId: match.matchId,
          sessionName: match.sessionName,
          maxPlayers: match.maxPlayers,
          realPlayerCount: match.players.length,
          bet: match.bet,
          region: match.region,
          typeMatchGid: match.typeMatchGid,
          characterSelectionsCsv: match.characterSelectionsCsv,
          botCharacterModelIdsCsv: match.botCharacterModelIdsCsv,
        });
        match.dsHostPort = assignResult.hostPort ?? null;
        match.dsContainerIp = assignResult.containerIp ?? null;
        console.info('Assigned idle DS', { matchId: match.matchId, dsContainerName: picked.name, hostPort: match.dsHostPort, containerIp: match.dsContainerIp });

        // consume idle xong thì bù lại warm pool ngay
        this.replenishWarmPool(match.region, match.typeMatchGid);

        return { kind: "WAITING_FOR_READY", startedByThisProcess: true };
      }
    } catch (err) {
      const errText = String(err ?? "");
      const isAlreadyAssigned =
        errText.includes("ALREADY_ASSIGNED") ||
        errText.includes("status=409") ||
        errText.includes("INVALID_PAYLOAD");

      if (isAlreadyAssigned) {
        console.warn('Assign already handled by another worker/container, skip fallback spawn', {
          matchId: match.matchId,
          pickedName,
          err: errText,
        });
        return { kind: "WAITING_FOR_READY", startedByThisProcess: false };
      }

      if (pickedName) this.lockedIdleContainers.delete(pickedName);
      if (poolEnabled) {
        console.warn('Assign to idle DS failed, falling back to spawn', { matchId: match.matchId, pickedName, err: String(err) });
      } else {
        console.warn('Assign/check idle DS failed in single-room mode; using fixed test session without spawning', {
          matchId: match.matchId,
          pickedName,
          err: String(err),
        });
      }
    }

    if (!poolEnabled) {
      const sessionName = getSingleRoomSessionName(match);
      const hostPort = getOptionalEnvNumber("QUICKMATCH_SINGLE_ROOM_HOST_PORT");
      const containerIp = process.env.QUICKMATCH_SINGLE_ROOM_CONTAINER_IP?.trim() || null;
      match.sessionName = sessionName;
      match.dsHostPort = hostPort;
      match.dsContainerIp = containerIp;

      console.info("Room container pool disabled; using single-room quickmatch session", {
        matchId: match.matchId,
        sessionName,
        region: match.region,
        hostPort,
        containerIp,
      });

      return {
        kind: "READY_NOW",
        sessionName,
        region: match.region,
        hostPort,
        containerIp,
      };
    }

    // 2) Fallback: spawn match container on-demand
    try {
      const info = await DockerOrchestrator.spawnMatchContainer({
        region: match.region,
        typeMatchGid: match.typeMatchGid,
        matchId: match.matchId,
        sessionName: match.sessionName,
        maxPlayers: match.maxPlayers,
        realPlayerCount: match.players.length,
        bet: match.bet,
        characterSelectionsCsv: match.characterSelectionsCsv,
        botCharacterModelIdsCsv: match.botCharacterModelIdsCsv,
      });
      // gán tên container để có thể cleanup nếu cần
      match.dsContainerName = info.name;
      match.dsHostPort = info.hostPort ?? null;
      console.info('Spawned match container', { matchId: match.matchId, containerId: info.containerId, name: info.name, hostPort: info.hostPort });

      // Post-spawn health check: verify the container survives for a few seconds
      await this.verifyContainerAlive(info.name, match.matchId);

      // fallback spawn cũng đã tiêu tốn 1 slot phục vụ match, nên vẫn cần bù pool
      this.replenishWarmPool(match.region, match.typeMatchGid);

      return { kind: "WAITING_FOR_READY", startedByThisProcess: true };
    } catch (err) {
      console.error('Failed to spawn match container', { matchId: match.matchId, err: String(err) });
      throw err;
    }
  }

  /**
   * After spawning or assigning a DS container, wait a few seconds and verify
   * it is still running. If the container crashed immediately (e.g. Unity crash),
   * we detect it early and can fail fast with diagnostic logs.
   */
  private async verifyContainerAlive(containerName: string, matchId: string) {
    const CHECKS = [
      { delayMs: 3000, label: "3s" },
      { delayMs: 5000, label: "8s" },
    ];

    const { resolveContainerRuntime } = await import("./containerRuntime");
    const dockerBin = await resolveContainerRuntime();

    for (const { delayMs, label } of CHECKS) {
      await new Promise((r) => setTimeout(r, delayMs));

      let running = false;
      try {
        const { execFile } = await import("child_process");
        const { promisify } = await import("util");
        const execAsync = promisify(execFile);
        const { stdout } = await execAsync(
          dockerBin,
          ["inspect", "-f", "{{.State.Running}}", containerName],
          { timeout: 5_000 },
        );
        running = (stdout || "").trim() === "true";
      } catch {
        running = false;
      }

      if (!running) {
        // Container already exited — grab logs for diagnosis
        let logs: string | null = null;
        try {
          const { execFile } = await import("child_process");
          const { promisify } = await import("util");
          const execAsync = promisify(execFile);
          const { stdout, stderr } = await execAsync(
            dockerBin,
            ["logs", "--tail", "80", containerName],
            { timeout: 10_000 },
          );
          logs = ((stdout || "") + "\n" + (stderr || "")).trim() || null;
        } catch { /* ignore */ }

        console.error("DS container exited prematurely", {
          matchId,
          containerName,
          checkAt: label,
          logs: logs ? logs.slice(-2000) : "(no logs)",
        });
        throw new Error(`DS_CONTAINER_EXITED_PREMATURELY at ${label}`);
      }
    }

    console.info("DS container alive check passed", { matchId, containerName });
  }

  private cancelMatch(matchId: string, io: any, reason: string) {
    const m = this.matches.get(matchId);
    if (!m) return;

    m.state = "CANCELLED";
    this.matches.set(matchId, m);

    this.reservedCCUSlots = Math.max(0, this.reservedCCUSlots - m.players.length);

    if (m.dsContainerName) this.lockedIdleContainers.delete(m.dsContainerName);

    for (const uid of m.players) {
      io.to(userRoom(uid)).emit("match:cancelled", { matchId: m.matchId, reason });
    }

    this.cleanupMatch(m);
  }

  private async failMatch(matchId: string, io: any, reason: string, detail?: string) {
    const m = this.matches.get(matchId);
    if (!m) return;

    m.state = "FAILED";
    this.matches.set(matchId, m);

    // free CCU slots
    this.reservedCCUSlots = Math.max(0, this.reservedCCUSlots - m.players.length);

    // unlock warm ds if assigned
    if (m.dsContainerName) this.lockedIdleContainers.delete(m.dsContainerName);

    // On SERVER_READY_TIMEOUT, dump DS container logs for diagnosis
    if (reason === "SERVER_READY_TIMEOUT" && m.dsContainerName) {
      try {
        const { resolveContainerRuntime } = await import("./containerRuntime");
        const { execFile } = await import("child_process");
        const { promisify } = await import("util");
        const execAsync = promisify(execFile);
        const dockerBin = await resolveContainerRuntime();
        const { stdout, stderr } = await execAsync(
          dockerBin,
          ["logs", "--tail", "100", m.dsContainerName],
          { timeout: 10_000 },
        );
        const logs = ((stdout || "") + "\n" + (stderr || "")).trim();
        if (logs) {
          console.error("DS container logs on SERVER_READY_TIMEOUT", {
            matchId,
            containerName: m.dsContainerName,
            logs: logs.slice(-3000),
          });
        }
      } catch { /* ignore */ }
    }

    for (const uid of m.players) {
      io.to(userRoom(uid)).emit("match:failed", { matchId: m.matchId, reason, detail });
    }

    this.cleanupMatch(m);

    // best-effort stop: nếu là match container theo on-demand, tên thường ds_match_<matchId>... không cố định.
    // Nếu bạn muốn stop chắc chắn, hãy truyền DS_ID/ContainerName từ DS callback READY/RESULT (optional).
    // Ở giai đoạn này, fail-safe chính là DS tự exit hoặc orchestrator cleanup theo TTL.
    try {
      // Nếu match dùng warm pool (idle container assigned), nên stop nó để tránh stuck
      if (m.dsContainerName) await DockerOrchestrator.tryStopContainerById(m.dsContainerName);
    } catch {
      // ignore
    }
  }

  private async cleanupRoomData(match: MatchRecord, result: unknown) {
    const roomId = this.resolveRoomId(result);
    const fallbackPlayerId = match.players.find((id) => Number.isFinite(id) && id > 0);
    const resolvedRoomId = roomId ?? (await this.resolveRoomIdFromPlayer(fallbackPlayerId));

    if (!resolvedRoomId) {
      return;
    }

    try {
      await leaveRoomAndCleanup(resolvedRoomId);
    } catch (error) {
      console.warn(`Không thể dọn dữ liệu phòng ${resolvedRoomId}:`, error);
    }
  }

  private cleanupMatch(match: MatchRecord) {
    this.clearPaperLegendCharacterSelectionTimers(match);
    if (match.ackTimer) clearTimeout(match.ackTimer);
    if (match.serverReadyTimer) clearTimeout(match.serverReadyTimer);
    if (match.playerJoinTimer) clearTimeout(match.playerJoinTimer);

    for (const uid of match.players) {
      this.removeUserFromQueue(uid);
    }

    void this.clearPersistedMatchSnapshot(match);
    this.matches.delete(match.matchId);
    void this.clearReadyIdempotencyKeys(match.matchId);
  }

  private removeUserFromQueue(userId: number) {
    let removed = false;

    for (const [k, arr] of this.queue.entries()) {
      const next = arr.filter((id) => id !== userId);
      if (next.length !== arr.length) {
        removed = true;
        if (next.length > 0) {
          this.queue.set(k, next);
        } else {
          this.queue.delete(k);
        }
      }
    }

    if (removed || this.queuedUsers.has(userId) || this.queuedAtByUser.has(userId)) {
      this.queuedUsers.delete(userId);
      this.queuedAtByUser.delete(userId);
      return true;
    }

    return false;
  }

  private pruneOfflineQueuedUsers() {
    for (const [k, arr] of this.queue.entries()) {
      const onlinePlayers = arr.filter((playerId) => {
        const isOnline = isPlayerOnline(playerId);
        if (!isOnline) {
          this.queuedUsers.delete(playerId);
          this.queuedAtByUser.delete(playerId);
          console.warn("Pruned offline player from matchmaking snapshot", { playerId, bucket: k });
        }

        return isOnline;
      });

      if (onlinePlayers.length > 0) {
        this.queue.set(k, onlinePlayers);
      } else {
        this.queue.delete(k);
      }
    }

    for (const userId of Array.from(this.queuedUsers)) {
      const isStillQueued = Array.from(this.queue.values()).some((arr) => arr.includes(userId));
      if (!isStillQueued) {
        this.queuedUsers.delete(userId);
        this.queuedAtByUser.delete(userId);
      }
    }
  }

  private resolveRoomId(result: unknown): number | null {
    if (!result || typeof result !== "object") return null;

    const candidate = result as { roomId?: unknown; room?: { id?: unknown } };
    const directRoomId = this.normalizeRoomId(candidate.roomId);
    if (directRoomId) return directRoomId;

    const nestedRoomId = this.normalizeRoomId(candidate.room?.id);
    if (nestedRoomId) return nestedRoomId;

    return null;
  }

  private normalizeRoomId(value: unknown): number | null {
    const parsed = Number(value);
    if (!Number.isFinite(parsed) || parsed <= 0) return null;
    return parsed;
  }

  private async resolveRoomIdFromPlayer(playerId?: number) {
    if (!playerId) return null;
    const roomInfo = await findRoomByPlayerId(playerId);
    return roomInfo?.room?.id ?? null;
  }
}




