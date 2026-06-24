import type { Prisma } from '@prisma/client';
import prisma from '../models/prismaClient';

const getTodayUtcDate = (): Date => {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
};

const SHOP_CURRENCY = {
  MONEY: 1,
  RINGBALL: 2,
} as const;

const BALL_SKILL_MIN_GID = 11400001;
const BALL_SKILL_MAX_GID = 11400006;

export const generateRandomBallSkillGid = (): number =>
  Math.floor(Math.random() * (BALL_SKILL_MAX_GID - BALL_SKILL_MIN_GID + 1)) + BALL_SKILL_MIN_GID;

export const buyItem = async (playerId: number, itemId: number, currencyType: number = SHOP_CURRENCY.MONEY) => {
  return prisma.$transaction(async (tx) => {
    const item = await (tx as any).item.findUnique({
      where: { id: itemId },
      select: { price: true, priceByBall: true, maxPurchasePerDay: true }
    });

    if (!item) {
      throw new Error('Item not found');
    }

    const player = await tx.player.findFirst({
      where: { id: playerId, IsActive: true },
      select: { Money: true, RingBall: true }
    });

    if (!player) {
      throw new Error('Player not found or inactive');
    }

    const maxPurchasePerDay = Number(item.maxPurchasePerDay ?? 0);
    const dailyPurchaseLimit = maxPurchasePerDay > 0 ? Math.floor(maxPurchasePerDay) : null;
    const today = getTodayUtcDate();

    if (dailyPurchaseLimit !== null) {
      const purchaseTracker = await tx.dailyShopPurchase.findUnique({
        where: {
          userId_itemId_purchaseDate: {
            userId: playerId,
            itemId,
            purchaseDate: today,
          },
        },
      });

      if (
        purchaseTracker !== null &&
        purchaseTracker.purchaseCount >= dailyPurchaseLimit
      ) {
        throw new Error(
          `Daily purchase limit reached (${dailyPurchaseLimit}/day) for item ${itemId}`
        );
      }
    }

    const costMoney = item.price ?? 0;
    const costRingBall = (item as any).priceByBall ?? 0;

    if (itemId === 88000001) {
      if (currencyType !== SHOP_CURRENCY.MONEY) {
        throw new Error('Item is only purchasable by money');
      }
      if ((player.Money ?? 0) < costMoney) {
        throw new Error('Not enough money');
      }
      await tx.player.update({
        where: { id: playerId },
        data: {
          Money: { decrement: costMoney },
          RingBall: { increment: 10 },
        },
      });
      return { success: true } as const;
    }

    if (currencyType === SHOP_CURRENCY.MONEY) {
      if (costMoney <= 0) {
        throw new Error('Item is not purchasable by money');
      }
      if ((player.Money ?? 0) < costMoney) {
        throw new Error('Not enough money');
      }
    } else if (currencyType === SHOP_CURRENCY.RINGBALL) {
      if (costRingBall <= 0) {
        throw new Error('Item is not purchasable by RingBall');
      }
      if ((player.RingBall ?? 0) < costRingBall) {
        throw new Error('Not enough RingBall');
      }
    } else {
      throw new Error('Invalid currency type');
    }

    const lastSeq = await tx.playerItem.findFirst({
      where: {
        playerId,
        itemId
      },
      orderBy: { seq: 'desc' },
      select: { seq: true }
    });

    const seq = lastSeq ? lastSeq.seq + 1 : 0;
    const itemMeta = await tx.item.findUnique({
      where: { id: itemId },
      select: { typeGid: true },
    });
    const skillGid = itemMeta?.typeGid === 1 ? generateRandomBallSkillGid() : null;

    const playerItem = await tx.playerItem.create({
      data: {
        playerId,
        itemId,
        seq,
        level: 1,
        SkillGid: skillGid,
        description: '',
        damage: 0
      }
    });

    if (currencyType === SHOP_CURRENCY.MONEY) {
      await tx.player.update({
        where: { id: playerId },
        data: { Money: { decrement: costMoney } },
      });
    } else {
      await tx.player.update({
        where: { id: playerId },
        data: { RingBall: { decrement: costRingBall } },
      });
    }

    if (dailyPurchaseLimit !== null) {
      await tx.dailyShopPurchase.upsert({
        where: {
          userId_itemId_purchaseDate: {
            userId: playerId,
            itemId,
            purchaseDate: today,
          },
        },
        create: {
          userId: playerId,
          itemId,
          purchaseDate: today,
          purchaseCount: 1,
          maxPurchasePerDay: dailyPurchaseLimit,
        },
        update: {
          purchaseCount: { increment: 1 },
          maxPurchasePerDay: dailyPurchaseLimit,
        },
      });
    }

    return playerItem;
  });
};

