import WebSocket from 'ws';
import type { PlayerMap } from './handlers';

type Emitter = {
  to: (room: string) => {
    emit: (event: string, payload?: Record<string, unknown>) => void;
  };
};

let playersRegistry: PlayerMap | null = null;

export const setPlayersRegistry = (players: PlayerMap) => {
  playersRegistry = players;
};

export const getPlayersRegistry = () => playersRegistry;

export const getOnlinePlayerCount = () => (playersRegistry ? playersRegistry.size : 0);

export const isPlayerOnline = (playerId: number) => {
  if (!playersRegistry) {
    return false;
  }

  const socket = playersRegistry.get(playerId);
  return Boolean(socket && socket.readyState === WebSocket.OPEN);
};

export const getPlayerSocketState = (playerId: number) => {
  if (!playersRegistry) {
    return null;
  }

  const socket = playersRegistry.get(playerId);
  if (!socket) {
    return null;
  }

  return {
    readyState: socket.readyState,
  };
};

const resolveUserId = (room: string) => {
  const prefix = 'user:';
  if (!room.startsWith(prefix)) {
    return null;
  }

  const id = Number(room.slice(prefix.length));
  return Number.isFinite(id) ? id : null;
};

export const createWebSocketEmitter = (): Emitter => ({
  to: (room: string) => ({
    emit: (event: string, payload: Record<string, unknown> = {}) => {
      const userId = resolveUserId(room);
      if (userId === null) {
        return;
      }

      if (!playersRegistry) {
        return;
      }

      const socket = playersRegistry.get(userId);
      if (!socket) {
        console.warn('Emit failed: no socket for user', { room, userId, event, payload });
        return;
      }

      if (socket.readyState !== WebSocket.OPEN) {
        console.warn('Emit failed: socket not open', { room, userId, readyState: socket.readyState, event, payload });
        return;
      }

      try {
        socket.send(JSON.stringify({ type: event, ...payload }));
        console.info('Emit sent to user', { room, userId, event });
      } catch (err) {
        console.error('Emit error', { room, userId, event, err: String(err) });
      }
    },
  }),
});
