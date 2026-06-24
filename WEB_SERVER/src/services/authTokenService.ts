import { SignJWT, jwtVerify } from 'jose';
import { createHash, randomUUID } from 'crypto';
import prisma from '../models/prismaClient';

const accessTokenTtlSeconds = Number(process.env.ACCESS_TOKEN_TTL_SECONDS ?? 900);
const refreshTokenTtlDays = Number(process.env.REFRESH_TOKEN_TTL_DAYS ?? 14);
const tokenClockToleranceSeconds = Number(process.env.TOKEN_CLOCK_TOLERANCE_SECONDS ?? 60);

const accessSecret = new TextEncoder().encode(
  process.env.ACCESS_TOKEN_SECRET ?? 'development-access-secret'
);
const refreshSecret = new TextEncoder().encode(
  process.env.REFRESH_TOKEN_SECRET ?? 'development-refresh-secret'
);

export interface TokenPairResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
}

export interface VerifiedAccessToken {
  userId: number;
  deviceId: string;
  jti?: string;
}

const hashToken = (token: string) => createHash('sha256').update(token).digest('hex');

const buildAccessToken = async (userId: number, deviceId: string, jti: string, now: Date) => {
  const expiresAt = new Date(now.getTime() + accessTokenTtlSeconds * 1000);

  const token = await new SignJWT({ deviceId })
    .setProtectedHeader({ alg: 'HS256' })
    .setSubject(String(userId))
    .setJti(jti)
    .setIssuedAt(Math.floor(now.getTime() / 1000))
    .setExpirationTime(Math.floor(expiresAt.getTime() / 1000))
    .sign(accessSecret);

  return { token, expiresAt };
};

const buildRefreshToken = async (
  userId: number,
  deviceId: string,
  jti: string,
  now: Date
) => {
  const expiresAt = new Date(now.getTime() + refreshTokenTtlDays * 24 * 60 * 60 * 1000);

  const token = await new SignJWT({ deviceId })
    .setProtectedHeader({ alg: 'HS256' })
    .setSubject(String(userId))
    .setJti(jti)
    .setIssuedAt(Math.floor(now.getTime() / 1000))
    .setExpirationTime(Math.floor(expiresAt.getTime() / 1000))
    .sign(refreshSecret);

  return { token, expiresAt };
};

const persistRefreshToken = async (
  userId: number,
  deviceId: string,
  refreshToken: string,
  refreshExpiresAt: Date,
  jti: string
) => {
  const refreshTokenHash = hashToken(refreshToken);

  await prisma.userRefreshToken.create({
    data: {
      userId,
      refreshTokenHash,
      deviceId,
      jti,
      expiresAt: refreshExpiresAt,
    },
  });
};

export const issueTokensForDevice = async (
  userId: number,
  deviceId: string
): Promise<TokenPairResponse> => {
  const now = new Date();
  const accessJti = randomUUID();
  const refreshJti = randomUUID();

  const accessToken = await buildAccessToken(userId, deviceId, accessJti, now);
  const refreshToken = await buildRefreshToken(userId, deviceId, refreshJti, now);

  await persistRefreshToken(userId, deviceId, refreshToken.token, refreshToken.expiresAt, refreshJti);

  return {
    accessToken: accessToken.token,
    accessTokenExpiresAt: accessToken.expiresAt.toISOString(),
    refreshToken: refreshToken.token,
    refreshTokenExpiresAt: refreshToken.expiresAt.toISOString(),
  };
};

export const revokeTokensForOtherDevices = async (userId: number, deviceId: string) => {
  await prisma.userRefreshToken.updateMany({
    where: {
      userId,
      deviceId: { not: deviceId },
      revokedAt: null,
    },
    data: { revokedAt: new Date() },
  });
};

const verifyStoredRefreshToken = async (refreshToken: string) => {
  const { payload } = await jwtVerify(refreshToken, refreshSecret, {
    algorithms: ['HS256'],
    clockTolerance: tokenClockToleranceSeconds,
  });
  const userId = Number(payload.sub);
  const jti = typeof payload.jti === 'string' ? payload.jti : undefined;
  const deviceId = typeof payload.deviceId === 'string' ? payload.deviceId : undefined;

  if (!Number.isInteger(userId)) {
    throw new Error('Invalid refresh token subject');
  }

  const tokenHash = hashToken(refreshToken);

  const stored = await prisma.userRefreshToken.findUnique({
    where: { refreshTokenHash: tokenHash },
  });

  if (!stored) {
    throw new Error('Refresh token not found');
  }

  return { userId, jti, deviceId, stored };
};

export const refreshTokensForDevice = async (
  refreshToken: string,
  deviceId: string
): Promise<TokenPairResponse> => {
  const { userId, jti, deviceId: tokenDeviceId, stored } = await verifyStoredRefreshToken(
    refreshToken
  );

  if (stored.revokedAt) {
    throw new Error('Refresh token revoked');
  }

  if (tokenDeviceId && tokenDeviceId !== deviceId) {
    await prisma.userRefreshToken.update({
      where: { id: stored.id },
      data: { revokedAt: new Date(), lastUsedAt: new Date() },
    });
    throw new Error('Refresh token belongs to another device');
  }

  if (jti && stored.jti !== jti) {
    await prisma.userRefreshToken.update({
      where: { id: stored.id },
      data: { revokedAt: new Date(), lastUsedAt: new Date() },
    });
    throw new Error('Refresh token identifier mismatch');
  }

  if (stored.expiresAt.getTime() <= Date.now()) {
    await prisma.userRefreshToken.update({
      where: { id: stored.id },
      data: { revokedAt: new Date(), lastUsedAt: new Date() },
    });
    throw new Error('Refresh token expired');
  }

  await prisma.userRefreshToken.update({
    where: { id: stored.id },
    data: { revokedAt: new Date(), lastUsedAt: new Date() },
  });

  return issueTokensForDevice(userId, deviceId);
};

export const revokeRefreshToken = async (refreshToken: string) => {
  try {
    const { stored } = await verifyStoredRefreshToken(refreshToken);

    if (!stored.revokedAt) {
      await prisma.userRefreshToken.update({
        where: { id: stored.id },
        data: { revokedAt: new Date() },
      });
    }
  } catch (error) {
    // Ignore errors to prevent token fishing
  }
};

export const revokeAllRefreshTokens = async (refreshToken: string) => {
  const { userId } = await verifyStoredRefreshToken(refreshToken);

  await prisma.userRefreshToken.updateMany({
    where: { userId },
    data: { revokedAt: new Date() },
  });
};

export const verifyAccessToken = async (accessToken: string): Promise<VerifiedAccessToken> => {
  const { payload } = await jwtVerify(accessToken, accessSecret, {
    algorithms: ['HS256'],
    clockTolerance: tokenClockToleranceSeconds,
  });
  const userId = Number(payload.sub);
  const deviceId = typeof payload.deviceId === 'string' ? payload.deviceId : undefined;

  if (!Number.isInteger(userId) || !deviceId) {
    throw new Error('Invalid access token');
  }

  return { userId, deviceId, jti: typeof payload.jti === 'string' ? payload.jti : undefined };
};
