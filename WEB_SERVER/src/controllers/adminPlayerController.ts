import { Request, Response } from 'express';
import prisma from '../models/prismaClient';
import { sendMessage } from '../services/friendService';
import {
  AdminTokenPayload,
  isAdminPasswordConfigured,
  verifyAdminPassword,
} from '../middleware/adminAuth';

const DEFAULT_PAGE_SIZE = 10;
const MAX_PAGE_SIZE = 50;
const MAX_LOG_LIMIT = 50;

const parseNumber = (value: unknown, fallback: number) => {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return fallback;
  }
  return Math.floor(parsed);
};

const parseNonNegativeNumber = (value: unknown) => {
  if (value === undefined || value === null || value === '') {
    return 0;
  }
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    return null;
  }
  return Math.floor(parsed);
};

const parsePlayerIds = (value: unknown): number[] => {
  if (Array.isArray(value)) {
    return value
      .map((item) => Number(item))
      .filter((item) => Number.isInteger(item) && item > 0);
  }
  if (value === undefined || value === null || value === '') {
    return [];
  }
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return [];
  }
  return [parsed];
};

const parseBooleanValue = (value: unknown) => {
  if (value === true || value === 'true' || value === 1 || value === '1') {
    return true;
  }
  return false;
};

const getPagination = (req: Request) => {
  const page = parseNumber(req.query.page, 1);
  const pageSize = Math.min(parseNumber(req.query.pageSize, DEFAULT_PAGE_SIZE), MAX_PAGE_SIZE);
  return { page, pageSize };
};

export const getPlayers = async (req: Request, res: Response): Promise<void> => {
  const { page, pageSize } = getPagination(req);
  const friendCode = typeof req.query.friendCode === 'string' ? req.query.friendCode.trim() : '';

  const where = friendCode
    ? {
        friendCode: {
          contains: friendCode,
          mode: 'insensitive' as const,
        },
      }
    : undefined;

  try {
    const [totalItems, players] = await prisma.$transaction([
      prisma.player.count({ where }),
      prisma.player.findMany({
        where,
        orderBy: { id: 'desc' },
        skip: (page - 1) * pageSize,
        take: pageSize,
        select: {
          id: true,
          friendCode: true,
          PlayerName: true,
          Level: true,
          RingBall: true,
          Money: true,
          IsActive: true,
          ProviderType: true,
          createdAt: true,
          lastLoginAt: true,
        },
      }),
    ]);

    const totalPages = totalItems > 0 ? Math.ceil(totalItems / pageSize) : 1;
    res.json({
      players,
      pagination: {
        page,
        pageSize,
        totalItems,
        totalPages,
      },
    });
  } catch (error) {
    console.error('Lỗi khi lấy danh sách người chơi:', error);
    res.status(500).json({ error: 'Không thể tải danh sách người chơi.' });
  }
};

export const getPlayerDetail = async (req: Request, res: Response): Promise<void> => {
  const playerId = Number(req.params.id);
  if (!Number.isInteger(playerId)) {
    res.status(400).json({ error: 'Thiếu ID người chơi.' });
    return;
  }

  try {
    const player = await prisma.player.findUnique({
      where: { id: playerId },
      select: {
        id: true,
        friendCode: true,
        PlayerName: true,
        Level: true,
        Exp: true,
        Body: true,
        RingBall: true,
        Money: true,
        TalentPoint: true,
        IsActive: true,
        Email: true,
        ProviderType: true,
        createdAt: true,
        lastLoginAt: true,
      },
    });

    if (!player) {
      res.status(404).json({ error: 'Không tìm thấy người chơi.' });
      return;
    }

    res.json({ player });
  } catch (error) {
    console.error('Lỗi khi lấy thông tin người chơi:', error);
    res.status(500).json({ error: 'Không thể tải thông tin người chơi.' });
  }
};



