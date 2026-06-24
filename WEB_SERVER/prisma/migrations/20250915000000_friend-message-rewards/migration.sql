-- AlterTable
ALTER TABLE "FriendMessage" ADD COLUMN     "itemRewardId" INTEGER,
ADD COLUMN     "moneyReward" INTEGER NOT NULL DEFAULT 0,
ADD COLUMN     "ringBallReward" INTEGER NOT NULL DEFAULT 0;

