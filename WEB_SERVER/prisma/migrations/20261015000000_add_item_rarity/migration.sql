-- Add rarity GID for items
ALTER TABLE "Item" ADD COLUMN "Rarity_Gid" INTEGER NOT NULL DEFAULT 11300001;

CREATE INDEX "Item_Rarity_Gid_idx" ON "Item"("Rarity_Gid");
