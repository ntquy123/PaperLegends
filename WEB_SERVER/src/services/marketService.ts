import prisma from '../models/prismaClient';
import { Prisma } from '@prisma/client';
import { emitMarketUpdate } from './adminRealtime';

export interface ItemPriceOverview {
  minPrice: number;
  maxPrice: number;
  suggestedPrice: number;
}

export const listItemForSale = async (
  playerId: number,
  itemId: number,
  seq: number,
  price: number
) => {
  const playerItem = await prisma.playerItem.findUnique({
    where: { playerId_itemId_seq: { playerId, itemId, seq } },
    select: { IsSolded: true }
  });

  if (!playerItem) {
    throw new Error('PlayerItem not found');
  }

  if (playerItem.IsSolded === 2) {
    throw new Error('Item already sold');
  }

  const result = await prisma.playerItem.update({
    where: { playerId_itemId_seq: { playerId, itemId, seq } },
    data: { IsSolded: 1, Price: price }
  });
  emitMarketUpdate();
  return result;
};

export const buyMarketItem = async (
  buyerId: number,
  sellerId: number,
  itemId: number,
  seq: number
) => {
  return prisma.$transaction(async (tx) => {
    const item = await tx.playerItem.findUnique({
      where: { playerId_itemId_seq: { playerId: sellerId, itemId, seq } },
      select: { IsSolded: true, Price: true, SkillGid: true }
    });

    if (!item || item.IsSolded !== 1) {
      throw new Error('Item not available');
    }

    const buyer = await tx.player.findFirst({
      where: { id: buyerId, IsActive: true },
      select: { RingBall: true, PlayerName: true }
    });

    if (!buyer) {
      throw new Error('Buyer not found or inactive');
    }

    const price = item.Price ?? 0;
    if ((buyer.RingBall ?? 0) < price) {
      throw new Error('Not enough RingBall');
    }

    const seller = await tx.player.findFirst({
      where: { id: sellerId, IsActive: true },
      select: { id: true }
    });

    if (!seller) {
      throw new Error('Seller not found or inactive');
    }

    await tx.player.update({
      where: { id: buyerId },
      data: { RingBall: { decrement: price } }
    });

    const lastSystemMessage = await tx.friendMessage.findFirst({
      where: { senderId: 0 },
      orderBy: { seqMess: 'desc' },
      select: { seqMess: true }
    });

    const systemSeq = (lastSystemMessage?.seqMess ?? 0) + 1;
    const buyerName = buyer.PlayerName ?? '';

    await tx.friendMessage.create({
      data: {
        senderId: 0,
        receiverId: sellerId,
        seqMess: systemSeq,
        message: `[${buyerName}] (purchased)`,
        ringBallReward: price,
        status: 'PENDING'
      }
    });

    await tx.playerItem.update({
      where: { playerId_itemId_seq: { playerId: sellerId, itemId, seq } },
      data: { IsSolded: 2 }
    });

    const lastSeq = await tx.playerItem.findFirst({
      where: { playerId: buyerId, itemId },
      orderBy: { seq: 'desc' },
      select: { seq: true }
    });
    const newSeq = lastSeq ? lastSeq.seq + 1 : 0;

    await tx.playerItem.create({
      data: {
        playerId: buyerId,
        itemId,
        seq: newSeq,
        level: 1,
        SkillGid: item.SkillGid ?? null,
        description: '',
        Price: 0,
        IsSolded: 0
      }
    });

    await tx.itemTradeHistory.create({
      data: {
        playerIdBuy: buyerId,
        playerIdSold: sellerId,
        itemId,
        seq,
        price,
        quantity: 1
      }
    });

    emitMarketUpdate();
    return { newSeq };
  });
};

export const cancelSale = async (
  playerId: number,
  itemId: number,
  seq: number
) => {
  const item = await prisma.playerItem.findUnique({
    where: { playerId_itemId_seq: { playerId, itemId, seq } },
    select: { IsSolded: true }
  });

  if (!item) {
    throw new Error('PlayerItem not found');
  }

  if (item.IsSolded === 2) {
    throw new Error('item đã bán rồi');
  }

  const result = await prisma.playerItem.update({
    where: { playerId_itemId_seq: { playerId, itemId, seq } },
    data: { IsSolded: 0, Price: 0 }
  });
  emitMarketUpdate();
  return result;
};

export const getAllListedItems = async () => {
  return prisma.playerItem.findMany({
    where: { IsSolded: 1 },
    include: {
      activeSkill: {
        select: { GenCode: true, GenName: true, description: true },
      },
      item: {
        include: {
          activeSkill: {
            select: { GenCode: true, GenName: true, description: true },
          },
        },
      },
    }
  }).then((rows) =>
    rows.map((row) => ({
      ...row,
      item: {
        ...row.item,
        SkillGid: row.SkillGid ?? null,
        activeSkill: row.activeSkill ?? null,
      },
    }))
  );
};

