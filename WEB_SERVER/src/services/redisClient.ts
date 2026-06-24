import { createClient } from 'redis';

const redisUrl =
  process.env.REDIS_URL ||
  `redis://${process.env.REDIS_HOST || '127.0.0.1'}:${process.env.REDIS_PORT || '6379'}`;

const isLocalhostUrl = /redis:\/\/(localhost|127\.0\.0\.1)(:\d+)?/i.test(redisUrl);

const redisClient = createClient({
  url: redisUrl,
  socket: {
    reconnectStrategy: (retries: number) => Math.min(2000 + retries * 250, 10_000),
    connectTimeout: 10_000,
    keepAlive: true,
    keepAliveInitialDelay: 10_000,
    noDelay: true,
  },
  pingInterval: 30_000, // Send PING every 30s to keep connection alive and detect dead sockets early
});
let connectPromise: Promise<typeof redisClient> | null = null;
let lastRedisErrorLogAt = 0;
let suppressedRedisErrorCount = 0;

redisClient.on('error', (error) => {
  const now = Date.now();
  if (now - lastRedisErrorLogAt >= 10_000) {
    console.error('Redis connection error:', {
      error: String(error),
      suppressed: suppressedRedisErrorCount,
    });
    lastRedisErrorLogAt = now;
    suppressedRedisErrorCount = 0;
    return;
  }

  suppressedRedisErrorCount += 1;
});

redisClient.on('ready', () => {
  console.info('Redis client connected and ready', { url: redisUrl });
});

if (process.env.NODE_ENV === 'production' && isLocalhostUrl) {
  console.warn(
    'Redis is configured to use localhost in production. If API runs in Docker, set REDIS_URL/REDIS_HOST to the Redis container name (e.g. redis://banculi-redis:6379).',
  );
}

export const getRedisClient = async () => {
  if (!redisClient.isOpen) {
    // Reset stale promise so a fresh connect attempt is made
    connectPromise = null;
    if (!connectPromise) {
      connectPromise = redisClient.connect().catch((error) => {
        connectPromise = null;
        throw error;
      });
    }
    await connectPromise;
  }

  return redisClient;
};
