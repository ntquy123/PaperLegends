import { Job, Worker } from 'bullmq';
import prisma from '../models/prismaClient';
import { MARKET_BUY_REQUEST_MATCHER_QUEUE_NAME } from './marketBuyRequestMatcher.queue';

type MarketBuyRequestMatcherJobData = {
  reason?: string;
};

const redisHost = process.env.REDIS_HOST || '127.0.0.1';
const redisPort = Number(process.env.REDIS_PORT || 6379);
const connection = process.env.REDIS_URL
  ? { url: process.env.REDIS_URL }
  : { host: redisHost, port: redisPort };

const PROCESS_BATCH_SIZE = Number(process.env.MARKET_BUY_REQUEST_MATCH_BATCH_SIZE || 100);

const processBuyRequestOrders = async (job: Job<MarketBuyRequestMatcherJobData>) => {
  const startedAtMs = Date.now();

  try {
    let processed = 0;

    for (;;) {
      const orders = await prisma.buyRequestOrder.findMany({
        where: { status: 0 },
        orderBy: { createDate: 'asc' },
        take: PROCESS_BATCH_SIZE,
        select: { playerId: true, itemId: true, seq: true, price: true },
      });

      if (!orders.length) {
        break;
      }

      for (const order of orders) {
        const matched = await prisma.$transaction(async (tx) => {
          const targetOrder = await tx.buyRequestOrder.findUnique({
            where: {
              playerId_itemId_seq: {
                playerId: order.playerId,
                itemId: order.itemId,
                seq: order.seq,
              },
            },
          });

          if (!targetOrder || targetOrder.status !== 0) {
            return false;
          }

          const sellerItem = await tx.playerItem.findFirst({
            where: {
              itemId: targetOrder.itemId,
              Price: targetOrder.price,
              IsSolded: 1,
              playerId: { not: targetOrder.playerId },
            },
            orderBy: { seq: 'asc' },
          });

          if (!sellerItem) {
            return false;
          }

          await tx.playerItem.update({
            where: {
              playerId_itemId_seq: {
                playerId: sellerItem.playerId,
                itemId: sellerItem.itemId,
                seq: sellerItem.seq,
              },
            },
            data: { IsSolded: 2 },
          });

          const buyerSeqAgg = await tx.playerItem.aggregate({
            where: { playerId: targetOrder.playerId, itemId: targetOrder.itemId },
            _max: { seq: true },
          });
          const buyerNextSeq = (buyerSeqAgg._max.seq ?? 0) + 1;

          await tx.playerItem.create({
            data: {
              playerId: targetOrder.playerId,
              itemId: sellerItem.itemId,
              seq: buyerNextSeq,
              level: sellerItem.level,
              SkillGid: sellerItem.SkillGid ?? null,
              description: sellerItem.description || '',
              Price: 0,
              damage: sellerItem.damage,
              IsSolded: 0,
              Isbought: 1,
            },
          });

          const [sellerBhSeqAgg, buyerBhSeqAgg] = await Promise.all([
            tx.balanceHistory.aggregate({ where: { userId: sellerItem.playerId }, _max: { seq: true } }),
            tx.balanceHistory.aggregate({ where: { userId: targetOrder.playerId }, _max: { seq: true } }),
          ]);

          await tx.balanceHistory.create({
            data: {
              userId: sellerItem.playerId,
              seq: (sellerBhSeqAgg._max.seq ?? 0) + 1,
              ringBall: 0,
              money: targetOrder.price,
              description: `Sell item #${targetOrder.itemId} to player #${targetOrder.playerId}`,
              eventType: 'MARKET_SELL',
            },
          });

          await tx.balanceHistory.create({
            data: {
              userId: targetOrder.playerId,
              seq: (buyerBhSeqAgg._max.seq ?? 0) + 1,
              ringBall: 0,
              money: -targetOrder.price,
              description: `Buy item #${targetOrder.itemId} from player #${sellerItem.playerId}`,
              eventType: 'MARKET_BUY',
            },
          });

          const tradeSeqAgg = await tx.itemTradeHistory.aggregate({
            where: {
              playerIdBuy: targetOrder.playerId,
              playerIdSold: sellerItem.playerId,
              itemId: targetOrder.itemId,
            },
            _max: { seq: true },
          });

          await tx.itemTradeHistory.create({
            data: {
              playerIdBuy: targetOrder.playerId,
              playerIdSold: sellerItem.playerId,
              itemId: targetOrder.itemId,
              seq: (tradeSeqAgg._max.seq ?? 0) + 1,
              price: targetOrder.price,
              quantity: 1,
            },
          });

          await tx.buyRequestOrder.update({
            where: {
              playerId_itemId_seq: {
                playerId: targetOrder.playerId,
                itemId: targetOrder.itemId,
                seq: targetOrder.seq,
              },
            },
            data: { status: 1 },
          });

          return true;
        });

        if (matched) {
          processed += 1;
        }
      }

      if (orders.length < PROCESS_BATCH_SIZE) {
        break;
      }
    }

    const durationMs = Date.now() - startedAtMs;
    console.info('[market-buy-request-matcher-worker] run finished', {
      processed,
      durationMs,
      batchSize: PROCESS_BATCH_SIZE,
    });

    return { processed, durationMs };
  } catch (error) {
    console.error('[market-buy-request-matcher-worker] run failed', {
      jobId: job.id,
      error: String(error),
    });
    throw error;
  }
};

const marketBuyRequestMatcherWorker = new Worker<MarketBuyRequestMatcherJobData>(
  MARKET_BUY_REQUEST_MATCHER_QUEUE_NAME,
  processBuyRequestOrders,
  {
    connection,
    concurrency: 1,
  },
);

marketBuyRequestMatcherWorker.on('completed', (job, result) => {
  console.info('[market-buy-request-matcher-worker] completed', { jobId: job.id, result });
});

marketBuyRequestMatcherWorker.on('failed', (job, error) => {
  console.error('[market-buy-request-matcher-worker] failed', { jobId: job?.id, error: String(error) });
});

const shutdown = async () => {
  await marketBuyRequestMatcherWorker.close();
  await prisma.$disconnect();
};

process.on('SIGINT', () => {
  void shutdown().finally(() => process.exit(0));
});

process.on('SIGTERM', () => {
  void shutdown().finally(() => process.exit(0));
});

console.info('[market-buy-request-matcher-worker] started');
