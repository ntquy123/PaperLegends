import prisma from '../models/prismaClient';
import { Item, Player } from '@prisma/client';

type ItemActiveSkillInfo = {
  GenCode: number;
  GenName: string;
  description: string | null;
};

export interface InventoryItem extends Omit<Item, 'level'> {
  seq: number;
  level: number;
  IsSolded: number;
  damage: number;
  activeSkill: ItemActiveSkillInfo | null;
}

export interface EquippedInventoryItem extends InventoryItem {
  locationId: number;
}

export type InventoryByPlayer = Omit<Player, 'playerItems' | 'equipPlayers'> & {
  playerItems: InventoryItem[];
  equippedItems: EquippedInventoryItem[];
};

const getTodayUtcDate = (): Date => {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
};

export const getAllItems = async (locationGid: number, userId?: number) => {
  const items = await prisma.item.findMany({
    where: { locationGid, isOpen: true },
    orderBy: { id: 'asc' },
    include: {
      activeSkill: {
        select: { GenCode: true, GenName: true, description: true },
      },
    },
  });

  if (userId === undefined) {
    return items.map((item) => ({
      ...item,
      dailyPurchaseLimit: Math.max(0, Number((item as any).maxPurchasePerDay ?? 0)),
      dailyPurchasedCount: 0,
      isDailyPurchaseLocked: false,
    }));
  }

  const limitedItems = items.filter(
    (item) => Number((item as any).maxPurchasePerDay ?? 0) > 0
  );

  if (limitedItems.length === 0) {
    return items.map((item) => ({
      ...item,
      dailyPurchaseLimit: 0,
      dailyPurchasedCount: 0,
      isDailyPurchaseLocked: false,
    }));
  }

  const today = getTodayUtcDate();
  const dailyPurchases = await prisma.dailyShopPurchase.findMany({
    where: {
      userId,
      purchaseDate: today,
      itemId: { in: limitedItems.map((item) => item.id) },
    },
    select: {
      itemId: true,
      purchaseCount: true,
    },
  });

  const purchaseMap = new Map<number, number>();
  dailyPurchases.forEach((entry) => purchaseMap.set(entry.itemId, entry.purchaseCount));

  return items.map((item) => {
    const dailyPurchaseLimit = Math.max(0, Number((item as any).maxPurchasePerDay ?? 0));
    const dailyPurchasedCount = purchaseMap.get(item.id) ?? 0;

    return {
      ...item,
      dailyPurchaseLimit,
      dailyPurchasedCount,
      isDailyPurchaseLocked:
        dailyPurchaseLimit > 0 && dailyPurchasedCount >= dailyPurchaseLimit,
    };
  });
};

export const getInventoryByPlayer = async (
  playerId: number
): Promise<InventoryByPlayer | null> => {
  // Lấy thông tin player
  const player = await prisma.player.findFirst({
    where: { id: playerId, IsActive: true },
    include: {
      playerItems: {
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
        },
      },
      equipPlayers: {
        include: {
          item: {
            include: {
              activeSkill: {
                select: { GenCode: true, GenName: true, description: true },
              },
            },
          },
        },
      },
    },
  });

  if (!player) return null;

  // Trả thông tin player kèm danh sách playerItems đơn giản hóa
  const simplifiedItems: InventoryItem[] = player.playerItems.map((pi) => {
    const { level: _level, ...itemWithoutLevel } = pi.item;
    return {
      seq: pi.seq,
      level: pi.level,
      IsSolded: pi.IsSolded,
      damage: Number(pi.damage ?? 0),
      ...itemWithoutLevel,
      SkillGid: pi.SkillGid ?? null,
      activeSkill: pi.activeSkill ?? null,
    };
  });

  // Thông tin các vật phẩm đang trang bị
  const equippedItems: EquippedInventoryItem[] = [];

  // Lấy thông tin trang bị từ bảng EquipPlayer
  for (const equip of player.equipPlayers) {
    const pi = player.playerItems.find(
      (i) => i.itemId === equip.itemId && i.seq === equip.seqItem
    );

    const level = pi?.level ?? 1;
    const { level: _lvl, ...itemWithoutLevel } = equip.item as any;

      equippedItems.push({
        locationId: equip.locationId,
        seq: equip.seqItem,
        level,
        IsSolded: pi?.IsSolded ?? 0,
        damage: Number(pi?.damage ?? 0),
        ...itemWithoutLevel,
        SkillGid: pi?.SkillGid ?? null,
        activeSkill: pi?.activeSkill ?? null,
      });
  }

  // Body vẫn được lưu ở bảng Player
  if (player.Body !== null && player.Body !== undefined) {
    const pi = player.playerItems.find((i) => i.itemId === player.Body);

    if (pi) {
      const { level: _lvl, ...itemWithoutLevel } = pi.item;
      equippedItems.push({
        locationId: 0,
        seq: pi.seq,
        level: pi.level,
        IsSolded: pi.IsSolded,
        damage: Number(pi.damage ?? 0),
        ...itemWithoutLevel,
        SkillGid: pi.SkillGid ?? null,
        activeSkill: pi.activeSkill ?? null,
      });
    } else {
      const item = await prisma.item.findUnique({
        where: { id: player.Body },
        include: {
          activeSkill: {
            select: { GenCode: true, GenName: true, description: true },
          },
        },
      });
      if (item) {
        const { level: _lvl, ...itemWithoutLevel } = item;
        equippedItems.push({
          locationId: 0,
          seq: 0,
          level: 1,
          IsSolded: 0,
          damage: 0,
          ...itemWithoutLevel,
        });
      }
    }
  }

  const equippedIdSeq = new Set(
    equippedItems.map((ei: any) => `${ei.id}-${ei.seq}`)
  );

  const filteredItems = simplifiedItems.filter(
    (it) => !equippedIdSeq.has(`${it.id}-${it.seq}`)
  );

  const { playerItems, equipPlayers, ...rest } = player;
  return { ...rest, playerItems: filteredItems, equippedItems };
};
