-- AlterTable
ALTER TABLE "PlayerAchievementStatus" DROP CONSTRAINT "PlayerAchievementStatus_pkey",
ALTER COLUMN "typeGid" SET DATA TYPE TEXT,
ADD CONSTRAINT "PlayerAchievementStatus_pkey" PRIMARY KEY ("playerId", "typeGid", "achievementId");

-- AddForeignKey
ALTER TABLE "PlayerAchievementStatus" ADD CONSTRAINT "PlayerAchievementStatus_typeGid_achievementId_fkey" FOREIGN KEY ("typeGid", "achievementId") REFERENCES "PlayerAchievement"("rewardType", "seq") ON DELETE RESTRICT ON UPDATE CASCADE;