export interface ListedItemFilters {
  itemName?: string;
  levelFrom?: number;
  levelTo?: number;
  rarityGids?: number[];
  skip?: number;
  take?: number;
}

export type ListedItemResult = Prisma.PlayerItemGetPayload<{
  include: {
    activeSkill: {
      select: { GenCode: true; GenName: true; description: true };
    };
    item: {
      include: {
        activeSkill: {
          select: { GenCode: true; GenName: true; description: true };
        };
      };
    };
    player: { select: { PlayerName: true } };
  };
}>;

export const getListedItems = async ({
  itemName,
  levelFrom,
  levelTo,
  rarityGids,
  skip = 0,
  take = 10
}: ListedItemFilters): Promise<ListedItemResult[]> => {
  const where: any = { IsSolded: 1 };

  if (levelFrom !== undefined || levelTo !== undefined) {
    where.level = {};
    if (levelFrom !== undefined && !Number.isNaN(levelFrom)) {
      where.level.gte = levelFrom;
    }
    if (levelTo !== undefined && !Number.isNaN(levelTo)) {
      where.level.lte = levelTo;
    }

    if (Object.keys(where.level).length === 0) {
      delete where.level;
    }
  }

  if (itemName) {
    where.item = { name: { contains: itemName, mode: 'insensitive' } };
  }

  if (Array.isArray(rarityGids) && rarityGids.length > 0) {
    where.item = {
      ...(where.item ?? {}),
      rarityGid: { in: rarityGids },
    };
  }

  return prisma.playerItem.findMany({
    where,
    include: {
      activeSkill: {
        select: { GenCode: true, GenName: true, description: true },
      },
      item: {
        include: {
          activeSkill: {
            select: { GenCode: true, GenName: true, description: true },
          },
        },
      },
      player: { select: { PlayerName: true } },
    },
    skip,
    take,
  }).then((rows) =>
    rows.map((row) => ({
      ...row,
      item: {
        ...row.item,
        SkillGid: row.SkillGid ?? null,
        activeSkill: row.activeSkill ?? null,
      },
    }))
  );
};

export const getItemPriceOverview = async (
  itemId: number,
  level?: number
): Promise<ItemPriceOverview> => {
  const where: Prisma.PlayerItemWhereInput = {
    itemId,
    IsSolded: 1,
    Price: { gt: 0 },
  };

  if (level !== undefined && !Number.isNaN(level)) {
    where.level = level;
  }

  const aggregate = await prisma.playerItem.aggregate({
    where,
    _min: { Price: true },
    _max: { Price: true },
  });

  const minPrice = aggregate._min.Price ?? 0;
  const maxPrice = aggregate._max.Price ?? 0;

  let suggestedPrice: number;
  if (minPrice === 0 && maxPrice === 0) {
    const fallbackItem = await prisma.item.findUnique({
      where: { id: itemId },
      select: { price: true },
    });
    suggestedPrice = fallbackItem?.price ?? 0;
  } else {
    suggestedPrice = (minPrice + maxPrice) / 2;
  }

  return { minPrice, maxPrice, suggestedPrice };
};

export const getMarketCatalogItems = async (typeGid: number = 1) => {
  return prisma.item.findMany({
    where: { typeGid },
    orderBy: { id: 'asc' },
    include: {
      activeSkill: {
        select: { GenCode: true, GenName: true, description: true },
      },
    },
  });
};

export const createBuyRequestOrder = async (playerId: number, itemId: number, price: number) => {
  return prisma.$transaction(async (tx) => {
    const lastSeq = await tx.buyRequestOrder.findFirst({
      where: { playerId, itemId },
      orderBy: { seq: 'desc' },
      select: { seq: true },
    });

    const seq = (lastSeq?.seq ?? -1) + 1;

    const result = await tx.buyRequestOrder.create({
      data: { playerId, itemId, seq, price, status: 0 },
    });
    emitMarketUpdate();
    return result;
  });
};

export const getMarketOrderBoard = async (itemId: number) => {
  const sellGrouped = await prisma.playerItem.groupBy({
    by: ['Price'],
    where: { itemId, IsSolded: 1 },
    _count: { Price: true },
    orderBy: { Price: 'asc' },
    take: 3,
  });

  const sellingOrders = sellGrouped.map(g => ({
    price: g.Price ?? 0,
    count: g._count.Price,
  }));

  const buyGrouped = await prisma.buyRequestOrder.groupBy({
    by: ['price'],
    where: { itemId, status: 0 },
    _count: { price: true },
    orderBy: { price: 'desc' },
    take: 3,
  });

  const buyOrders = buyGrouped.map(g => ({
    price: g.price,
    count: g._count.price,
  }));

  return { sellingOrders, buyOrders };
};
