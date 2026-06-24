// src/services/playerService.ts
import { Prisma } from '@prisma/client';
import prisma from '../models/prismaClient'; // Import Prisma Client

// Location id used in EquipPlayer to mark the equipped ball slot
const BALL_SLOT_LOCATION_ID = 2;
const BALL_SKILL_MIN_GID = 11400001;
const BALL_SKILL_MAX_GID = 11400006;
const generateRandomBallSkillGid = (): number =>
  Math.floor(Math.random() * (BALL_SKILL_MAX_GID - BALL_SKILL_MIN_GID + 1)) + BALL_SKILL_MIN_GID;

 const generateFriendCode  = (): string => {
  // Bộ ký tự không dễ nhầm
  const letters = 'ABCDEFGHJKLMNPQRSTUVWXYZ'; // bỏ O, I
  const numbers = '23456789';                 // bỏ 0, 1
  
  // Nhóm 1: 3 chữ cái
  let part1 = '';
  for (let i = 0; i < 3; i++) {
    part1 += letters.charAt(Math.floor(Math.random() * letters.length));
  }
  
  // Nhóm 2: 3 chữ số
  let part2 = '';
  for (let i = 0; i < 3; i++) {
    part2 += numbers.charAt(Math.floor(Math.random() * numbers.length));
  }
  
  // Kết quả dạng ABC-123
  return `${part1}-${part2}`;
};


const STARTING_EFFECTS = [
  { level: 1, effectId: 11000001, power: 0, spin: 0, isPassive: false, IsActive: true, IsEquiped: true, charges: 1 },
  { level: 1, effectId: 11000002, power: 0, spin: 0.5, isPassive: false,IsActive: true ,IsEquiped: true, charges: 1 },
  { level: 1, effectId: 11000003, power: 0, spin: 0, isPassive: false, charges: 1 },
  { level: 1, effectId: 11000004, power: 0, spin: 0, isPassive: false,IsActive: false, charges: 1 },
  { level: 1, effectId: 11000005, power: 0, spin: 0, isPassive: false,IsActive: true, charges: 1 },
  { level: 1, effectId: 11000006, power: 0, spin: 0, isPassive: false,IsActive: true, charges: 1 },
  { level: 1, effectId: 11000007, power: 0, spin: 0, isPassive: false, IsActive: true, IsEquiped: true, charges: 1 },
  { level: 1, effectId: 11000008, power: 0, spin: 0, isPassive: true, IsActive: true, charges: 1 },
  { level: 1, effectId: 11000009, power: 0, spin: 0, isPassive: false, charges: 1 },
  { level: 1, effectId: 11000010, power: 0, spin: 0, isPassive: false,IsActive: true, charges: 1 },
  { level: 1, effectId: 11000011, power: 0, spin: 0, isPassive: false,IsActive: true, charges: 1 },
  { level: 1, effectId: 11000012, power: 0, spin: 0, isPassive: false,IsActive: true, charges: 1 },
  { level: 1, effectId: 11000013, power: 0, spin: 0, isPassive: false,IsActive: true, charges: 1 },
  { level: 1, effectId: 11000014, power: 0, spin: 0, isPassive: false,IsActive: true, charges: 1 },
];

const seedNewPlayerData = async (
  tx: Prisma.TransactionClient,
  playerId: number
) => {
  await tx.effectPlayer.createMany({
    data: STARTING_EFFECTS.map((effect) => ({
      playerId,
      effectId: effect.effectId,
      power: effect.power,
      spin: effect.spin,
      level: effect.level,
      isPassive: effect.isPassive,
      IsActive: effect.IsActive,
      IsEquiped: effect.IsEquiped,
      charges: effect.charges,
      description: `Skill ${effect.effectId}`,
    })),
  });
};

const createNewPlayerRewards = async (
  tx: Prisma.TransactionClient,
  playerId: number
) => {
  const lastMessage = await tx.friendMessage.findFirst({
    where: { senderId: 0 },
    orderBy: { seqMess: 'desc' },
    select: { seqMess: true },
  });

  const baseSeq = lastMessage?.seqMess ?? 0;

  const rewardMessages: Prisma.FriendMessageUncheckedCreateInput[] = [
    {
      senderId: 0,
      receiverId: playerId,
      seqMess: baseSeq + 1,
      message: '',
      ringBallReward: 20,
    },
    {
      senderId: 0,
      receiverId: playerId,
      seqMess: baseSeq + 2,
      message: '',
      itemRewardId: 98000001,
    },
    {
      senderId: 0,
      receiverId: playerId,
      seqMess: baseSeq + 3,
      message: '',
      itemRewardId: 98000001,
    },
    {
      senderId: 0,
      receiverId: playerId,
      seqMess: baseSeq + 4,
      message: '',
      itemRewardId: 98000002,
    },
  ];

  await tx.friendMessage.createMany({
    data: rewardMessages,
  });
};