export const sellItem = async (
  playerId: number,
  itemId: number,
  seq: number
) => {
  return prisma.$transaction(async (tx) => {
    const item = await (tx as any).item.findUnique({
      where: { id: itemId },
      select: { price: true, priceByBall: true }
    });

    if (!item) {
      throw new Error('Item not found');
    }

    const active = await tx.player.findFirst({
      where: { id: playerId, IsActive: true },
      select: { id: true },
    });

    if (!active) {
      throw new Error('Player not found or inactive');
    }

    if (itemId === 88000001) {
      await tx.player.update({
        where: { id: playerId },
        data: {
          Money: { increment: item.price ?? 0 },
          RingBall: { decrement: 10 },
        },
      });
      return true;
    }

    const playerItem = await tx.playerItem.findUnique({
      where: {
        playerId_itemId_seq: {
          playerId,
          itemId,
          seq,
        },
      },
      select: { playerId: true },
    });

    if (!playerItem) {
      throw new Error('PlayerItem not found');
    }

    await tx.playerItem.delete({
      where: {
        playerId_itemId_seq: {
          playerId,
          itemId,
          seq
        }
      }
    });

    const costMoney = item.price ?? 0;
    const costRingBall = (item as any).priceByBall ?? 0;

    if (costMoney > 0) {
      await tx.player.update({
        where: { id: playerItem.playerId },
        data: { Money: { increment: costMoney } },
      });
    } else {
      await tx.player.update({
        where: { id: playerItem.playerId },
        data: { RingBall: { increment: costRingBall } },
      });
    }

    return true;
  });
};

type Material = { id: number; seq: number };

export type PlayerItemDamageEntry = {
  playerId: number;
  itemId: number;
  seq: number;
  damage: number;
};

export const levelUpPlayerItem = async (
  playerId: number,
  itemId: number,
  seq: number,
  materials: Material[] = []
) => {
  return prisma.$transaction(async (tx) => {
    const playerItem = await tx.playerItem.findUnique({
      where: {
        playerId_itemId_seq: {
          playerId,
          itemId,
          seq,
        },
      },
      select: { playerId: true },
    });

    if (!playerItem) {
      throw new Error('PlayerItem not found');
    }

    const updated = await tx.playerItem.update({
      where: {
        playerId_itemId_seq: {
          playerId,
          itemId,
          seq,
        },
      },
      data: {
        level: { increment: 1 },
      },
    });

    if (materials.length > 0) {
      await tx.playerItem.deleteMany({
        where: {
          playerId,
          OR: materials.map((m) => ({ itemId: m.id, seq: m.seq })),
        },
      });
    }

    return updated;
  });
};

type AddItemOptions = {
  level?: number;
  description?: string;
  price?: number;
  isSolded?: number;
  skillGid?: number | null;
};

export const addItemToInventory = async (
  playerId: number,
  itemId: number,
  txClient?: Prisma.TransactionClient,
  options?: AddItemOptions,
) => {
  const execute = async (tx: Prisma.TransactionClient) => {
    const lastSeq = await tx.playerItem.findFirst({
      where: {
        playerId,
        itemId,
      },
      orderBy: { seq: 'desc' },
      select: { seq: true },
    });

    const seq = lastSeq ? lastSeq.seq + 1 : 0;
    const { level = 1, description = '', price = 0, isSolded = 0, skillGid } = options ?? {};
    const itemMeta = await tx.item.findUnique({
      where: { id: itemId },
      select: { typeGid: true },
    });
    const resolvedSkillGid =
      skillGid !== undefined
        ? skillGid
        : itemMeta?.typeGid === 1
          ? generateRandomBallSkillGid()
          : null;

    return tx.playerItem.create({
      data: {
        playerId,
        itemId,
        seq,
        level,
        SkillGid: resolvedSkillGid,
        description,
        Price: price,
        IsSolded: isSolded,
        damage: 0,
      },
    });
  };

  if (txClient) {
    return execute(txClient);
  }

  return prisma.$transaction(async (tx) => execute(tx));
};

