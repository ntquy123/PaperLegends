import { Router } from 'express';
import * as marketController from '../controllers/marketController';

const router = Router();
router.post('/market/sell', marketController.sellOnMarket);
router.post('/market/buy', marketController.buyOnMarket);
router.post('/market/cancel', marketController.cancelSell);
router.get('/market/items', marketController.getListedItems);
router.get('/market/item-price-overview', marketController.getItemPriceOverview);
router.get('/market/catalog', marketController.getMarketCatalog);
router.post('/market/buy-request', marketController.placeBuyRequestOrder);
router.get('/market/order-board', marketController.getMarketOrderBoardByItem);
export default router;
