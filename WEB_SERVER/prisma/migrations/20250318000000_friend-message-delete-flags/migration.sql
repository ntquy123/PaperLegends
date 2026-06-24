-- AlterTable
ALTER TABLE "FriendMessage" ADD COLUMN     "isSenderDelete" BOOLEAN NOT NULL DEFAULT false,
ADD COLUMN     "isReceiverDelete" BOOLEAN NOT NULL DEFAULT false;

