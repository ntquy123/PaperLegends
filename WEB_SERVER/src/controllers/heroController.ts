import { Request, Response } from 'express';
import { getHeroSelectionPayload, parseHeroIdsQuery } from '../services/heroService';

const parseIncludeInactive = (value: unknown): boolean =>
  value === true || value === 'true' || value === '1';

const normalizeBodyIds = (body: unknown): string[] => {
  if (!body || typeof body !== 'object') {
    return [];
  }

  const ids = (body as { ids?: unknown }).ids;
  if (!Array.isArray(ids)) {
    return [];
  }

  return ids.map((id) => String(id));
};

export const getHeroes = async (req: Request, res: Response): Promise<void> => {
  try {
    const ids = parseHeroIdsQuery(req.query.ids);
    const includeInactive = parseIncludeInactive(req.query.includeInactive);
    const payload = await getHeroSelectionPayload(ids, includeInactive);

    res.json(payload);
  } catch (error: any) {
    res.status(500).json({ message: error.message ?? 'Failed to get heroes' });
  }
};

export const getSelectedHeroes = async (req: Request, res: Response): Promise<void> => {
  try {
    const ids = normalizeBodyIds(req.body);

    if (ids.length === 0) {
      res.status(400).json({ message: 'ids must be a non-empty array' });
      return;
    }

    const includeInactive = parseIncludeInactive(req.body?.includeInactive);
    const payload = await getHeroSelectionPayload(ids, includeInactive);

    res.json(payload);
  } catch (error: any) {
    res.status(500).json({ message: error.message ?? 'Failed to get selected heroes' });
  }
};
