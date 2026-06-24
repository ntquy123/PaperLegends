const dotenv = require('dotenv');

dotenv.config();

const MONITOR_INTERVAL_MS = Number(process.env.MONITOR_INTERVAL_MS) || 30_000;
const MIN_IDLE_OVERRIDE =
  process.env.MONITOR_MIN_IDLE_POOL !== undefined
    ? Number(process.env.MONITOR_MIN_IDLE_POOL)
    : undefined;

const resolveDockerPoolService = () => {
  try {
    return require('./dist/services/dockerPoolService');
  } catch (error) {
    try {
      require('ts-node/register');
      return require('./src/services/dockerPoolService');
    } catch (innerError) {
      console.error('[Monitor] Không thể load ensureIdlePool:', innerError);
      throw error;
    }
  }
};

const { ensureIdlePool } = resolveDockerPoolService();

const runMonitorOnce = async () => {
  const onlineCount = 0;

  try {
    await ensureIdlePool({
      onlineCount,
      reason: 'monitor',
      minIdleOverride: MIN_IDLE_OVERRIDE,
    });
  } catch (error) {
    console.error('[Monitor] ensureIdlePool thất bại:', error);
  }
};

console.log('[Monitor] Monitor Service đang chạy...');
console.log(`[Monitor] Interval: ${MONITOR_INTERVAL_MS}ms`);

runMonitorOnce().catch((error) => console.error('[Monitor] Lỗi khởi động:', error));
setInterval(() => {
  runMonitorOnce().catch((error) => console.error('[Monitor] Lỗi định kỳ:', error));
}, MONITOR_INTERVAL_MS);
