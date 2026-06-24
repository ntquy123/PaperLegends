import { execFile } from 'child_process';
import { promisify } from 'util';

const execFileAsync = promisify(execFile);
const MONITOR_PROCESS_NAME = process.env.MONITOR_PROCESS_NAME || 'paper-legends-monitor';

const runPm2 = async (args: string[]) => {
  const { stdout, stderr } = await execFileAsync('pm2', args, { timeout: 10_000 });
  return { stdout, stderr };
};

export const startMonitorProcess = async () =>
  runPm2(['start', MONITOR_PROCESS_NAME]).then(() => ({
    message: `Đã bật PM2 process ${MONITOR_PROCESS_NAME}.`,
  }));

export const stopMonitorProcess = async () =>
  runPm2(['stop', MONITOR_PROCESS_NAME]).then(() => ({
    message: `Đã tắt PM2 process ${MONITOR_PROCESS_NAME}.`,
  }));
