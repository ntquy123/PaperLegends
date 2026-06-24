DO $$
BEGIN
  CREATE TYPE "HeroRole" AS ENUM ('WARRIOR', 'ASSASSIN', 'TANK', 'MAGE', 'SUPPORT', 'MARKSMAN');
EXCEPTION
  WHEN duplicate_object THEN NULL;
END $$;

CREATE TABLE IF NOT EXISTS "Hero" (
  "id" TEXT NOT NULL,
  "code" TEXT NOT NULL,
  "name" TEXT NOT NULL,
  "role" "HeroRole" NOT NULL DEFAULT 'WARRIOR',
  "description" TEXT,
  "hp" INTEGER NOT NULL DEFAULT 100,
  "attack" INTEGER NOT NULL DEFAULT 10,
  "defense" INTEGER NOT NULL DEFAULT 0,
  "speed" DOUBLE PRECISION NOT NULL DEFAULT 1,
  "weight" DOUBLE PRECISION NOT NULL DEFAULT 1,
  "bounce" DOUBLE PRECISION NOT NULL DEFAULT 0.35,
  "friction" DOUBLE PRECISION NOT NULL DEFAULT 0.5,
  "flickForce" DOUBLE PRECISION NOT NULL DEFAULT 1,
  "modelId" TEXT,
  "prefabAddress" TEXT,
  "iconUrl" TEXT,
  "portraitUrl" TEXT,
  "selectionImageUrl" TEXT,
  "sortOrder" INTEGER NOT NULL DEFAULT 0,
  "isActive" BOOLEAN NOT NULL DEFAULT true,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "updatedAt" TIMESTAMP(3) NOT NULL,

  CONSTRAINT "Hero_pkey" PRIMARY KEY ("id")
);

CREATE TABLE IF NOT EXISTS "HeroSkill" (
  "id" TEXT NOT NULL,
  "heroId" TEXT NOT NULL,
  "slot" INTEGER NOT NULL,
  "code" TEXT NOT NULL,
  "name" TEXT NOT NULL,
  "description" TEXT,
  "cooldown" DOUBLE PRECISION NOT NULL DEFAULT 0,
  "manaCost" INTEGER NOT NULL DEFAULT 0,
  "damage" DOUBLE PRECISION NOT NULL DEFAULT 0,
  "range" DOUBLE PRECISION NOT NULL DEFAULT 0,
  "duration" DOUBLE PRECISION NOT NULL DEFAULT 0,
  "config" JSONB,
  "iconUrl" TEXT,
  "vfxCode" TEXT,
  "sfxCode" TEXT,
  "isPassive" BOOLEAN NOT NULL DEFAULT false,
  "isActive" BOOLEAN NOT NULL DEFAULT true,

  CONSTRAINT "HeroSkill_pkey" PRIMARY KEY ("id")
);

ALTER TABLE "Hero" ADD COLUMN IF NOT EXISTS "description" TEXT;
ALTER TABLE "Hero" ADD COLUMN IF NOT EXISTS "modelId" TEXT;
ALTER TABLE "Hero" ADD COLUMN IF NOT EXISTS "prefabAddress" TEXT;
ALTER TABLE "Hero" ADD COLUMN IF NOT EXISTS "selectionImageUrl" TEXT;
ALTER TABLE "Hero" ADD COLUMN IF NOT EXISTS "sortOrder" INTEGER NOT NULL DEFAULT 0;

CREATE UNIQUE INDEX IF NOT EXISTS "Hero_code_key" ON "Hero"("code");
CREATE UNIQUE INDEX IF NOT EXISTS "Hero_modelId_key" ON "Hero"("modelId");
CREATE INDEX IF NOT EXISTS "Hero_role_idx" ON "Hero"("role");
CREATE INDEX IF NOT EXISTS "Hero_isActive_sortOrder_idx" ON "Hero"("isActive", "sortOrder");
CREATE UNIQUE INDEX IF NOT EXISTS "HeroSkill_heroId_slot_key" ON "HeroSkill"("heroId", "slot");
CREATE UNIQUE INDEX IF NOT EXISTS "HeroSkill_heroId_code_key" ON "HeroSkill"("heroId", "code");
CREATE INDEX IF NOT EXISTS "HeroSkill_heroId_idx" ON "HeroSkill"("heroId");
CREATE INDEX IF NOT EXISTS "HeroSkill_isActive_idx" ON "HeroSkill"("isActive");

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_constraint
    WHERE conname = 'HeroSkill_heroId_fkey'
  ) THEN
    ALTER TABLE "HeroSkill"
    ADD CONSTRAINT "HeroSkill_heroId_fkey"
    FOREIGN KEY ("heroId") REFERENCES "Hero"("id")
    ON DELETE CASCADE ON UPDATE CASCADE;
  END IF;
END $$;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_constraint
    WHERE conname = 'HeroSkill_slot_check'
  ) THEN
    ALTER TABLE "HeroSkill"
    ADD CONSTRAINT "HeroSkill_slot_check"
    CHECK ("slot" BETWEEN 1 AND 4);
  END IF;
END $$;
