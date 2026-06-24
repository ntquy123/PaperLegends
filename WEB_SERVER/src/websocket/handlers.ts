import WebSocket from 'ws';
import { verifyAccessToken } from '../services/authTokenService';
import prisma from '../models/prismaClient';
import { getRoomUsersSnapshot, leaveRoom as leaveRoomService } from '../services/roomService';
import { findRoomByPlayerId } from '../services/matchmakingService';
import { getFriendList } from '../services/friendService';
import {
  findRoomIdByPlayerId,
  getCachedRoomByCode,
  getCachedRoomById,
  getRoomPlayers,
  setRoomPlayerReady,
} from '../services/roomRedisStore';
import { Matchmaker } from '../services/matchmaker';
import { createWebSocketEmitter } from './registry';
import { acknowledgeRoomStart, resendPendingRoomStartForPlayer } from './roomStartSync';

export type PlayerMap = Map<number, WebSocket>;

export interface HandlerContext {
  playerId: number | null;
}

type MessageHandler = (
  ws: WebSocket,
  players: PlayerMap,
  data: any,
  context: HandlerContext
) => void | Promise<void>;

export const handleRegister: MessageHandler = (ws, players, data, context) => {
  const rawPlayerId = data?.playerId;
  const id = resolvePositivePlayerId(rawPlayerId, context.playerId);
  if (id === null) {
    if (rawPlayerId !== undefined && rawPlayerId !== null && rawPlayerId !== '') {
      ws.send(
        JSON.stringify({ type: 'error', message: 'playerId must be a positive number' })
      );
      return;
    }
    ws.send(
      JSON.stringify({ type: 'error', message: 'playerId is required for register' })
    );
    return;
  }
  const existingSocket = players.get(id);
  if (existingSocket && existingSocket !== ws) {
    if (existingSocket.readyState !== WebSocket.OPEN) {
      context.playerId = id;
      players.set(id, ws);
      resyncAuthenticatedPlayer(players, id);
      return;
    }
    ws.send(
      JSON.stringify({
        type: 'error',
        message: 'Player is already online',
        code: 'player_online'
      })
    );
    ws.close(4001, 'player_online');
    return;
  }
  context.playerId = id;
  players.set(id, ws);
  console.info('Player registered', { playerId: id, connectedSockets: players.size });

  resyncAuthenticatedPlayer(players, id);
};

const resyncAuthenticatedPlayer = (players: PlayerMap, playerId: number) => {
  const io = createWebSocketEmitter();
  Matchmaker.instance.notifyPendingMatchForUser(playerId, io);
  void resendPendingRoomStartForPlayer(players, playerId);
};

export const handleGetOnlinePlayers: MessageHandler = (ws, players) => {
  const onlineIds = Array.from(players.keys());
  ws.send(JSON.stringify({ type: 'online_players', playerIds: onlineIds }));
};

export const handleInvite: MessageHandler = (ws, players, data, context) => {
  const { playerId } = data;
  if (!playerId) {
    ws.send(
      JSON.stringify({ type: 'error', message: 'playerId is required for invite' })
    );
    return;
  }
    const target = players.get(playerId);
    target?.send(
      JSON.stringify({ type: 'invite', message: 'You have a new friend invite!' })
    );
  
};

 export const handleFriendChallenge: MessageHandler = async (ws, players, data, context) => {
  const senderId = Number(data.senderId);
  const receiverId = Number(data.receiverId);
  const bet = data.bet;
  if (!senderId || !receiverId || typeof bet === 'undefined') {
    ws.send(
      JSON.stringify({
        type: 'error',
        message: 'senderId, receiverId, and bet are required for friend_challenge',
      })
    );
    return;
  }
  const payloadRoomId = Number(data.roomId);
  let resolvedRoomId = Number.isFinite(payloadRoomId) && payloadRoomId > 0 ? payloadRoomId : null;
  if (!resolvedRoomId) {
    resolvedRoomId =
      (await findRoomByPlayerId(senderId))?.room?.id ??
      (await findRoomIdByPlayerId(senderId));
  }
  const target = players.get(receiverId);
  if (target) {
    target.send(
      JSON.stringify({
        type: 'friend_challenge',
        senderId,
        receiverId,
        bet,
        roomId: resolvedRoomId ?? 0,
      })
    );
    ws.send(
      JSON.stringify({
        type: 'friend_challenge_ack',
        message: 'Challenge sent successfully',
        receiverId,
        roomId: resolvedRoomId ?? 0,
      })
    );
  } else {
    ws.send(
      JSON.stringify({
        type: 'error',
        message: 'Target player is offline or not found',
      })
    );
  }
};

