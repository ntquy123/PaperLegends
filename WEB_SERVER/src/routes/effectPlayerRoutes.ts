import { Router } from 'express';
import * as EffectPlayerController from '../controllers/effectPlayerController';

const router = Router();

router.get('/effect-player/:playerId', EffectPlayerController.getEffectPlayers);
router.post('/effect-player/level-up', EffectPlayerController.levelUpEffect);
router.post('/effect-player/item-level-up', EffectPlayerController.levelUpItem);
router.post('/effect-player/equip', EffectPlayerController.equipEffect);

export default router;
