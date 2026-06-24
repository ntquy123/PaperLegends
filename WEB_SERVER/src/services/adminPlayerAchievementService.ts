import prisma from '../models/prismaClient';

export interface PlayerAchievementPayload {
  rewardType: string;
  seq: number;
  locationId?: number | null;
  rewardAmount?: number | null;
  itemId?: number | null;
  countGif?: number | null;
  isUsed: boolean;
  achievedAt?: Date | null;
}

const toRewardTypeNumber = (rewardType: string) => {
  const parsed = Number(rewardType);
  return Number.isInteger(parsed) ? parsed : null;
};

const decorateAchievements = async (achievements: any[]) => {
  const rewardTypeIds = Array.from(
    new Set(
      achievements
        .map((achievement) => toRewardTypeNumber(String(achievement.rewardType)))
        .filter((value): value is number => value !== null),
    ),
  );

  const itemIds = Array.from(
    new Set(
      achievements
        .map((achievement) => achievement.itemId)
        .filter((value): value is number => Number.isInteger(value)),
    ),
  );

  const [generals, items] = await Promise.all([
    rewardTypeIds.length
      ? prisma.sysMasGeneral.findMany({
          where: { GenCode: { in: rewardTypeIds } },
          select: { GenCode: true, GenName: true },
        })
      : Promise.resolve([]),
    itemIds.length
      ? prisma.item.findMany({
          where: { id: { in: itemIds } },
          select: { id: true, name: true },
        })
      : Promise.resolve([]),
  ]);

  const generalMap = new Map(generals.map((general) => [general.GenCode, general.GenName]));
  const itemMap = new Map(items.map((item) => [item.id, item.name]));

  return achievements.map((achievement) => {
    const rewardType = String(achievement.rewardType ?? '');
    const rewardTypeNumber = toRewardTypeNumber(rewardType);
    return {
      rewardType,
      seq: achievement.seq,
      locationId: achievement.locationId,
      rewardAmount: achievement.rewardAmount,
      itemId: achievement.itemId,
      countGif: achievement.countGif,
      isUsed: achievement.isUsed,
      achievedAt: achievement.achievedAt,
      rewardTypeName: rewardTypeNumber !== null ? generalMap.get(rewardTypeNumber) ?? null : null,
      itemName: Number.isInteger(achievement.itemId) ? itemMap.get(achievement.itemId) ?? null : null,
    };
  });
};

export const getAllPlayerAchievements = async () => {
  const achievements = await prisma.playerAchievement.findMany({
    orderBy: [{ rewardType: 'asc' }, { seq: 'asc' }],
  });

  return decorateAchievements(achievements);
};

export const createPlayerAchievement = async (payload: PlayerAchievementPayload) => {
  const achievement = await prisma.playerAchievement.create({
    data: {
      rewardType: payload.rewardType,
      seq: payload.seq,
      locationId: payload.locationId ?? null,
      rewardAmount: payload.rewardAmount ?? null,
      itemId: payload.itemId ?? null,
      countGif: payload.countGif ?? null,
      isUsed: payload.isUsed,
      achievedAt: payload.achievedAt ?? undefined,
    },
  });

  return decorateAchievements([achievement]).then((rows) => rows[0]);
};

export const updatePlayerAchievement = async (
  rewardType: string,
  seq: number,
  payload: Partial<PlayerAchievementPayload>,
) => {
  const achievement = await prisma.playerAchievement.update({
    where: { rewardType_seq: { rewardType, seq } },
    data: {
      locationId: payload.locationId ?? null,
      rewardAmount: payload.rewardAmount ?? null,
      itemId: payload.itemId ?? null,
      countGif: payload.countGif ?? null,
      isUsed: payload.isUsed,
      achievedAt: payload.achievedAt ?? undefined,
    },
  });

  return decorateAchievements([achievement]).then((rows) => rows[0]);
};

export const deletePlayerAchievement = async (rewardType: string, seq: number) => {
  return prisma.playerAchievement.delete({
    where: { rewardType_seq: { rewardType, seq } },
  });
};
