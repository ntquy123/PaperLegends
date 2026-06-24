import { Server } from 'socket.io';

let io: Server | null = null;

export const setAdminSocketServer = (server: Server) => {
  io = server;
};

type MarketUpdatePayload = {
  itemId: number;
  oldPrice: number;
  newPrice: number;
  changePercent: number;
  buyDemand: number;
  sellSupply: number;
  tradeVolume: number;
  createdAt: string;
};

export const emitMarketUpdate = (payload?: Partial<MarketUpdatePayload>) => {
  io?.emit('market:update', payload ?? { ts: Date.now() });
};
