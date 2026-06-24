import { Router } from 'express';
import {
  getHistories,
  getHistoryLeaderboard,
  getHistoryStats,
  getHistoriesByTransnoController,
} from '../controllers/historyController';

const router = Router();

router.get('/histories/:playerId/stats', getHistoryStats);
// Example query: /histories?playerId=1&page=1&pageSize=10
router.get('/histories', getHistories);
router.get('/histories/leaderboard', getHistoryLeaderboard);
router.get('/histories/transno/:transNo', getHistoriesByTransnoController);

export default router;
