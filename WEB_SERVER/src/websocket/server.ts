import http from 'http';
import WebSocket, { Server } from 'ws';
import { cleanupRoomOnDisconnect, handleMessage, HandlerContext } from './handlers';
import { createWebSocketEmitter, setPlayersRegistry } from './registry';
import { Matchmaker } from '../services/matchmaker';

export const initWebSocketServer = (apiServer: http.Server) => {
  const wss: Server = new WebSocket.Server({ noServer: true });
  const players = new Map<number, WebSocket>();
  setPlayersRegistry(players);
  const pingIntervalMs = Number(process.env.WS_PING_INTERVAL_MS ?? 30000);

  wss.on('connection', (ws: WebSocket) => {
    console.log('A new WebSocket client connected!');

    const trackedSocket = ws as WebSocket & { isAlive?: boolean };
    trackedSocket.isAlive = true;
    ws.on('pong', () => {
      trackedSocket.isAlive = true;
    });

    const context: HandlerContext = {
      playerId: null
    };

    ws.on('message', (message: WebSocket.RawData) => {
      const payload = typeof message === 'string' ? message : message.toString();
      console.log('Received: %s', payload);
      let data: any;
      try {
        data = JSON.parse(payload);
      } catch (e) {
        console.error('Error parsing message:', e);
        return;
      }

      handleMessage(ws, players, data, context).catch((error) => {
        console.error('Error handling message:', error);
        ws.send(JSON.stringify({ type: 'error', message: 'Internal server error' }));
      });
    });

    ws.on('close', () => {
      let disconnectedPlayerId: number | null = null;

      if (context.playerId && players.get(context.playerId) === ws) {
        disconnectedPlayerId = context.playerId;
        players.delete(context.playerId);
        console.info('Player disconnected and removed from registry', { playerId: context.playerId, connectedSockets: players.size });
      }

      if (disconnectedPlayerId) {
        const io = createWebSocketEmitter();
        const cleanupResult = Matchmaker.instance.onPlayerDisconnected(disconnectedPlayerId, io);
        console.info('Matchmaking cleanup after websocket disconnect', {
          playerId: disconnectedPlayerId,
          cleanupResult,
        });
      }

      void cleanupRoomOnDisconnect(players, disconnectedPlayerId);
    });

    const welcomeMessage = { type: 'welcome', message: 'Welcome to the game server!' };
    ws.send(JSON.stringify(welcomeMessage));
  });

  const pingTimer = setInterval(() => {
    wss.clients.forEach((client) => {
      const trackedClient = client as WebSocket & { isAlive?: boolean };
      if (!trackedClient.isAlive) {
        client.terminate();
        return;
      }
      trackedClient.isAlive = false;
      client.ping();
    });
  }, pingIntervalMs);

  wss.on('close', () => {
    clearInterval(pingTimer);
  });

  apiServer.on('upgrade', (request, socket, head) => {
    if (request.headers['upgrade'] !== 'websocket') return socket.destroy();

    const requestPath = (request.url ?? '').split('?')[0];
    if (requestPath === '/socket.io' || requestPath.startsWith('/socket.io/')) {
      // Let Socket.IO handle its own upgrade requests.
      return;
    }

    wss.handleUpgrade(request, socket, head, (ws) => {
      wss.emit('connection', ws, request);
    });
  });
  console.log('WebSocket server V_2026_02_02_V3 running on shared API HTTP server.');
};

export default initWebSocketServer;
