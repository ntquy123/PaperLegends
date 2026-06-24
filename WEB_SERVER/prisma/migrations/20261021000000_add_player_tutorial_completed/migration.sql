ALTER TABLE "Player"
ADD COLUMN "isTutorialCompleted" BOOLEAN NOT NULL DEFAULT false;

-- Accounts created before the tutorial feature are not first-time players.
UPDATE "Player"
SET "isTutorialCompleted" = true;
