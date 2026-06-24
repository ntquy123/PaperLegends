import { Router } from 'express';
import { deductBetOnGameStart, overGame } from '../controllers/gameController';

const router = Router();

router.post('/game-start/bets', deductBetOnGameStart);
router.post('/over-game', overGame);

export default router;
