import type { Request, RequestHandler, Response } from 'express';
import { getClientIp, getUserAgent } from './requestClientInfo';

const readPositiveNumber = (value: string | undefined, fallback: number): number => {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
};

const SECURITY_BLOCK_ENABLED = (process.env.SECURITY_IP_BLOCK_ENABLED ?? 'true').toLowerCase() !== 'false';
const BLOCK_DURATION_MS = readPositiveNumber(process.env.SECURITY_IP_BLOCK_MS, 24 * 60 * 60 * 1000);
const NOT_FOUND_WINDOW_MS = readPositiveNumber(process.env.SECURITY_404_WINDOW_MS, 60 * 1000);
const NOT_FOUND_THRESHOLD = Math.floor(readPositiveNumber(process.env.SECURITY_404_THRESHOLD, 1));

type BlockInfo = {
  blockedUntil: number;
  reason: string;
  userAgent: string | null;
  lastPath: string;
};

type NotFoundInfo = {
  firstSeenAt: number;
  count: number;
};

const blockedIps = new Map<string, BlockInfo>();
const notFoundByIp = new Map<string, NotFoundInfo>();

const SENSITIVE_PROBE_PATTERNS: RegExp[] = [
  /(^|\/)\.env($|[.\-_/])/i,
  /(^|\/)\.(git|svn|hg)(\/|$)/i,
  /(^|\/)\.ds_store$/i,
  /(^|\/)phpinfo\.php$/i,
  /(^|\/)(config|database)\.php$/i,
  /(^|\/)docker-compose[^/]*\.ya?ml$/i,
  /(^|\/)(application|parameters|database|settings|config)\.ya?ml$/i,
  /(^|\/)(application|database|settings|config)\.properties$/i,
  /(^|\/)(credentials|secrets|keys|appsettings|settings|config)\.json$/i,
  /(^|\/)(wp-login\.php|xmlrpc\.php)$/i,
  /(^|\/)(wp-admin|wp-content|vendor\/phpunit)(\/|$)/i,
  /(^|\/)(composer\.(json|lock)|package-lock\.json|yarn\.lock)$/i,
];

const getRequestPath = (req: Request): string => {
  const rawPath = (req.originalUrl || req.url || '').split('?')[0] || '/';
  try {
    return decodeURIComponent(rawPath);
  } catch {
    return rawPath;
  }
};

const isExpectedClientMissPath = (path: string): boolean =>
  /^\/api\/playerRoom\/\d+$/.test(path);

const isApiPath = (path: string): boolean =>
  path === '/api' || path.startsWith('/api/');

const isSensitiveProbePath = (path: string): boolean =>
  SENSITIVE_PROBE_PATTERNS.some((pattern) => pattern.test(path));

const cleanupExpiredEntries = (now: number) => {
  for (const [ip, block] of blockedIps) {
    if (block.blockedUntil <= now) {
      blockedIps.delete(ip);
    }
  }

  for (const [ip, info] of notFoundByIp) {
    if (now - info.firstSeenAt > NOT_FOUND_WINDOW_MS) {
      notFoundByIp.delete(ip);
    }
  }
};

const blockIp = (ip: string, req: Request, reason: string) => {
  const now = Date.now();
  const path = getRequestPath(req);
  const blockedUntil = now + BLOCK_DURATION_MS;
  blockedIps.set(ip, {
    blockedUntil,
    reason,
    userAgent: getUserAgent(req),
    lastPath: path,
  });
  notFoundByIp.delete(ip);
  console.warn(
    `[Security] Blocked IP ${ip} for ${Math.round(BLOCK_DURATION_MS / 1000)}s. reason=${reason} path=${path} ua=${getUserAgent(req) ?? '-'}`
  );
};

const registerNotFound = (ip: string, req: Request) => {
  const now = Date.now();
  const existing = notFoundByIp.get(ip);
  const info = existing && now - existing.firstSeenAt <= NOT_FOUND_WINDOW_MS
    ? { firstSeenAt: existing.firstSeenAt, count: existing.count + 1 }
    : { firstSeenAt: now, count: 1 };

  notFoundByIp.set(ip, info);

  if (info.count >= NOT_FOUND_THRESHOLD) {
    blockIp(ip, req, `too_many_404:${info.count}/${Math.round(NOT_FOUND_WINDOW_MS / 1000)}s`);
  }
};

const sendBlockedResponse = (res: Response, block: BlockInfo) => {
  const retryAfterSeconds = Math.max(1, Math.ceil((block.blockedUntil - Date.now()) / 1000));
  res.locals.securityBlocked = true;
  res.setHeader('Retry-After', String(retryAfterSeconds));
  res.status(403).json({ error: 'Forbidden' });
};

export const ipBlockerMiddleware: RequestHandler = (req, res, next) => {
  if (!SECURITY_BLOCK_ENABLED) {
    next();
    return;
  }

  const ip = getClientIp(req);
  if (!ip) {
    next();
    return;
  }

  const now = Date.now();
  cleanupExpiredEntries(now);

  const activeBlock = blockedIps.get(ip);
  if (activeBlock && activeBlock.blockedUntil > now) {
    sendBlockedResponse(res, activeBlock);
    return;
  }

  const path = getRequestPath(req);
  if (isSensitiveProbePath(path)) {
    blockIp(ip, req, 'sensitive_path_probe');
    res.status(404).send('Not found');
    return;
  }

  res.on('finish', () => {
    if (res.statusCode !== 404 || !isApiPath(path) || isExpectedClientMissPath(path)) {
      return;
    }

    registerNotFound(ip, req);
  });

  next();
};
