import { Request, Response } from 'express';
import {
  getHistories as getHistoriesService,
  getHistoryStatsByPlayer,
  getRankLeaderboard,
  getHistoriesByTransno,
} from '../services/historyService';

export const getHistories = async (req: Request, res: Response) => {
  try {
    const page = parseInt(req.query.page as string) || 1;
    const pageSize = parseInt(req.query.pageSize as string) || 10;
    const playerIdParam = req.query.playerId as string | undefined;
    const playerId = playerIdParam ? parseInt(playerIdParam, 10) : undefined;

    if (playerIdParam !== undefined && Number.isNaN(playerId)) {
      res.status(400).json({ message: 'playerId must be a number' });
      return;
    }

    const skip = (page - 1) * pageSize;
    const histories = await getHistoriesService(skip, pageSize, playerId);
    const serializedHistories = histories.map((history) => ({
      ...history,
      transno: history.transno.toString(),
    }));
    res.json(serializedHistories);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getHistoryStats = async (req: Request, res: Response) => {
  try {
    const playerId = parseInt(req.params.playerId, 10);

    if (Number.isNaN(playerId)) {
      res.status(400).json({ message: 'playerId must be a number' });
      return;
    }

    const stats = await getHistoryStatsByPlayer(playerId);
    res.json(stats);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getHistoryLeaderboard = async (req: Request, res: Response) => {
  try {
    const playerIdParam = req.query.playerId as string | undefined;
    const playerId = playerIdParam ? parseInt(playerIdParam, 10) : undefined;

    if (playerIdParam !== undefined && Number.isNaN(playerId)) {
      res.status(400).json({ message: 'playerId must be a number' });
      return;
    }

    const leaderboard = await getRankLeaderboard(100, playerId);
    res.json(leaderboard);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getHistoriesByTransnoController = async (req: Request, res: Response) => {
  try {
    const { transNo: transnoParam } = req.params;

    if (!transnoParam) {
      res.status(400).json({ message: 'transNo is required' });
      return;
    }

    let transno: bigint;

    try {
      transno = BigInt(transnoParam);
    } catch {
      res.status(400).json({ message: 'transNo must be a valid number' });
      return;
    }

    const histories = await getHistoriesByTransno(transno);
    const serializedHistories = histories.map((history) => ({
      ...history,
      transno: history.transno.toString(),
    }));

    res.json(serializedHistories);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};