export const handleFriendChallengeResponse: MessageHandler = (
  ws,
  players,
  data,
  context
) => {
  const { senderId, receiverId, bet, accepted } = data;
  const payloadRoomId = Number(data?.roomId);
  const roomId = Number.isFinite(payloadRoomId) && payloadRoomId > 0 ? payloadRoomId : 0;
  if (
    !senderId ||
    !receiverId ||
    typeof bet === 'undefined' ||
    typeof accepted === 'undefined'
  ) {
    ws.send(
      JSON.stringify({
        type: 'error',
        message:
          'senderId, receiverId, bet, and accepted are required for friend_challenge_response',
      })
    );
    return;
  }
  if (senderId !== context.playerId) {
    ws.send(
      JSON.stringify({
        type: 'error',
        message: 'senderId does not match current player',
      })
    );
    return;
  }

    const target = players.get(receiverId);
    if (!target) {
      ws.send(JSON.stringify({ type: 'error', message: 'Target player not found' }));
      return;
    }
    target.send(
      JSON.stringify({
        type: 'friend_challenge_response_fromSocket',
        senderId,
        receiverId,
        bet,
        roomId,
        accepted,
      })
    );

};
export const handleHeartbeat: MessageHandler = async (ws, players, data, context) => {
  const accessToken = typeof data?.accessToken === 'string' ? data.accessToken.trim() : '';

  if (!accessToken) {
    ws.send(JSON.stringify({ type: 'error', message: 'accessToken is required for heartbeat' }));
    return;
  }

  try {
    const payload = await verifyAccessToken(accessToken);
    const existingSocket = players.get(payload.userId);
    if (existingSocket && existingSocket !== ws) {
      if (existingSocket.readyState !== WebSocket.OPEN) {
        context.playerId = payload.userId;
        players.set(payload.userId, ws);
        ws.send(
          JSON.stringify({
            type: 'heartbeat',
            status: 'ok',
            userId: payload.userId,
            deviceId: payload.deviceId,
          })
        );
        resyncAuthenticatedPlayer(players, payload.userId);
        return;
      }
      ws.send(
        JSON.stringify({
          type: 'error',
          message: 'Player is already online',
          code: 'player_online'
        })
      );
      ws.close(4001, 'player_online');
      return;
    }
    const shouldResync = context.playerId !== payload.userId || existingSocket !== ws;
    context.playerId = payload.userId;
    players.set(payload.userId, ws);

    ws.send(
      JSON.stringify({
        type: 'heartbeat',
        status: 'ok',
        userId: payload.userId,
        deviceId: payload.deviceId,
      })
    );
    if (shouldResync) {
      resyncAuthenticatedPlayer(players, payload.userId);
    }
  } catch (error: any) {
    ws.send(
      JSON.stringify({
        type: 'error',
        message: error?.message ?? 'Unauthorized',
        code: 'unauthorized',
      })
    );
    ws.close(4001, 'unauthorized');
  }
};
export const handleCheckPlayerOnline: MessageHandler = (ws, players, data, context) => {
  const rawPlayerId = data?.playerId;
  const resolvedPlayerId = resolvePositivePlayerId(rawPlayerId, context.playerId);
  if (resolvedPlayerId === null) {
    if (rawPlayerId !== undefined && rawPlayerId !== null && rawPlayerId !== '') {
      ws.send(
        JSON.stringify({
          type: 'error',
          message: 'playerId must be a positive number for check_player_online',
        })
      );
      return;
    }
    ws.send(
      JSON.stringify({
        type: 'error',
        message: 'playerId is required for check_player_online',
      })
    );
    return;
  }
  const isOnline = players.has(resolvedPlayerId);
  ws.send(
    JSON.stringify({ type: 'check_player_online', playerId: resolvedPlayerId, isOnline })
  );
};

