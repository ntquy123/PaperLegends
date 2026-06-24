import { marketBuyRequestMatcherQueue } from './marketBuyRequestMatcher.queue';

const REPEAT_EVERY_MS = Number(process.env.MARKET_BUY_REQUEST_MATCH_INTERVAL_MS || 5_000);
const JOB_NAME = 'process-market-buy-request-orders';
const JOB_ID = 'market-buy-request-matcher-periodic-job';

const ensureMarketBuyRequestMatcherJob = async () => {
  const repeatJobs = await marketBuyRequestMatcherQueue.getRepeatableJobs();
  const existed = repeatJobs.some((job) => job.id === JOB_ID && Number(job.every) === REPEAT_EVERY_MS);

  if (!existed) {
    await marketBuyRequestMatcherQueue.add(
      JOB_NAME,
      { reason: 'periodic-buy-request-matching' },
      {
        repeat: {
          every: REPEAT_EVERY_MS,
        },
        jobId: JOB_ID,
      },
    );
  }

  console.info('[market-buy-request-matcher-scheduler] repeatable job ensured', {
    everyMs: REPEAT_EVERY_MS,
    existed,
  });
};

void ensureMarketBuyRequestMatcherJob().catch((error) => {
  console.error('[market-buy-request-matcher-scheduler] failed to register repeatable job', error);
  process.exit(1);
});

const shutdown = async () => {
  await marketBuyRequestMatcherQueue.close();
};

process.on('SIGINT', () => {
  void shutdown().finally(() => process.exit(0));
});

process.on('SIGTERM', () => {
  void shutdown().finally(() => process.exit(0));
});
