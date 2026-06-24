import { inspect } from 'util';

type LogLevel = 'log' | 'info' | 'warn' | 'error' | 'debug';

export type ServerLogEntry = {
  timestamp: string;
  level: LogLevel;
  message: string;
};

const MAX_LOG_ENTRIES = Number(process.env.ADMIN_LOG_BUFFER_SIZE ?? 2000);
const logBuffer: ServerLogEntry[] = [];

const formatLogArg = (value: unknown) => {
  if (value instanceof Error) {
    return value.stack ?? value.message;
  }

  if (typeof value === 'string') {
    return value;
  }

  if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') {
    return String(value);
  }

  return inspect(value, {
    depth: 4,
    breakLength: 120,
    maxArrayLength: 50,
  });
};

const pushLog = (level: LogLevel, args: unknown[]) => {
  const message = args.map(formatLogArg).join(' ');

  logBuffer.push({
    timestamp: new Date().toISOString(),
    level,
    message,
  });

  if (logBuffer.length > MAX_LOG_ENTRIES) {
    logBuffer.splice(0, logBuffer.length - MAX_LOG_ENTRIES);
  }
};

export const getServerLogs = (tail = 200): ServerLogEntry[] =>
  logBuffer.slice(Math.max(0, logBuffer.length - tail));

export const initServerLogCapture = () => {
  const consoleRef = console as Console & { __logCaptureInitialized?: boolean };
  if (consoleRef.__logCaptureInitialized) {
    return;
  }

  consoleRef.__logCaptureInitialized = true;

  (['log', 'info', 'warn', 'error', 'debug'] as LogLevel[]).forEach((level) => {
    const original = console[level].bind(console);
    console[level] = (...args: unknown[]) => {
      pushLog(level, args);
      original(...args);
    };
  });
};
