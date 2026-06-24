import { Router } from 'express';
import { drawRewardController, luckyDrawController } from '../controllers/drawController';

const router = Router();

router.post('/draw-reward', drawRewardController);
router.post('/lucky-draw', luckyDrawController);

export default router;
