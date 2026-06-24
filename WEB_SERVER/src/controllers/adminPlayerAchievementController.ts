import { Request, Response } from 'express';
import {
  createPlayerAchievement as createPlayerAchievementService,
  deletePlayerAchievement as deletePlayerAchievementService,
  getAllPlayerAchievements,
  updatePlayerAchievement as updatePlayerAchievementService,
} from '../services/adminPlayerAchievementService';

const parseNumber = (value: any, field: string, allowNull = false) => {
  if (value === undefined || value === null || value === '') {
    return allowNull ? null : ({ error: `${field} không được để trống.` } as const);
  }

  const parsed = Number(value);
  if (Number.isNaN(parsed)) {
    return { error: `${field} phải là số.` } as const;
  }

  return parsed;
};

const parseBoolean = (value: any, field: string) => {
  if (value === undefined || value === null || value === '') {
    return { error: `${field} không được để trống.` } as const;
  }

  if (value === true || value === false) return value;
  if (value === 'true' || value === '1' || value === 1) return true;
  if (value === 'false' || value === '0' || value === 0) return false;

  return { error: `${field} phải là true/false.` } as const;
};

const parseDate = (value: any) => {
  if (value === undefined || value === null || value === '') return null;
  const parsed = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(parsed.valueOf())) {
    return { error: 'AchievedAt không hợp lệ.' } as const;
  }
  return parsed;
};

const parsePlayerAchievementPayload = (req: Request) => {
  const rewardType = String(req.body?.rewardType ?? '').trim();
  if (!rewardType) return { error: 'rewardType không được để trống.' } as const;

  const seq = parseNumber(req.body?.seq, 'Seq');
  if (typeof seq === 'object' && 'error' in seq) return seq;

  const locationId = parseNumber(req.body?.locationId, 'LocationId', true);
  if (typeof locationId === 'object' && 'error' in locationId) return locationId;

  const rewardAmount = parseNumber(req.body?.rewardAmount, 'RewardAmount', true);
  if (typeof rewardAmount === 'object' && 'error' in rewardAmount) return rewardAmount;

  const itemId = parseNumber(req.body?.itemId, 'ItemId', true);
  if (typeof itemId === 'object' && 'error' in itemId) return itemId;

  const countGif = parseNumber(req.body?.countGif, 'CountGif', true);
  if (typeof countGif === 'object' && 'error' in countGif) return countGif;

  const isUsed = parseBoolean(req.body?.isUsed, 'isUsed');
  if (typeof isUsed === 'object' && 'error' in isUsed) return isUsed;

  const achievedAt = parseDate(req.body?.achievedAt);
  if (typeof achievedAt === 'object' && 'error' in achievedAt) return achievedAt;

  return {
    rewardType,
    seq,
    locationId,
    rewardAmount,
    itemId,
    countGif,
    isUsed,
    achievedAt,
  } as const;
};

export const getPlayerAchievements = async (_req: Request, res: Response): Promise<void> => {
  try {
    const achievements = await getAllPlayerAchievements();
    res.json(achievements);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const createPlayerAchievement = async (req: Request, res: Response): Promise<void> => {
  const payload = parsePlayerAchievementPayload(req);
  if ('error' in payload) {
    res.status(400).json({ message: payload.error });
    return;
  }

  try {
    const achievement = await createPlayerAchievementService(payload);
    res.status(201).json({ message: 'Thêm PlayerAchievement thành công.', achievement });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const updatePlayerAchievement = async (req: Request, res: Response): Promise<void> => {
  const rewardType = String(req.params.rewardType ?? '').trim();
  const seq = Number(req.params.seq);

  if (!rewardType || !Number.isInteger(seq)) {
    res.status(400).json({ message: 'Thiếu rewardType hoặc seq cần cập nhật.' });
    return;
  }

  const payload = parsePlayerAchievementPayload(req);
  if ('error' in payload) {
    res.status(400).json({ message: payload.error });
    return;
  }

  try {
    const achievement = await updatePlayerAchievementService(rewardType, seq, {
      locationId: payload.locationId,
      rewardAmount: payload.rewardAmount,
      itemId: payload.itemId,
      countGif: payload.countGif,
      isUsed: payload.isUsed,
      achievedAt: payload.achievedAt,
    });
    res.json({ message: 'Cập nhật PlayerAchievement thành công.', achievement });
  } catch (error: any) {
    if (error?.code === 'P2025') {
      res.status(404).json({ message: 'Không tìm thấy PlayerAchievement cần cập nhật.' });
      return;
    }
    res.status(500).json({ message: error.message });
  }
};

export const deletePlayerAchievement = async (req: Request, res: Response): Promise<void> => {
  const rewardType = String(req.params.rewardType ?? '').trim();
  const seq = Number(req.params.seq);

  if (!rewardType || !Number.isInteger(seq)) {
    res.status(400).json({ message: 'Thiếu rewardType hoặc seq cần xóa.' });
    return;
  }

  try {
    await deletePlayerAchievementService(rewardType, seq);
    res.json({ message: 'Xóa PlayerAchievement thành công.' });
  } catch (error: any) {
    if (error?.code === 'P2025') {
      res.status(404).json({ message: 'Không tìm thấy PlayerAchievement cần xóa.' });
      return;
    }
    res.status(500).json({ message: error.message });
  }
};
