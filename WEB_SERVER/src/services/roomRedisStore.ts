import { getRedisClient } from './redisClient';

const roomKey = (roomId: number) => `room:${roomId}`;
const roomPlayersKey = (roomId: number) => `room:${roomId}:players`;
const roomReadyKey = (roomId: number) => `room:${roomId}:ready`;
const roomIndexKey = () => 'rooms:active';
const roomCodeIndexKey = () => 'rooms:code';
const roomIdCounterKey = () => 'rooms:id:counter';

export type RoomRedisState = {
  roomId: number;
  roomCode?: string;
  roomName: string;
  bet: number;
  maxPlayers: number;
  maxRound?: number;
  mapId: number;
  createId: number;
};

export const cacheRoomState = async (state: RoomRedisState) => {
  const client = await getRedisClient();
  const roomCode = state.roomCode?.trim() || String(state.roomId);
  await client.hSet(roomKey(state.roomId), {
    roomId: String(state.roomId),
    roomCode,
    roomName: state.roomName,
    bet: String(state.bet),
    maxPlayers: String(state.maxPlayers),
    maxRound: String(state.maxRound ?? 5),
    mapId: String(state.mapId),
    createId: String(state.createId),
  });
  await client.sAdd(roomIndexKey(), String(state.roomId));
  await client.hSet(roomCodeIndexKey(), roomCode, String(state.roomId));
};

export const addRoomPlayer = async (roomId: number, playerId: number) => {
  const client = await getRedisClient();
  await client.sAdd(roomPlayersKey(roomId), String(playerId));
};

export const removeRoomPlayer = async (roomId: number, playerId: number) => {
  const client = await getRedisClient();
  await client.sRem(roomPlayersKey(roomId), String(playerId));
  await client.sRem(roomReadyKey(roomId), String(playerId));
};

export const setRoomPlayerReady = async (roomId: number, playerId: number, ready: boolean) => {
  const client = await getRedisClient();
  if (ready) {
    await client.sAdd(roomReadyKey(roomId), String(playerId));
  } else {
    await client.sRem(roomReadyKey(roomId), String(playerId));
  }
};

export const getRoomPlayers = async (roomId: number) => {
  const client = await getRedisClient();
  const members = await client.sMembers(roomPlayersKey(roomId));
  return Array.from(members).map((id) => Number(id)).filter((id) => Number.isFinite(id));
};

export const getRoomReadyPlayers = async (roomId: number) => {
  const client = await getRedisClient();
  const members = await client.sMembers(roomReadyKey(roomId));
  return Array.from(members).map((id) => Number(id)).filter((id) => Number.isFinite(id));
};

export const clearRoomState = async (roomId: number) => {
  const client = await getRedisClient();
  const roomCode = await client.hGet(roomKey(roomId), 'roomCode');
  await client.del([roomKey(roomId), roomPlayersKey(roomId), roomReadyKey(roomId)]);
  await client.sRem(roomIndexKey(), String(roomId));
  if (roomCode) {
    await client.hDel(roomCodeIndexKey(), roomCode);
  }
};

const parseRoomState = (state: Record<string, string | Buffer> | Map<string, string | Buffer> | (string | Buffer)[] | null) => {
  if (!state) {
    return null;
  }

  // Convert Map to plain object if needed
  let stateObj: Record<string, string>;
  if (state instanceof Map) {
    stateObj = Object.fromEntries(
      Array.from(state.entries()).map(([k, v]) => [
        k,
        typeof v === 'string' ? v : v.toString(),
      ])
    );
  } else if (Array.isArray(state)) {
    // Handle array format [key1, value1, key2, value2, ...]
    const entries: [string, string][] = [];
    for (let i = 0; i < state.length; i += 2) {
      const keyRaw = state[i];
      const valueRaw = state[i + 1];
      const key = typeof keyRaw === 'string' ? keyRaw : keyRaw.toString();
      const value = typeof valueRaw === 'string' ? valueRaw : valueRaw.toString();
      entries.push([key, value]);
    }
    stateObj = Object.fromEntries(entries);
  } else {
    stateObj = Object.fromEntries(
      Object.entries(state).map(([k, v]) => [
        k,
        typeof v === 'string' ? v : v.toString(),
      ])
    );
  }

  if (Object.keys(stateObj).length === 0) {
    return null;
  }

  const roomId = Number(stateObj.roomId);
  if (!Number.isFinite(roomId)) {
    return null;
  }

  return {
    roomId,
    roomCode: stateObj.roomCode || '',
    roomName: stateObj.roomName || '',
    bet: Number(stateObj.bet) || 0,
    maxPlayers: Number(stateObj.maxPlayers) || 0,
    maxRound: Number(stateObj.maxRound || stateObj.rounds) || 5,
    mapId: Number(stateObj.mapId) || 0,
    createId: Number(stateObj.createId) || 0,
  };
};