const resolvePositivePlayerId = (
  rawPlayerId: unknown,
  fallbackPlayerId: number | null
) => {
  const candidateIds = [
    rawPlayerId,
    fallbackPlayerId,
  ];

  for (const candidate of candidateIds) {
    const playerId = Number(candidate);
    if (Number.isFinite(playerId) && playerId > 0) {
      return playerId;
    }
  }

  return null;
};

const resolvePlayerId = (data: any, context: HandlerContext) => {
  const candidates = [data?.playerId, data?.userId, context.playerId];

  for (const candidate of candidates) {
    const playerId = Number(candidate);
    if (Number.isFinite(playerId) && playerId > 0) {
      return playerId;
    }
  }

  return null;
};

const leaveRoomForPlayer = async (playerId: number, roomId?: number) => {
  const resolvedRoomId =
    roomId ||
    (await findRoomByPlayerId(playerId))?.room?.id ||
    (await findRoomIdByPlayerId(playerId));
  if (!resolvedRoomId) {
    return null;
  }

  try {
    await leaveRoomService(resolvedRoomId, [playerId]);
    return resolvedRoomId;
  } catch (error) {
    if (error instanceof Error && error.message === 'ROOM_NOT_FOUND') {
      return null;
    }
    throw error;
  }
};

const sendRoomUsersUpdate = async (players: PlayerMap, roomId: number, targetId?: number) => {
  const users = await getRoomUsersSnapshot(roomId);
  const payload = JSON.stringify({ type: 'room_users', roomId, users });
  const targetIds =
    typeof targetId === 'number'
      ? [targetId]
      : users.map((user) => user.userId).filter((id) => Number.isFinite(id));

  targetIds.forEach((id) => {
    const socket = players.get(id);
    if (socket && socket.readyState === WebSocket.OPEN) {
      socket.send(payload);
    }
  });
};

const resolveRoomOwnerId = async (roomId: number) => {
  const room = await getCachedRoomById(roomId);
  return room?.createId ?? null;
};

export const handleRoomLeave: MessageHandler = async (ws, players, data, context) => {
  const playerId = resolvePlayerId(data, context);
  if (!playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId is required for room_leave' }));
    return;
  }

  if (context.playerId && context.playerId !== playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId mismatch for room_leave' }));
    return;
  }

  const roomId = Number(data?.roomId);
  const normalizedRoomId = Number.isFinite(roomId) ? roomId : undefined;

  try {
    const resolvedRoomId = await leaveRoomForPlayer(playerId, normalizedRoomId);
    ws.send(
      JSON.stringify({
        type: 'room_leave',
        status: resolvedRoomId ? 'ok' : 'noop',
        roomId: resolvedRoomId ?? null,
      }),
    );
    if (resolvedRoomId) {
      await sendRoomUsersUpdate(players, resolvedRoomId);
    }
  } catch (error) {
    console.error('Lá»—i khi xá»­ lÃ½ room_leave:', error);
    ws.send(JSON.stringify({ type: 'error', message: 'KhÃ´ng thá»ƒ rá»i phÃ²ng' }));
  }
};

export const handleRoomReady: MessageHandler = async (ws, players, data, context) => {
  const playerId = resolvePlayerId(data, context);
  if (!playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId is required for room_ready' }));
    return;
  }

  if (context.playerId && context.playerId !== playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId mismatch for room_ready' }));
    return;
  }

  const roomId = Number(data?.roomId);
  if (!Number.isFinite(roomId)) {
    ws.send(JSON.stringify({ type: 'error', message: 'roomId is required for room_ready' }));
    return;
  }

  const ready = Boolean(data?.ready);

  try {
    await setRoomPlayerReady(roomId, playerId, ready);
    const roomPlayers = await getRoomPlayers(roomId);

    roomPlayers.forEach((id) => {
      const socket = players.get(id);
      if (socket && socket.readyState === WebSocket.OPEN) {
        socket.send(
          JSON.stringify({
            type: 'room_ready_update',
            roomId,
            playerId,
            ready,
          }),
        );
      }
    });
  } catch (error) {
    console.error('Lá»—i khi xá»­ lÃ½ room_ready:', error);
    ws.send(JSON.stringify({ type: 'error', message: 'KhÃ´ng thá»ƒ cáº­p nháº­t tráº¡ng thÃ¡i sáºµn sÃ ng' }));
  }
};

