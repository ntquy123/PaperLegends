import { Request, Response } from 'express';
import { drawReward, luckyDrawAfterMatch } from '../services/drawService';

export const drawRewardController = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);
    if (isNaN(playerId)) {
      res.status(400).json({ message: 'Invalid playerId' });
      return;
    }
    const result = await drawReward(playerId);
    res.json(result);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const luckyDrawController = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);

    if (isNaN(playerId)) {
      res.status(400).json({ message: 'Invalid playerId' });
      return;
    }

    const result = await luckyDrawAfterMatch(playerId);
    res.json(result);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};