export const getPlayerByAccountId = async (accountId: string) => {
  return await prisma.player.findFirst({
    where: { IdAccount: accountId, IsActive: true },
  });
};

export const completePlayerTutorial = async (playerId: number) => {
  const player = await prisma.player.findFirst({
    where: { id: playerId, IsActive: true },
    select: { id: true },
  });

  if (!player) {
    throw new Error('Player not found or inactive');
  }

  return prisma.player.update({
    where: { id: playerId },
    data: { isTutorialCompleted: true },
    select: { id: true, isTutorialCompleted: true },
  });
};

export const updatePlayerLastLoginAt = async (playerId: number) => {
  return prisma.player.update({
    where: { id: playerId },
    data: { lastLoginAt: new Date() },
  });
};

export const getBotPlayers = async (count: number = 1) => {
  const bots = await prisma.player.findMany({
    where: { ProviderType: "BOT", IsActive: true },
    include: {
      equipPlayers: true,
    },
  });

  // Shuffle ngẫu nhiên rồi lấy count bot
  for (let i = bots.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [bots[i], bots[j]] = [bots[j], bots[i]];
  }

  return bots.slice(0, count);
};

// export const updatePlayerName = async (playerId: number, playerName: string) => {
//   return prisma.player.update({
//     where: { id: playerId },
//     data: { PlayerName: playerName },
//   });
// };

export const confirmPlayerName = async (
  playerId: number,
  playerName: string,
  companionBallItemId: number
) => {
  return prisma.$transaction(async (tx) => {
    const updatedPlayer = await tx.player.update({
      where: { id: playerId },
      data: { PlayerName: playerName },
    });

    await tx.playerItem.create({
      data: {
        playerId,
        itemId: companionBallItemId,
        seq: 0,
        level: 1,
        SkillGid: generateRandomBallSkillGid(),
        description: '',
        Price: 0,
        IsSolded: 3,
      },
    });

    await tx.equipPlayer.upsert({
      where: { playerId_locationId: { playerId, locationId: 1 } },
      update: { itemId: companionBallItemId, seqItem: 0 },
      create: {
        playerId,
        locationId: 1,
        itemId: companionBallItemId,
        seqItem: 0,
        createdDate: new Date(),
      },
    });

    await createNewPlayerRewards(tx, playerId);

    return updatedPlayer;
  });
};


export const getPlayerByListId = async (ids: number[]) => {
  const players = await prisma.player.findMany({
    where: {
      id: {
        in: ids,
      },
      IsActive: true,
    },
    include: {
      effectPlayers: {
        select: {
          spin: true,
          power: true,
          level: true,
        },
      },
      // Include equipPlayers relation so caller can know equipped items
      equipPlayers: true,
    },
  });

  return players.map((player) => {
    const totals = player.effectPlayers.reduce(
      (acc, ef) => {
        //acc.totalSpin += ef.spin * ef.level;
        //acc.totalPower += ef.power * ef.level;
        acc.totalSpin = 1.5;
        acc.totalPower = 5;
        return acc;
      },
      { totalSpin: 0, totalPower: 0 }
    );
    // totals.totalSpin += 1;
    // totals.totalPower += 3;
    const { effectPlayers, ...rest } = player;
    return { ...rest, ...totals };
  });
};

export const updatePlayerStats = async (
  playerId: number,
  expGain: number,
  ballDelta: number
) => {
  const player = await prisma.player.findFirst({
    where: { id: playerId, IsActive: true },
    select: { Exp: true, Level: true, TalentPoint: true },
  });

  if (!player) {
    throw new Error('Player not found');
  }

  const currentExp = player.Exp ?? 0;
  const currentLevel = player.Level ?? 1;

  const totalExp = currentExp + expGain;

  const levelSteps = [0, 20, 40, 60, 80, 110, 140, 170, 200, 230, 270, 310, 350, 390, 430, 480, 530, 580, 630, 680, 740, 800, 860, 920, 980];

  let newLevel = currentLevel;
  for (let i = levelSteps.length - 1; i >= 0; i--) {
    if (totalExp >= levelSteps[i]) {
      newLevel = i + 1;
      break;
    }
  }
  const levelDiff = newLevel - currentLevel;
  const data: any = {
    Exp: totalExp,
    Level: newLevel,
    RingBall: { increment: ballDelta },
  };

  if (levelDiff > 0) {
    const currentTP = player.TalentPoint ?? 0;
    data.TalentPoint = currentTP + levelDiff;
  }

  return prisma.player.update({
    where: { id: playerId },
    data,
  });
};

