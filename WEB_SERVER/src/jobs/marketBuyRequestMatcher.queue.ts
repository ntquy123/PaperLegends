import { Queue } from 'bullmq';

const redisHost = process.env.REDIS_HOST || '127.0.0.1';
const redisPort = Number(process.env.REDIS_PORT || 6379);

const connection = process.env.REDIS_URL
  ? { url: process.env.REDIS_URL }
  : { host: redisHost, port: redisPort };

export const MARKET_BUY_REQUEST_MATCHER_QUEUE_NAME = 'market-buy-request-matcher';

export const marketBuyRequestMatcherQueue = new Queue(MARKET_BUY_REQUEST_MATCHER_QUEUE_NAME, {
  connection,
  defaultJobOptions: {
    removeOnComplete: 100,
    removeOnFail: 100,
    attempts: Number(process.env.MARKET_BUY_REQUEST_MATCH_ATTEMPTS || 3),
    backoff: {
      type: 'exponential',
      delay: Number(process.env.MARKET_BUY_REQUEST_MATCH_BACKOFF_MS || 2_000),
    },
  },
});
