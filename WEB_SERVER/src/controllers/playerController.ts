// src/controllers/playerController.ts
import { Request, Response, RequestHandler } from 'express';
import {
  getPlayerByAccountId,
  getPlayerByListId,
  equipPlayerItem,
  unequipPlayerItem,
  getBotPlayers,
  completePlayerTutorial,
} from '../services/playerService';
import { getInventoryByPlayer } from '../services/itemService';
import {
  countPendingFriendMessages,
  countPendingFriendRequests,
} from '../services/friendService';

export const getPlayerController = async (req: Request, res: Response) => {
  try {
    const playerId = req.params.id;
    const player = await getPlayerByAccountId(playerId);
    if (player) {
      res.json(player);
    } else {
      res.status(404).json({ message: 'Player not found' });
    }
  } catch (error : any) {
    res.status(500).json({ message: error.message });
  }
};

 
 
export const getPlayerByIdsController = async (req: Request, res: Response) => {
  try {
    let ids = req.body.ids;
    const players = await getPlayerByListId(ids);
    res.json({ players });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getBotPlayersController = async (req: Request, res: Response) => {
  try {
    const count = Math.max(1, Math.min(10, Number(req.query.count) || 1));
    const bots = await getBotPlayers(count);
    res.json({ players: bots });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const completeTutorialController: RequestHandler = async (
  req,
  res
): Promise<void> => {
  try {
    const playerId = Number(req.params.id);
    if (!Number.isInteger(playerId) || playerId <= 0) {
      res.status(400).json({ message: 'Invalid player id' });
      return;
    }

    const player = await completePlayerTutorial(playerId);
    res.json(player);
    return;
  } catch (error: any) {
    const status = error.message === 'Player not found or inactive' ? 404 : 500;
    res.status(status).json({ message: error.message });
    return;
  }
};

export const getInventoryController: RequestHandler = async (
  req,
  res
): Promise<void> => {
  try {
    const id = Number(req.params.id);
    if (isNaN(id)) {
      res.status(400).json({ message: 'Invalid player id' });
      return;
    }
    const [inventory, newmessage, newreqfriends] = await Promise.all([
      getInventoryByPlayer(id),
      countPendingFriendMessages(id),
      countPendingFriendRequests(id),
    ]);

    if (!inventory) {
      res.status(404).json({
        message: 'Inventory not found',
        newmessage,
        newreqfriends,
      });
      return;
    }

    res.json({
      ...inventory,
      newmessage,
      newreqfriends,
    });
    return;
  } catch (error: any) {
    res.status(500).json({ message: error.message });
    return;
  }
};

export const equipItemController: RequestHandler = async (req, res): Promise<void> => {
  try {
    const playerId = Number(req.body.playerId);
    const locationId = Number(req.body.locationId);
    const itemId = Number(req.body.itemId);
    const seqItem = Number(req.body.seqItem);

    if (isNaN(playerId) || isNaN(locationId) || isNaN(itemId) || isNaN(seqItem)) {
      res.status(400).json({ message: 'Invalid parameters' });
      return;
    }

    const result = await equipPlayerItem(playerId, locationId, itemId, seqItem);
    res.json(result);
    return;
  } catch (error: any) {
    res.status(500).json({ message: error.message });
    return;
  }
};

export const unequipItemController: RequestHandler = async (
  req,
  res
): Promise<void> => {
  try {
    const playerId = Number(req.body.playerId);
    const locationId = Number(req.body.locationId);

    if (isNaN(playerId) || isNaN(locationId)) {
      res.status(400).json({ message: 'Invalid parameters' });
      return;
    }

    const result = await unequipPlayerItem(playerId, locationId);
    res.json(result);
    return;
  } catch (error: any) {
    res.status(500).json({ message: error.message });
    return;
  }
};
