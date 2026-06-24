import { Request, Response } from 'express';
import {
  listPlayerAchievements,
  claimAchievement,
} from '../services/achievementService';

export const getPlayerAchievements = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.query.playerId);
    const achievements = await listPlayerAchievements(playerId);
    res.json(achievements);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const claimAchievementReward = async (req: Request, res: Response) => {
  try {
    const { playerId, typeGid, achievementId } = req.body || {};

    if (
      typeof playerId !== 'number' ||
      typeof typeGid !== 'string' ||
      typeof achievementId !== 'number'
    ) {
      res
        .status(400)
        .json({ message: 'playerId, typeGid, achievementId are required' });
      return;
    }

    const result = await claimAchievement(playerId, typeGid, achievementId);

    if (!result) {
      res.status(404).json({ message: 'Achievement not found or already claimed' });
      return;
    }

    res.json(result);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};
