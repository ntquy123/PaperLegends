import { Prisma } from '@prisma/client';
import prisma from '../models/prismaClient';

const MAX_TEXT_LENGTH = 8000;

export type ApiErrorLogParams = {
  method: string;
  path: string;
  statusCode?: number | null;
  requestParams?: unknown;
  errorMessage: string;
  errorStack?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  createdAt?: Date;
};

export type ApiErrorLogSearchParams = {
  page?: number;
  pageSize?: number;
  search?: string;
  from?: Date;
  to?: Date;
};

type ApiErrorLogRow = {
  logId: number;
  method: string;
  path: string;
  statusCode: number | null;
  requestParams: unknown;
  errorMessage: string;
  errorStack: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: Date;
};

const truncateText = (value: unknown, maxLength = MAX_TEXT_LENGTH): string => {
  const text = typeof value === 'string' ? value : JSON.stringify(value);
  if (!text) return '';
  return text.length > maxLength ? `${text.slice(0, maxLength)}...` : text;
};

export const recordApiErrorLog = async (params: ApiErrorLogParams) => {
  const requestParamsSql = params.requestParams == null
    ? Prisma.sql`NULL`
    : Prisma.sql`${JSON.stringify(params.requestParams)}::jsonb`;

  return prisma.$executeRaw`
    INSERT INTO "ApiErrorLog" (
      "method",
      "path",
      "statusCode",
      "requestParams",
      "errorMessage",
      "errorStack",
      "ipAddress",
      "userAgent",
      "createdAt"
    )
    VALUES (
      ${params.method.toUpperCase()},
      ${truncateText(params.path, 1000)},
      ${params.statusCode ?? null},
      ${requestParamsSql},
      ${truncateText(params.errorMessage)},
      ${params.errorStack ? truncateText(params.errorStack, 12000) : null},
      ${params.ipAddress ? truncateText(params.ipAddress, 255) : null},
      ${params.userAgent ? truncateText(params.userAgent, 1000) : null},
      ${params.createdAt ?? new Date()}
    )
  `;
};

const buildWhereSql = (params: ApiErrorLogSearchParams) => {
  const conditions: Prisma.Sql[] = [];

  if (params.from instanceof Date && !Number.isNaN(params.from.getTime())) {
    conditions.push(Prisma.sql`"createdAt" >= ${params.from}`);
  }

  if (params.to instanceof Date && !Number.isNaN(params.to.getTime())) {
    conditions.push(Prisma.sql`"createdAt" <= ${params.to}`);
  }

  const search = typeof params.search === 'string' ? params.search.trim() : '';
  if (search) {
    const keyword = `%${search}%`;
    conditions.push(Prisma.sql`(
      "method" ILIKE ${keyword}
      OR "path" ILIKE ${keyword}
      OR "errorMessage" ILIKE ${keyword}
      OR "ipAddress" ILIKE ${keyword}
      OR "userAgent" ILIKE ${keyword}
    )`);
  }

  return conditions.length > 0
    ? Prisma.sql`WHERE ${Prisma.join(conditions, ' AND ')}`
    : Prisma.empty;
};

export const getApiErrorLogs = async (params: ApiErrorLogSearchParams = {}) => {
  const page = Number.isFinite(params.page) && (params.page ?? 0) > 0 ? Math.floor(params.page as number) : 1;
  const pageSize = Number.isFinite(params.pageSize) && (params.pageSize ?? 0) > 0
    ? Math.min(Math.floor(params.pageSize as number), 200)
    : 50;

  const skip = (page - 1) * pageSize;
  const whereSql = buildWhereSql(params);
  const countRows = await prisma.$queryRaw<{ count: bigint }[]>(Prisma.sql`
    SELECT COUNT(*)::bigint AS "count"
    FROM "ApiErrorLog"
    ${whereSql}
  `);
  const logs = await prisma.$queryRaw<ApiErrorLogRow[]>(Prisma.sql`
    SELECT
      "logId",
      "method",
      "path",
      "statusCode",
      "requestParams",
      "errorMessage",
      "errorStack",
      "ipAddress",
      "userAgent",
      "createdAt"
    FROM "ApiErrorLog"
    ${whereSql}
    ORDER BY "createdAt" DESC
    OFFSET ${skip}
    LIMIT ${pageSize}
  `);

  const totalItems = Number(countRows[0]?.count ?? 0);

  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));

  return {
    logs,
    pagination: {
      page,
      pageSize,
      totalItems,
      totalPages,
    },
  };
};
