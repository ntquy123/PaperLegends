import { exec, execFile } from 'child_process';
import util from 'util';
import { resolveContainerRuntime } from './containerRuntime';

const execPromise = util.promisify(exec);
const execFilePromise = util.promisify(execFile);


export type RunningContainer = {
  id: string;
  name: string;
  image: string;
  status: string;
  ports: string;
  cpu?: string;
  memory?: string;
};

type ContainerStats = {
  id: string;
  cpu: string;
  memory: string;
};

const parseDockerErrors = (stderr: string) => {
  const stderrLines = stderr
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);

  if (stderrLines.length > 0) {
    throw new Error(stderrLines.join('; '));
  }
};

const fetchContainerStats = async (containerIds: string[]): Promise<Record<string, ContainerStats>> => {
  if (containerIds.length === 0) {
    return {};
  }

  const dockerRuntime = await resolveContainerRuntime();
  const { stdout, stderr } = await execPromise(
    `${dockerRuntime} stats --no-stream --format "{{.ID}}|{{.CPUPerc}}|{{.MemUsage}}" ${containerIds.join(' ')}`,
  );

  parseDockerErrors(stderr);

  const stats: Record<string, ContainerStats> = {};

  stdout
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .forEach((line) => {
      const [id, cpu, memory] = line.split('|');
      if (id) {
        stats[id] = {
          id,
          cpu: cpu ?? '—',
          memory: memory ?? '—',
        };
      }
    });

  return stats;
};

export async function listRunningContainers(): Promise<RunningContainer[]> {
  const dockerRuntime = await resolveContainerRuntime();
  const { stdout, stderr } = await execPromise(
    `${dockerRuntime} ps --format "{{.ID}}|{{.Names}}|{{.Image}}|{{.Status}}|{{.Ports}}"`,
  );

  parseDockerErrors(stderr);

  const containers = stdout
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const [id, name, image, status, ports] = line.split('|');
      return {
        id: id ?? '',
        name: name ?? '',
        image: image ?? '',
        status: status ?? '',
        ports: ports ?? '',
      };
    });

  const statsMap = await fetchContainerStats(containers.map((container) => container.id));

  return containers.map((container) => ({
    ...container,
    cpu: statsMap[container.id]?.cpu ?? '—',
    memory: statsMap[container.id]?.memory ?? '—',
  }));
}

export async function fetchContainerLogs(containerId: string, tail = 200): Promise<string> {
  const sanitizedTail = Number.isFinite(tail) && tail > 0 ? Math.min(tail, 1000) : 200;
  const dockerRuntime = await resolveContainerRuntime();
  try {
    const { stdout, stderr } = await execPromise(
      `${dockerRuntime} logs --timestamps --details --tail ${sanitizedTail} ${containerId}`,
    );

    parseDockerErrors(stderr);

    return stdout || 'Không có log để hiển thị.';
  } catch (err: any) {
    const msg = String(err?.message ?? err ?? "");
    if (msg.includes("No such container") || msg.includes("is not running")) {
      return `Container ${containerId} không tồn tại hoặc đã bị xóa.`;
    }
    throw err;
  }
}

export async function stopRunningContainer(containerIdOrName: string): Promise<{ output: string }> {
  const target = containerIdOrName.trim();

  if (!/^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$/.test(target)) {
    throw new Error('Container id/name khong hop le.');
  }

  const dockerRuntime = await resolveContainerRuntime();

  try {
    const { stdout, stderr } = await execFilePromise(dockerRuntime, ['stop', target], {
      timeout: 15_000,
    });

    parseDockerErrors(stderr);

    return { output: stdout.trim() };
  } catch (error: any) {
    const stderr = typeof error?.stderr === 'string' ? error.stderr.trim() : '';
    const message = stderr || error?.message || 'Khong the stop container.';
    throw new Error(message);
  }
}
