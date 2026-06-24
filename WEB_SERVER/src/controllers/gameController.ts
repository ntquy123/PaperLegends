import { Request, Response } from 'express';
import { createHistory } from '../services/historyService';
import { updatePlayerStats } from '../services/playerService';
import { normalizeBetTransactions, processBetTransactions } from '../services/gameService';
import prisma from '../models/prismaClient';

export const deductBetOnGameStart = async (req: Request, res: Response) => {
  try {
    if (!Array.isArray(req.body)) {
      res.status(400).json({ message: 'Request body must be an array' });
      return;
    }

    const transactions = normalizeBetTransactions(req.body);

    if (!transactions.length) {
      res.status(400).json({ message: 'No valid transactions provided' });
      return;
    }

    const histories = await processBetTransactions(transactions);

    res.json({ message: 'Bet deductions recorded', histories });
  } catch (error: any) {
    const errorMessage = error?.message ?? 'Failed to deduct bets';
    let statusCode = 500;

    if (errorMessage.toLowerCase().includes('not found')) {
      statusCode = 404;
    } else if (errorMessage.includes('Not enough RingBall') || errorMessage.includes('must be a positive')) {
      statusCode = 400;
    }

    res.status(statusCode).json({ message: errorMessage });
  }
};

export const overGame = async (req: Request, res: Response) => {
  try {
    if (!Array.isArray(req.body)) {
      res.status(400).json({ message: 'Request body must be an array' });
      return;
    }
    const generateTransno = (): bigint => {
      const now = new Date();
      const year = now.getFullYear();
      const month = String(now.getMonth() + 1).padStart(2, '0');
      const day = String(now.getDate()).padStart(2, '0');
      const hour = String(now.getHours()).padStart(2, '0');
      const minute = String(now.getMinutes()).padStart(2, '0');
      const second = String(now.getSeconds()).padStart(2, '0');
      const milli = String(now.getMilliseconds()).padStart(3, '0');
      return BigInt(`${year}${month}${day}${hour}${minute}${second}${milli}`);
    };
    const transno = generateTransno();
    const playerIds = Array.from(
      new Set(
        req.body
          .map((entry) => Number(entry?.playerId))
          .filter((playerId) => Number.isFinite(playerId))
      )
    );
    const players = await prisma.player.findMany({
      where: { id: { in: playerIds } },
      select: { id: true, ProviderType: true },
    });
    const providerTypeByPlayerId = new Map(players.map((player) => [player.id, player.ProviderType]));

    for (const entry of req.body) {
      const {
        playerId,
        turnOrder,
        typeMatchGid,
        StatusWin,
        rounds,
        MapGame,
        MaxPlayer,
        marbBet,
        marblesWon,
        marblesLost,
        expGained,
        description,
      } = entry;

      if (typeof playerId !== 'number') {
        continue;
      }

      const exp = typeof expGained === 'number' ? expGained : 0;
      const marblesActual = marblesWon > 0 ? marblesWon : -marblesLost;
      const rankPoints = StatusWin === 1 ? Math.abs(marblesActual) : -Math.abs(marblesActual);
      const providerType = providerTypeByPlayerId.get(playerId);

      if (providerType !== 'BOT') {
        await updatePlayerStats(playerId, exp, marblesActual);
      }

      await createHistory({
        playerId,
        transno,
        turnOrder,
        typeMatchGid,
        statusWin: StatusWin,
        mapGame: MapGame,
        maxPlayer: MaxPlayer,
        rounds,
        marbBet,
        marblesWon,
        marblesLost,
        expGained: exp,
        rankPoints,
        description,
      });
    }

    // ⚠️ KHÔNG cleanup room/container ở đây!
    // Container gọi /over-game chính là DS đang chạy trận.
    // Nếu stop container tại đây, DS sẽ chết TRƯỚC khi:
    //   - Nhận HTTP response
    //   - Gửi RPC kết quả cho clients
    //   - Gọi /internal/match/result
    // → Client nhận "Server disconnected" thay vì kết quả trận.
    //
    // Cleanup sẽ được thực hiện sau khi DS gọi /internal/match/result
    // (xem matchmaker.onMatchResult → cleanupRoomData) hoặc khi DS
    // tự shutdown và container exit tự nhiên.

    res.json({ message: 'Game results recorded' });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};
