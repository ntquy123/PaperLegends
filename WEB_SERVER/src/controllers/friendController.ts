import { Request, Response } from 'express';
import {
  sendFriendRequest,
  removeFriend,
  respondFriendRequest,
  sendMessage,
  readMessage,
  deleteFriendMessage,
  receiveItems,
  getFriendList,
  getPendingFriendRequests,
  getFriendMessages,
  getSystemMessages,
  getConversationHistory,
  claimSystemMessageReward,
  searchPlayerById,
} from '../services/friendService';

const parseNumericParam = (...values: unknown[]): number | undefined => {
  for (const value of values) {
    if (value === undefined || value === null) {
      continue;
    }

    const candidate = Array.isArray(value) ? value[0] : value;
    if (candidate === undefined || candidate === null) {
      continue;
    }

    if (typeof candidate === 'string') {
      const trimmed = candidate.trim();
      if (trimmed === '') {
        continue;
      }
      const parsed = Number(trimmed);
      if (!Number.isNaN(parsed)) {
        return parsed;
      }
      continue;
    }

    if (typeof candidate === 'number') {
      if (!Number.isNaN(candidate)) {
        return candidate;
      }
      continue;
    }

    if (typeof candidate === 'bigint') {
      return Number(candidate);
    }
  }

  return undefined;
};

