import type { Prisma } from '@prisma/client';
import prisma from '../models/prismaClient';
import { addItemToInventory } from './playerItemService';
import { luckyDrawAfterMatchWithTx } from './drawService';
import { getPlayerByListId } from './playerService';

const EARLY_EXIT_LUCKY_DRAW_MESSAGE_MARKER = '__MATCH_EARLY_EXIT_LUCKY_DRAW__';

export const countPendingFriendMessages = async (receiverId: number) => {
  return prisma.friendMessage.count({
    where: { receiverId, status: 'PENDING' },
  });
};

export const countPendingFriendRequests = async (receiverId: number) => {
  return prisma.friendRequest.count({
    where: { receiverId, status: 'PENDING' },
  });
};

export const searchPlayerById = async (id: number) => {
  try {
    const player = await prisma.player.findUnique({
      where: { id, IsActive: true },
    });
    if (!player) {
      return { success: false, message: 'Player not found' };
    }
    return { success: true, data: player };
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};

export const sendFriendRequest = async (
  senderId: number,
  friendCode: string
) => {
  try {
    const receiver = await prisma.player.findUnique({
      where: { friendCode, IsActive: true, NOT: { id: senderId } },
    });
    if (!receiver) {
      return { success: false, message: 'Receiver player not found' };
    }
    const receiverId = Number(receiver.id);

    const alreadyFriend = await prisma.friendship.findFirst({
      where: {
        OR: [
          { playerId: senderId, friendId: receiverId },
          { playerId: receiverId, friendId: senderId },
        ],
      },
    });
    if (alreadyFriend) {
      return { success: false, message: 'Already friends' };
    }

    const incoming = await prisma.friendRequest.findUnique({
      where: {
        senderId_receiverId: { senderId: receiverId, receiverId: senderId },
      },
    });

    if (incoming) {
      if (incoming.status === 'PENDING') {
        const updated = await prisma.friendRequest.update({
          where: {
            senderId_receiverId: { senderId: receiverId, receiverId: senderId },
          },
          data: { status: 'ACCEPTED' },
        });
        await prisma.friendship.createMany({
          data: [
            { playerId: senderId, friendId: receiverId },
            { playerId: receiverId, friendId: senderId },
          ],
          skipDuplicates: true,
        });
        return {
          success: true,
          message: 'Friend request auto-accepted',
          data: updated,
        };
      }
      return { success: false, message: 'Already handled' };
    }

    const existing = await prisma.friendRequest.findUnique({
      where: { senderId_receiverId: { senderId, receiverId } },
    });
    if (existing) {
      return { success: false, message: 'Request already sent' };
    }

    const request = await prisma.friendRequest.create({
      data: { senderId, receiverId },
    });
    return { success: true, data: request };
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};

export const removeFriend = async (playerId: number, friendId: number) => {
  try {
    await prisma.friendship.deleteMany({
      where: {
        OR: [
          { playerId, friendId },
          { playerId: friendId, friendId: playerId },
        ],
      },
    });
     await prisma.friendRequest.delete({
  where: {
    senderId_receiverId: { senderId: playerId, receiverId: friendId },
  },
});
    return { success: true };
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};

export const respondFriendRequest = async (
  senderId: number,
  receiverId: number,
  accept: boolean
) => {
  try {
    const request = await prisma.friendRequest.update({
      where: { senderId_receiverId: { senderId, receiverId } },
      data: { status: accept ? 'ACCEPTED' : 'REJECTED' },
    });

    if (accept) {
      await prisma.friendship.createMany({
        data: [
          { playerId: senderId, friendId: receiverId },
          { playerId: receiverId, friendId: senderId },
        ],
        skipDuplicates: true,
      });
    }

    return { success: true, data: request };
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};

export const getFriendList = async (playerId: number) => {
  try {
    const friendships = await prisma.friendship.findMany({
      where: { playerId },
      select: { friendId: true },
    });
    const friendIds = friendships.map((f) => f.friendId);
    if (friendIds.length === 0) {
      return { success: true, data: [] };
    }
    const players = await getPlayerByListId(friendIds);
    return { success: true, data: players };
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};

export const getPendingFriendRequests = async (
  receiverId: number
) => {
  try {
    const requests = await prisma.friendRequest.findMany({
      where: { receiverId, status: 'PENDING' },
      include: { sender: true },
    });
    const senders = requests.map((r) => r.sender);
    return { success: true, data: senders };
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};

export const getFriendMessages = async (receiverId: number) => {
  try {
    const latestMessages = await prisma.$queryRaw`
     SELECT DISTINCT ON (a."senderId")
    a."senderId",
    b."PlayerName",
    a."message",
    a."createdAt",
    a."status",
    a."receiverId",
    a."seqMess"
FROM "FriendMessage" a
JOIN "Player" b ON a."senderId" = b."id"
WHERE a."receiverId" = ${receiverId}
  AND a."isReceiverDelete" = false
  AND a."senderId" != 0
ORDER BY a."senderId", a."createdAt" DESC;
    `;

    return { success: true, data: latestMessages };

  } catch (error: any) {
    console.error(error);
    return { success: false, message: error.message };
  }
};

export const getSystemMessages = async (receiverId: number) => {
  try {
    const messages = await prisma.friendMessage.findMany({
      where: {
        senderId: 0,
        isReceiverDelete: false,
        OR: [{ receiverId }, { receiverId: 0 }],
      },
      orderBy: { createdAt: 'desc' },
      select: {
        senderId: true,
        receiverId: true,
        message: true,
        status: true,
        createdAt: true,
        seqMess: true,
        itemId: true,
        seqId: true,
        ringBallReward: true,
        moneyReward: true,
        itemRewardId: true,
      },
    });

    return { success: true, data: messages };
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};

export const getConversationHistory = async (
  playerId: number,
  friendId: number
) => {
  try {
    const messages = await prisma.friendMessage.findMany({
      where: {
        OR: [
          {
            senderId: playerId,
            receiverId: friendId,
            isSenderDelete: false,
          },
          {
            senderId: friendId,
            receiverId: playerId,
            isReceiverDelete: false,
          },
        ],
      },
      include: { sender: true },
      orderBy: { createdAt: 'asc' },
    });
    return { success: true, data: messages };
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};

export const sendMessage = async (
  senderId: number,
  receiverId: number,
  message: string,
  itemId?: number,
  seqId?: number,
  rewards?: {
    ringBallReward?: number;
    moneyReward?: number;
    itemRewardId?: number | null;
  }
) => {
  try {
    const messageCount = await prisma.friendMessage.count({
      where: { senderId },
    });

    if (messageCount >= 100) {
      throw new Error('Message limit reached');
    }

    const last = await prisma.friendMessage.findFirst({
      where: { senderId },
      orderBy: { seqMess: 'desc' },
      select: { seqMess: true },
    });

    const seqMess = (last?.seqMess ?? 0) + 1;

    const createData: Prisma.FriendMessageUncheckedCreateInput = {
      senderId,
      receiverId,
      message,
      itemId: itemId ?? null,
      seqId: seqId ?? null,
      seqMess,
    };

    if (senderId === 0) {
      createData.ringBallReward = rewards?.ringBallReward ?? 0;
      createData.moneyReward = rewards?.moneyReward ?? 0;
      const normalizedItemReward = rewards?.itemRewardId ?? null;
      createData.itemRewardId =
        normalizedItemReward && normalizedItemReward > 0
          ? normalizedItemReward
          : null;
    }

    const msg = await prisma.friendMessage.create({
      data: createData,
    });

    return { success: true, data: msg };
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};

export const readMessage = async (playerId: number, seqMess: number) => {
  try {
    await prisma.friendMessage.updateMany({
      where: {
        receiverId: playerId,
        seqMess,
      },
      data: { status: 'READ' },
    });
    return { success: true };
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};

export const receiveItems = async (
  senderId: number,
  receiverId: number,
  items: Array<{ itemId: number; seq: number }>,
  seqMess?: number
) => {
  try {
    await prisma.$transaction(async (tx) => {
      await tx.friendMessage.updateMany({
        where: {
          senderId,
          receiverId,
          ...(seqMess !== undefined ? { seqMess } : {}),
        },
        data: { status: 'READ' },
      });

      for (const { itemId, seq } of items) {
        if (itemId === 0) {
          const quantity = seq;
          const sender = await tx.player.findUnique({
            where: { id: senderId },
            select: { RingBall: true },
          });
          if (!sender || (sender.RingBall ?? 0) < quantity) {
            throw new Error('Not enough RingBall');
          }
          await tx.player.update({
            where: { id: senderId },
            data: { RingBall: { decrement: quantity } },
          });
          await tx.player.update({
            where: { id: receiverId },
            data: { RingBall: { increment: quantity } },
          });
        } else {
          const playerItem = await tx.playerItem.findUnique({
            where: {
              playerId_itemId_seq: { playerId: senderId, itemId, seq },
            },
          });
          if (!playerItem) {
            throw new Error('Sender does not own item');
          }
          await tx.playerItem.delete({
            where: {
              playerId_itemId_seq: { playerId: senderId, itemId, seq },
            },
          });
          const lastSeq = await tx.playerItem.findFirst({
            where: { playerId: receiverId, itemId },
            orderBy: { seq: 'desc' },
            select: { seq: true },
          });
          const newSeq = lastSeq ? lastSeq.seq + 1 : 0;
          await tx.playerItem.create({
            data: {
              playerId: receiverId,
              itemId,
              seq: newSeq,
              level: playerItem.level,
              SkillGid: playerItem.SkillGid ?? null,
              description: `item được gửi từ user: ${senderId}`,
            },
          });
        }
      }
    });
    return { success: true };
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};

export const claimSystemMessageReward = async (
  receiverId: number,
  seqMess: number,
  postedRewards?: {
    ringBallReward?: number;
    moneyReward?: number;
    itemRewardId?: number | null;
  }
) => {
  try {
    const result = await prisma.$transaction(async (tx) => {
      let message = await tx.friendMessage.findFirst({
        where: {
          senderId: 0,
          seqMess,
          OR: [{ receiverId }, { receiverId: 0 }],
        },
      });

      if (!message) {
        message = await tx.friendMessage.findFirst({
          where: {
            seqMess,
            OR: [{ receiverId }, { receiverId: 0 }],
            message: {
              startsWith: EARLY_EXIT_LUCKY_DRAW_MESSAGE_MARKER,
            },
          },
        });
      }

      if (!message) {
        throw new Error('System message not found');
      }

      if (message.receiverId !== receiverId && message.receiverId !== 0) {
        throw new Error('Message not available for this player');
      }

      if (message.status !== 'PENDING') {
        throw new Error('Reward already claimed');
      }

      const isEarlyExitLuckyDrawMessage =
        typeof message.message === 'string' &&
        message.message.startsWith(EARLY_EXIT_LUCKY_DRAW_MESSAGE_MARKER);

      if (message.senderId !== 0 && !isEarlyExitLuckyDrawMessage) {
        throw new Error('Not a system message');
      }

      if (isEarlyExitLuckyDrawMessage) {
        const playerExists = await tx.player.findUnique({
          where: { id: receiverId },
          select: { id: true },
        });

        if (!playerExists) {
          throw new Error('Player not found');
        }

        const luckyDrawReward = await luckyDrawAfterMatchWithTx(receiverId, tx);

        await tx.friendMessage.delete({
          where: {
            senderId_seqMess: {
              senderId: message.senderId,
              seqMess: message.seqMess,
            },
          },
        });

        const sanitizedMessage = {
          ...message,
          status: 'READ',
          ringBallReward: 0,
          moneyReward: 0,
          itemRewardId: null,
        };

        return {
          message: sanitizedMessage,
          rewards: {
            ringBallReward: 0,
            moneyReward: 0,
            itemRewardId: null,
          },
          luckyDrawReward,
        };
      }

      const ringBallReward = message.ringBallReward ?? 0;
      const moneyReward = message.moneyReward ?? 0;
      const itemRewardId = message.itemRewardId ?? null;

      if (
        postedRewards?.ringBallReward !== undefined &&
        postedRewards.ringBallReward !== ringBallReward
      ) {
        throw new Error('Reward mismatch');
      }

      if (
        postedRewards?.moneyReward !== undefined &&
        postedRewards.moneyReward !== moneyReward
      ) {
        throw new Error('Reward mismatch');
      }

      if (postedRewards?.itemRewardId !== undefined) {
        const normalizedPostedItemId =
          postedRewards.itemRewardId && postedRewards.itemRewardId > 0
            ? postedRewards.itemRewardId
            : null;
        if (normalizedPostedItemId !== itemRewardId) {
          throw new Error('Reward mismatch');
        }
      }

      const playerUpdateData: Prisma.PlayerUpdateInput = {};

      if (ringBallReward > 0) {
        playerUpdateData.RingBall = { increment: ringBallReward };
      }

      if (moneyReward > 0) {
        playerUpdateData.Money = { increment: moneyReward };
      }

      const playerExists = await tx.player.findUnique({
        where: { id: receiverId },
        select: { id: true },
      });

      if (!playerExists) {
        throw new Error('Player not found');
      }

      if (Object.keys(playerUpdateData).length > 0) {
        await tx.player.update({
          where: { id: receiverId },
          data: playerUpdateData,
        });
      }

      if (itemRewardId && itemRewardId > 0) {
        await addItemToInventory(receiverId, itemRewardId, tx);
      }

      await tx.friendMessage.delete({
        where: {
          senderId_seqMess: {
            senderId: message.senderId,
            seqMess: message.seqMess,
          },
        },
      });

      const sanitizedMessage = {
        ...message,
        status: 'READ',
        ringBallReward: 0,
        moneyReward: 0,
        itemRewardId: null,
      };

      return {
        message: sanitizedMessage,
        rewards: {
          ringBallReward,
          moneyReward,
          itemRewardId,
        },
      };
    });

    return { success: true, data: result };
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};

export const deleteFriendMessage = async (
  requesterId: number,
  partnerId: number
) => {
  try {
    const result = await prisma.$transaction(async (tx) => {
      const conversation = await tx.friendMessage.findMany({
        where: {
          OR: [
            { senderId: requesterId, receiverId: partnerId },
            { senderId: partnerId, receiverId: requesterId },
          ],
        },
        select: {
          senderId: true,
          receiverId: true,
          seqMess: true,
          isSenderDelete: true,
          isReceiverDelete: true,
        },
      });

      if (conversation.length === 0) {
        return { success: false as const, message: 'Message not found' };
      }

      const participates = conversation.some(
        (message) =>
          message.senderId === requesterId || message.receiverId === requesterId
      );

      if (!participates) {
        return { success: false as const, message: 'Forbidden' };
      }

      const deleteSent = await tx.friendMessage.deleteMany({
        where: {
          senderId: requesterId,
          receiverId: partnerId,
          isReceiverDelete: true,
        },
      });

      const deleteReceived = await tx.friendMessage.deleteMany({
        where: {
          senderId: partnerId,
          receiverId: requesterId,
          isSenderDelete: true,
        },
      });

      const hideSent = await tx.friendMessage.updateMany({
        where: {
          senderId: requesterId,
          receiverId: partnerId,
          isSenderDelete: false,
        },
        data: { isSenderDelete: true },
      });

      const hideReceived = await tx.friendMessage.updateMany({
        where: {
          senderId: partnerId,
          receiverId: requesterId,
          isReceiverDelete: false,
        },
        data: { isReceiverDelete: true },
      });

      return {
        success: true as const,
        data: {
          hiddenCount: hideSent.count + hideReceived.count,
          hardDeletedCount: deleteSent.count + deleteReceived.count,
        },
      };
    });

    return result;
  } catch (error: any) {
    return { success: false, message: error.message };
  }
};



