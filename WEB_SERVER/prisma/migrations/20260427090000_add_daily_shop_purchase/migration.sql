CREATE TABLE "DailyShopPurchase" (
    "userId" INTEGER NOT NULL,
    "itemId" INTEGER NOT NULL,
    "purchaseDate" DATE NOT NULL DEFAULT CURRENT_DATE,
    "purchaseCount" INTEGER NOT NULL DEFAULT 0,
    "maxPurchasePerDay" INTEGER NOT NULL,

    CONSTRAINT "DailyShopPurchase_pkey" PRIMARY KEY ("userId", "itemId", "purchaseDate")
);

ALTER TABLE "DailyShopPurchase"
ADD CONSTRAINT "DailyShopPurchase_userId_fkey"
FOREIGN KEY ("userId") REFERENCES "Player"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

ALTER TABLE "DailyShopPurchase"
ADD CONSTRAINT "DailyShopPurchase_itemId_fkey"
FOREIGN KEY ("itemId") REFERENCES "Item"("id") ON DELETE RESTRICT ON UPDATE CASCADE;
