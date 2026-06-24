// src/routes/friendRoutes.ts
import { Router } from 'express';
import {
  sendFriendRequestController,
  removeFriendController,
  respondFriendRequestController,
  sendMessageController,
  readFriendMessageController,
  receiveItemsController,
  getFriendListController,
  getPendingFriendRequestsController,
  getFriendMessagesController,
  getSystemMessagesController,
  getConversationHistoryController,
  deleteFriendMessageController,
  claimSystemMessageRewardController,
  searchPlayerByIdController,
} from '../controllers/friendController';

const router = Router();

router.post('/friend-request', sendFriendRequestController);
router.post('/friend-remove', removeFriendController);
router.post('/friend-respond', respondFriendRequestController);
router.post('/send-message', sendMessageController);
router.post('/read-message', readFriendMessageController);
router.post('/receive-items', receiveItemsController);
router.get('/friend-search/:id', searchPlayerByIdController);
router.get('/friend-list/:playerId', getFriendListController);
router.get('/friend-requests/:receiverId', getPendingFriendRequestsController);
router.get(
  '/messages/conversation/:playerId/:friendId',
  getConversationHistoryController
);
router.get(
  '/messages/system/:receiverId',
  getSystemMessagesController
);
router.post(
  '/messages/system/claim',
  claimSystemMessageRewardController
);
router.get('/messages/:receiverId', getFriendMessagesController);
// DELETE /messages/:playerId/:partnerId
router.delete(
  '/messages/:playerId/:partnerId',
  deleteFriendMessageController
);

export default router;
