import cluster from 'cluster';
import initCluster from './cluster';
import createHttpServer from './httpServer';
import { getRedisClient } from './services/redisClient';
import initWebSocketServer from './websocket/server';
import loadEnv from './config/loadEnv';

// Load environment variables
loadEnv();

// API and WebSocket share the same HTTP port.
const API_PORT = Number(process.env.API_PORT) || 5001;

if (cluster.isPrimary) {
  initCluster(API_PORT);
} else {
  const apiServer = createHttpServer();
  initWebSocketServer(apiServer);

  void getRedisClient()
    .then(() => {
      console.info('Redis startup check: kết nối thành công.');
    })
    .catch((error) => {
      console.error('Redis startup check: kết nối thất bại.', error);
    });

  process.on('message', (message, socket: any) => {
    if (message !== 'sticky-session:connection') return;
    apiServer.emit('connection', socket);
    socket.resume();
  });
}