export const searchPlayerByIdController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const raw =
      (req.params.id as string | undefined) ??
      (req.query.search as string | undefined);
    if (raw === undefined) {
      res.status(400).json({ message: 'Invalid id' });
      return;
    }

    const id = Number(raw);
    if (isNaN(id) || String(id) !== raw) {
      res.status(400).json({ message: 'Invalid id' });
      return;
    }

    const result = await searchPlayerById(id);
    if (result.success) {
      res.json(result.data);
      return;
    }
    res.status(404).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const sendFriendRequestController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const senderId = Number(req.body.senderId ?? req.params.senderId);
    const friendCode =
      (req.body.friendCode ?? req.params.friendCode)?.toString();

    if (isNaN(senderId) || !friendCode) {
      res.status(400).json({ message: 'Invalid senderId or friendCode' });
      return;
    }

    const result = await sendFriendRequest(senderId, friendCode);
    if (result.success) {
      res.json(result.data);
      return;
    }
    res.status(400).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const removeFriendController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const playerId = Number(req.body.playerId ?? req.params.playerId);
    const friendId = Number(req.body.friendId ?? req.params.friendId);

    if (isNaN(playerId) || isNaN(friendId)) {
      res.status(400).json({ message: 'Invalid playerId or friendId' });
      return;
    }

    const result = await removeFriend(playerId, friendId);
    if (result.success) {
      res.json({ success: true });
      return;
    }
    res.status(400).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const respondFriendRequestController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const senderId = Number(req.body.senderId ?? req.params.senderId);
    const receiverId = Number(req.body.receiverId ?? req.params.receiverId);
    const acceptParam = req.body.status ?? req.params.status;
    const accept = acceptParam === 1;

    if (isNaN(senderId) || isNaN(receiverId)) {
      res.status(400).json({ message: 'Invalid senderId or receiverId' });
      return;
    }

    const result = await respondFriendRequest(senderId, receiverId, accept);
    if (result.success) {
      res.json(result.data);
      return;
    }
    res.status(400).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getFriendListController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const playerId = Number(req.params.playerId);
    if (isNaN(playerId)) {
      res.status(400).json({ message: 'Invalid playerId' });
      return;
    }
    const result = await getFriendList(playerId);
    if (result.success) {
      res.json(result.data);
      return;
    }
    res.status(400).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getPendingFriendRequestsController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const receiverId = Number(req.params.receiverId);
    if (isNaN(receiverId)) {
      res.status(400).json({ message: 'Invalid receiverId' });
      return;
    }
    const result = await getPendingFriendRequests(receiverId);
    if (result.success) {
      res.json(result.data);
      return;
    }
    res.status(400).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getFriendMessagesController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const receiverId = Number(req.params.receiverId);
    if (isNaN(receiverId)) {
      res.status(400).json({ message: 'Invalid receiverId' });
      return;
    }
    const result = await getFriendMessages(receiverId);
    if (result.success) {
      res.json(result.data);
      return;
    }
    res.status(400).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getSystemMessagesController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const receiverId = Number(req.params.receiverId);
    if (isNaN(receiverId)) {
      res.status(400).json({ message: 'Invalid receiverId' });
      return;
    }
    const result = await getSystemMessages(receiverId);
    if (result.success) {
      res.json(result.data);
      return;
    }
    res.status(400).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getConversationHistoryController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const playerId = Number(req.params.playerId);
    const friendId = Number(req.params.friendId);
    if (isNaN(playerId) || isNaN(friendId)) {
      res
        .status(400)
        .json({ message: 'Invalid playerId or friendId' });
      return;
    }
    const result = await getConversationHistory(playerId, friendId);
    if (result.success) {
      res.json(result.data);
      return;
    }
    res.status(400).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const readFriendMessageController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const playerIdRaw = req.body.playerId ?? req.params.playerId;
    const seqMessRaw = req.body.seqMess ?? req.params.seqMess;

    const playerId = Number(playerIdRaw);
    const seqMess = Number(seqMessRaw);

    if (
      playerIdRaw === undefined ||
      seqMessRaw === undefined ||
      Number.isNaN(playerId) ||
      Number.isNaN(seqMess)
    ) {
      res.status(400).json({ message: 'Invalid playerId or seqMess' });
      return;
    }

    const result = await readMessage(playerId, seqMess);
    if (result.success) {
      res.json({ success: true });
      return;
    }
    res.status(400).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const deleteFriendMessageController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const requesterId = parseNumericParam(
      req.body.requesterId,
      req.query.requesterId,
      req.body.playerId,
      req.query.playerId,
      req.params.playerId
    );
    const partnerId = parseNumericParam(
      req.params.partnerId,
      req.body.partnerId,
      req.query.partnerId
    );
    const routePlayerId = parseNumericParam(req.params.playerId);

    if (requesterId === undefined || partnerId === undefined) {
      res
        .status(400)
        .json({ message: 'Invalid requesterId or partnerId' });
      return;
    }

    const participants = new Set<number>();
    if (routePlayerId !== undefined) {
      participants.add(routePlayerId);
    }
    participants.add(partnerId);

    if (!participants.has(requesterId)) {
      res.status(403).json({ message: 'Forbidden' });
      return;
    }

    const result = await deleteFriendMessage(requesterId, partnerId);
    if (result.success) {
      res.json({ success: true, data: result.message });
      return;
    }
    const status =
      result.message === 'Forbidden'
        ? 403
        : result.message === 'Message not found'
          ? 404
          : 400;
    res.status(status).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const sendMessageController = async (
  req: Request,
  res: Response
): Promise<void> => {
  const senderId = Number(req.body.senderId ?? req.params.senderId);
  const receiverId = Number(req.body.receiverId ?? req.params.receiverId);
  const message = req.body.content;
  const itemId =
    req.body.itemId !== undefined ? Number(req.body.itemId) : undefined;
  const seqId =
    req.body.seqId !== undefined ? Number(req.body.seqId) : undefined;
  const ringBallRewardRaw = req.body.ringBallReward;
  const moneyRewardRaw = req.body.moneyReward;
  const itemRewardIdRaw = req.body.itemRewardId;
  const ringBallReward =
    ringBallRewardRaw !== undefined ? Number(ringBallRewardRaw) : undefined;
  const moneyReward =
    moneyRewardRaw !== undefined ? Number(moneyRewardRaw) : undefined;
  let itemRewardId: number | null | undefined;
  if (itemRewardIdRaw !== undefined) {
    if (itemRewardIdRaw === null) {
      itemRewardId = null;
    } else {
      const parsed = Number(itemRewardIdRaw);
      itemRewardId = parsed;
    }
  }

  if (
    isNaN(senderId) ||
    isNaN(receiverId) ||
    typeof message !== 'string' ||
    (itemId !== undefined && isNaN(itemId)) ||
    (seqId !== undefined && isNaN(seqId)) ||
    (ringBallRewardRaw !== undefined && isNaN(ringBallReward)) ||
    (moneyRewardRaw !== undefined && isNaN(moneyReward)) ||
    (itemRewardIdRaw !== undefined &&
      itemRewardIdRaw !== null &&
      (itemRewardId === undefined || isNaN(itemRewardId)))
  ) {
    res.status(400).json({ message: 'Invalid parameters' });
    return;
  }

  try {
    const rewardPayload: {
      ringBallReward?: number;
      moneyReward?: number;
      itemRewardId?: number | null;
    } = {};

    if (ringBallRewardRaw !== undefined) {
      rewardPayload.ringBallReward = ringBallReward ?? 0;
    }

    if (moneyRewardRaw !== undefined) {
      rewardPayload.moneyReward = moneyReward ?? 0;
    }

    if (itemRewardIdRaw !== undefined) {
      rewardPayload.itemRewardId =
        itemRewardIdRaw === null ? null : itemRewardId ?? null;
    }

    const result = await sendMessage(
      senderId,
      receiverId,
      message,
      itemId,
      seqId,
      Object.keys(rewardPayload).length > 0 ? rewardPayload : undefined
    );
    if (result.success) {
      res.json(result.data);
      return;
    }
    const status =
      result.message === 'Message limit reached' ? 400 : 500;
    res.status(status).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const receiveItemsController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const senderId = Number(req.body.senderId ?? req.params.senderId);
    const receiverId = Number(req.body.receiverId ?? req.params.receiverId);
    const items = req.body.items;
    const seqMess =
      req.body.seqMess !== undefined ? Number(req.body.seqMess) : undefined;

    if (
      isNaN(senderId) ||
      isNaN(receiverId) ||
      !Array.isArray(items) ||
      !items.every(
        (it: any) =>
          it &&
          typeof it === 'object' &&
          !isNaN(Number(it.itemId)) &&
          !isNaN(Number(it.seq))
      )
      || (seqMess !== undefined && isNaN(seqMess))
    ) {
      res.status(400).json({ message: 'Invalid parameters' });
      return;
    }

    const parsedItems = items.map((it: any) => ({
      itemId: Number(it.itemId),
      seq: Number(it.seq),
    }));

    const result = await receiveItems(
      senderId,
      receiverId,
      parsedItems,
      seqMess
    );
    if (result.success) {
      res.json(result);
      return;
    }
    res.status(400).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const claimSystemMessageRewardController = async (
  req: Request,
  res: Response
): Promise<void> => {
  try {
    const receiverId = Number(req.body.receiverId ?? req.params.receiverId);
    const seqMessRaw =
      req.body.seqMess ??
      req.params.seqMess ??
      req.body.messageSeq ??
      req.params.messageSeq;
    const seqMess = Number(seqMessRaw);

    const ringBallRewardRaw = req.body.ringBallReward;
    const moneyRewardRaw = req.body.moneyReward;
    const itemRewardIdRaw = req.body.itemRewardId;

    if (isNaN(receiverId) || seqMessRaw === undefined || isNaN(seqMess)) {
      res.status(400).json({ message: 'Invalid receiverId or seqMess' });
      return;
    }

    const ringBallReward =
      ringBallRewardRaw !== undefined ? Number(ringBallRewardRaw) : undefined;
    const moneyReward =
      moneyRewardRaw !== undefined ? Number(moneyRewardRaw) : undefined;
    let itemRewardId: number | null | undefined;
    if (itemRewardIdRaw !== undefined) {
      if (itemRewardIdRaw === null) {
        itemRewardId = null;
      } else {
        const parsed = Number(itemRewardIdRaw);
        itemRewardId = parsed;
      }
    }

    if (
      (ringBallRewardRaw !== undefined && isNaN(ringBallReward)) ||
      (moneyRewardRaw !== undefined && isNaN(moneyReward)) ||
      (itemRewardIdRaw !== undefined &&
        itemRewardIdRaw !== null &&
        (itemRewardId === undefined || isNaN(itemRewardId)))
    ) {
      res.status(400).json({ message: 'Invalid reward metadata' });
      return;
    }

    const postedRewards: {
      ringBallReward?: number;
      moneyReward?: number;
      itemRewardId?: number | null;
    } = {};

    if (ringBallRewardRaw !== undefined) {
      postedRewards.ringBallReward = ringBallReward ?? 0;
    }

    if (moneyRewardRaw !== undefined) {
      postedRewards.moneyReward = moneyReward ?? 0;
    }

    if (itemRewardIdRaw !== undefined) {
      postedRewards.itemRewardId =
        itemRewardIdRaw === null ? null : itemRewardId ?? null;
    }

    const result = await claimSystemMessageReward(
      receiverId,
      seqMess,
      Object.keys(postedRewards).length > 0 ? postedRewards : undefined
    );

    if (result.success) {
      res.json(result.data);
      return;
    }

    const status = (() => {
      switch (result.message) {
        case 'System message not found':
        case 'Player not found':
        case 'Player not found or inactive':
          return 404;
        case 'Message not available for this player':
          return 403;
        case 'Reward already claimed':
          return 409;
        case 'Reward mismatch':
          return 400;
        default:
          return 400;
      }
    })();

    res.status(status).json({ message: result.message });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