export const handleRoomStartCancel: MessageHandler = async (ws, players, data, context) => {
  const playerId = resolvePlayerId(data, context);
  if (!playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId is required for room_start_cancel' }));
    return;
  }

  if (context.playerId && context.playerId !== playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId mismatch for room_start_cancel' }));
    return;
  }

  const roomId = Number(data?.roomId);
  if (!Number.isFinite(roomId)) {
    ws.send(JSON.stringify({ type: 'error', message: 'roomId is required for room_start_cancel' }));
    return;
  }

  try {
    const ownerId = await resolveRoomOwnerId(roomId);
    if (!ownerId || ownerId !== playerId) {
      ws.send(JSON.stringify({ type: 'error', message: 'Only room owner can cancel start' }));
      return;
    }

    const roomPlayers = await getRoomPlayers(roomId);
    roomPlayers.forEach((id) => {
      const socket = players.get(id);
      if (socket && socket.readyState === WebSocket.OPEN) {
        socket.send(
          JSON.stringify({
            type: 'room_start_cancel',
            roomId,
            playerId,
          }),
        );
      }
    });
  } catch (error) {
    console.error('Lá»—i khi xá»­ lÃ½ room_start_cancel:', error);
    ws.send(JSON.stringify({ type: 'error', message: 'KhÃ´ng thá»ƒ há»§y báº¯t Ä‘áº§u phÃ²ng' }));
  }
};

export const handleRoomKick: MessageHandler = async (ws, players, data, context) => {
  const requesterId = resolvePlayerId(data, context);
  if (!requesterId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId is required for room_kick' }));
    return;
  }

  if (context.playerId && context.playerId !== requesterId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId mismatch for room_kick' }));
    return;
  }

  const roomId = Number(data?.roomId);
  const targetId = Number(data?.playerId);
  if (!Number.isFinite(roomId) || !Number.isFinite(targetId)) {
    ws.send(JSON.stringify({ type: 'error', message: 'roomId and playerId are required for room_kick' }));
    return;
  }

  if (requesterId === targetId) {
    ws.send(JSON.stringify({ type: 'error', message: 'Cannot kick yourself' }));
    return;
  }

  try {
    const ownerId = await resolveRoomOwnerId(roomId);
    if (!ownerId || ownerId !== requesterId) {
      ws.send(JSON.stringify({ type: 'error', message: 'Only room owner can kick players' }));
      return;
    }

    await leaveRoomService(roomId, [targetId]);
    await sendRoomUsersUpdate(players, roomId);

    const targetSocket = players.get(targetId);
    if (targetSocket && targetSocket.readyState === WebSocket.OPEN) {
      targetSocket.send(
        JSON.stringify({
          type: 'room_kicked',
          roomId,
          playerId: targetId,
          requesterId,
        }),
      );
    }
  } catch (error) {
    console.error('Lá»—i khi xá»­ lÃ½ room_kick:', error);
    ws.send(JSON.stringify({ type: 'error', message: 'KhÃ´ng thá»ƒ Ä‘uá»•i ngÆ°á»i chÆ¡i khá»i phÃ²ng' }));
  }
};

export const handleRoomUsers: MessageHandler = async (ws, players, data, context) => {
  const playerId = resolvePlayerId(data, context);
  if (!playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId is required for room_users' }));
    return;
  }

  if (context.playerId && context.playerId !== playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId mismatch for room_users' }));
    return;
  }

  const roomId = Number(data?.roomId);
  if (!Number.isFinite(roomId)) {
    ws.send(JSON.stringify({ type: 'error', message: 'roomId is required for room_users' }));
    return;
  }

  try {
    await sendRoomUsersUpdate(players, roomId, playerId);
    await resendPendingRoomStartForPlayer(players, playerId);
  } catch (error) {
    console.error('Lá»—i khi xá»­ lÃ½ room_users:', error);
    ws.send(JSON.stringify({ type: 'error', message: 'KhÃ´ng thá»ƒ láº¥y danh sÃ¡ch ngÆ°á»i chÆ¡i' }));
  }
};

