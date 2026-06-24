import { marketPriceQueue } from './marketPrice.queue';

const REPEAT_EVERY_MS = Number(process.env.MARKET_PRICE_JOB_INTERVAL_MS || 300_000);
const JOB_NAME = 'recalculate-market-price';
const JOB_ID = 'market-price-periodic-job';

const ensureMarketPriceJob = async () => {
  const repeatJobs = await marketPriceQueue.getRepeatableJobs();
  const existed = repeatJobs.some((job) => job.id === JOB_ID && Number(job.every) === REPEAT_EVERY_MS);

  if (!existed) {
    await marketPriceQueue.add(
      JOB_NAME,
      { reason: 'periodic-price-refresh' },
      {
        repeat: {
          every: REPEAT_EVERY_MS,
        },
        jobId: JOB_ID,
      },
    );
  }

  console.info('[market-price-scheduler] repeatable job ensured', {
    everyMs: REPEAT_EVERY_MS,
    existed,
  });
};

void ensureMarketPriceJob().catch((error) => {
  console.error('[market-price-scheduler] failed to register repeatable job', error);
  process.exit(1);
});

const shutdown = async () => {
  await marketPriceQueue.close();
};

process.on('SIGINT', () => {
  void shutdown().finally(() => process.exit(0));
});

process.on('SIGTERM', () => {
  void shutdown().finally(() => process.exit(0));
});
