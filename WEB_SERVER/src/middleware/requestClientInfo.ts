import type { Request } from 'express';

export const firstHeaderValue = (value: string | string[] | undefined): string | null => {
  if (Array.isArray(value)) {
    return value.length > 0 ? value[0] : null;
  }

  return value ?? null;
};

export const getClientIp = (req: Request): string | null => {
  const realIp = firstHeaderValue(req.headers['x-real-ip']);
  if (realIp) {
    return realIp.trim();
  }

  const cfIp = firstHeaderValue(req.headers['cf-connecting-ip']);
  if (cfIp) {
    return cfIp.trim();
  }

  const forwardedFor = firstHeaderValue(req.headers['x-forwarded-for']);
  if (forwardedFor) {
    const firstIp = forwardedFor.split(',')[0]?.trim();
    if (firstIp) {
      return firstIp;
    }
  }

  return req.ip ?? req.socket.remoteAddress ?? null;
};

export const getUserAgent = (req: Request): string | null => {
  const userAgent = firstHeaderValue(req.headers['user-agent']);
  return userAgent ? userAgent.trim() : null;
};
