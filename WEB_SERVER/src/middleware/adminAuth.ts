import crypto from 'crypto';
import { RequestHandler } from 'express';

const ADMIN_UI_SECRET = process.env.ADMIN_UI_SECRET || 'banculi-admin-ui-secret';

export interface AdminTokenPayload {
  friendCode: string;
  playerId: number;
  providerType: string | null;
  issuedAt: number;
}

export function isAdminPasswordConfigured(): boolean {
  return typeof process.env.ADMIN_UI_PASSWORD === 'string' && process.env.ADMIN_UI_PASSWORD.length > 0;
}

export function verifyAdminPassword(candidate: unknown): boolean {
  const expected = process.env.ADMIN_UI_PASSWORD;
  if (typeof candidate !== 'string' || !expected) {
    return false;
  }

  const candidateHash = crypto.createHash('sha256').update(candidate).digest();
  const expectedHash = crypto.createHash('sha256').update(expected).digest();
  return crypto.timingSafeEqual(candidateHash, expectedHash);
}

export function createAdminToken(payload: AdminTokenPayload): string {
  const base = Buffer.from(JSON.stringify(payload)).toString('base64url');
  const signature = crypto.createHmac('sha256', ADMIN_UI_SECRET).update(base).digest('base64url');
  return `${base}.${signature}`;
}

export function verifyAdminToken(token: string): AdminTokenPayload | null {
  const [base, providedSignature] = token.split('.');
  if (!base || !providedSignature) {
    return null;
  }

  const expectedSignature = crypto.createHmac('sha256', ADMIN_UI_SECRET).update(base).digest('base64url');
  const providedBuffer = Buffer.from(providedSignature);
  const expectedBuffer = Buffer.from(expectedSignature);

  if (providedBuffer.length !== expectedBuffer.length) {
    return null;
  }

  if (!crypto.timingSafeEqual(providedBuffer, expectedBuffer)) {
    return null;
  }

  try {
    const payload = JSON.parse(Buffer.from(base, 'base64url').toString('utf8')) as AdminTokenPayload;
    return payload;
  } catch (error) {
    console.error('Invalid admin token payload', error);
    return null;
  }
}

export const requireAdminAuth: RequestHandler = (req, res, next) => {
  const header = req.headers.authorization;
  const token = header?.startsWith('Bearer ') ? header.slice('Bearer '.length) : undefined;

  if (!token) {
    res.status(401).json({ error: 'Bạn cần đăng nhập để sử dụng tính năng này.' });
    return;
  }

  const payload = verifyAdminToken(token);

  if (!payload) {
    res.status(401).json({ error: 'Phiên đăng nhập không hợp lệ, vui lòng đăng nhập lại.' });
    return;
  }

  res.locals.admin = payload;
  next();
};
