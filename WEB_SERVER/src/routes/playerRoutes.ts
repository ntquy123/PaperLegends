// src/routes/playerRoutes.ts
import { Router } from 'express';
import * as PlayerController from '../controllers/playerController';
const router = Router();

router.get('/player/bots', PlayerController.getBotPlayersController);
router.get('/player/:id', PlayerController.getPlayerController);
router.post('/player/by-list-id', PlayerController.getPlayerByIdsController);
router.post('/player/equip', PlayerController.equipItemController);
router.post('/player/unequip', PlayerController.unequipItemController);
router.patch('/player/:id/tutorial-complete', PlayerController.completeTutorialController);
 

// router.post('/player/:id/experience', updateExperienceController);
// router.post('/player/:id/level-up', levelUpController);

export default router;
