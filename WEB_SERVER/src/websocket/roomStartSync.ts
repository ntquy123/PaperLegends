import WebSocket from 'ws';
import { getRoomPlayers } from '../services/roomRedisStore';
import type { PlayerMap } from './handlers';

type RoomStartPayload = {
  roomId: number;
  roomName: string;
  port: number;
  mapId: number;
};

type PendingRoomStart = {
  payload: RoomStartPayload;
  ackedPlayerIds: Set<number>;
  attempts: number;
  timer: NodeJS.Timeout | null;
};

const pendingStarts = new Map<number, PendingRoomStart>();
const MAX_RETRY_ATTEMPTS = 6;
const RETRY_INTERVAL_MS = 1200;

const sendRoomStartToPlayers = async (
  players: PlayerMap,
  pending: PendingRoomStart,
  forcePlayerId?: number,
) => {
  const roomId = pending.payload.roomId;
  const roomPlayers = await getRoomPlayers(roomId);
  const targetPlayerIds =
    typeof forcePlayerId === 'number'
      ? roomPlayers.includes(forcePlayerId)
        ? [forcePlayerId]
        : []
      : roomPlayers;

  if (targetPlayerIds.length === 0) {
    return { roomPlayers, sentCount: 0 };
  }

  let sentCount = 0;
  const payload = JSON.stringify({ type: 'room_start', ...pending.payload });

  targetPlayerIds.forEach((playerId) => {
    if (pending.ackedPlayerIds.has(playerId)) {
      return;
    }

    const socket = players.get(playerId);
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      return;
    }

    socket.send(payload);
    sentCount++;
  });

  return { roomPlayers, sentCount };
};

const cleanupPending = (roomId: number) => {
  const pending = pendingStarts.get(roomId);
  if (!pending) {
    return;
  }

  if (pending.timer) {
    clearTimeout(pending.timer);
    pending.timer = null;
  }
  pendingStarts.delete(roomId);
};

const scheduleRetry = (players: PlayerMap, roomId: number) => {
  const pending = pendingStarts.get(roomId);
  if (!pending) {
    return;
  }

  pending.timer = setTimeout(async () => {
    const current = pendingStarts.get(roomId);
    if (!current) {
      return;
    }

    current.attempts += 1;
    const { roomPlayers } = await sendRoomStartToPlayers(players, current);
    const allAcked = roomPlayers.length > 0 && roomPlayers.every((id) => current.ackedPlayerIds.has(id));
    if (allAcked || current.attempts >= MAX_RETRY_ATTEMPTS) {
      cleanupPending(roomId);
      return;
    }

    scheduleRetry(players, roomId);
  }, RETRY_INTERVAL_MS);
};

export const beginRoomStartBroadcast = async (players: PlayerMap, payload: RoomStartPayload) => {
  cleanupPending(payload.roomId);

  const pending: PendingRoomStart = {
    payload,
    ackedPlayerIds: new Set<number>(),
    attempts: 0,
    timer: null,
  };

  pendingStarts.set(payload.roomId, pending);
  await sendRoomStartToPlayers(players, pending);
  scheduleRetry(players, payload.roomId);
};

export const acknowledgeRoomStart = async (roomId: number, playerId: number) => {
  const pending = pendingStarts.get(roomId);
  if (!pending) {
    return;
  }

  pending.ackedPlayerIds.add(playerId);
  const roomPlayers = await getRoomPlayers(roomId);
  const allAcked = roomPlayers.length > 0 && roomPlayers.every((id) => pending.ackedPlayerIds.has(id));
  if (allAcked) {
    cleanupPending(roomId);
  }
};

export const resendPendingRoomStartForPlayer = async (players: PlayerMap, playerId: number) => {
  const roomIds = Array.from(pendingStarts.keys());
  for (const roomId of roomIds) {
    const pending = pendingStarts.get(roomId);
    if (!pending || pending.ackedPlayerIds.has(playerId)) {
      continue;
    }

    await sendRoomStartToPlayers(players, pending, playerId);
  }
};
