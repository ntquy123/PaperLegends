-- Add GenCate for SysMasGeneral and index it for faster lookup
ALTER TABLE "SysMasGeneral" ADD COLUMN "GenCate" INTEGER;

UPDATE "SysMasGeneral"
SET "GenCate" = CAST(LEFT(CAST("GenCode" AS TEXT), 3) AS INTEGER);

ALTER TABLE "SysMasGeneral" ALTER COLUMN "GenCate" SET NOT NULL;

CREATE INDEX "SysMasGeneral_GenCate_idx" ON "SysMasGeneral"("GenCate");
