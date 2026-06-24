import prisma from '../models/prismaClient';
import { TypeMatchGid } from '../config/typeMatchGid';
import {
  addRoomPlayer,
  cacheRoomState,
  clearRoomState,
  createNextRoomId,
  getCachedRoomById,
  getCachedRooms,
  getRoomPlayers,
  removeRoomPlayer,
  setRoomPlayerReady,
  updateRoomCreatorId,
} from './roomRedisStore';

const MATCH_ROOM_TYPE_GID = TypeMatchGid.MatchRoom;
const MIN_CUSTOM_PLAYERS = 2;
const MAX_CUSTOM_PLAYERS = 3;
const MIN_CUSTOM_ROUNDS = 5;
const MAX_CUSTOM_ROUNDS = 10;

const findCachedRoomByName = async (roomName: string) => {
  const rooms = await getCachedRooms();
  return rooms.find((room) => room.roomName === roomName) ?? null;
};

export const createRoom = async (data: {
  userId: number;
  bet?: number;
  maxPlayer?: number;
  mapId?: number;
  roomName?: string;
  maxRound?: number;
}) => {
  const { userId, bet = 0, maxPlayer, mapId, roomName, maxRound } = data;

  if (!roomName?.trim()) {
    throw new Error('roomName is required');
  }

  if (!userId) {
    throw new Error('userId is required');
  }

  const normalizedRoomName = roomName.trim();
  const targetMaxPlayer = Math.min(
    Math.max(maxPlayer ?? MIN_CUSTOM_PLAYERS, MIN_CUSTOM_PLAYERS),
    MAX_CUSTOM_PLAYERS,
  );
  const targetMaxRound = Math.min(
    Math.max(maxRound ?? MIN_CUSTOM_ROUNDS, MIN_CUSTOM_ROUNDS),
    MAX_CUSTOM_ROUNDS,
  );

  const existingRoom = await findCachedRoomByName(normalizedRoomName);

  if (existingRoom) {
    const players = await getRoomPlayers(existingRoom.roomId);

    if (!players.includes(userId)) {
      if (players.length >= existingRoom.maxPlayers) {
        throw new Error('ROOM_FULL');
      }
      await addRoomPlayer(existingRoom.roomId, userId);
      await setRoomPlayerReady(existingRoom.roomId, userId, false);
    }

    return {
      message: 'Room created',
      roomId: existingRoom.roomId,
      roomName: existingRoom.roomName,
      mapId: existingRoom.mapId,
      bet: existingRoom.bet,
      maxPlayer: existingRoom.maxPlayers,
      maxRound: existingRoom.maxRound ?? MIN_CUSTOM_ROUNDS,
      rounds: existingRoom.maxRound ?? MIN_CUSTOM_ROUNDS,
      createId: existingRoom.createId,
      typeMatchGid: MATCH_ROOM_TYPE_GID,
    };
  }

  const roomId = await createNextRoomId();

  await cacheRoomState({
    roomId,
    roomCode: String(roomId),
    roomName: normalizedRoomName,
    bet,
    maxPlayers: targetMaxPlayer,
    maxRound: targetMaxRound,
    mapId: mapId ?? 0,
    createId: userId,
  });
  await addRoomPlayer(roomId, userId);
  await setRoomPlayerReady(roomId, userId, false);

  return {
    message: 'Room created',
    roomId,
    roomName: normalizedRoomName,
    mapId: mapId ?? 0,
    bet,
    maxPlayer: targetMaxPlayer,
    maxRound: targetMaxRound,
    rounds: targetMaxRound,
    createId: userId,
    typeMatchGid: MATCH_ROOM_TYPE_GID,
  };
};

export const joinRoom = async (roomId: number, userId: number) => {
  const room = await getCachedRoomById(roomId);

  if (!room) {
    throw new Error('ROOM_NOT_FOUND');
  }

  const player = await prisma.player.findUnique({
    where: { id: userId },
    select: { RingBall: true },
  });

  if (!player || (player.RingBall ?? 0) < room.bet) {
    throw new Error('NOT_ENOUGH_RINGBALL');
  }

  const players = await getRoomPlayers(roomId);
  if (!players.includes(userId) && players.length >= room.maxPlayers) {
    throw new Error('ROOM_FULL');
  }

  if (!players.includes(userId)) {
    await addRoomPlayer(roomId, userId);
  }
  await setRoomPlayerReady(roomId, userId, false);

  return { message: 'User joined the room successfully', room };
};

export const leaveRoom = async (roomId: number, userIds: number[]) => {
  const room = await getCachedRoomById(roomId);

  if (!room) {
    throw new Error('ROOM_NOT_FOUND');
  }

  await Promise.all(userIds.map((userId) => removeRoomPlayer(roomId, userId)));

  const remainingPlayers = await getRoomPlayers(roomId);

  if (remainingPlayers.length === 0) {
    await clearRoomState(roomId);
    return { message: 'User left the room and room closed' };
  }

  if (userIds.includes(room.createId)) {
    await updateRoomCreatorId(roomId, remainingPlayers[0]);
  }

  return { message: 'User left the room successfully' };
};

export const deleteRoom = async (roomId: number) => {
  await clearRoomState(roomId);
  return { message: 'Room deleted' };
};

export const getActiveRooms = async () => {
  return [];
};

export const updateRoomCreator = async (roomId: number, userId: number) => {
  const room = await getCachedRoomById(roomId);

  if (!room) {
    throw new Error('ROOM_NOT_FOUND');
  }

  const players = await getRoomPlayers(roomId);
  if (!players.includes(userId)) {
    throw new Error('USER_NOT_IN_ROOM');
  }

  await updateRoomCreatorId(roomId, userId);

  return {
    message: 'Room creator updated successfully',
    room: {
      ...room,
      createId: userId,
      typeMatchGid: MATCH_ROOM_TYPE_GID,
    },
  };
};

export const getUserRooms = async (roomId: number) => {
  return getRoomUsersSnapshot(roomId);
};

export const getRoomUsersSnapshot = async (roomId: number) => {
  try {
    const roomPlayerIds = await getRoomPlayers(roomId);
    if (roomPlayerIds.length === 0) {
      return [];
    }

    const players = await prisma.player.findMany({
      where: { id: { in: roomPlayerIds } },
    });

    const userMap = new Map(players.map((player) => [player.id, player]));
    return roomPlayerIds
      .map((id) => userMap.get(id))
      .filter((player): player is NonNullable<typeof player> => Boolean(player))
      .map((player) => ({
        roomId,
        userId: player.id,
        player,
        hasLeft: false,
      }));
  } catch (err) {
    console.error('❌ Lỗi khi lấy danh sách người dùng trong phòng:', err);
    throw new Error('Không thể lấy danh sách người dùng trong phòng');
  }
};

export const markRoomUserLeft = async (roomId: number, userId: number) => {
  const room = await getCachedRoomById(roomId);

  if (!room) {
    return { updated: false, message: 'Room user not found' };
  }

  const players = await getRoomPlayers(roomId);
  if (!players.includes(userId)) {
    return { updated: false, message: 'Room user not found' };
  }

  await removeRoomPlayer(roomId, userId);

  const remainingPlayers = await getRoomPlayers(roomId);
  if (remainingPlayers.length === 0) {
    await clearRoomState(roomId);
  } else if (room.createId === userId) {
    await updateRoomCreatorId(roomId, remainingPlayers[0]);
  }

  return { updated: true, message: 'Room user marked as left' };
};