export const handleRoomStartAck: MessageHandler = async (ws, _players, data, context) => {
  const playerId = resolvePlayerId(data, context);
  if (!playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId is required for room_start_ack' }));
    return;
  }

  if (context.playerId && context.playerId !== playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId mismatch for room_start_ack' }));
    return;
  }

  const roomId = Number(data?.roomId);
  if (!Number.isFinite(roomId)) {
    ws.send(JSON.stringify({ type: 'error', message: 'roomId is required for room_start_ack' }));
    return;
  }

  await acknowledgeRoomStart(roomId, playerId);
};

export const handleRoomChat: MessageHandler = async (ws, players, data, context) => {
  const playerId = resolvePlayerId(data, context);
  if (!playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId is required for room_chat' }));
    return;
  }

  if (context.playerId && context.playerId !== playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId mismatch for room_chat' }));
    return;
  }

  const roomId = Number(data?.roomId);
  if (!Number.isFinite(roomId)) {
    ws.send(JSON.stringify({ type: 'error', message: 'roomId is required for room_chat' }));
    return;
  }

  const message = typeof data?.message === 'string' ? data.message.trim() : '';
  if (!message) {
    ws.send(JSON.stringify({ type: 'error', message: 'message is required for room_chat' }));
    return;
  }

  const senderName = typeof data?.senderName === 'string' ? data.senderName.trim() : '';

  try {
    const roomPlayers = await getRoomPlayers(roomId);
    if (roomPlayers.length === 0) {
      ws.send(JSON.stringify({ type: 'error', message: 'room does not exist or has no players' }));
      return;
    }

    if (!roomPlayers.includes(playerId)) {
      ws.send(JSON.stringify({ type: 'error', message: 'player is not in room' }));
      return;
    }

    const payload = JSON.stringify({
      type: 'room_chat',
      roomId,
      senderId: playerId,
      senderName,
      message,
    });

    roomPlayers.forEach((id) => {
      const socket = players.get(id);
      if (socket && socket.readyState === WebSocket.OPEN) {
        socket.send(payload);
      }
    });
  } catch (error) {
    console.error('Lá»—i khi xá»­ lÃ½ room_chat:', error);
    ws.send(JSON.stringify({ type: 'error', message: 'KhÃ´ng thá»ƒ gá»­i tin nháº¯n phÃ²ng' }));
  }
};

export const handleRoomLookup: MessageHandler = async (ws, _players, data, context) => {
  const playerId = resolvePlayerId(data, context);
  if (!playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId is required for room_lookup' }));
    return;
  }

  if (context.playerId && context.playerId !== playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId mismatch for room_lookup' }));
    return;
  }

  const roomCode = typeof data?.roomCode === 'string' ? data.roomCode.trim() : '';
  if (!roomCode) {
    ws.send(JSON.stringify({ type: 'room_lookup_result', success: false, message: 'roomCode is required' }));
    return;
  }

  try {
    const room = await getCachedRoomByCode(roomCode);
    if (!room) {
      ws.send(JSON.stringify({
        type: 'room_lookup_result',
        success: false,
        roomCode,
        message: 'Room not found',
      }));
      return;
    }

    ws.send(JSON.stringify({
      type: 'room_lookup_result',
      success: true,
      roomCode,
      roomId: room.roomId,
      roomName: room.roomName,
      bet: room.bet,
      maxPlayers: room.maxPlayers,
      mapId: room.mapId,
      createId: room.createId,
      currentPlayers: room.currentPlayers,
    }));
  } catch (error) {
    console.error('Lá»—i khi xá»­ lÃ½ room_lookup:', error);
    ws.send(JSON.stringify({ type: 'room_lookup_result', success: false, roomCode, message: 'KhÃ´ng thá»ƒ tÃ¬m phÃ²ng' }));
  }
};

