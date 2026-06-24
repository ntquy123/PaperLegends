import { Request, Response } from 'express';
import {
  listRewards,
  listRewardPlayerAchievements,
  refreshRewards as refreshRewardsService,
  claimReward as claimRewardService,
  confirmAdWatch as confirmAdWatchService,
  insertPlayerAchievement as insertPlayerAchievementService,
  RewardClaimError,
} from '../services/rewardService';

export const getRewards = async (req: Request, res: Response) => {
  try {
    const { rewardType, playerId, dayofweek } = req.query;

    if (typeof rewardType !== 'string' || typeof playerId !== 'string') {
      res.status(400).json({ message: 'Missing or invalid rewardType or playerId' });
      return;
    }

    const playerIdNum = Number(playerId);
    if (isNaN(playerIdNum)) {
      res.status(400).json({ message: 'Invalid playerId' });
      return;
    }

    let dayOfWeekNum: number | undefined;
    if (dayofweek !== undefined) {
      const dayStr = Array.isArray(dayofweek) ? dayofweek[0] : dayofweek;
      dayOfWeekNum = Number(dayStr);
      if (isNaN(dayOfWeekNum)) {
        res.status(400).json({ message: 'Invalid dayofweek' });
        return;
      }
    }

    const rewards = await listRewards(rewardType, playerIdNum, dayOfWeekNum);
    res.json(rewards);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getPlayerAchievements = async (req: Request, res: Response) => {
  try {
    const playerIdParam = Array.isArray(req.query.playerId)
      ? req.query.playerId[0]
      : req.query.playerId;
    const rewardTypeParam = Array.isArray(req.query.rewardType)
      ? req.query.rewardType[0]
      : req.query.rewardType;

    if (typeof rewardTypeParam !== 'string' || typeof playerIdParam !== 'string') {
      res
        .status(400)
        .json({ message: 'Missing or invalid rewardType or playerId' });
      return;
    }

    const playerId = Number(playerIdParam);
    if (Number.isNaN(playerId)) {
      res.status(400).json({ message: 'Invalid playerId' });
      return;
    }

    const achievements = await listRewardPlayerAchievements(playerId, rewardTypeParam);
    res.json(achievements);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const refreshRewards = async (req: Request, res: Response) => {
  try {
    const playerIdParam = (req.query.playerId ?? req.body.playerId) as string | undefined;
    if (playerIdParam === undefined) {
      res.status(400).json({ message: 'Missing playerId' });
      return;
    }

    const playerIdNum = Number(playerIdParam);
    if (isNaN(playerIdNum)) {
      res.status(400).json({ message: 'Invalid playerId' });
      return;
    }

    const rewardTypeParam = (req.query.rewardType ?? req.body.rewardType) as
      | string
      | undefined;
    const rewardTypeValue =
      typeof rewardTypeParam === 'string' ? rewardTypeParam : undefined;

    const rewards = await refreshRewardsService(
      playerIdNum,
      rewardTypeValue,
    );
    res.json(rewards);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const claimReward = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);
    const locationId = Number(req.body.locationId);
    const rewardType = req.body.rewardType;

    if (
      isNaN(playerId) ||
      isNaN(locationId) ||
      typeof rewardType !== 'string'
    ) {
      res.status(400).json({ message: 'Missing or invalid parameters' });
      return;
    }

    const achievement = await claimRewardService(
      playerId,
      locationId,
      rewardType,
    );

    if (!achievement) {
      res.status(404).json({ message: 'No reward found' });
      return;
    }

    res.json(achievement);
  } catch (error: any) {
    if (error instanceof RewardClaimError) {
      res.status(error.statusCode).json({ message: error.message });
      return;
    }
    res.status(500).json({ message: error.message });
  }
};

export const confirmAdWatch = async (req: Request, res: Response) => {
  try {
    const { playerId, rewardType, achievementId, locationId } = req.body ?? {};

    const playerIdNum = Number(playerId);
    if (Number.isNaN(playerIdNum)) {
      res.status(400).json({ message: 'Missing or invalid parameters' });
      return;
    }

    if (typeof rewardType !== 'string') {
      res.status(400).json({ message: 'Missing or invalid parameters' });
      return;
    }

    const rawAchievementId = achievementId ?? locationId;
    const achievementIdNum = Number(rawAchievementId);
    if (rawAchievementId === undefined || Number.isNaN(achievementIdNum)) {
      res.status(400).json({ message: 'Missing or invalid parameters' });
      return;
    }

    const reward = await confirmAdWatchService(
      playerIdNum,
      rewardType,
      achievementIdNum,
    );

    if (!reward) {
      res.status(404).json({ message: 'Reward status not found' });
      return;
    }

    res.json(reward);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const insertPlayerAchievement = async (req: Request, res: Response) => {
  try {
    const { playerId, rewardType } = req.body;

    const playerIdNum = Number(playerId);
    if (isNaN(playerIdNum) || typeof rewardType !== 'string') {
      res
        .status(400)
        .json({ message: 'Missing or invalid playerId or rewardType' });
      return;
    }

    const achievements = await insertPlayerAchievementService(
      playerIdNum,
      rewardType,
    );
    res.json(achievements);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};
