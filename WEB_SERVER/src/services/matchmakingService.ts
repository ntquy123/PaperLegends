import { exec } from 'child_process';
import util from 'util';
import crypto from 'crypto';
import { Prisma } from '@prisma/client';
import prisma from '../models/prismaClient';
import { TypeMatchGid } from '../config/typeMatchGid';
import { resolveContainerRuntime } from './containerRuntime';
import { listRunningContainers, type RunningContainer } from './dockerService';
import { DockerOrchestrator } from './orchestrator';
import { ensureWarmIdleContainers } from './orchestratorWarmPool';
import { ensureIdlePool } from './dockerPoolService';
import { getOnlinePlayerCount } from '../websocket/registry';
import { BetTransactionInput, processBetTransactions } from './gameService';
import { clearRoomState } from './roomRedisStore';
import { isRoomContainerPoolEnabled } from '../config/roomContainerPool';

const execPromise = util.promisify(exec);

export const MAX_ROOMS = Number(process.env.MAX_ROOMS) || 20;
export const MIN_EMPTY_ROOMS = Number(process.env.MIN_EMPTY_ROOMS) || 2;
const DEFAULT_MAX_PLAYERS = Number(process.env.ROOM_MAX_PLAYERS) || 4;
const DEFAULT_MATCH_TYPE_GID = TypeMatchGid.MatchRandomNormal;
const RANK_MATCH_TYPE_GID = TypeMatchGid.MatchRandomRank;
const ROOM_CONTAINER_PREFIX = process.env.ROOM_CONTAINER_PREFIX || 'paperlegends-room-';
const DEFAULT_REGION = process.env.DEFAULT_REGION || 'asia';

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

function parseHostPort(ports: string): number | null {
  if (!ports) return null;
  const udpMatch = ports.match(/:(\d+)->\d+\/udp/i);
  if (udpMatch?.[1]) return Number(udpMatch[1]);
  const tcpMatch = ports.match(/:(\d+)->\d+\/tcp/i);
  if (tcpMatch?.[1]) return Number(tcpMatch[1]);
  return null;
}

async function clearAllRoomsAndUsers() {
  await prisma.$transaction(async (tx) => {
    await tx.roomUser.deleteMany();
    await tx.room.deleteMany();
  });
}