export const handleFriendList: MessageHandler = async (ws, players, data, context) => {
  const playerId = resolvePlayerId(data, context);
  if (!playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId is required for friend_list' }));
    return;
  }

  if (context.playerId && context.playerId !== playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId mismatch for friend_list' }));
    return;
  }

  try {
    const result = await getFriendList(playerId);
    if (!result.success) {
      ws.send(JSON.stringify({ type: 'error', message: result.message ?? 'KhÃ´ng thá»ƒ láº¥y danh sÃ¡ch báº¡n bÃ¨' }));
      return;
    }

    const friends = (result.data ?? []).map((friend: any) => {
      const friendId = Number(friend.id ?? friend.playerId ?? friend.PlayerId);
      return {
        playerId: Number.isFinite(friendId) ? friendId : 0,
        level: Number(friend.Level ?? friend.level ?? 0),
        fullname: String(friend.PlayerName ?? friend.fullname ?? ''),
        isOnline: Number.isFinite(friendId) ? players.has(friendId) : false,
      };
    });

    ws.send(JSON.stringify({ type: 'friend_list', playerId, friends }));
  } catch (error) {
    console.error('Lá»—i khi xá»­ lÃ½ friend_list:', error);
    ws.send(JSON.stringify({ type: 'error', message: 'KhÃ´ng thá»ƒ láº¥y danh sÃ¡ch báº¡n bÃ¨' }));
  }
};

export const handleMatchAck: MessageHandler = async (ws, _players, data, context) => {
  const playerId = resolvePlayerId(data, context);
  if (!playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId is required for match_ack' }));
    console.warn('Match ACK rejected: missing playerId', {
      matchId: data?.matchId,
      payloadPlayerId: data?.playerId,
      payloadUserId: data?.userId,
      contextPlayerId: context.playerId,
    });
    return;
  }

  if (context.playerId && context.playerId !== playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId mismatch for match_ack' }));
    console.warn('Match ACK rejected: playerId mismatch', {
      matchId: data?.matchId,
      payloadPlayerId: data?.playerId,
      payloadUserId: data?.userId,
      contextPlayerId: context.playerId,
      resolvedPlayerId: playerId,
    });
    return;
  }

  const matchId = typeof data?.matchId === 'string' ? data.matchId.trim() : '';
  if (!matchId) {
    ws.send(JSON.stringify({ type: 'error', message: 'matchId is required for match_ack' }));
    console.warn('Match ACK rejected: missing matchId', {
      payloadPlayerId: data?.playerId,
      payloadUserId: data?.userId,
      contextPlayerId: context.playerId,
    });
    return;
  }

  const io = createWebSocketEmitter();
  console.info('Match ACK received from socket', {
    matchId,
    playerId,
  });
  Matchmaker.instance.onMatchAck({ matchId, userId: playerId, io });
};

export const handlePaperLegendCharacterSelect: MessageHandler = async (ws, _players, data, context) => {
  const playerId = resolvePlayerId(data, context);
  if (!playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId is required for paper_legend:character_select' }));
    console.warn('Paper Legends character select rejected: missing playerId', {
      matchId: data?.matchId,
      payloadPlayerId: data?.playerId,
      payloadUserId: data?.userId,
      contextPlayerId: context.playerId,
    });
    return;
  }

  if (context.playerId && context.playerId !== playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId mismatch for paper_legend:character_select' }));
    console.warn('Paper Legends character select rejected: playerId mismatch', {
      matchId: data?.matchId,
      payloadPlayerId: data?.playerId,
      payloadUserId: data?.userId,
      contextPlayerId: context.playerId,
      resolvedPlayerId: playerId,
    });
    return;
  }

  const matchId = typeof data?.matchId === 'string' ? data.matchId.trim() : '';
  if (!matchId) {
    ws.send(JSON.stringify({ type: 'error', message: 'matchId is required for paper_legend:character_select' }));
    console.warn('Paper Legends character select rejected: missing matchId', {
      playerId,
      payloadPlayerId: data?.playerId,
      payloadUserId: data?.userId,
      contextPlayerId: context.playerId,
    });
    return;
  }

  const characterModelId = Number(data?.characterModelId ?? data?.modelId);
  if (!Number.isFinite(characterModelId) || characterModelId <= 0) {
    ws.send(JSON.stringify({ type: 'error', message: 'characterModelId is required for paper_legend:character_select' }));
    console.warn('Paper Legends character select rejected: missing characterModelId', {
      matchId,
      playerId,
      payloadCharacterModelId: data?.characterModelId,
      payloadModelId: data?.modelId,
    });
    return;
  }

  const io = createWebSocketEmitter();
  const result = await Matchmaker.instance.onPaperLegendCharacterSelect({
    matchId,
    playerId,
    characterModelId,
    io,
  });

  if (result.status !== 'OK') {
    ws.send(
      JSON.stringify({
        type: 'paper_legend:character_selection_rejected',
        matchId,
        playerId,
        characterModelId,
        reason: result.reason,
      })
    );
    return;
  }

  ws.send(
    JSON.stringify({
      type: 'paper_legend:character_select_ack',
      matchId,
      playerId,
      characterModelId,
    })
  );
};

