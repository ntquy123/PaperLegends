import prisma from '../models/prismaClient';
import { addItemToInventory } from './playerItemService';

type NormalizeOptions = {
  achievement?: any;
};

const toFiniteNumber = (value: unknown) =>
  typeof value === 'number' && Number.isFinite(value) ? value : undefined;

const buildAchievementKey = (rewardType: unknown, seq: unknown) => {
  const normalizedSeq = Number(seq);
  const keySeq = Number.isFinite(normalizedSeq) ? normalizedSeq : '';

  return `${String(rewardType ?? '')}:${keySeq}`;
};

const normalizeAchievementStatus = (
  status: any,
  { achievement }: NormalizeOptions = {},
) => {
  if (!status) {
    return status;
  }

  const normalized = {
    ...status,
    itemId:
      toFiniteNumber(status.itemId) ??
      toFiniteNumber(achievement?.itemId) ??
      null,
    ringBall:
      toFiniteNumber(status.ringBall) ??
      toFiniteNumber(achievement?.ringBall) ??
      toFiniteNumber(status.rewardAmount) ??
      toFiniteNumber(achievement?.rewardAmount) ??
      0,
  };

  return normalized;
};

export const listPlayerAchievements = async (playerId: number) => {
  const statuses = await prisma.playerAchievementStatus.findMany({
    where: { playerId },
  });

  if (statuses.length === 0) {
    return statuses;
  }

  const rewardTypes = new Set<string>();
  const sequences = new Set<number>();

  statuses.forEach((status: any) => {
    if (status?.typeGid === undefined || status?.typeGid === null) {
      return;
    }

    const seq = Number(status.achievementId);

    if (!Number.isFinite(seq)) {
      return;
    }

    rewardTypes.add(String(status.typeGid));
    sequences.add(seq);
  });

  let achievements: any[] = [];

  if (rewardTypes.size > 0 && sequences.size > 0) {
    achievements = await prisma.playerAchievement.findMany({
      where: {
        rewardType: { in: Array.from(rewardTypes) },
        seq: { in: Array.from(sequences) },
      },
    });
  }

  const achievementMap = new Map<string, any>();

  achievements.forEach((achievement: any) => {
    const key = buildAchievementKey(achievement?.rewardType, achievement?.seq);
    if (!achievementMap.has(key)) {
      achievementMap.set(key, achievement);
    }
  });

  return statuses.map((status: any) => {
    const achievement = achievementMap.get(
      buildAchievementKey(status?.typeGid, status?.achievementId),
    );
    return normalizeAchievementStatus(status, { achievement });
  });
};

export const claimAchievement = async (
  playerId: number,
  typeGid: string,
  achievementId: number,
) => {
  const result = await prisma.$transaction(async (tx) => {
    const record: any = await (tx.playerAchievementStatus as any).findFirst({
      where: {
        playerId,
        typeGid,
        achievementId,
      },
      orderBy: { TransDate: 'desc' },
    });

    if (!record || !record.isComplete || record.isGiftReceived) {
      return null;
    }

    const rewardType = String(record.typeGid);

    const possibleAchievements = await tx.playerAchievement.findMany({
      where: {
        rewardType,
        seq: record.achievementId,
      },
    });

    const achievement = possibleAchievements[0];

    const normalizedRecord = normalizeAchievementStatus(record, {
      achievement,
    });

    await (tx.playerAchievementStatus as any).update({
      where: {
        playerId_typeGid_achievementId_TransDate: {
          playerId,
          typeGid,
          achievementId,
          TransDate: record.TransDate,
        },
      },
      data: { isGiftReceived: true },
    });

    const updatedRecord = {
      ...normalizedRecord,
      isGiftReceived: true,
    };

    const ringBallReward =
      typeof updatedRecord.ringBall === 'number' ? updatedRecord.ringBall : 0;

    if (ringBallReward > 0) {
      await tx.player.update({
        where: { id: playerId },
        data: { RingBall: { increment: ringBallReward } },
      });
    }

    return updatedRecord;
  });

  const rewardItemId =
    typeof (result as any)?.itemId === 'number' &&
    Number.isFinite((result as any).itemId)
      ? (result as any).itemId
      : null;

  if (rewardItemId && rewardItemId > 0) {
    await addItemToInventory(playerId, rewardItemId);
  }

  return result;
};
