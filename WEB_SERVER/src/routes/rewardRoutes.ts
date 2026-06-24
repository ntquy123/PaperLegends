import { Router } from 'express';
import {
  getRewards,
  getPlayerAchievements,
  refreshRewards,
  claimReward,
  confirmAdWatch,
  insertPlayerAchievement,
} from '../controllers/rewardController';

const router = Router();
//for case Ads
router.get('/rewards/player-achievements', getPlayerAchievements);
router.post('/rewards/confirm-ad', confirmAdWatch);

//for case normal rewards
router.get('/rewards', getRewards);
router.post('/rewards/refresh', refreshRewards);
router.post('/rewards/claim', claimReward);
router.post('/rewards/insert', insertPlayerAchievement);

export default router;
