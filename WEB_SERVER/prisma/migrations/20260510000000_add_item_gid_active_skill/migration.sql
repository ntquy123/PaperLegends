ALTER TABLE "Item" ADD COLUMN "SkillGid" INTEGER;

CREATE INDEX "Item_SkillGid_idx" ON "Item"("SkillGid");

ALTER TABLE "Item"
ADD CONSTRAINT "Item_SkillGid_fkey"
FOREIGN KEY ("SkillGid") REFERENCES "SysMasGeneral"("GenCode")
ON DELETE SET NULL ON UPDATE CASCADE;
