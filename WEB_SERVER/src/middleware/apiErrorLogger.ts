import type { ErrorRequestHandler, NextFunction, Request, RequestHandler, Response } from 'express';
import { recordApiErrorLog } from '../services/apiErrorLogService';
import { getClientIp, getUserAgent } from './requestClientInfo';

const MAX_PARAM_TEXT_LENGTH = 12000;
const REDACTED_VALUE = '[REDACTED]';
const SENSITIVE_KEY_PATTERN = /password|token|authorization|cookie|secret|credential|refresh/i;

const isTrackedRequest = (req: Request): boolean =>
  (req.method === 'GET' || req.method === 'POST') && req.originalUrl.startsWith('/api/');

const truncateText = (value: string, maxLength = MAX_PARAM_TEXT_LENGTH): string =>
  value.length > maxLength ? `${value.slice(0, maxLength)}...` : value;

const sanitizeValue = (value: unknown): unknown => {
  if (Array.isArray(value)) {
    return value.map((item) => sanitizeValue(item));
  }

  if (value && typeof value === 'object') {
    const output: Record<string, unknown> = {};
    Object.entries(value as Record<string, unknown>).forEach(([key, child]) => {
      output[key] = SENSITIVE_KEY_PATTERN.test(key) ? REDACTED_VALUE : sanitizeValue(child);
    });
    return output;
  }

  if (typeof value === 'string') {
    return truncateText(value, 2000);
  }

  return value;
};

const safeParams = (req: Request) => {
  const params = {
    routeParams: sanitizeValue(req.params ?? {}),
    query: sanitizeValue(req.query ?? {}),
    body: sanitizeValue(req.body ?? {}),
  };

  const text = JSON.stringify(params);
  if (text.length <= MAX_PARAM_TEXT_LENGTH) {
    return params;
  }

  return {
    truncated: true,
    raw: truncateText(text),
  };
};

const normalizeBody = (body: unknown): unknown => {
  if (Buffer.isBuffer(body)) {
    return body.toString('utf8');
  }

  return body;
};

const extractErrorMessage = (statusCode: number, body: unknown): string => {
  const normalized = normalizeBody(body);

  if (typeof normalized === 'string') {
    try {
      const parsed = JSON.parse(normalized);
      return extractErrorMessage(statusCode, parsed);
    } catch {
      return truncateText(normalized || `HTTP ${statusCode}`);
    }
  }

  if (normalized && typeof normalized === 'object') {
    const payload = normalized as Record<string, unknown>;
    const message = payload.error ?? payload.message ?? payload.detail;
    if (typeof message === 'string' && message.trim()) {
      return truncateText(message.trim());
    }

    return truncateText(JSON.stringify(sanitizeValue(payload)));
  }

  return `HTTP ${statusCode}`;
};

const isExpectedClientMiss = (req: Request, res: Response): boolean => {
  if (res.statusCode !== 404 || req.method !== 'GET') {
    return false;
  }

  return /^\/api\/playerRoom\/\d+(?:[?#].*)?$/.test(req.originalUrl);
};

const writeApiErrorLog = (req: Request, res: Response, errorMessage: string, errorStack?: string | null) => {
  void recordApiErrorLog({
    method: req.method,
    path: req.originalUrl,
    statusCode: res.statusCode,
    requestParams: safeParams(req),
    errorMessage,
    errorStack,
    ipAddress: getClientIp(req),
    userAgent: getUserAgent(req),
  }).catch((logError) => {
    console.error('Failed to save API error log:', logError);
  });
};

export const apiErrorLogMiddleware: RequestHandler = (req, res, next: NextFunction) => {
  if (!isTrackedRequest(req)) {
    next();
    return;
  }

  let responseBody: unknown;
  const originalJson = res.json.bind(res);
  const originalSend = res.send.bind(res);

  res.json = ((body?: unknown) => {
    responseBody = body;
    return originalJson(body);
  }) as Response['json'];

  res.send = ((body?: unknown) => {
    responseBody = body;
    return originalSend(body as any);
  }) as Response['send'];

  res.on('finish', () => {
    if (res.locals.apiErrorAlreadyLogged) {
      return;
    }

    if (res.locals.securityBlocked) {
      return;
    }

    if (res.statusCode < 400) {
      return;
    }

    const errorMessage = extractErrorMessage(res.statusCode, responseBody);
    if (isExpectedClientMiss(req, res)) {
      return;
    }

    writeApiErrorLog(req, res, errorMessage);
  });

  next();
};

export const apiErrorHandler: ErrorRequestHandler = (error, req, res, next) => {
  if (!isTrackedRequest(req)) {
    next(error);
    return;
  }

  if (res.headersSent) {
    next(error);
    return;
  }

  const statusCode = typeof (error as any)?.status === 'number'
    ? (error as any).status
    : typeof (error as any)?.statusCode === 'number'
      ? (error as any).statusCode
      : 500;

  res.status(statusCode);
  res.locals.apiErrorAlreadyLogged = true;

  const message = error instanceof Error ? error.message : 'Unhandled API error';
  const stack = error instanceof Error ? error.stack ?? null : null;
  writeApiErrorLog(req, res, message, stack);

  res.json({ error: statusCode >= 500 ? 'Internal server error' : message });
};
