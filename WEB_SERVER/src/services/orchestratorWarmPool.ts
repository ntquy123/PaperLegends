import { TypeMatchGid } from "../config/typeMatchGid";
import { isRoomContainerPoolEnabled, parseBooleanEnv } from "../config/roomContainerPool";
import { DockerOrchestrator, DockerContainerInfo } from "./orchestrator";

// Periodic cleanup of dead containers (since --rm is now off by default)
let lastCleanupMs = 0;
const CLEANUP_INTERVAL_MS = 120_000; // every 2 minutes

export interface WarmupSummary {
  warmBuffer: Record<number, DockerContainerInfo[]>;
  minEmptyRooms: number; // giữ tên cũ để UI/debug không đổi nhiều
  maxRooms: number;
  region: string;
}

const MAX_ROOMS = Number(process.env.MAX_ROOMS) || 20;
const DEFAULT_REGION = process.env.DEFAULT_REGION || "asia";
const DEFAULT_MIN_IDLE_PER_TYPE = Math.max(3, Number(process.env.MIN_IDLE_DS_PER_TYPE) || 0);

// Keep both names for backward compatibility; AUTOSPAWN_ROOM takes precedence.
const isAutoSpawnRoomEnabled = () =>
  parseBooleanEnv(process.env.AUTOSPAWN_ROOM ?? process.env.AUTOSPAM_ROOM, true);
const DEFAULT_TYPES_TO_WARM: TypeMatchGid[] = [
  TypeMatchGid.MatchRandomNormal,
  TypeMatchGid.MatchRandomRank,
  TypeMatchGid.MatchRoom,
];

type EnsureWarmParams = {
  region: string;
  types: TypeMatchGid[];
  minIdlePerType: number;
};

export async function ensureWarmIdleContainers(params: EnsureWarmParams) {
  const { region, types, minIdlePerType } = params;

  // Periodic cleanup of exited/dead containers
  const now = Date.now();
  if (now - lastCleanupMs >= CLEANUP_INTERVAL_MS) {
    lastCleanupMs = now;
    DockerOrchestrator.cleanupDeadContainers().catch(() => {});
  }

  // Tạm tắt auto-spawn room/server để giảm áp lực RAM.
  // Khi cần bật lại chỉ cần set AUTOSPAM_ROOM=true.
  if (!isRoomContainerPoolEnabled() || !isAutoSpawnRoomEnabled()) {
    return;
  }

  // Luôn giữ tối thiểu 2 IDLE/container cho mỗi type để giảm thời gian chờ
  const minIdleTargetPerType = Math.max(1, minIdlePerType);
  const desiredIdleTotalByTypes = minIdleTargetPerType * Math.max(types.length, 1);
  const maxIdleTotal = Math.max(
    Number(process.env.MAX_IDLE_DS_TOTAL) || 0,
    desiredIdleTotalByTypes,
  );

  const currentIdle = await DockerOrchestrator.listManagedContainers({
    region,
    mode: "IDLE",
  });

  let remainingBudget = Math.max(0, maxIdleTotal - currentIdle.length);
  if (remainingBudget <= 0) return;
  const idleByType = new Map<string, number>();
  for (const container of currentIdle) {
    const key = container.labels.typeMatchGid;
    idleByType.set(key, (idleByType.get(key) || 0) + 1);
  }

  for (const type of types) {
    if (remainingBudget <= 0) break;

    const typeKey = String(type);
    const existing = idleByType.get(typeKey) || 0;
    const need = Math.max(0, minIdleTargetPerType - existing);
    if (need <= 0) continue;

    const spawnCount = Math.min(need, remainingBudget);

    for (let i = 0; i < spawnCount; i += 1) {
      // eslint-disable-next-line no-await-in-loop
      await DockerOrchestrator.startDedicatedServer({
        mode: "IDLE",
        region,
        typeMatchGid: type,
      });
      idleByType.set(typeKey, (idleByType.get(typeKey) || 0) + 1);
    }

    remainingBudget -= spawnCount;
  }
}

export async function getWarmPoolSummary(params: EnsureWarmParams): Promise<WarmupSummary> {
  const { region, types, minIdlePerType } = params;

  const warmBuffer: Record<number, DockerContainerInfo[]> = {};

  const current = await DockerOrchestrator.listManagedContainers({
    region,
    mode: "IDLE",
  });

  for (const type of types) {
    warmBuffer[type] = current.filter((c) => c.labels.typeMatchGid === String(type));
  }

  return {
    warmBuffer,
    minEmptyRooms: minIdlePerType,
    maxRooms: MAX_ROOMS,
    region,
  };
}

export async function buildWarmPoolSummary({
  region = DEFAULT_REGION,
  types = DEFAULT_TYPES_TO_WARM,
  minIdlePerType = DEFAULT_MIN_IDLE_PER_TYPE,
}: Partial<EnsureWarmParams> = {}): Promise<WarmupSummary> {
  await ensureWarmIdleContainers({ region, types, minIdlePerType });
  return getWarmPoolSummary({ region, types, minIdlePerType });
}