export const updatePlayerItemDamage = async (
  entries: PlayerItemDamageEntry[]
) => {
  if (!entries || entries.length === 0) {
    return [];
  }

  return prisma.$transaction(async (tx) => {
    const playerIds = Array.from(new Set(entries.map((entry) => entry.playerId)));
    const players = await tx.player.findMany({
      where: { id: { in: playerIds } },
      select: { id: true, ProviderType: true },
    });

    const providerTypeByPlayerId = new Map(players.map((player) => [player.id, player.ProviderType]));
    const filteredEntries = entries.filter((entry) => providerTypeByPlayerId.get(entry.playerId) !== 'BOT');

    if (filteredEntries.length === 0) {
      return [];
    }

    const updates = filteredEntries.map((entry) =>
      tx.playerItem.update({
        where: {
          playerId_itemId_seq: {
            playerId: entry.playerId,
            itemId: entry.itemId,
            seq: entry.seq,
          },
        },
        data: {
          damage: { increment: entry.damage },
        },
      })
    );

    return Promise.all(updates);
  });
};

const getRarityRank = (rarityGid: number): number => {
  switch (rarityGid) {
    case 11300001:
      return 1;
    case 11300002:
      return 2;
    case 11300003:
      return 3;
    case 11300004:
      return 4;
    case 11300005:
      return 5;
    default:
      return 1;
  }
};

const calculateBallDismantleShardReward = (level: number, rarityGid: number): number => {
  const levelValue = Math.max(level, 1);
  const rarityRank = getRarityRank(rarityGid);
  const baseValue = 4 + levelValue * levelValue * 0.5;
  return Math.max(1, Math.round(baseValue * rarityRank));
};

const calculateBallRepairShardCost = (level: number, rarityGid: number): number => {
  const levelValue = Math.max(level, 1);
  const rarityRank = getRarityRank(rarityGid);
  const baseValue = 3 + levelValue * levelValue * 0.35;
  return Math.max(1, Math.round(baseValue * rarityRank * 0.5));
};

export const dismantleBall = async (playerId: number, itemId: number, seq: number) => {
  return prisma.$transaction(async (tx) => {
    const playerItem = await tx.playerItem.findUnique({
      where: {
        playerId_itemId_seq: {
          playerId,
          itemId,
          seq,
        },
      },
      include: { item: true },
    });

    if (!playerItem) {
      throw new Error('PlayerItem not found');
    }

    if (playerItem.item.typeGid !== 1) {
      throw new Error('Item is not a ball');
    }

    if (playerItem.IsSolded === 1) {
      throw new Error('Item is currently on sale');
    }

    const reward = calculateBallDismantleShardReward(playerItem.level, playerItem.item.rarityGid);

    await tx.playerItem.delete({
      where: {
        playerId_itemId_seq: {
          playerId,
          itemId,
          seq,
        },
      },
    });

    await tx.player.update({
      where: { id: playerId },
      data: {
        GlassShard: { increment: reward },
      },
    });

    return reward;
  });
};

export const repairBall = async (playerId: number, itemId: number, seq: number) => {
  return prisma.$transaction(async (tx) => {
    const playerItem = await tx.playerItem.findUnique({
      where: {
        playerId_itemId_seq: {
          playerId,
          itemId,
          seq,
        },
      },
      include: { item: true },
    });

    if (!playerItem) {
      throw new Error('PlayerItem not found');
    }

    if (playerItem.item.typeGid !== 1) {
      throw new Error('Item is not a ball');
    }

    if (Number(playerItem.damage ?? 0) <= 0) {
      throw new Error('Item is not damaged');
    }

    const player = await tx.player.findFirst({
      where: { id: playerId, IsActive: true },
      select: { GlassShard: true },
    });

    if (!player) {
      throw new Error('Player not found or inactive');
    }

    const cost = calculateBallRepairShardCost(playerItem.level, playerItem.item.rarityGid);
    if ((player.GlassShard ?? 0) < cost) {
      throw new Error('Not enough glass shards');
    }

    await tx.playerItem.update({
      where: {
        playerId_itemId_seq: {
          playerId,
          itemId,
          seq,
        },
      },
      data: {
        damage: 0,
      },
    });

    await tx.player.update({
      where: { id: playerId },
      data: {
        GlassShard: { decrement: cost },
      },
    });

    return cost;
  });
};
