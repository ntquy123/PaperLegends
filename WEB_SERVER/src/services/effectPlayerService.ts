import prisma from '../models/prismaClient';

export const getByPlayerId = async (playerId: number) => {
  return prisma.effectPlayer.findMany({
    where: { playerId, player: { IsActive: true } },
    include: {
      sysMasGeneral: {
        select: {
          GenName: true, // Lấy tên kỹ năng
          ParentCode: true, // Lấy parentId nếu cần
          description: true // Lấy mô tả kỹ năng
        },
      },
       player: {
        select: {
          TalentPoint: true,
          Level: true
        }
      }
    },
  });
};

export const levelUpEffectPlayer = async (
  playerId: number,
  effectId: number
) => {
  return prisma.$transaction(async (tx) => {
    const player = await tx.player.findFirst({
      where: { id: playerId, IsActive: true },
      select: { TalentPoint: true },
    });

    if (!player) {
      throw new Error('Player not found or inactive');
    }

    const currentTP = player.TalentPoint ?? 0;
    if (currentTP <= 0) {
      throw new Error('Không còn điểm TalentPoint để tăng cấp');
    }

    await tx.player.update({
      where: { id: playerId },
      data: { TalentPoint: currentTP - 1 },
    });

    await tx.effectPlayer.update({
      where: { playerId_effectId: { playerId, effectId } },
      data: {
        level: { increment: 1 },
      },
    });

    return { TalentPoint: currentTP - 1 };

  });
};

export const equipEffectPlayer = async (
  playerId: number,
  oldEffectId: number,
  newEffectId: number
) => {
  return prisma.$transaction(async (tx) => {
    const player = await tx.player.findFirst({
      where: { id: playerId, IsActive: true },
      select: { id: true },
    });

    if (!player) {
      throw new Error('Player not found or inactive');
    }
    const OldEffect = await tx.effectPlayer.findUnique({
      where: { playerId_effectId: { playerId, effectId: oldEffectId } },
      select: { IsActive: true, IsEquiped: true },
    });
    const newEffect = await tx.effectPlayer.findUnique({
      where: { playerId_effectId: { playerId, effectId: newEffectId } },
      select: { IsActive: true, IsEquiped: true },
    });

    if (!OldEffect) {
      throw new Error('Old Skill not found for player');
    }
    if (!newEffect) {
      throw new Error('Skill not found for player');
    }

    if (!newEffect.IsActive) {
      throw new Error('Chỉ có thể trang bị kỹ năng đang hoạt động');
    }

    await tx.effectPlayer.updateMany({
      where: { playerId, effectId: oldEffectId },
      data: { IsEquiped: false },
    });

    const equippedCount = await tx.effectPlayer.count({
      where: {
        playerId,
        IsEquiped: true,
        effectId: { not: newEffectId },
      },
    });

    if (!newEffect.IsEquiped && equippedCount >= 3) {
      throw new Error('Người chơi chỉ có thể trang bị tối đa 3 kỹ năng');
    }

    return tx.effectPlayer.update({
      where: { playerId_effectId: { playerId, effectId: newEffectId } },
      data: { IsEquiped: true },
    });
  });
};
