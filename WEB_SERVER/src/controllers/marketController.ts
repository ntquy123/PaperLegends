import { Request, Response } from 'express';
import {
  listItemForSale,
  buyMarketItem,
  cancelSale,
  getListedItems as getListedItemsService,
  ListedItemResult,
  getItemPriceOverview as getItemPriceOverviewService,
  getMarketCatalogItems,
  createBuyRequestOrder,
  getMarketOrderBoard
} from '../services/marketService';

export const sellOnMarket = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);
    const itemId = Number(req.body.itemId);
    const seq = Number(req.body.seq);
    const price = Number(req.body.price);

    if ([playerId, itemId, seq, price].some((v) => isNaN(v))) {
      res.status(400).json({ message: 'Invalid parameters' });
      return;
    }

    const result = await listItemForSale(playerId, itemId, seq, price);
    res.json(result);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const buyOnMarket = async (req: Request, res: Response) => {
  try {
    const buyerId = Number(req.body.buyerId);
    const sellerId = Number(req.body.sellerId);
    const itemId = Number(req.body.itemId);
    const seq = Number(req.body.seq);

    if ([buyerId, sellerId, itemId, seq].some((v) => isNaN(v))) {
      res.status(400).json({ message: 'Invalid parameters' });
      return;
    }

    const result = await buyMarketItem(buyerId, sellerId, itemId, seq);
    res.json(result);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const cancelSell = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);
    const itemId = Number(req.body.itemId);
    const seq = Number(req.body.seq);

    if ([playerId, itemId, seq].some((v) => isNaN(v))) {
      res.status(400).json({ message: 'Invalid parameters' });
      return;
    }

    const result = await cancelSale(playerId, itemId, seq);
    res.json(result);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getListedItems = async (req: Request, res: Response) => {
  try {
    const itemName = req.query.itemName as string | undefined;
    const levelFrom = req.query.levelFrom ? Number(req.query.levelFrom) : undefined;
    const levelTo = req.query.levelTo ? Number(req.query.levelTo) : undefined;
    const rarityGids = typeof req.query.rarityGids === 'string'
      ? req.query.rarityGids
          .split(',')
          .map((v) => Number(v.trim()))
          .filter((v) => !Number.isNaN(v))
      : undefined;

    const page = parseInt(req.query.page as string) || 1;
    const pageSize = 10;
    const skip = (page - 1) * pageSize;

    const items = await getListedItemsService({
      itemName,
      levelFrom,
      levelTo,
      rarityGids,
      skip,
      take: pageSize,
    });
    const response: (ListedItemResult & { playerName: string | null })[] = items.map(
      (i) => ({
        ...i,
        playerName: i.player?.PlayerName ?? null,
      })
    );
    res.json(response);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getItemPriceOverview = async (req: Request, res: Response) => {
  try {
    const itemId = Number(req.query.itemId);
    const level = req.query.level ? Number(req.query.level) : undefined;

    if (Number.isNaN(itemId)) {
      res.status(400).json({ message: 'itemId is required and must be a number' });
      return;
    }

    const overview = await getItemPriceOverviewService(itemId, level);
    res.json(overview);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getMarketCatalog = async (req: Request, res: Response) => {
  try {
    const typeGid = req.query.typeGid ? Number(req.query.typeGid) : 1;
    const items = await getMarketCatalogItems(typeGid);
    res.json(items);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const placeBuyRequestOrder = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);
    const itemId = Number(req.body.itemId);
    const price = Number(req.body.price);

    if ([playerId, itemId, price].some((v) => Number.isNaN(v))) {
      res.status(400).json({ message: 'Invalid parameters' });
      return;
    }

    const order = await createBuyRequestOrder(playerId, itemId, price);
    res.json(order);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getMarketOrderBoardByItem = async (req: Request, res: Response) => {
  try {
    const itemId = Number(req.query.itemId);
    if (Number.isNaN(itemId)) {
      res.status(400).json({ message: 'itemId is required and must be a number' });
      return;
    }

    const board = await getMarketOrderBoard(itemId);
    res.json(board);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};
