import { RequestHandler } from 'express';
import {
  assignRoomToPlayer,
  ensureEmptyRooms,
  getEmptyRooms,
  joinUsersToRoomByNameAndDeductBets,
  findRoomByPlayerId,
  leaveRoom,
} from '../services/matchmakingService';
import { TypeMatchGid } from '../config/typeMatchGid';
import { buildWarmPoolSummary } from '../services/orchestratorWarmPool';
import { DockerOrchestrator } from '../services/orchestrator';
import { normalizeBetTransactions } from '../services/gameService';
import { createWebSocketEmitter, getPlayersRegistry } from '../websocket/registry';
import { beginRoomStartBroadcast } from '../websocket/roomStartSync';
import { isRoomContainerPoolEnabled } from '../config/roomContainerPool';

const DEFAULT_REGION = process.env.DEFAULT_REGION || 'asia';

export const availableRooms: RequestHandler = async (_req, res) => {
  try {
    const summary = await buildWarmPoolSummary();
    res.json({ availableRooms: summary.warmBuffer, ...summary });
  } catch (error) {
    if (error instanceof Error && error.message.startsWith('Docker start error')) {
      res.status(500).json({ error: 'Không thể khởi động container phòng.', detail: error.message });
      return;
    }

    if (error instanceof Error && error.message === 'DOCKER_NOT_AVAILABLE') {
      res.status(503).json({ error: 'Docker chưa sẵn sàng để khởi động server.' });
      return;
    }

    console.error('Lỗi khi đảm bảo phòng trống:', error);
    const detail = error instanceof Error ? error.message : 'Unknown error';
    res.status(500).json({ error: 'Không thể lấy danh sách phòng', detail });
  }
};

export const joinRoom: RequestHandler = async (req, res) => {
  try {
    const { userId, typeMatchGid } = req.body;
    const matchType = Number(typeMatchGid) || TypeMatchGid.MatchRandomNormal;
    if (!userId) {
      res.status(400).json({ error: 'userId is required' });
      return;
    }

    const room = await assignRoomToPlayer(Number(userId), matchType);
    res.json({
      room,
      message: 'Đã gán phòng thành công',
      typeMatchGid: matchType,
    });
  } catch (error) {
    if (error instanceof Error && error.message === 'SERVER_CAPACITY_REACHED') {
      res.status(503).json({ error: 'Server đang quá tải, vui lòng thử lại sau.' });
      return;
    }

    if (error instanceof Error && error.message === 'ROOM_FULL') {
      res.status(409).json({ error: 'Phòng đã đầy, vui lòng thử lại.' });
      return;
    }

    console.error('Lỗi khi join room:', error);
    res.status(500).json({ error: 'Không thể join room' });
  }
};

export const joinRoomBatch: RequestHandler = async (req, res) => {
  try {
    const { userIds, roomName, typeMatchGid, mapId, port, containerId, bet, maxRound, rounds, betTransactions } = req.body as {
      userIds: unknown;
      roomName: unknown;
      typeMatchGid?: unknown;
      mapId?: unknown;
      port?: unknown;
      containerId?: unknown;
      bet?: unknown;
      maxRound?: unknown;
      rounds?: unknown;
      betTransactions?: unknown;
    };
    const matchType = Number(typeMatchGid) || TypeMatchGid.MatchRandomNormal;
    const parsedMapId = typeof mapId === 'number' ? mapId : Number(mapId) || undefined;
    const parsedPort = typeof port === 'number' ? port : Number(port) || undefined;
    const parsedContainerId = typeof containerId === 'string' ? containerId.trim() : undefined;
    const parsedBet = typeof bet === 'number' ? bet : Number(bet) || 0;
    const parsedMaxRound = typeof maxRound === 'number'
      ? maxRound
      : Number(maxRound ?? rounds) || 0;
    const transactions = normalizeBetTransactions(betTransactions);

    if (typeof roomName !== 'string' || roomName.trim() === '') {
      res.status(400).json({ error: 'roomName is required' });
      return;
    }

    if (!Array.isArray(userIds) || userIds.length === 0) {
      res.status(400).json({ error: 'userIds must be a non-empty array' });
      return;
    }

    try {
      let resolvedPort = parsedPort;
      let resolvedContainerId = parsedContainerId;

      if (matchType === TypeMatchGid.MatchRoom && (!resolvedPort || !resolvedContainerId)) {
        const emptyRooms = await ensureEmptyRooms(matchType, 1);
        const availableRoom = emptyRooms.find((room) => room.containerId);

        if (availableRoom?.containerId) {
          resolvedPort = availableRoom.portNo;
          resolvedContainerId = availableRoom.containerId;

          const managedContainers = await DockerOrchestrator.listManagedContainers({
            region: DEFAULT_REGION,
            mode: 'IDLE',
          });
          const containerName = managedContainers.find((container) => container.id === availableRoom.containerId)?.name;

          if (!containerName) {
            throw new Error('ROOM_NOT_READY');
          }

          await DockerOrchestrator.assignToIdleDs({
            dsContainerName: containerName,
            matchId: roomName.trim(),
            sessionName: roomName.trim(),
            maxPlayers: (userIds as number[]).length,
            realPlayerCount: (userIds as number[]).length,
            bet: parsedBet,
            maxRound: parsedMaxRound,
            region: DEFAULT_REGION,
            typeMatchGid: matchType,
          });
        } else {
          if (!isRoomContainerPoolEnabled()) {
            throw new Error('ROOM_CONTAINER_POOL_DISABLED');
          }

          const spawned = await DockerOrchestrator.spawnMatchContainer({
            region: DEFAULT_REGION,
            typeMatchGid: matchType,
            matchId: roomName.trim(),
            sessionName: roomName.trim(),
            maxPlayers: (userIds as number[]).length,
            realPlayerCount: (userIds as number[]).length,
            bet: parsedBet,
            maxRound: parsedMaxRound,
          });

          resolvedPort = spawned.hostPort;
          resolvedContainerId = spawned.containerId;
        }
      }

      const result = await joinUsersToRoomByNameAndDeductBets(
        roomName.trim(),
        userIds as number[],
        parsedMapId,
        {
          port: resolvedPort,
          containerId: resolvedContainerId,
          typeMatchGid: matchType,
          maxRound: parsedMaxRound,
        },
        transactions,
      );

      if (matchType === TypeMatchGid.MatchRoom) {
        await ensureEmptyRooms(matchType);
        const responseRoom = result.room;
        const resolvedRoomId = responseRoom?.id ?? 0;
        const responseRoomName = responseRoom?.roomName ?? roomName.trim();
        const responsePort = responseRoom?.port ?? resolvedPort ?? 0;
        const responseMapId = responseRoom?.mapId ?? parsedMapId ?? 0;
        const players = getPlayersRegistry();
        if (players) {
          await beginRoomStartBroadcast(players, {
            roomId: resolvedRoomId,
            roomName: responseRoomName,
            port: responsePort,
            mapId: responseMapId,
          });
        } else {
          const emitter = createWebSocketEmitter();
          (userIds as number[]).forEach((userId) => {
            emitter
              .to(`user:${userId}`)
              .emit('room_start', {
                roomId: resolvedRoomId,
                roomName: responseRoomName,
                port: responsePort,
                mapId: responseMapId,
              });
          });
        }
      }

      res.json(result);
    } catch (error) {
      if (error instanceof Error && error.message === 'ROOM_NOT_FOUND') {
        res.status(404).json({ error: 'Không tìm thấy phòng' });
        return;
      }
      if (error instanceof Error && error.message === 'ROOM_NOT_READY') {
        res.status(503).json({ error: 'Không có server chờ sẵn sàng' });
        return;
      }
      if (error instanceof Error && error.message === 'ROOM_CONTAINER_POOL_DISABLED') {
        res.status(503).json({ error: 'Room container pool is disabled.' });
        return;
      }
      if (error instanceof Error && error.message === 'PORT_IN_USE') {
        res.status(409).json({ error: 'Port đã được sử dụng' });
        return;
      }

      if (
        error instanceof Error &&
        (error.message.includes('Not enough RingBall') || error.message.includes('must be a positive'))
      ) {
        res.status(400).json({ error: error.message });
        return;
      }

      console.error('Lỗi khi join room hàng loạt:', error);
      res.status(500).json({ error: 'Không thể join room' });
    }
  } catch (error) {
    console.error('Lỗi khi join room hàng loạt:', error);
    res.status(500).json({ error: 'Không thể join room' });
  }
};

