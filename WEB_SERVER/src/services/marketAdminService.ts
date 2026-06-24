import prisma from '../models/prismaClient';

const RANGE_MAP: Record<string, number> = { '24h': 1, '7d': 7, '30d': 30 };

export const getRangeDate = (range: string) => {
  const days = RANGE_MAP[range] ?? 1;
  const from = new Date();
  from.setUTCDate(from.getUTCDate() - days);
  return from;
};

export const getMarketOverview = async (range: string) => {
  const from = getRangeDate(range);
  const [buyOrders, sellOrders, trades] = await Promise.all([
    prisma.buyRequestOrder.count({ where: { createDate: { gte: from } } }),
    prisma.playerItem.count({ where: { IsSolded: 1 } }),
    prisma.itemTradeHistory.aggregate({ where: { createdAt: { gte: from } }, _sum: { quantity: true } }),
  ]);

  const timeline = await prisma.itemTradeHistory.groupBy({
    by: ['createdAt'],
    where: { createdAt: { gte: from } },
    _count: { _all: true },
    orderBy: { createdAt: 'asc' },
  });

  return {
    from,
    buyOrders,
    sellOrders,
    totalVolume: trades._sum.quantity ?? 0,
    buySellRatio: sellOrders > 0 ? Number((buyOrders / sellOrders).toFixed(2)) : 0,
    timeline: timeline.map((x) => ({ time: x.createdAt, value: x._count._all })),
  };
};

export const getTopPriceMovers = async (range: string, limit: number, direction: 'gainers' | 'losers') => {
  const from = getRangeDate(range);
  const rows = await prisma.itemPriceHistory.findMany({
    where: { createdAt: { gte: from } },
    include: { item: { select: { id: true, name: true } } },
    orderBy: { createdAt: 'desc' },
    take: 300,
  });

  const latestByItem = new Map<number, (typeof rows)[number]>();
  for (const row of rows) {
    if (!latestByItem.has(row.itemId)) latestByItem.set(row.itemId, row);
  }

  const list = Array.from(latestByItem.values()).map((x) => ({
    itemId: x.itemId,
    itemName: x.item.name,
    percent: x.changePercent,
    amount: x.newPrice - x.oldPrice,
    oldPrice: x.oldPrice,
    newPrice: x.newPrice,
  }));

  const filtered = direction === 'gainers'
    ? list.filter((x) => x.amount > 0).sort((a, b) => b.percent - a.percent)
    : list.filter((x) => x.amount < 0).sort((a, b) => a.percent - b.percent);

  return filtered.slice(0, limit);
};

