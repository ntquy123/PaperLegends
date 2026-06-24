import http from 'http';
import app from './app';
import { createWebSocketEmitter } from './websocket/registry';
import { Server } from 'socket.io';
import { setAdminSocketServer } from './services/adminRealtime';

const createHttpServer = (): http.Server => {
  app.set('io', createWebSocketEmitter());
  const server = http.createServer(app);
  const io = new Server(server, { path: '/socket.io', cors: { origin: '*' } });
  setAdminSocketServer(io);
  return server;
};

export default createHttpServer;
