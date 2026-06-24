ALTER TABLE "PlayerAchievement" DROP CONSTRAINT IF EXISTS "PlayerAchievement_playerId_fkey";
ALTER TABLE "PlayerAchievement" DROP COLUMN IF EXISTS "playerId";
