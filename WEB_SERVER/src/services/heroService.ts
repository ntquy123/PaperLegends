import prisma from '../models/prismaClient';

type HeroIdInput = string | number;

const normalizeHeroIds = (ids?: HeroIdInput[]): string[] => {
  if (!Array.isArray(ids)) {
    return [];
  }

  return Array.from(
    new Set(
      ids
        .map((id) => String(id).trim())
        .filter((id) => id.length > 0)
    )
  );
};

export const parseHeroIdsQuery = (rawIds: unknown): string[] => {
  if (rawIds === undefined) {
    return [];
  }

  if (Array.isArray(rawIds)) {
    return normalizeHeroIds(rawIds.flatMap((entry) => String(entry).split(',')));
  }

  return normalizeHeroIds(String(rawIds).split(','));
};

export const getHeroes = async (ids?: HeroIdInput[], includeInactive = false) => {
  const normalizedIds = normalizeHeroIds(ids);
  const hasIdFilter = normalizedIds.length > 0;

  return prisma.hero.findMany({
    where: {
      ...(includeInactive ? {} : { isActive: true }),
      ...(hasIdFilter
        ? {
            OR: [
              { id: { in: normalizedIds } },
              { code: { in: normalizedIds } },
              { modelId: { in: normalizedIds } },
            ],
          }
        : {}),
    },
    include: {
      skills: {
        where: includeInactive ? {} : { isActive: true },
        orderBy: { slot: 'asc' },
      },
    },
    orderBy: [
      { sortOrder: 'asc' },
      { name: 'asc' },
    ],
  });
};

export const getHeroSelectionPayload = async (ids?: HeroIdInput[], includeInactive = false) => {
  const heroes = await getHeroes(ids, includeInactive);

  return {
    count: heroes.length,
    heroes,
  };
};