export const leaveRoomController: RequestHandler = async (req, res) => {
  try {
    const { roomId, userId } = req.body;
    if (!roomId || !userId) {
      res.status(400).json({ error: 'roomId và userId là bắt buộc' });
      return;
    }

    const room = await leaveRoom(Number(roomId), Number(userId));
    res.json({
      room,
      message: 'Đã rời phòng',
    });
  } catch (error) {
    if (error instanceof Error && error.message === 'ROOM_NOT_FOUND') {
      res.status(404).json({ error: 'Không tìm thấy phòng' });
      return;
    }

    console.error('Lỗi khi leave room:', error);
    res.status(500).json({ error: 'Không thể rời phòng' });
  }
};

export const getEmptyRoomList: RequestHandler = async (_req, res) => {
  try {
    const rooms = await getEmptyRooms();
    res.json({ rooms });
  } catch (error) {
    console.error('Lỗi khi lấy phòng trống:', error);
    res.status(500).json({ error: 'Không thể lấy danh sách phòng trống' });
  }
};

export const shutdownServers: RequestHandler = async (_req, res) => {
  try {
    const activeMatches = await DockerOrchestrator.listManagedContainers({ mode: 'MATCH' });
    if (activeMatches.length > 0) {
      res.status(400).json({ error: 'Không thể tắt server khi vẫn còn phòng đang bận' });
      return;
    }

    const idleContainers = await DockerOrchestrator.listManagedContainers({ mode: 'IDLE' });
    const stopResults = await Promise.all(
      idleContainers.map((container) => DockerOrchestrator.tryStopContainerById(container.id)),
    );

    const stoppedContainers = stopResults.filter(Boolean).length;
    res.json({
      message: 'Đã tắt toàn bộ warm pool server',
      stoppedContainers,
    });
  } catch (error) {
    console.error('Lỗi khi tắt server:', error);
    const detail = error instanceof Error ? error.message : 'Unknown error';
    res.status(500).json({ error: 'Không thể tắt server', detail });
  }
};

export const getPlayerRoom: RequestHandler = async (req, res) => {
  const { playerId: playerIdParam } = req.params;
  const playerId = Number(playerIdParam);

  if (!playerIdParam || Number.isNaN(playerId)) {
    res.status(400).json({ error: 'playerId là bắt buộc và phải là số' });
    return;
  }

  try {
    const roomInfo = await findRoomByPlayerId(playerId);

    if (!roomInfo) {
      res.status(404).json({ message: 'Người chơi không tham gia phòng nào' });
      return;
    }

    res.json(roomInfo);
  } catch (error) {
    console.error('Lỗi khi lấy phòng của người chơi:', error);
    res.status(500).json({ error: 'Không thể lấy thông tin phòng của người chơi' });
  }
};