export const getPlayerHistories = async (req: Request, res: Response): Promise<void> => {
  const playerId = Number(req.params.id);

  // Kiểm tra playerId hợp lệ
  if (!Number.isInteger(playerId)) {
    res.status(400).json({ error: 'Thiếu ID người chơi.' });
    return;
  }

  // Xử lý limit query parameter
  const limit = Math.min(parseNumber(req.query.limit, 20), MAX_LOG_LIMIT);

  try {
    // Truy vấn lịch sử trận đấu từ cơ sở dữ liệu
    const histories = await prisma.history.findMany({
      where: { playerId },
      orderBy: { createdAt: 'desc' },
      take: limit,
      select: {
        transno: true,
        statusWin: true,
        typeMatchGid: true,
        mapGame: true,
        rounds: true,
        marbBet: true,
        marblesWon: true,
        marblesLost: true,
        expGained: true,
        rankPoints: true,
        description: true,
        createdAt: true,
      },
    });

    // Nếu không có lịch sử, trả về mảng rỗng
    if (histories.length === 0) {
      res.json({ histories: [] });
      return;
    }

    // Chuyển đổi transno thành chuỗi nếu cần
    const serializedHistories = histories.map((history) => ({
      ...history,
      transno: history.transno.toString(),
    }));

    // Trả về dữ liệu
    res.json({ histories: serializedHistories });
  } catch (error) {
    console.error('Lỗi khi lấy lịch sử trận đấu:', error);
    res.status(500).json({ error: 'Không thể tải lịch sử trận đấu.' });
  }
};

export const getBalanceHistories = async (req: Request, res: Response): Promise<void> => {
  const playerId = Number(req.params.id);
  if (!Number.isInteger(playerId)) {
    res.status(400).json({ error: 'Thiếu ID người chơi.' });
    return;
  }

  const limit = Math.min(parseNumber(req.query.limit, 20), MAX_LOG_LIMIT);

  try {
    const balanceHistories = await prisma.balanceHistory.findMany({
      where: { userId: playerId },
      orderBy: { createdAt: 'desc' },
      take: limit,
      select: {
        seq: true,
        ringBall: true,
        money: true,
        description: true,
        eventType: true,
        createdAt: true,
      },
    });
    res.json({ balanceHistories });
  } catch (error) {
    console.error('Lỗi khi lấy lịch sử trừ tiền:', error);
    res.status(500).json({ error: 'Không thể tải lịch sử trừ tiền.' });
  }
};

export const getItemTradeHistories = async (req: Request, res: Response): Promise<void> => {
  const playerId = Number(req.params.id);
  if (!Number.isInteger(playerId)) {
    res.status(400).json({ error: 'Thiếu ID người chơi.' });
    return;
  }

  const limit = Math.min(parseNumber(req.query.limit, 20), MAX_LOG_LIMIT);

  try {
    const trades = await prisma.itemTradeHistory.findMany({
      where: {
        OR: [{ playerIdBuy: playerId }, { playerIdSold: playerId }],
      },
      orderBy: { createdAt: 'desc' },
      take: limit,
    });

    const itemIds = Array.from(new Set(trades.map((trade) => trade.itemId)));
    const playerIds = Array.from(
      new Set(trades.flatMap((trade) => [trade.playerIdBuy, trade.playerIdSold])),
    );

    const [items, players] = await Promise.all([
      prisma.item.findMany({
        where: { id: { in: itemIds } },
        select: { id: true, name: true },
      }),
      prisma.player.findMany({
        where: { id: { in: playerIds } },
        select: { id: true, PlayerName: true, friendCode: true },
      }),
    ]);

    const itemMap = new Map(items.map((item) => [item.id, item.name]));
    const playerMap = new Map(
      players.map((player) => [
        player.id,
        {
          name: player.PlayerName ?? 'Không tên',
          friendCode: player.friendCode,
        },
      ]),
    );

    const itemTradeHistories = trades.map((trade) => ({
      ...trade,
      itemName: itemMap.get(trade.itemId) ?? 'Không rõ',
      buyer: playerMap.get(trade.playerIdBuy) ?? null,
      seller: playerMap.get(trade.playerIdSold) ?? null,
    }));

    res.json({ itemTradeHistories });
  } catch (error) {
    console.error('Lỗi khi lấy lịch sử buôn bán:', error);
    res.status(500).json({ error: 'Không thể tải lịch sử buôn bán.' });
  }
};

