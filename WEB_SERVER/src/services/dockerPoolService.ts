import prisma from '../models/prismaClient';
import { listRunningContainers } from './dockerService';
import { DockerOrchestrator } from './orchestrator';
import { isRoomContainerPoolEnabled } from '../config/roomContainerPool';

type EnsureIdlePoolParams = {
  onlineCount?: number;
  reason?: string;
  region?: string;
  typeMatchGid?: number;
  minIdleOverride?: number;
};

type CleanupPoolResult = {
  stoppedContainers: number;
  deletedRecords: number;
  skipped: boolean;
};

type EnsureIdlePoolResult = {
  desiredIdle: number;
  currentIdle: number;
  created: number;
  skipped: boolean;
};

const DEFAULT_REGION = process.env.DEFAULT_REGION || 'asia';
const DEFAULT_TYPE_MATCH_GID = Number(process.env.DEFAULT_IDLE_TYPE_MATCH_GID) || 0;
const BASE_IDLE_POOL = Number(process.env.MIN_IDLE_POOL ?? process.env.MIN_EMPTY_ROOMS) || 3;
const PLAYERS_PER_POOL = Number(process.env.PLAYERS_PER_POOL) || 20;
const MAX_IDLE_POOL = Number(process.env.MAX_IDLE_POOL ?? process.env.MAX_IDLE_DS_TOTAL) || 10;
const POOL_LOCK_ID = Number(process.env.DOCKER_POOL_LOCK_ID) || 42_420;

const parseHostPort = (ports: string): number | null => {
  if (!ports) return null;
  const udpMatch = ports.match(/:(\d+)->\d+\/udp/i);
  if (udpMatch?.[1]) return Number(udpMatch[1]);
  const tcpMatch = ports.match(/:(\d+)->\d+\/tcp/i);
  if (tcpMatch?.[1]) return Number(tcpMatch[1]);
  return null;
};

const resolveDesiredIdle = (onlineCount: number, minIdleOverride?: number) => {
  const baseIdle = typeof minIdleOverride === 'number' ? minIdleOverride : BASE_IDLE_POOL;
  const extraIdle = Math.floor(Math.max(0, onlineCount) / PLAYERS_PER_POOL);
  return Math.min(MAX_IDLE_POOL, baseIdle + extraIdle);
};

async function syncIdleContainersToPool(typeMatchGid: number, region: string) {
  const idleContainers = await DockerOrchestrator.listManagedContainers({ region, mode: 'IDLE' });
  const targetContainers = idleContainers.filter(
    (container) => container.labels.typeMatchGid === String(typeMatchGid),
  );

  if (targetContainers.length === 0) {
    return [] as const;
  }

  const runningContainers = await listRunningContainers();
  const portMap = new Map(runningContainers.map((container) => [container.id, container.ports]));
  const containerIds = new Set(targetContainers.map((container) => container.id));

  await Promise.all(
    targetContainers.map(async (container) => {
      const ports = portMap.get(container.id) ?? '';
      const hostPort = parseHostPort(ports);
      if (!hostPort) {
        return;
      }

      await prisma.serverPortPool.upsert({
        where: { portNo: hostPort },
        create: {
          portNo: hostPort,
          isBusy: 0,
          roomNameRef: container.name,
          containerId: container.id,
          lastUpdate: new Date(),
          typeMatchGid,
        },
        update: {
          isBusy: 0,
          roomNameRef: container.name,
          containerId: container.id,
          lastUpdate: new Date(),
          typeMatchGid,
        },
      });
    }),
  );

  const staleRecords = await prisma.serverPortPool.findMany({
    where: { isBusy: 0, typeMatchGid, containerId: { not: null } },
  });
  const stalePorts = staleRecords
    .filter((record) => record.containerId && !containerIds.has(record.containerId))
    .map((record) => record.portNo);

  if (stalePorts.length > 0) {
    await prisma.serverPortPool.deleteMany({ where: { portNo: { in: stalePorts } } });
  }

  return prisma.serverPortPool.findMany({
    where: { isBusy: 0, typeMatchGid, containerId: { not: null } },
    orderBy: { portNo: 'asc' },
  });
}

async function withPoolLock<T>(fn: () => Promise<T>): Promise<T | null> {
  try {
    const lockRows = await prisma.$queryRaw<{ locked: boolean }[]>`
      SELECT pg_try_advisory_lock(${POOL_LOCK_ID}) AS locked
    `;
    const locked = lockRows?.[0]?.locked ?? false;
    if (!locked) {
      console.log('[Pool] Bỏ qua vì không lấy được advisory lock.');
      return null;
    }

    try {
      return await fn();
    } finally {
      await prisma.$queryRaw`
        SELECT pg_advisory_unlock(${POOL_LOCK_ID})
      `;
    }
  } catch (error) {
    console.warn('[Pool] Không thể dùng advisory lock, tiếp tục chạy không lock:', error);
    return fn();
  }
}

