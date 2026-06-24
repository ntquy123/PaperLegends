import { Prisma } from '@prisma/client';
import prisma from '../models/prismaClient';

export interface BetTransactionInput {
  userId: number;
  ringBall: number;
  money?: number;
  description?: string;
  eventType?: string;
}

export const normalizeBetTransactions = (entries: unknown): BetTransactionInput[] => {
  if (!Array.isArray(entries)) {
    return [];
  }

  return entries
    .map((entry) => {
      if (!entry || typeof entry !== 'object') {
        return null;
      }

      const payload = entry as Record<string, unknown>;
      const userId = Number(payload.userId ?? payload.playerId);
      const ringBall = Number(payload.ringBall ?? payload.bet ?? payload.marbBet);

      if (!Number.isFinite(userId) || !Number.isFinite(ringBall)) {
        return null;
      }

      return {
        userId,
        ringBall,
        money: payload.money !== undefined ? Number(payload.money) : 0,
        description: typeof payload.description === 'string' ? payload.description : undefined,
        eventType: typeof payload.eventType === 'string' ? payload.eventType : undefined,
      } as BetTransactionInput;
    })
    .filter((entry): entry is BetTransactionInput => entry !== null);
};

const getNextSeqForUser = async (
  tx: Prisma.TransactionClient,
  userId: number
): Promise<number> => {
  const latest = await tx.balanceHistory.aggregate({
    where: { userId },
    _max: { seq: true },
  });

  return (latest._max.seq ?? 0) + 1;
};

export const processBetTransactions = async (
  transactions: BetTransactionInput[],
  tx?: Prisma.TransactionClient,
) => {
  if (!transactions.length) {
    return [];
  }

  const run = async (transactionClient: Prisma.TransactionClient) => {
    const histories = [] as { userId: number; seq: number }[];

    for (const transaction of transactions) {
      const ringBallChange = transaction.ringBall;
      const moneyChange = transaction.money ?? 0;

      if (!Number.isFinite(ringBallChange) || ringBallChange <= 0) {
        throw new Error('ringBall must be a positive number');
      }

      const player = await transactionClient.player.findUnique({
        where: { id: transaction.userId },
        select: { RingBall: true, ProviderType: true },
      });

      if (!player) {
        throw new Error(`Player ${transaction.userId} not found`);
      }

      if (player.ProviderType === 'BOT') {
        continue;
      }

      const currentRingBall = player.RingBall ?? 0;
      if (currentRingBall < ringBallChange) {
        throw new Error(`Not enough RingBall for player ${transaction.userId}`);
      }

      await transactionClient.player.update({
        where: { id: transaction.userId },
        data: { RingBall: { decrement: ringBallChange } },
      });

      const nextSeq = await getNextSeqForUser(transactionClient, transaction.userId);

      const history = await transactionClient.balanceHistory.create({
        data: {
          userId: transaction.userId,
          seq: nextSeq,
          ringBall: -ringBallChange,
          money: moneyChange,
          description: transaction.description,
          eventType: transaction.eventType ?? 'GAME_BET_DEDUCTION',
        },
      });

      histories.push({ userId: history.userId, seq: history.seq });
    }

    return histories;
  };

  if (tx) {
    return run(tx);
  }

  return prisma.$transaction(async (transactionClient) => run(transactionClient));
};
