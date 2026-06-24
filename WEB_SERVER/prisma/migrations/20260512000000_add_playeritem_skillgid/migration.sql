ALTER TABLE "PlayerItem"
ADD COLUMN IF NOT EXISTS "SkillGid" INTEGER;

UPDATE "PlayerItem" pi
SET "SkillGid" = i."SkillGid"
FROM "Item" i
WHERE pi."itemId" = i."id"
  AND pi."SkillGid" IS NULL
  AND i."SkillGid" IS NOT NULL;

CREATE INDEX IF NOT EXISTS "PlayerItem_SkillGid_idx"
ON "PlayerItem" ("SkillGid");

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_constraint
    WHERE conname = 'PlayerItem_SkillGid_fkey'
  ) THEN
    ALTER TABLE "PlayerItem"
    ADD CONSTRAINT "PlayerItem_SkillGid_fkey"
    FOREIGN KEY ("SkillGid") REFERENCES "SysMasGeneral"("GenCode")
    ON DELETE SET NULL ON UPDATE CASCADE;
  END IF;
END $$;
