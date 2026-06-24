import { execFile } from 'child_process';
import util from 'util';
import { resolveContainerRuntime } from './containerRuntime';

const execFileAsync = util.promisify(execFile);

const REDIS_CONTAINER_NAME = 'banculi-redis';
const REDIS_IMAGE = 'docker.io/library/redis:7-alpine';
const REDIS_PORT = 6379;
const REDIS_READY_TIMEOUT_MS = 60_000;
const REDIS_READY_INTERVAL_MS = 1_000;

const runDocker = async (args: string[]) => {
  try {
    const dockerRuntime = await resolveContainerRuntime();
    const { stdout, stderr } = await execFileAsync(dockerRuntime, args, {
      timeout: 60_000,
    });
    return { stdout, stderr };
  } catch (error) {
    if (error instanceof Error) {
      throw new Error(`Docker error: ${error.message}`);
    }
    throw error;
  }
};

const waitForRedisReady = async (
  timeoutMs = REDIS_READY_TIMEOUT_MS,
  intervalMs = REDIS_READY_INTERVAL_MS,
) => {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const { stdout } = await runDocker(['exec', REDIS_CONTAINER_NAME, 'redis-cli', 'ping']);
      if (stdout.trim().toUpperCase() === 'PONG') {
        return;
      }
    } catch {
      // Redis may still be booting, retry until timeout.
    }

    await new Promise((resolve) => {
      setTimeout(resolve, intervalMs);
    });
  }

  throw new Error(`Redis container did not become ready within ${timeoutMs}ms.`);
};

export const startRedisContainer = async () => {
  try {
    // Check if container already exists
    await runDocker(['container', 'inspect', REDIS_CONTAINER_NAME]);
    // If exists, start it
    await runDocker(['start', REDIS_CONTAINER_NAME]);
    await waitForRedisReady();
    return { message: 'Đã khởi động Redis container và sẵn sàng kết nối.' };
  } catch {
    // Container doesn't exist, create and start it
    await runDocker([
      'run',
      '-d',
      '--name', REDIS_CONTAINER_NAME,
      '-p', `${REDIS_PORT}:${REDIS_PORT}`,
      REDIS_IMAGE,
    ]);
    await waitForRedisReady();
    return { message: 'Đã tạo, khởi động Redis container và sẵn sàng kết nối.' };
  }
};

export const stopRedisContainer = async () => {
  try {
    await runDocker(['stop', REDIS_CONTAINER_NAME]);
    await runDocker(['rm', REDIS_CONTAINER_NAME]);
    return { message: 'Đã tắt và xóa Redis container.' };
  } catch (error) {
    if (error instanceof Error && error.message.includes('no such container')) {
      return { message: 'Redis container không tồn tại.' };
    }
    throw error;
  }
};

export const getRedisContainerStatus = async () => {
  try {
    const { stdout } = await runDocker(['container', 'inspect', '--format', '{{.State.Running}}', REDIS_CONTAINER_NAME]);
    const running = stdout.trim() === 'true';
    return { running };
  } catch {
    return { running: false };
  }
};
