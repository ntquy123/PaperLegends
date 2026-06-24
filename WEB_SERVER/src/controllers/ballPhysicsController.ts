import { Request, Response } from 'express';
import { getBallPhysicsByPlayers } from '../services/ballPhysicsService';

export const getBallPhysics = async (req: Request, res: Response) => {
  try {
    const ids = req.body.ids as number[];
    if (!Array.isArray(ids) || ids.some((id) => typeof id !== 'number')) {
      res.status(400).json({ message: 'Invalid player ids' });
      return;
    }

    const physics = await getBallPhysicsByPlayers(ids);
    res.json({ physics });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};
