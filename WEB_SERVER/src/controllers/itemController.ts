import { Request, Response } from 'express';
import * as ItemService from '../services/itemService';

export const getItems = async (req: Request, res: Response): Promise<void> => {
  try {
    const locationGidParam = Number(req.query.locationGid ?? 2);
    const userIdRaw = req.query.userId;

    if (Number.isNaN(locationGidParam)) {
      res
        .status(400)
        .json({ message: 'locationGid is required and must be a number' });

      return;
    }

    let userId: number | undefined;
    if (userIdRaw !== undefined) {
      const parsedUserId = Number(userIdRaw);
      if (Number.isNaN(parsedUserId)) {
        res.status(400).json({ message: 'userId must be a number' });
        return;
      }

      userId = parsedUserId;
    }

    const items = await ItemService.getAllItems(locationGidParam, userId);
    res.json(items);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};