export const updatePlayerActiveStatus = async (req: Request, res: Response): Promise<void> => {
  const playerId = Number(req.params.id);
  const isActive = req.body?.isActive;

  if (!Number.isInteger(playerId)) {
    res.status(400).json({ error: 'Thiếu ID người chơi.' });
    return;
  }

  if (typeof isActive !== 'boolean') {
    res.status(400).json({ error: 'Trạng thái IsActive không hợp lệ.' });
    return;
  }

  try {
    const updated = await prisma.player.update({
      where: { id: playerId },
      data: { IsActive: isActive },
      select: {
        id: true,
        friendCode: true,
        PlayerName: true,
        IsActive: true,
      },
    });

    res.json({
      message: updated.IsActive ? 'Đã mở khóa tài khoản.' : 'Đã khóa tài khoản.',
      player: updated,
    });
  } catch (error: any) {
    if (error?.code === 'P2025') {
      res.status(404).json({ error: 'Không tìm thấy người chơi.' });
      return;
    }
    console.error('Lỗi khi cập nhật trạng thái tài khoản:', error);
    res.status(500).json({ error: 'Không thể cập nhật trạng thái tài khoản.' });
  }
};

export const deletePlayer = async (req: Request, res: Response): Promise<void> => {
  const playerId = Number(req.params.id);
  const password = req.body?.password;

  if (!Number.isInteger(playerId) || playerId <= 0) {
    res.status(400).json({ error: 'ID người chơi không hợp lệ.' });
    return;
  }

  if (!isAdminPasswordConfigured()) {
    res.status(503).json({ error: 'Server chưa cấu hình ADMIN_UI_PASSWORD cho tài khoản quản trị.' });
    return;
  }

  if (!verifyAdminPassword(password)) {
    res.status(403).json({ error: 'Mật khẩu quản trị không chính xác. Không thể xóa người chơi.' });
    return;
  }

  const admin = res.locals.admin as AdminTokenPayload | undefined;
  try {
    const player = await prisma.player.findUnique({
      where: { id: playerId },
      select: {
        id: true,
        friendCode: true,
        ProviderType: true,
      },
    });

    if (!player) {
      res.status(404).json({ error: 'Không tìm thấy người chơi.' });
      return;
    }

    if (player.ProviderType === 'System' || admin?.playerId === playerId) {
      res.status(403).json({ error: 'Không thể xóa tài khoản quản trị hệ thống.' });
      return;
    }

    const deleted = await prisma.$transaction(async (tx) => {
      const refreshTokens = await tx.userRefreshToken.deleteMany({ where: { userId: playerId } });
      const effects = await tx.effectPlayer.deleteMany({ where: { playerId } });
      const equips = await tx.equipPlayer.deleteMany({ where: { playerId } });
      const balances = await tx.balanceHistory.deleteMany({ where: { userId: playerId } });
      const messages = await tx.friendMessage.deleteMany({
        where: { OR: [{ senderId: playerId }, { receiverId: playerId }] },
      });
      const requests = await tx.friendRequest.deleteMany({
        where: { OR: [{ senderId: playerId }, { receiverId: playerId }] },
      });
      const friendships = await tx.friendship.deleteMany({
        where: { OR: [{ playerId }, { friendId: playerId }] },
      });
      const histories = await tx.history.deleteMany({ where: { playerId } });
      const trades = await tx.itemTradeHistory.deleteMany({
        where: { OR: [{ playerIdBuy: playerId }, { playerIdSold: playerId }] },
      });
      const achievements = await tx.playerAchievementStatus.deleteMany({ where: { playerId } });
      const items = await tx.playerItem.deleteMany({ where: { playerId } });
      const roomUsers = await tx.roomUser.deleteMany({ where: { userId: playerId } });
      const dailyRareItems = await tx.dailyRareItem.deleteMany({ where: { playerId } });
      const dailyShopPurchases = await tx.dailyShopPurchase.deleteMany({ where: { userId: playerId } });
      const buyRequestOrders = await tx.buyRequestOrder.deleteMany({ where: { playerId } });

      await tx.player.delete({ where: { id: playerId } });

      return {
        refreshTokens: refreshTokens.count,
        effects: effects.count,
        equips: equips.count,
        balances: balances.count,
        messages: messages.count,
        requests: requests.count,
        friendships: friendships.count,
        histories: histories.count,
        trades: trades.count,
        achievements: achievements.count,
        items: items.count,
        roomUsers: roomUsers.count,
        dailyRareItems: dailyRareItems.count,
        dailyShopPurchases: dailyShopPurchases.count,
        buyRequestOrders: buyRequestOrders.count,
      };
    });

    res.json({
      message: `Đã xóa người chơi ${player.friendCode}.`,
      playerId,
      deleted,
    });
  } catch (error: any) {
    if (error?.code === 'P2025') {
      res.status(404).json({ error: 'Không tìm thấy người chơi.' });
      return;
    }

    console.error('Lỗi khi xóa người chơi:', error);
    res.status(500).json({ error: 'Không thể xóa người chơi.' });
  }
};

