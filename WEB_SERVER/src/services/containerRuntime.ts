import { execFile } from 'child_process';
import { constants as fsConstants, promises as fs } from 'fs';
import path from 'path';
import { promisify } from 'util';

const execFileAsync = promisify(execFile);
const DEFAULT_RUNTIMES = ['docker'];
const DEFAULT_RUNTIME_PATHS = [
  '/usr/bin/docker',
  '/usr/local/bin/docker',
  '/snap/bin/docker',
  '/bin/docker',
];
const DEFAULT_ROOM_IMAGE = 'paperlegends/unity-dedicated:latest';
const LOCALHOST_ROOM_IMAGE = 'localhost/paperlegends/unity-dedicated:latest';

let runtimePromise: Promise<string> | null = null;

const isMissingBinary = (error: unknown) => {
  const err = error as NodeJS.ErrnoException;
  const message = err?.message ? String(err.message) : '';
  return err?.code === 'ENOENT' || message.includes('ENOENT') || message.includes('not found');
};

async function validateRuntime(binary: string) {
  try {
    await execFileAsync(binary, ['--version']);
  } catch (error) {
    if (isMissingBinary(error)) {
      throw error;
    }
  }
}

async function resolveRuntimeFromPath(runtime: string): Promise<string | null> {
  try {
    const { stdout } = await execFileAsync('bash', ['-lc', `command -v ${runtime}`], { timeout: 3000 });
    const resolved = stdout.trim();
    return resolved || null;
  } catch {
    return null;
  }
}

function buildCandidates(configured?: string) {
  const candidates = new Set<string>();

  if (configured) {
    candidates.add(configured);
    if (!path.isAbsolute(configured)) {
      candidates.add(`/usr/bin/${configured}`);
      candidates.add(`/usr/local/bin/${configured}`);
    }
  }

  DEFAULT_RUNTIMES.forEach((candidate) => candidates.add(candidate));
  DEFAULT_RUNTIME_PATHS.forEach((candidate) => candidates.add(candidate));

  return [...candidates];
}

export async function resolveContainerRuntime(): Promise<string> {
  if (runtimePromise) {
    return runtimePromise;
  }

  runtimePromise = (async () => {
    const configured = process.env.DOCKER_BIN?.trim();
    const candidates = buildCandidates(configured);
    const discovered = await Promise.all(DEFAULT_RUNTIMES.map((runtime) => resolveRuntimeFromPath(runtime)));
    discovered.filter((value): value is string => Boolean(value)).forEach((pathCandidate) => {
      if (!candidates.includes(pathCandidate)) {
        candidates.push(pathCandidate);
      }
    });

    for (const candidate of candidates) {
      try {
        if (path.isAbsolute(candidate)) {
          await fs.access(candidate, fsConstants.X_OK);
        }
        await validateRuntime(candidate);
        return candidate;
      } catch (error) {
        if (isMissingBinary(error)) {
          continue;
        }
        return candidate;
      }
    }

    throw new Error('DOCKER_NOT_AVAILABLE');
  })();

  return runtimePromise;
}

async function imageExists(runtime: string, image: string): Promise<boolean> {
  try {
    await execFileAsync(runtime, ['image', 'inspect', image], { timeout: 5000 });
    return true;
  } catch {
    return false;
  }
}

export async function resolveRoomDockerImage(runtime?: string): Promise<string> {
  const configured = process.env.ROOM_DOCKER_IMAGE?.trim();
  if (configured) {
    return configured;
  }

  const resolvedRuntime = runtime ?? (await resolveContainerRuntime());
  const defaultExists = await imageExists(resolvedRuntime, DEFAULT_ROOM_IMAGE);
  if (defaultExists) {
    return DEFAULT_ROOM_IMAGE;
  }

  const localExists = await imageExists(resolvedRuntime, LOCALHOST_ROOM_IMAGE);
  if (localExists) {
    return LOCALHOST_ROOM_IMAGE;
  }

  return DEFAULT_ROOM_IMAGE;
}
