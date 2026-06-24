import dotenv from 'dotenv';
import { existsSync } from 'fs';
import path from 'path';

let loaded = false;

export default function loadEnv() {
  if (loaded) return;

  const cwd = process.cwd();
  const nodeEnv = process.env.NODE_ENV?.trim();
  const explicitEnvFile = process.env.ENV_FILE?.trim();

  const files = [
    explicitEnvFile,
    nodeEnv ? `.env.${nodeEnv}` : undefined,
    '.env',
  ].filter((value, index, arr): value is string => Boolean(value) && arr.indexOf(value) === index);

  for (const file of files) {
    const fullPath = path.resolve(cwd, file);
    if (!existsSync(fullPath)) continue;
    dotenv.config({ path: fullPath, override: false });
  }

  loaded = true;
}
