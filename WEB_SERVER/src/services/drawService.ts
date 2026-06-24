import type { Prisma } from '@prisma/client';
import prisma from '../models/prismaClient';
import { addItemToInventory } from './playerItemService';

interface RewardResult {
  type: 'item' | 'culi';
  itemId?: number;
  amount?: number;
}

const getStartOfDay = () => {
  const d = new Date();
  d.setUTCHours(0, 0, 0, 0);
  return d;
};

export const drawReward = async (playerId: number): Promise<RewardResult> => {
  const active = await prisma.player.findFirst({
    where: { id: playerId, IsActive: true },
    select: { id: true },
  });

  if (!active) {
    throw new Error('Player not found or inactive');
  }
  const histories = await prisma.history.findMany({
    where: { playerId, marbBet: { gte: 5 } },
    orderBy: { createdAt: 'desc' },
    take: 5,
  });

  let winStreak = 0;
  if (histories.length > 0) {
    const status = histories[0].statusWin;
    for (const h of histories) {
      if (h.statusWin === status) {
        winStreak += 1;
      } else {
        break;
      }
    }
  }

  let winBonus = 0;
  if (winStreak >= 5) winBonus = 30;
  else if (winStreak >= 4) winBonus = 20;
  else if (winStreak >= 3) winBonus = 10;

  const lastThree = histories.slice(0, 3);
  const totalBet = lastThree.reduce((sum, h) => sum + (h.marbBet ?? 0), 0);
  let betBonus = 0;
  if (totalBet >= 60) betBonus = 50;
  else if (totalBet >= 30) betBonus = 25;
  else betBonus = 10;

  let probability = winBonus + betBonus;
  if (probability > 100) probability = 100;

  const randomValue = Math.random() * 100;
  let isRare = randomValue < probability;

  if (isRare) {
    const day = getStartOfDay();
    let daily = await prisma.dailyRareItem.findUnique({
      where: { playerId_date: { playerId, date: day } },
    });
    if (!daily) {
      daily = await prisma.dailyRareItem.create({
        data: { playerId, date: day, count: 0 },
      });
    }

    if (daily.count >= 3) {
      isRare = false;
    } else {
      const items = await prisma.item.findMany({ where: { locationGid: 3 } });
      if (items.length > 0) {
        const chosen = items[Math.floor(Math.random() * items.length)];
        await addItemToInventory(playerId, chosen.id);
        await prisma.dailyRareItem.update({
          where: { playerId_date: { playerId, date: day } },
          data: { count: { increment: 1 } },
        });
        return { type: 'item', itemId: chosen.id };
      } else {
        isRare = false;
      }
    }
  }

  // Fallback to common reward
   let amount = 0;
  const rand = Math.random() * 100;
  if (rand < 30) amount = 0;         // 30% được 0 viên
  else if (rand < 60) amount = 1;    // 30% được 1 viên
  else if (rand < 80) amount = 2;    // 20% được 2 viên
  else if (rand < 95) amount = 3;    // 15% được 3 viên
  else amount = 4;   
  if (amount > 0) {
    await prisma.player.update({
      where: { id: playerId },
      data: { RingBall: { increment: amount } },
    });
  }
  return { type: 'culi', amount };
};

type LuckyDrawReward =
  | {
      rewardType: 'item';
      itemId: number;
      itemName: string;
      isRare: boolean;
      isItem: true;
      luckyRate: number;
    }
  | {
      rewardType: 'stats';
      itemName: string;
      ringBall: number;
      exp: number;
      isRare: false;
      isItem: false;
      luckyRate: number;
    };

const pickByWeight = <T extends { weight: number }>(options: T[]): T => {
  const total = options.reduce((sum, option) => sum + option.weight, 0);
  let roll = Math.random() * total;

  for (const option of options) {
    if (roll < option.weight) {
      return option;
    }
    roll -= option.weight;
  }

  return options[options.length - 1];
};

export const luckyDrawAfterMatch = async (
  playerId: number,
): Promise<LuckyDrawReward> => {
  return prisma.$transaction(async (tx) => luckyDrawAfterMatchWithTx(playerId, tx));
};

export const luckyDrawAfterMatchWithTx = async (
  playerId: number,
  tx: Prisma.TransactionClient,
): Promise<LuckyDrawReward> => {
    const player = await tx.player.findFirst({
      where: { id: playerId, IsActive: true },
      select: { LuckyRate: true },
    });

    if (!player) {
      throw new Error('Player not found or inactive');
    }

    let luckyRate = player.LuckyRate ?? 5;
    const isGuaranteedRare = luckyRate >= 100;
    const isRare = isGuaranteedRare || Math.random() * 100 < luckyRate;

    if (isRare) {
      const rareItemIds = [
        99000006, 99000007, 99000008, 99000009, 99000010,
        99000011, 99000012, 99000013, 99000014, 99000015,
      ];
      const rareItemId =
        rareItemIds[Math.floor(Math.random() * rareItemIds.length)];

      const rareItem = await tx.item.findUnique({
        where: { id: rareItemId },
        select: { name: true },
      });
      const itemName = rareItem?.name ?? 'Unknown Item';

      await addItemToInventory(playerId, rareItemId, tx);

      await tx.player.update({
        where: { id: playerId },
        data: { LuckyRate: 0 },
      });

      return {
        rewardType: 'item',
        itemId: rareItemId,
        itemName,
        isRare: true,
        isItem: true,
        luckyRate: 0,
      };
    }

    luckyRate = Math.min(luckyRate + 20, 100);

    const chooseItem = Math.random() < 0.5;

    if (chooseItem) {
      const itemOptions = [
        { itemId: 98000001, weight: 50 },
        { itemId: 98000002, weight: 30 },
        { itemId: 98000003, weight: 10 },
      ];

      const chosenItem = pickByWeight(itemOptions).itemId;

      const item = await tx.item.findUnique({
        where: { id: chosenItem },
        select: { name: true },
      });
      const itemName = item?.name ?? 'Unknown Item';

      await addItemToInventory(playerId, chosenItem, tx);

      await tx.player.update({
        where: { id: playerId },
        data: { LuckyRate: luckyRate },
      });

      return {
        rewardType: 'item',
        itemId: chosenItem,
        itemName,
        isRare: false,
        isItem: true,
        luckyRate,
      };
    }

    const ringBallOptions = [
      { amount: 1, weight: 50 },
      { amount: 2, weight: 30 },
      { amount: 3, weight: 10 },
    ];

    const ringBall = pickByWeight(ringBallOptions).amount;
    const exp = Math.floor(Math.random() * 51) + 50; // 50 - 100 EXP

    await tx.player.update({
      where: { id: playerId },
      data: {
        LuckyRate: luckyRate,
        RingBall: { increment: ringBall },
        Exp: { increment: exp },
      },
    });

    return {
      rewardType: 'stats',
      itemName: '',
      ringBall,
      exp,
      isRare: false,
      isItem: false,
      luckyRate,
    };
};