export const getRoomIdByCode = async (roomCode: string) => {
  const code = roomCode.trim();
  if (!code) {
    return null;
  }

  const client = await getRedisClient();
  const mappedRoomId = await client.hGet(roomCodeIndexKey(), code);
  if (mappedRoomId) {
    const numeric = Number(mappedRoomId);
    return Number.isFinite(numeric) ? numeric : null;
  }

  const numeric = Number(code);
  return Number.isFinite(numeric) ? numeric : null;
};

export const getCachedRoomByCode = async (roomCode: string) => {
  const roomId = await getRoomIdByCode(roomCode);
  if (!roomId) {
    return null;
  }

  const client = await getRedisClient();
  const [state, playerCount, readyCount] = await Promise.all([
    client.hGetAll(roomKey(roomId)),
    client.sCard(roomPlayersKey(roomId)),
    client.sCard(roomReadyKey(roomId)),
  ]);

  const parsed = parseRoomState(state);
  if (!parsed) {
    return null;
  }

  return {
    ...parsed,
    currentPlayers: playerCount ?? 0,
    readyPlayers: readyCount ?? 0,
  };
};

export const getCachedRoomById = async (roomId: number) => {
  if (!Number.isFinite(roomId)) {
    return null;
  }

  const client = await getRedisClient();
  const [state, playerCount, readyCount] = await Promise.all([
    client.hGetAll(roomKey(roomId)),
    client.sCard(roomPlayersKey(roomId)),
    client.sCard(roomReadyKey(roomId)),
  ]);

  const parsed = parseRoomState(state);
  if (!parsed) {
    return null;
  }

  return {
    ...parsed,
    currentPlayers: playerCount ?? 0,
    readyPlayers: readyCount ?? 0,
  };
};

export const getCachedRooms = async () => {
  const client = await getRedisClient();
  const roomIds = await client.sMembers(roomIndexKey());
  const numericRoomIds = Array.from(roomIds).map((id) => Number(id)).filter((id) => Number.isFinite(id));

  if (numericRoomIds.length === 0) {
    return [];
  }

  const entries = await Promise.all(
    numericRoomIds.map(async (roomId) => {
      const [state, playerCount, readyCount] = await Promise.all([
        client.hGetAll(roomKey(roomId)),
        client.sCard(roomPlayersKey(roomId)),
        client.sCard(roomReadyKey(roomId)),
      ]);

      const parsed = parseRoomState(state);
      if (!parsed) {
        await client.sRem(roomIndexKey(), String(roomId));
        return null;
      }

      return {
        ...parsed,
        currentPlayers: playerCount ?? 0,
        readyPlayers: readyCount ?? 0,
      };
    }),
  );

  return entries.filter((entry): entry is NonNullable<typeof entry> => Boolean(entry));
};

export const findRoomIdByPlayerId = async (playerId: number) => {
  if (!Number.isFinite(playerId) || playerId <= 0) {
    return null;
  }

  const rooms = await getCachedRooms();
  for (const room of rooms) {
    const players = await getRoomPlayers(room.roomId);
    if (players.includes(playerId)) {
      return room.roomId;
    }
  }

  return null;
};

export const createNextRoomId = async () => {
  const client = await getRedisClient();
  const nextId = await client.incr(roomIdCounterKey());
  return Number(nextId);
};

export const updateRoomCreatorId = async (roomId: number, createId: number) => {
  const client = await getRedisClient();
  await client.hSet(roomKey(roomId), { createId: String(createId) });
};