export const sendSystemMessage = async (req: Request, res: Response): Promise<void> => {
  const message = typeof req.body?.message === 'string' ? req.body.message.trim() : '';
  if (!message) {
    res.status(400).json({ error: 'Nội dung tin nhắn không được để trống.' });
    return;
  }

  const ringBallReward = parseNonNegativeNumber(req.body?.ringBallReward);
  const moneyReward = parseNonNegativeNumber(req.body?.moneyReward);
  if (ringBallReward === null || moneyReward === null) {
    res.status(400).json({ error: 'Điểm thưởng phải là số không âm.' });
    return;
  }

  const rawItemRewardId = req.body?.itemRewardId;
  const itemRewardId =
    rawItemRewardId === undefined || rawItemRewardId === null || rawItemRewardId === ''
      ? null
      : Number(rawItemRewardId);

  if (itemRewardId !== null && (!Number.isInteger(itemRewardId) || itemRewardId <= 0)) {
    res.status(400).json({ error: 'Item đính kèm không hợp lệ.' });
    return;
  }

  const sendAll = parseBooleanValue(req.body?.sendAll);
  const playerIds = parsePlayerIds(req.body?.playerIds);

  if (!sendAll && playerIds.length === 0) {
    res.status(400).json({ error: 'Vui lòng chọn ít nhất một người chơi.' });
    return;
  }

  try {
    let validatedPlayerIds = playerIds;

    if (!sendAll) {
      const existingPlayers = await prisma.player.findMany({
        where: { id: { in: playerIds } },
        select: { id: true },
      });

      validatedPlayerIds = existingPlayers.map((player) => player.id);
      if (validatedPlayerIds.length !== playerIds.length) {
        res.status(404).json({ error: 'Một hoặc nhiều người chơi không tồn tại.' });
        return;
      }
    }

    if (itemRewardId) {
      const itemExists = await prisma.item.findUnique({
        where: { id: itemRewardId },
        select: { id: true },
      });
      if (!itemExists) {
        res.status(404).json({ error: 'Không tìm thấy item đính kèm.' });
        return;
      }
    }

    const receiverIds = sendAll ? [0] : Array.from(new Set(validatedPlayerIds));

    let sentCount = 0;
    for (const receiverId of receiverIds) {
      const result = await sendMessage(0, receiverId, message, undefined, undefined, {
        ringBallReward,
        moneyReward,
        itemRewardId,
      });
      if (!result.success) {
        res.status(500).json({ error: result.message ?? 'Không thể gửi tin nhắn.' });
        return;
      }
      sentCount += 1;
    }

    res.json({
      message: sendAll
        ? 'Đã gửi tin nhắn hệ thống tới toàn server.'
        : `Đã gửi tin nhắn tới ${sentCount} người chơi.`,
      sentCount,
    });
  } catch (error) {
    console.error('Lỗi khi gửi tin nhắn hệ thống:', error);
    res.status(500).json({ error: 'Không thể gửi tin nhắn hệ thống.' });
  }
};
