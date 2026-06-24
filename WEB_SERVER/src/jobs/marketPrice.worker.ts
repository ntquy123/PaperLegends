import { Job, Worker } from 'bullmq';
import prisma from '../models/prismaClient';
import { emitMarketUpdate } from '../services/adminRealtime';
import { MARKET_PRICE_QUEUE_NAME } from './marketPrice.queue';

type MarketPriceJobData = {
  reason?: string;
};

const redisHost = process.env.REDIS_HOST || '127.0.0.1';
const redisPort = Number(process.env.REDIS_PORT || 6379);
const connection = process.env.REDIS_URL
  ? { url: process.env.REDIS_URL }
  : { host: redisHost, port: redisPort };

const WINDOW_MINUTES = Number(process.env.MARKET_PRICE_WINDOW_MINUTES || 60);
const MAX_CHANGE_PERCENT = Number(process.env.MARKET_PRICE_MAX_CHANGE_PERCENT || 2);
const BATCH_SIZE = Number(process.env.MARKET_PRICE_BATCH_SIZE || 100);

const clamp = (value: number, min: number, max: number) => Math.max(min, Math.min(max, value));

const recalculateMarketPrice = async (job: Job<MarketPriceJobData>) => {
  const startedAtMs = Date.now();
  const windowStart = new Date(Date.now() - WINDOW_MINUTES * 60 * 1000);

  try {
    const [buyActive, sellActive, tradeActive] = await Promise.all([
      prisma.buyRequestOrder.findMany({
        where: { createDate: { gte: windowStart }, status: 0 },
        select: { itemId: true },
        distinct: ['itemId'],
      }),
      prisma.playerItem.findMany({
        where: { IsSolded: 1 },
        select: { itemId: true },
        distinct: ['itemId'],
      }),
      prisma.itemTradeHistory.findMany({
        where: { createdAt: { gte: windowStart } },
        select: { itemId: true },
        distinct: ['itemId'],
      }),
    ]);

    const activeItemIds = Array.from(
      new Set([...buyActive, ...sellActive, ...tradeActive].map((row) => row.itemId)),
    );

    if (activeItemIds.length === 0) {
      console.info('[market-price-worker] no active market items', { windowMinutes: WINDOW_MINUTES });
      return { updated: 0, processed: 0, increased: 0, decreased: 0, stable: 0 };
    }

    let processed = 0;
    let increased = 0;
    let decreased = 0;
    let stable = 0;
    let updated = 0;

    for (let i = 0; i < activeItemIds.length; i += BATCH_SIZE) {
      const batchItemIds = activeItemIds.slice(i, i + BATCH_SIZE);

      const [items, buyDemand, sellSupply, tradeVolume] = await Promise.all([
        prisma.item.findMany({
          where: { id: { in: batchItemIds } },
          select: { id: true, price: true },
        }),
        prisma.buyRequestOrder.groupBy({
          by: ['itemId'],
          _count: { itemId: true },
          where: {
            itemId: { in: batchItemIds },
            status: 0,
            createDate: { gte: windowStart },
          },
        }),
        prisma.playerItem.groupBy({
          by: ['itemId'],
          _count: { itemId: true },
          where: {
            itemId: { in: batchItemIds },
            IsSolded: 1,
          },
        }),
        prisma.itemTradeHistory.groupBy({
          by: ['itemId'],
          _sum: { quantity: true },
          where: {
            itemId: { in: batchItemIds },
            createdAt: { gte: windowStart },
          },
        }),
      ]);

      const demandMap = new Map<number, number>(buyDemand.map((row) => [row.itemId, row._count.itemId]));
      const supplyMap = new Map<number, number>(sellSupply.map((row) => [row.itemId, row._count.itemId]));
      const tradeMap = new Map<number, number>(tradeVolume.map((row) => [row.itemId, row._sum.quantity ?? 0]));

      for (const item of items) {
        processed += 1;

        const demand = demandMap.get(item.id) || 0;
        const supply = supplyMap.get(item.id) || 0;
        const volume = tradeMap.get(item.id) || 0;

        let reason = 'PRICE_STABLE';
        let changePercent = 0;
        let newPrice = item.price;

        if (!(demand === 0 && supply === 0)) {
          const denominator = Math.max(demand + supply + volume, 1);
          const marketPressure = (demand - supply) / denominator;
          changePercent = clamp(
            marketPressure * MAX_CHANGE_PERCENT,
            -MAX_CHANGE_PERCENT,
            MAX_CHANGE_PERCENT,
          );

          newPrice = Math.round(item.price * (1 + changePercent / 100));

          if (demand > supply) reason = 'PRICE_UP_DEMAND_HIGH';
          else if (supply > demand) reason = 'PRICE_DOWN_SUPPLY_HIGH';
          else reason = 'PRICE_STABLE';
        }

        if (newPrice > item.price) increased += 1;
        else if (newPrice < item.price) decreased += 1;
        else stable += 1;

        if (newPrice === item.price) {
          continue;
        }

        await prisma.$transaction(async (tx) => {
          await tx.item.update({ where: { id: item.id }, data: { price: newPrice } });
          await tx.itemPriceHistory.create({
            data: {
              itemId: item.id,
              oldPrice: item.price,
              newPrice,
              changePercent,
              buyDemand: demand,
              sellSupply: supply,
              reason,
            },
          });
        });

        updated += 1;
        emitMarketUpdate({
          itemId: item.id,
          oldPrice: item.price,
          newPrice,
          changePercent,
          buyDemand: demand,
          sellSupply: supply,
          tradeVolume: volume,
          createdAt: new Date().toISOString(),
        });
      }
    }

    const durationMs = Date.now() - startedAtMs;
    console.info('[market-price-worker] run finished', {
      processed,
      updated,
      increased,
      decreased,
      stable,
      durationMs,
      windowMinutes: WINDOW_MINUTES,
      maxChangePercent: MAX_CHANGE_PERCENT,
    });

    return { processed, updated, increased, decreased, stable, durationMs };
  } catch (error) {
    console.error('[market-price-worker] recalculate failed', {
      jobId: job.id,
      error: String(error),
    });
    throw error;
  }
};

const marketPriceWorker = new Worker<MarketPriceJobData>(
  MARKET_PRICE_QUEUE_NAME,
  recalculateMarketPrice,
  {
    connection,
    concurrency: 1,
  },
);

marketPriceWorker.on('completed', (job, result) => {
  console.info('[market-price-worker] completed', { jobId: job.id, result });
});

marketPriceWorker.on('failed', (job, error) => {
  console.error('[market-price-worker] failed', { jobId: job?.id, error: String(error) });
});

const shutdown = async () => {
  await marketPriceWorker.close();
  await prisma.$disconnect();
};

process.on('SIGINT', () => {
  void shutdown().finally(() => process.exit(0));
});

process.on('SIGTERM', () => {
  void shutdown().finally(() => process.exit(0));
});

console.info('[market-price-worker] started');