export const handlePaperLegendCharacterLock: MessageHandler = async (ws, _players, data, context) => {
  const playerId = resolvePlayerId(data, context);
  if (!playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId is required for paper_legend:character_lock' }));
    return;
  }

  if (context.playerId && context.playerId !== playerId) {
    ws.send(JSON.stringify({ type: 'error', message: 'playerId mismatch for paper_legend:character_lock' }));
    return;
  }

  const matchId = typeof data?.matchId === 'string' ? data.matchId.trim() : '';
  if (!matchId) {
    ws.send(JSON.stringify({ type: 'error', message: 'matchId is required for paper_legend:character_lock' }));
    return;
  }

  const io = createWebSocketEmitter();
  const result = await Matchmaker.instance.onPaperLegendCharacterLock({
    matchId,
    playerId,
    io,
  });

  if (result.status !== 'OK') {
    ws.send(
      JSON.stringify({
        type: 'paper_legend:character_selection_rejected',
        matchId,
        playerId,
        characterModelId: Number(data?.characterModelId ?? data?.modelId ?? 0),
        reason: result.reason,
      })
    );
    return;
  }

  ws.send(
    JSON.stringify({
      type: 'paper_legend:character_lock_ack',
      matchId,
      playerId,
    })
  );
};

export const cleanupRoomOnDisconnect = async (players: PlayerMap, playerId: number | null) => {
  if (!playerId) {
    return;
  }

  try {
    const resolvedRoomId = await leaveRoomForPlayer(playerId);
    if (resolvedRoomId) {
      await sendRoomUsersUpdate(players, resolvedRoomId);
    }
  } catch (error) {
    console.error('Lá»—i khi cleanup phÃ²ng khi máº¥t káº¿t ná»‘i:', error);
  }
};
const handlers: Record<string, MessageHandler> = {
  register: handleRegister,
  get_online_players: handleGetOnlinePlayers,
  invite: handleInvite,
  friend_challenge: handleFriendChallenge,
  friend_challenge_response: handleFriendChallengeResponse,
  heartbeat: handleHeartbeat,
  check_player_online: handleCheckPlayerOnline,
  room_leave: handleRoomLeave,
  room_ready: handleRoomReady,
  room_start_cancel: handleRoomStartCancel,
  room_kick: handleRoomKick,
  room_users: handleRoomUsers,
  room_start_ack: handleRoomStartAck,
  room_chat: handleRoomChat,
  room_lookup: handleRoomLookup,
  friend_list: handleFriendList,
  "match:ack": handleMatchAck,
  "paper_legend:character_select": handlePaperLegendCharacterSelect,
  "paper_legend:character_lock": handlePaperLegendCharacterLock,
};

export const handleMessage = async (
  ws: WebSocket,
  players: PlayerMap,
  data: any,
  context: HandlerContext
) => {
  const handler = handlers[data.type];
  if (handler) {
    await handler(ws, players, data, context);
  } else {
    ws.send(JSON.stringify({ type: 'error', message: 'Unknown message type' }));
  }
};

export type { MessageHandler };