async function syncIdleContainersToPool(typeMatchGid: number) {
  const idleContainers = await DockerOrchestrator.listManagedContainers({ region: DEFAULT_REGION, mode: 'IDLE' });
  const targetContainers = idleContainers.filter(
    (container) => container.labels.typeMatchGid === String(typeMatchGid),
  );

  if (targetContainers.length === 0) {
    return [];
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
          roomNameRef: null,
          containerId: container.id,
          lastUpdate: new Date(),
          typeMatchGid,
        },
        update: {
          isBusy: 0,
          roomNameRef: null,
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

async function cleanupRoomResources(room: { id: number; roomName: string }) {
  const portPoolRecord = await prisma.serverPortPool.findFirst({ where: { roomNameRef: room.roomName } });
  const containerId = portPoolRecord?.containerId ?? null;

  if (containerId) {
    await DockerOrchestrator.tryStopContainerById(containerId);
  } else {
    const managedContainers = await DockerOrchestrator.listManagedContainers({});
    const matchedContainer = managedContainers.find(
      (container) => container.labels.matchId === room.roomName || container.labels.sessionName === room.roomName,
    );
    if (matchedContainer?.id) {
      await DockerOrchestrator.tryStopContainerById(matchedContainer.id);
    }
  }

  const deleteConditions: Prisma.ServerPortPoolWhereInput[] = [{ roomNameRef: room.roomName }];
  if (containerId) {
    deleteConditions.push({ containerId });
  }

  await prisma.serverPortPool.deleteMany({
    where: {
      OR: deleteConditions,
    },
  });

  await prisma.roomUser.deleteMany({ where: { roomId: room.id } });
  await prisma.room.deleteMany({ where: { id: room.id } });

  try {
    await clearRoomState(room.id);
  } catch (error) {
    console.warn('Không thể xóa phòng trong Redis khi cleanup:', error);
  }
}

async function stopContainerById(containerId: string) {
  if (!containerId) {
    return;
  }

  const runningContainers = await listRunningContainers();
  const matchedContainer = runningContainers.find((container) => container.id === containerId);
  const hostPort = parseHostPort(matchedContainer?.ports ?? '');
  const containerName = matchedContainer?.name ?? '';

  try {
    const dockerRuntime = await resolveContainerRuntime();
    await execPromise(`${dockerRuntime} stop ${containerId}`);
  } catch (error) {
    console.error(`Không thể dừng container ${containerId}:`, error);
  } finally {
    await prisma.serverPortPool.deleteMany({
      where: {
        OR: [
          { containerId },
          ...(containerName ? [{ roomNameRef: containerName }] : []),
          ...(hostPort ? [{ portNo: hostPort }] : []),
        ],
      },
    });
  }
}

async function stopContainerByName(containerName: string) {
  if (!containerName) {
    return;
  }

  const runningContainers = await listRunningContainers();
  const matchedContainer = runningContainers.find((container) => container.name === containerName);
  const hostPort = parseHostPort(matchedContainer?.ports ?? '');
  const containerId = matchedContainer?.id ?? '';

  try {
    const dockerRuntime = await resolveContainerRuntime();
    await execPromise(`${dockerRuntime} stop ${containerName}`);
  } catch (error) {
    console.error(`Không thể dừng container ${containerName}:`, error);
  } finally {
    await prisma.serverPortPool.deleteMany({
      where: {
        OR: [
          { roomNameRef: containerName },
          ...(containerId ? [{ containerId }] : []),
          ...(hostPort ? [{ portNo: hostPort }] : []),
        ],
      },
    });
  }
}

export async function resetServerPortPoolIfIdle() {
  const serverPool = await prisma.serverPortPool.findMany();

  if (serverPool.length === 0) {
    return false;
  }

  const hasBusyRoom = serverPool.some((record) => record.isBusy !== 0);
  if (hasBusyRoom) {
    return false;
  }

  for (const record of serverPool) {
    if (record.containerId) {
      // eslint-disable-next-line no-await-in-loop
      await stopContainerById(record.containerId);
      continue;
    }

    if (record.roomNameRef) {
      // eslint-disable-next-line no-await-in-loop
      await stopContainerByName(record.roomNameRef);
    }
  }

  await prisma.serverPortPool.deleteMany();

  return true;
}

export async function shutdownAllServersIfIdle() {
  const serverPool = await prisma.serverPortPool.findMany();

  if (serverPool.length === 0) {
    return { deletedRecords: 0, stoppedContainers: 0 };
  }

  const busyRecords = serverPool.filter((record) => record.isBusy == 2);
  if (busyRecords.length > 0) {
    throw new Error('SERVERS_BUSY');
  }

  const roomNames = Array.from(
    new Set(
      serverPool
        .map((record) => record.roomNameRef)
        .filter((roomName): roomName is string => Boolean(roomName)),
    ),
  );
  let stoppedContainers = 0;

  for (const record of serverPool) {
    if (record.containerId) {
      // eslint-disable-next-line no-await-in-loop
      await stopContainerById(record.containerId);
      stoppedContainers += 1;
      continue;
    }

    if (record.roomNameRef) {
      // eslint-disable-next-line no-await-in-loop
      await stopContainerByName(record.roomNameRef);
      stoppedContainers += 1;
    }
  }

  if (roomNames.length > 0) {
    const rooms = await prisma.room.findMany({
      where: { roomName: { in: roomNames } },
      select: { id: true },
    });

    const roomIds = rooms.map((room) => room.id);

    if (roomIds.length > 0) {
      await prisma.roomUser.deleteMany({ where: { roomId: { in: roomIds } } });
      await prisma.room.deleteMany({ where: { id: { in: roomIds } } });
    }
  }

  const deleteResult = await prisma.serverPortPool.deleteMany();

  return { deletedRecords: deleteResult.count, stoppedContainers };
}

export async function forceShutdownAllRoomServers() {
  const serverPool = await prisma.serverPortPool.findMany();

  let runningContainers: RunningContainer[] = [];

  try {
    runningContainers = await listRunningContainers();
  } catch (error) {
    console.warn('Không thể lấy danh sách container đang chạy, vẫn tiếp tục shutdown:', error);
  }

  const roomContainers = runningContainers.filter((container) =>
    container.name.startsWith(ROOM_CONTAINER_PREFIX),
  );
  let stoppedContainers = 0;
  const stoppedContainerIds = new Set<string>();

  for (const record of serverPool) {
    if (record.containerId) {
      // eslint-disable-next-line no-await-in-loop
      await stopContainerById(record.containerId);
      stoppedContainers += 1;
      stoppedContainerIds.add(record.containerId);
      continue;
    }

    if (record.roomNameRef) {
      // eslint-disable-next-line no-await-in-loop
      await stopContainerByName(record.roomNameRef);
      stoppedContainers += 1;
    }
  }

  for (const container of roomContainers) {
    if (stoppedContainerIds.has(container.id)) {
      continue;
    }
    // eslint-disable-next-line no-await-in-loop
    await stopContainerById(container.id);
    stoppedContainers += 1;
  }

  await clearAllRoomsAndUsers();

  const deleteResult = await prisma.serverPortPool.deleteMany();

  return { deletedRecords: deleteResult.count, stoppedContainers };
}

export async function shutdownTestServer(typeMatchGid: number = TypeMatchGid.MatchRandomRank) {
  const records = await prisma.serverPortPool.findMany({ where: { typeMatchGid } });

  if (records.length === 0) {
    return { deletedRecords: 0, stoppedContainers: 0 };
  }

  const busyRecords = records.filter((record) => record.isBusy === 2);
  if (busyRecords.length > 0) {
    throw new Error('TEST_SERVER_BUSY');
  }

  const roomNames = Array.from(
    new Set(records.map((record) => record.roomNameRef).filter((roomName): roomName is string => Boolean(roomName))),
  );
  let stoppedContainers = 0;

  for (const record of records) {
    if (record.containerId) {
      // eslint-disable-next-line no-await-in-loop
      await stopContainerById(record.containerId);
      stoppedContainers += 1;
      continue;
    }

    if (record.roomNameRef) {
      // eslint-disable-next-line no-await-in-loop
      await stopContainerByName(record.roomNameRef);
      stoppedContainers += 1;
    }
  }

  if (roomNames.length > 0) {
    const rooms = await prisma.room.findMany({
      where: { roomName: { in: roomNames } },
      select: { id: true },
    });

    const roomIds = rooms.map((room) => room.id);
    if (roomIds.length > 0) {
      await prisma.roomUser.deleteMany({ where: { roomId: { in: roomIds } } });
      await prisma.room.deleteMany({ where: { id: { in: roomIds } } });
    }
  }

  const deleteResult = await prisma.serverPortPool.deleteMany({ where: { typeMatchGid } });

  return { deletedRecords: deleteResult.count, stoppedContainers };
}

export async function ensureSingleTestServer(typeMatchGid: number = TypeMatchGid.MatchRandomRank) {
  if (!isRoomContainerPoolEnabled()) {
    throw new Error('ROOM_CONTAINER_POOL_DISABLED');
  }

  const rooms = await ensureEmptyRooms(typeMatchGid, 1);
  if (rooms.length === 0) {
    throw new Error('TEST_SERVER_BUSY');
  }

  return { created: false, rooms } as const;
}

export async function ensureEmptyRooms(
  typeMatchGid: number = DEFAULT_MATCH_TYPE_GID,
  minEmptyRooms: number = MIN_EMPTY_ROOMS,
) {
  if (!isRoomContainerPoolEnabled()) {
    return [];
  }

  await ensureWarmIdleContainers({
    region: DEFAULT_REGION,
    types: [typeMatchGid],
    minIdlePerType: minEmptyRooms,
  });

  return withPortAllocationLock(async () => syncIdleContainersToPool(typeMatchGid));
}

export async function assignRoomToPlayer(userId: number, typeMatchGid: number = DEFAULT_MATCH_TYPE_GID) {
  if (!userId) {
    throw new Error('INVALID_USER');
  }

  const availableRooms = await ensureEmptyRooms(typeMatchGid);
  const targetRoom = availableRooms[0];

  if (!targetRoom) {
    throw new Error('SERVER_CAPACITY_REACHED');
  }

  const { room, containerId, roomNameRef } = await prisma.$transaction(async (tx) => {
    const poolRecord = await tx.serverPortPool.findUnique({ where: { portNo: targetRoom.portNo } });

    if (!poolRecord || poolRecord.isBusy !== 0 || !poolRecord.containerId) {
      throw new Error('ROOM_NOT_FOUND');
    }

    const assignedRoomName = poolRecord.roomNameRef || crypto.randomUUID();
    let roomRecord = await tx.room.findFirst({ where: { roomName: assignedRoomName } });

    if (roomRecord && roomRecord.currentPlayers >= roomRecord.maxPlayers) {
      throw new Error('ROOM_FULL');
    }

    if (roomRecord) {
      roomRecord = await tx.room.update({
        where: { id: roomRecord.id },
        data: { currentPlayers: { increment: 1 } },
      });
    } else {
      roomRecord = await tx.room.create({
        data: {
          roomName: assignedRoomName,
          maxPlayers: DEFAULT_MAX_PLAYERS,
          currentPlayers: 1,
          createId: userId,
          typeMatchGid,
        },
      });
    }

    await tx.roomUser.upsert({
      where: { roomId_userId: { roomId: roomRecord.id, userId } },
      create: {
        roomId: roomRecord.id,
        userId,
      },
      update: {},
    });

    await tx.serverPortPool.update({
      where: { portNo: poolRecord.portNo },
      data: {
        isBusy: 1,
        roomNameRef: roomRecord.roomName,
        lastUpdate: new Date(),
        typeMatchGid,
      },
    });

    return { room: roomRecord, containerId: poolRecord.containerId, roomNameRef: roomRecord.roomName };
  });

  await ensureEmptyRooms(typeMatchGid);
  void ensureIdlePool({
    onlineCount: getOnlinePlayerCount(),
    reason: 'api-assign',
    typeMatchGid,
  }).catch(() => {});

  try {
    const managedContainers = await DockerOrchestrator.listManagedContainers({});
    const containerName = managedContainers.find((container) => container.id === containerId)?.name;

    if (containerName) {
      await DockerOrchestrator.assignToIdleDs({
        dsContainerName: containerName,
        matchId: roomNameRef,
        sessionName: roomNameRef,
        maxPlayers: room.maxPlayers ?? DEFAULT_MAX_PLAYERS,
        realPlayerCount: room.maxPlayers ?? DEFAULT_MAX_PLAYERS,
        bet: 0,
        region: DEFAULT_REGION,
        typeMatchGid,
      });
    }
  } catch (error) {
    console.warn('Không thể gán match vào DS idle:', error);
  }

  return { ...room, port: targetRoom.portNo };
}

export async function leaveRoom(roomId: number, userId: number) {
  if (!roomId || !userId) {
    throw new Error('INVALID_LEAVE_REQUEST');
  }

  const room = await prisma.$transaction(async (tx) => {
    await tx.roomUser.deleteMany({ where: { roomId, userId } });

    const existing = await tx.room.findUnique({ where: { id: roomId } });
    if (!existing) {
      throw new Error('ROOM_NOT_FOUND');
    }

    const nextCount = Math.max(existing.currentPlayers - 1, 0);

    if (nextCount === 0) {
      await tx.room.delete({ where: { id: roomId } });
      return existing;
    }

    const updated = await tx.room.update({
      where: { id: roomId },
      data: { currentPlayers: nextCount },
    });

    return updated;
  });

  let poolRecordForCleanup: { typeMatchGid: number } | null = null;

  if (room.currentPlayers <= 0) {
    const poolRecord = await prisma.serverPortPool.findFirst({ where: { roomNameRef: room.roomName } });
    poolRecordForCleanup = poolRecord ? { typeMatchGid: poolRecord.typeMatchGid } : null;

    if (poolRecord?.containerId) {
      await DockerOrchestrator.tryStopContainerById(poolRecord.containerId);
    }

    if (poolRecord) {
      await prisma.serverPortPool.delete({ where: { portNo: poolRecord.portNo } });
    }
  }

  const typeMatchGid = poolRecordForCleanup?.typeMatchGid ?? room.typeMatchGid ?? DEFAULT_MATCH_TYPE_GID;
  await ensureEmptyRooms(typeMatchGid);

  return room;
}

export async function leaveRoomAndCleanup(roomId: number) {
  if (!roomId) {
    throw new Error('INVALID_LEAVE_REQUEST');
  }

  const room = await prisma.$transaction(async (tx) => {
    const existing = await tx.room.findUnique({ where: { id: roomId } });

    if (!existing) {
      throw new Error('ROOM_NOT_FOUND');
    }

    await tx.roomUser.deleteMany({ where: { roomId } });
    await tx.room.delete({ where: { id: roomId } });

    return existing;
  });

  const typeMatchGid = room.typeMatchGid ?? DEFAULT_MATCH_TYPE_GID;
  await ensureEmptyRooms(typeMatchGid);

  return room;
}

export async function releaseServerPortPoolEntry(portNo: number, containerId: string) {
  if (!portNo || !containerId) {
    throw new Error('INVALID_RELEASE_REQUEST');
  }

  const runningContainers = await listRunningContainers();
  const normalizedContainerId = containerId.trim();
  const containerByName = runningContainers.find((container) => container.name === normalizedContainerId);
  const resolvedContainerId = containerByName?.id ?? normalizedContainerId;
  const resolvedHostPort = parseHostPort(containerByName?.ports ?? '');

  let poolRecord = await prisma.serverPortPool.findUnique({ where: { portNo } });

  if (!poolRecord) {
    poolRecord = await prisma.serverPortPool.findFirst({ where: { containerId: resolvedContainerId } });

    if (!poolRecord && resolvedHostPort) {
      poolRecord = await prisma.serverPortPool.findUnique({ where: { portNo: resolvedHostPort } });
    }

    if (!poolRecord) {
      return { status: 'NOT_FOUND' };
    }
  }

  if (poolRecord.containerId && poolRecord.containerId !== resolvedContainerId) {
    const stillRunning = runningContainers.some((container) => container.id === poolRecord.containerId);
    if (stillRunning) {
      return { status: 'CONTAINER_MISMATCH', portNo, containerId };
    }
  }

  await prisma.serverPortPool.delete({ where: { portNo: poolRecord.portNo } });

  return { status: 'DELETED', portNo, containerId };
}

export async function getEmptyRooms() {
  return ensureEmptyRooms();
}

type JoinRoomOptions = {
  port?: number;
  containerId?: string;
  typeMatchGid?: number;
  maxRound?: number;
};

async function joinUsersToRoomByNameWithTx(
  tx: Prisma.TransactionClient,
  roomName: string,
  userIds: number[],
  mapId?: number,
  options?: JoinRoomOptions,
) {
  if (!roomName) {
    throw new Error('INVALID_ROOM_NAME');
  }

  if (!Array.isArray(userIds) || userIds.length === 0) {
    throw new Error('INVALID_USER_IDS');
  }

  const normalizedContainerId = options?.containerId?.trim() || null;
  const requestedPort = options?.port && options.port > 0 ? options.port : null;
  const resolvedTypeMatchGid = options?.typeMatchGid && options.typeMatchGid > 0 ? options.typeMatchGid : undefined;
  const resolvedMaxRound = options?.maxRound && options.maxRound > 0 ? options.maxRound : undefined;

  let portPool = await tx.serverPortPool.findFirst({ where: { roomNameRef: roomName } });

  if (!portPool && requestedPort) {
    const existingByPort = await tx.serverPortPool.findUnique({ where: { portNo: requestedPort } });

    if (existingByPort && existingByPort.roomNameRef && existingByPort.roomNameRef !== roomName) {
      throw new Error('PORT_IN_USE');
    }

    if (existingByPort) {
      portPool = await tx.serverPortPool.update({
        where: { portNo: requestedPort },
        data: {
          isBusy: 1,
          roomNameRef: roomName,
          containerId: normalizedContainerId ?? existingByPort.containerId,
          lastUpdate: new Date(),
          typeMatchGid: existingByPort.typeMatchGid ?? resolvedTypeMatchGid ?? DEFAULT_MATCH_TYPE_GID,
        },
      });
    } else {
      portPool = await tx.serverPortPool.create({
        data: {
          portNo: requestedPort,
          isBusy: 1,
          roomNameRef: roomName,
          containerId: normalizedContainerId,
          lastUpdate: new Date(),
          typeMatchGid: resolvedTypeMatchGid ?? DEFAULT_MATCH_TYPE_GID,
        },
      });
    }
  }

  if (!portPool) {
    throw new Error('ROOM_NOT_FOUND');
  }

  if (normalizedContainerId && portPool.containerId !== normalizedContainerId) {
    portPool = await tx.serverPortPool.update({
      where: { portNo: portPool.portNo },
      data: { containerId: normalizedContainerId, lastUpdate: new Date() },
    });
  }

  let roomRecord = await tx.room.findFirst({ where: { roomName } });

  if (!roomRecord) {
    const creatorId = Number(userIds[0]) || 0;
    roomRecord = await tx.room.create({
      data: {
        roomName,
        maxPlayers: DEFAULT_MAX_PLAYERS,
        currentPlayers: 0,
        createId: creatorId,
        typeMatchGid: portPool.typeMatchGid ?? resolvedTypeMatchGid ?? DEFAULT_MATCH_TYPE_GID,
        mapId: mapId ?? 0,
      },
    });
  }

  let addedCount = 0;
  const results: Array<{ userId: number; message: string } | { userId: number; error: string }> = [];

  for (const rawId of userIds) {
    const userId = Number(rawId);

    if (!userId) {
      results.push({ userId, error: 'userId is required' });
      continue;
    }

    const alreadyJoined = await tx.roomUser.findUnique({
      where: { roomId_userId: { roomId: roomRecord.id, userId } },
    });

    if (alreadyJoined) {
      results.push({ userId, message: 'User already in room' });
      continue;
    }

    await tx.roomUser.create({
      data: {
        roomId: roomRecord.id,
        userId,
      },
    });

    addedCount += 1;
    results.push({ userId, message: 'Đã gán phòng thành công' });
  }

  if (addedCount > 0) {
    roomRecord = await tx.room.update({
      where: { id: roomRecord.id },
      data: { currentPlayers: { increment: addedCount } },
    });
  }

  await tx.serverPortPool.update({
    where: { portNo: portPool.portNo },
    data: {
      isBusy: 1,
      roomNameRef: roomRecord.roomName,
      lastUpdate: new Date(),
      typeMatchGid: portPool.typeMatchGid ?? resolvedTypeMatchGid ?? undefined,
    },
  });

  return { room: { ...roomRecord, port: portPool.portNo, maxRound: resolvedMaxRound, rounds: resolvedMaxRound }, results };
}

export async function joinUsersToRoomByName(
  roomName: string,
  userIds: number[],
  mapId?: number,
  options?: JoinRoomOptions,
) {
  if (!roomName) {
    throw new Error('INVALID_ROOM_NAME');
  }

  if (!Array.isArray(userIds) || userIds.length === 0) {
    throw new Error('INVALID_USER_IDS');
  }

  return prisma.$transaction(async (tx) => joinUsersToRoomByNameWithTx(tx, roomName, userIds, mapId, options));
}

export async function joinUsersToRoomByNameAndDeductBets(
  roomName: string,
  userIds: number[],
  mapId: number | undefined,
  options: JoinRoomOptions | undefined,
  transactions: BetTransactionInput[],
) {
  if (!transactions.length) {
    return joinUsersToRoomByName(roomName, userIds, mapId, options);
  }

  return prisma.$transaction(async (tx) => {
    const joinResult = await joinUsersToRoomByNameWithTx(tx, roomName, userIds, mapId, options);
    const betHistories = await processBetTransactions(transactions, tx);
    return { ...joinResult, betHistories };
  });
}

export async function findRoomByPlayerId(playerId: number) {
  if (!playerId) {
    throw new Error('INVALID_USER');
  }

  const roomUser = await prisma.roomUser.findFirst({
    where: { userId: playerId, hasLeft: false },
    include: { room: true },
    orderBy: { joinedAt: 'desc' },
  });

  if (!roomUser || !roomUser.room) {
    return null;
  }

  return { roomUser, room: roomUser.room };
}

export async function cleanupMatchRoomsByPlayerIds(playerIds: number[]) {
  const normalizedIds = Array.from(new Set(playerIds.map((id) => Number(id)).filter((id) => Number.isFinite(id) && id > 0)));

  if (normalizedIds.length === 0) {
    return { cleanedRooms: 0 };
  }

  const roomUsers = await prisma.roomUser.findMany({
    where: { userId: { in: normalizedIds } },
    include: { room: true },
    orderBy: { joinedAt: 'desc' },
  });

  const roomMap = new Map<number, { id: number; roomName: string; typeMatchGid: number | null }>();
  for (const roomUser of roomUsers) {
    if (!roomUser.room) {
      continue;
    }
    roomMap.set(roomUser.room.id, {
      id: roomUser.room.id,
      roomName: roomUser.room.roomName,
      typeMatchGid: roomUser.room.typeMatchGid ?? null,
    });
  }

  if (roomMap.size === 0) {
    return { cleanedRooms: 0 };
  }

  for (const room of roomMap.values()) {
    await cleanupRoomResources({ id: room.id, roomName: room.roomName });
  }

  const matchTypes = new Set<number>();
  for (const room of roomMap.values()) {
    if (room.typeMatchGid != null) {
      matchTypes.add(room.typeMatchGid);
    }
  }

  for (const typeMatchGid of matchTypes) {
    await ensureEmptyRooms(typeMatchGid);
  }

  return { cleanedRooms: roomMap.size };
}
