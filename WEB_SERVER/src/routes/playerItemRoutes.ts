import { Router } from 'express';
import {
  buyItemController,
  sellItemController,
  updatePlayerItemDamageController,
  dismantleBallController,
  repairBallController,
} from '../controllers/playerItemController';
import { getInventoryController } from '../controllers/playerController';

const router = Router();

router.get('/players/:id/inventory', getInventoryController);
router.post('/player-item/buy', buyItemController);
router.post('/player-item/sell', sellItemController);
router.post('/player-item/damage', updatePlayerItemDamageController);
router.post('/player-item/dismantle', dismantleBallController);
router.post('/player-item/repair', repairBallController);

export default router;
