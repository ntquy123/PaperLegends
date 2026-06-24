import { Request, Response } from 'express';
import { buyItem, sellItem, updatePlayerItemDamage, dismantleBall, repairBall } from '../services/playerItemService';
import { getInventoryByPlayer } from '../services/itemService';

export const buyItemController = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);
    const itemId = Number(req.body.itemId);
    const currencyType = Number(req.body.currencyType ?? 1);

    if (isNaN(playerId) || isNaN(itemId) || ![1, 2].includes(currencyType)) {
      res.status(400).json({ message: 'Invalid playerId, itemId, or currencyType' });
      return;
    }

    const result = await buyItem(playerId, itemId, currencyType);
    res.json(result);
  } catch (error: any) {
    const message = String(error?.message ?? 'Unknown error');
    if (
      message.startsWith('Daily purchase limit reached') ||
      message.startsWith('Not enough ') ||
      message.startsWith('Invalid currency type') ||
      message.startsWith('Item is not purchasable') ||
      message.startsWith('Item is only purchasable')
    ) {
      res.status(400).json({ message });
      return;
    }

    res.status(500).json({ message });
  }
};

export const sellItemController = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);
    const itemId = Number(req.body.itemId);
    const seq = Number(req.body.seq);

    if (isNaN(playerId) || isNaN(itemId) || isNaN(seq)) {
      res.status(400).json({ message: 'Invalid parameters' });
      return;
    }

    await sellItem(playerId, itemId, seq);
    res.json({ message: 'Item sold' });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const updatePlayerItemDamageController = async (req: Request, res: Response) => {
  try {
    const entries = req.body;
    if (
      !Array.isArray(entries) ||
      entries.length === 0 ||
      entries.some(
        (entry) =>
          typeof entry.playerId !== 'number' ||
          typeof entry.itemId !== 'number' ||
          typeof entry.seq !== 'number' ||
          typeof entry.damage !== 'number' ||
          entry.damage < 0
      )
    ) {
      res.status(400).json({ message: 'Invalid damage update payload' });
      return;
    }

    const result = await updatePlayerItemDamage(entries);
    res.json({ updated: result.length });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const dismantleBallController = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);
    const itemId = Number(req.body.itemId);
    const seq = Number(req.body.seq);

    if (isNaN(playerId) || isNaN(itemId) || isNaN(seq)) {
      res.status(400).json({ message: 'Invalid parameters' });
      return;
    }

    await dismantleBall(playerId, itemId, seq);
    const inventory = await getInventoryByPlayer(playerId);
    res.json(inventory);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const repairBallController = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);
    const itemId = Number(req.body.itemId);
    const seq = Number(req.body.seq);

    if (isNaN(playerId) || isNaN(itemId) || isNaN(seq)) {
      res.status(400).json({ message: 'Invalid parameters' });
      return;
    }

    await repairBall(playerId, itemId, seq);
    const inventory = await getInventoryByPlayer(playerId);
    res.json(inventory);
  } catch (error: any) {
    const message = String(error?.message ?? 'Unknown error');
    if (
      message === 'PlayerItem not found' ||
      message === 'Item is not a ball' ||
      message === 'Item is not damaged' ||
      message === 'Player not found or inactive' ||
      message === 'Not enough glass shards'
    ) {
      res.status(400).json({ message });
      return;
    }

    res.status(500).json({ message });
  }
};
