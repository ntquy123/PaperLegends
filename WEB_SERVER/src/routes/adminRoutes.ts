import express from 'express';
import path from 'path';
import {
  getActiveContainers,
  getAdminSession,
  getApiErrorLogsController,
  getContainerLogs,
  getMatchmakingQueueOverview,
  getRedisStatus,
  getServerLogsController,
  getRoomOverview,
  getReadmeContent,
  getAdminMarketOverview,
  getAdminMarketTopGainers,
  getAdminMarketTopLosers,
  getAdminItemHistory,
  getAdminRecentMarketEvents,
  loginAdmin,
  shutdownServersAdmin,
  shutdownTestServerController,
  startMonitorServer,
  startRedisServer,
  startServers,
  startTestServer,
  stopContainerController,
  stopMonitorServer,
  stopRedisServer,
} from '../controllers/adminController';
import {
  createLanguage,
  deleteLanguage,
  getLanguages,
  updateLanguage,
} from '../controllers/languageController';
import {
  createGeneral,
  deleteGeneral,
  getGenerals,
  getItemRarityOptions,
  getMatchTypeOptions,
  getRewardTypeOptions,
  updateGeneral,
} from '../controllers/generalController';
import {
  createItem as createAdminItem,
  deleteItem as deleteAdminItem,
  getItems as getAdminItems,
  getItemOptions,
  updateItem as updateAdminItem,
} from '../controllers/adminItemController';
import {
  createPlayerAchievement,
  deletePlayerAchievement,
  getPlayerAchievements,
  updatePlayerAchievement,
} from '../controllers/adminPlayerAchievementController';
import {
  deletePlayer,
  getBalanceHistories,
  getItemTradeHistories,
  getPlayerDetail,
  getPlayerHistories,
  getPlayers,
  sendSystemMessage,
  updatePlayerActiveStatus,
} from '../controllers/adminPlayerController';
import { requireAdminAuth } from '../middleware/adminAuth';

const router = express.Router();

router.post('/admin/login', loginAdmin);
router.get('/admin/session', requireAdminAuth, getAdminSession);
router.get('/admin/start', requireAdminAuth, startServers);
router.get('/admin/test-server/start', requireAdminAuth, startTestServer);
router.post('/admin/shutdown', requireAdminAuth, shutdownServersAdmin);
router.post('/admin/test-server/shutdown', requireAdminAuth, shutdownTestServerController);
router.get('/admin/redis/status', requireAdminAuth, getRedisStatus);
router.post('/admin/redis/start', requireAdminAuth, startRedisServer);
router.post('/admin/redis/stop', requireAdminAuth, stopRedisServer);
router.post('/admin/monitor/start', requireAdminAuth, startMonitorServer);
router.post('/admin/monitor/stop', requireAdminAuth, stopMonitorServer);
router.get('/admin/containers', requireAdminAuth, getActiveContainers);
router.post('/admin/containers/:id/stop', requireAdminAuth, stopContainerController);
router.get('/admin/containers/:id/logs', requireAdminAuth, getContainerLogs);
router.get('/admin/server-logs', requireAdminAuth, getServerLogsController);
router.get('/admin/api-error-logs', requireAdminAuth, getApiErrorLogsController);
router.get('/admin/rooms/overview', requireAdminAuth, getRoomOverview);
router.get('/admin/matchmaking/searching-players', requireAdminAuth, getMatchmakingQueueOverview);
router.get('/admin/readme', requireAdminAuth, getReadmeContent);
router.get('/admin/languages', requireAdminAuth, getLanguages);
router.post('/admin/languages', requireAdminAuth, createLanguage);
router.put('/admin/languages/:code', requireAdminAuth, updateLanguage);
router.delete('/admin/languages/:code', requireAdminAuth, deleteLanguage);
router.get('/admin/generals', requireAdminAuth, getGenerals);
router.post('/admin/generals', requireAdminAuth, createGeneral);
router.put('/admin/generals/:GenCode', requireAdminAuth, updateGeneral);
router.delete('/admin/generals/:GenCode', requireAdminAuth, deleteGeneral);
router.get('/admin/generals/reward-type-options', requireAdminAuth, getRewardTypeOptions);
router.get('/admin/generals/item-rarity-options', requireAdminAuth, getItemRarityOptions);
router.get('/admin/generals/match-type-options', requireAdminAuth, getMatchTypeOptions);
router.get('/admin/items', requireAdminAuth, getAdminItems);
router.get('/admin/items/options', requireAdminAuth, getItemOptions);
router.post('/admin/items', requireAdminAuth, createAdminItem);
router.put('/admin/items/:id', requireAdminAuth, updateAdminItem);
router.delete('/admin/items/:id', requireAdminAuth, deleteAdminItem);
router.get('/admin/player-achievements', requireAdminAuth, getPlayerAchievements);
router.post('/admin/player-achievements', requireAdminAuth, createPlayerAchievement);
router.put('/admin/player-achievements/:rewardType/:seq', requireAdminAuth, updatePlayerAchievement);
router.delete('/admin/player-achievements/:rewardType/:seq', requireAdminAuth, deletePlayerAchievement);
router.get('/admin/players', requireAdminAuth, getPlayers);
router.get('/admin/players/:id', requireAdminAuth, getPlayerDetail);
router.get('/admin/players/:id/histories', requireAdminAuth, getPlayerHistories);
router.get('/admin/players/:id/balance-histories', requireAdminAuth, getBalanceHistories);
router.get('/admin/players/:id/item-trade-histories', requireAdminAuth, getItemTradeHistories);
router.put('/admin/players/:id/active', requireAdminAuth, updatePlayerActiveStatus);
router.delete('/admin/players/:id', requireAdminAuth, deletePlayer);
router.post('/admin/players/messages', requireAdminAuth, sendSystemMessage);
router.get('/admin/market/overview', requireAdminAuth, getAdminMarketOverview);
router.get('/admin/market/top-gainers', requireAdminAuth, getAdminMarketTopGainers);
router.get('/admin/market/top-losers', requireAdminAuth, getAdminMarketTopLosers);
router.get('/admin/market/item/:itemId/history', requireAdminAuth, getAdminItemHistory);
router.get('/admin/market/recent-events', requireAdminAuth, getAdminRecentMarketEvents);

router.get('/', (_req, res) => {
  res.sendFile(path.join(__dirname, '../../public/admin/index.html'));
});

router.get('/admin/config', (_req, res) => {
  res.sendFile(path.join(__dirname, '../../public/admin/config.html'));
});

router.get('/admin/docker', (_req, res) => {
  res.sendFile(path.join(__dirname, '../../public/admin/docker.html'));
});
router.get('/admin/market-dashboard', (_req, res) => {
  res.sendFile(path.join(__dirname, '../../public/admin/market-dashboard.html'));
});

export default router;