export async function cleanupOrphanedPoolEntries(params: { region?: string } = {}): Promise<CleanupPoolResult> {
  const region = params.region ?? DEFAULT_REGION;

  const result = await withPoolLock(async () => {
    const managedContainers = await DockerOrchestrator.listManagedContainers({ region });
    const runningContainers = await listRunningContainers();
    const runningContainerIds = new Set(runningContainers.map((container) => container.id));
    const runningPorts = new Set<number>();

    for (const container of runningContainers) {
      const hostPort = parseHostPort(container.ports);
      if (hostPort) {
        runningPorts.add(hostPort);
      }
    }

    const poolRecords = await prisma.serverPortPool.findMany({
      select: { portNo: true, containerId: true },
    });
    const poolContainerIds = new Set(
      poolRecords.map((record) => record.containerId).filter((containerId): containerId is string => Boolean(containerId)),
    );

    let stoppedContainers = 0;

    for (const container of managedContainers) {
      if (poolContainerIds.has(container.id)) {
        continue;
      }

      // eslint-disable-next-line no-await-in-loop
      const stopped = await DockerOrchestrator.tryStopContainerById(container.id);
      if (stopped.ok) {
        stoppedContainers += 1;
      }
    }

    const stalePorts = poolRecords
      .filter((record) => {
        if (record.containerId) {
          return !runningContainerIds.has(record.containerId);
        }

        return !runningPorts.has(record.portNo);
      })
      .map((record) => record.portNo);

    let deletedRecords = 0;
    if (stalePorts.length > 0) {
      const deleteResult = await prisma.serverPortPool.deleteMany({ where: { portNo: { in: stalePorts } } });
      deletedRecords = deleteResult.count;
    }

    return { stoppedContainers, deletedRecords, skipped: false };
  });

  if (!result) {
    return { stoppedContainers: 0, deletedRecords: 0, skipped: true };
  }

  return result;
}

export async function ensureIdlePool(params: EnsureIdlePoolParams = {}): Promise<EnsureIdlePoolResult> {
  if (!isRoomContainerPoolEnabled()) {
    return {
      desiredIdle: 0,
      currentIdle: 0,
      created: 0,
      skipped: true,
    };
  }

  const region = params.region ?? DEFAULT_REGION;
  const typeMatchGid = params.typeMatchGid ?? DEFAULT_TYPE_MATCH_GID;
  const onlineCount = Number.isFinite(params.onlineCount) ? Number(params.onlineCount) : 0;

  const result = await withPoolLock(async () => {
    await syncIdleContainersToPool(typeMatchGid, region);

    const currentIdle = await prisma.serverPortPool.count({
      where: { isBusy: 0, typeMatchGid },
    });

    const desiredIdle = resolveDesiredIdle(onlineCount, params.minIdleOverride);
    const toCreate = Math.max(0, desiredIdle - currentIdle);

    console.log(
      `[Pool] reason=${params.reason ?? 'unknown'} online=${onlineCount} desired=${desiredIdle} idle=${currentIdle} create=${toCreate}`,
    );

    let created = 0;

    for (let i = 0; i < toCreate; i += 1) {
      try {
        // eslint-disable-next-line no-await-in-loop
        const started = await DockerOrchestrator.startDedicatedServer({
          mode: 'IDLE',
          region,
          typeMatchGid,
        });

        // eslint-disable-next-line no-await-in-loop
        await prisma.serverPortPool.upsert({
          where: { portNo: started.hostPort },
          create: {
            portNo: started.hostPort,
            isBusy: 0,
            roomNameRef: started.name,
            containerId: started.containerId,
            lastUpdate: new Date(),
            typeMatchGid,
          },
          update: {
            isBusy: 0,
            roomNameRef: started.name,
            containerId: started.containerId,
            lastUpdate: new Date(),
            typeMatchGid,
          },
        });

        created += 1;
        console.log(`[Pool] Created IDLE container name=${started.name} id=${started.containerId} image=${process.env.ROOM_DOCKER_IMAGE || 'paperlegends/unity-dedicated:latest'} port=${started.hostPort}.`);
      } catch (error) {
        console.error('[Pool] Lỗi khi tạo container IDLE:', error);
      }
    }

    return { desiredIdle, currentIdle, created, skipped: false };
  });

  if (!result) {
    return {
      desiredIdle: 0,
      currentIdle: 0,
      created: 0,
      skipped: true,
    };
  }

  return result;
}
