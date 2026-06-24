import { Queue } from 'bullmq';

const redisHost = process.env.REDIS_HOST || '127.0.0.1';
const redisPort = Number(process.env.REDIS_PORT || 6379);

const connection = process.env.REDIS_URL
  ? { url: process.env.REDIS_URL }
  : { host: redisHost, port: redisPort };

export const MARKET_PRICE_QUEUE_NAME = 'market-price-recalculation';

export const marketPriceQueue = new Queue(MARKET_PRICE_QUEUE_NAME, {
  connection,
  defaultJobOptions: {
    removeOnComplete: 100,
    removeOnFail: 100,
    attempts: Number(process.env.MARKET_PRICE_JOB_ATTEMPTS || 3),
    backoff: {
      type: 'exponential',
      delay: Number(process.env.MARKET_PRICE_JOB_BACKOFF_MS || 5_000),
    },
  },
});
