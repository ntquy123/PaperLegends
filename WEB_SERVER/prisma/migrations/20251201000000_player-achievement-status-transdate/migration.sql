ALTER TABLE "PlayerAchievementStatus" DROP CONSTRAINT "PlayerAchievementStatus_pkey";

ALTER TABLE "PlayerAchievementStatus"
ADD COLUMN     "TransDate" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_DATE;

UPDATE "PlayerAchievementStatus"
SET "TransDate" = DATE_TRUNC('day', "updatedAt")
WHERE "TransDate" IS NULL;

ALTER TABLE "PlayerAchievementStatus"
ADD CONSTRAINT "PlayerAchievementStatus_pkey" PRIMARY KEY ("playerId", "typeGid", "achievementId", "TransDate");