export const equipItem = async (
  playerId: number,
  typeGid: number,
  itemId: number,
  seq: number
) => {
  const active = await prisma.player.findFirst({
    where: { id: playerId, IsActive: true },
    select: { id: true },
  });

  if (!active) {
    throw new Error('Player not found or inactive');
  }
  const data: { Ball?: number; Shirt?: number; SeqBall?: number } = {};

  if (typeGid === 1) {
    data.Ball = itemId;
    data.SeqBall = seq;

    const locationId = BALL_SLOT_LOCATION_ID;
    const existing = await (prisma as any).equipPlayer.findFirst({
      where: { playerId, locationId },
      select: { playerId: true },
    });

    if (existing) {
      await (prisma as any).equipPlayer.update({
        where: { playerId_locationId: { playerId, locationId } },
        data: { itemId, seqItem: seq },
      });
    } else {
      await (prisma as any).equipPlayer.create({
        data: { playerId, locationId, itemId, seqItem: seq },
      });
    }
  } else if (typeGid === 2) {
    data.Shirt = itemId;
  } else {
    throw new Error('Unsupported typeGid');
  }

  return prisma.player.update({
    where: { id: playerId },
    data,
  });
};

export const equipPlayerItem = async (
  playerId: number,
  locationId: number,
  itemId: number,
  seqItem: number
) => {
  const existing = await prisma.equipPlayer.findUnique({
    where: { playerId_locationId: { playerId, locationId } },
  });

  if (existing) {
    return prisma.equipPlayer.update({
      where: { playerId_locationId: { playerId, locationId } },
      data: { itemId, seqItem },
    });
  }

  return prisma.equipPlayer.create({
    data: {
      playerId,
      locationId,
      itemId,
      seqItem,
      createdDate: new Date(),
    },
  });
};

export const unequipPlayerItem = async (
  playerId: number,
  locationId: number
) => {
  return prisma.equipPlayer.deleteMany({
    where: { playerId, locationId },
  });
};

export const createAccount = async (idToken: string, playerName: string, avatarUrl?: string) => {
  const MAX_ATTEMPTS = 5;
  for (let attempt = 0; attempt < MAX_ATTEMPTS; attempt++) {
    try {
      return await prisma.$transaction(async (tx) => {
        const loginAt = new Date();
        const existing = await tx.player.findFirst({
          where: { IdAccount: idToken, IsActive: true },
        });

        if (existing) {
          return tx.player.update({
            where: { id: existing.id },
            data: { lastLoginAt: loginAt },
          });
        }

        const player = await tx.player.create({
          data: {
            friendCode: generateFriendCode(),
            IdAccount: idToken,
            PlayerName: playerName,
            AvatarUrl: avatarUrl || null,
            Level: 1,
            Exp: 0,
            Body: 1,
            RingBall: 15,
            GlassShard: 100,
            Money: 0,
            TalentPoint: 0,
            IsActive: true,
            createdAt: loginAt,
            lastLoginAt: loginAt,
          },
        });

        await seedNewPlayerData(tx, player.id);

        return player;
      });
    } catch (error: any) {
      if (error.code === 'P2002') {
        continue; // Retry on unique constraint violation
      }
      throw error;
    }
  }

  throw new Error('Failed to create account');
};


export const loginOrCreateSocialAccount = async (
  firebaseUid: string,
  email: string,
  providerType: string,
  avatarUrl?: string
) => {
  const MAX_ATTEMPTS = 5;
  for (let attempt = 0; attempt < MAX_ATTEMPTS; attempt++) {
    try {
      return await prisma.$transaction(async (tx) => {
        const createdAt = new Date();
        const existing = await tx.player.findFirst({
          where: {
            IdAccount: firebaseUid,
            ProviderType: providerType,
            IsActive: true,
          },
        });

        if (existing) {
          const updates: Prisma.PlayerUpdateInput = {};

          if (email && email !== existing.Email) {
            updates.Email = email;
          }

          if (avatarUrl && avatarUrl !== existing.AvatarUrl) {
            updates.AvatarUrl = avatarUrl;
          }

          return tx.player.update({
            where: { id: existing.id },
            data: updates,
          });
        }

        const player = await tx.player.create({
          data: {
            friendCode: generateFriendCode(),
            IdAccount: firebaseUid,
            Email: email || null,
            ProviderType: providerType,
            PlayerName: email || null,
            AvatarUrl: avatarUrl || null,
            Level: 1,
            Exp: 0,
            Body: 1,
            RingBall: 20,
            GlassShard: 100,
            Money: 0,
            TalentPoint: 0,
            IsActive: true,
            createdAt,
          },
        });

        await seedNewPlayerData(tx, player.id);

        return player;
      });
    } catch (error: any) {
      if (error.code === 'P2002') {
        continue;
      }
      throw error;
    }
  }

  throw new Error('Failed to login or create social account');
};
