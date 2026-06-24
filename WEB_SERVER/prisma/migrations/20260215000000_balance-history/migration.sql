-- CreateTable
CREATE TABLE "BalanceHistory" (
    "userId" INTEGER NOT NULL,
    "seq" INTEGER NOT NULL,
    "ringBall" INTEGER NOT NULL DEFAULT 0,
    "money" INTEGER NOT NULL DEFAULT 0,
    "description" TEXT,
    "eventType" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "BalanceHistory_pkey" PRIMARY KEY ("userId","seq")
);

-- CreateIndex
CREATE INDEX "BalanceHistory_createdAt_idx" ON "BalanceHistory"("createdAt");

-- CreateIndex
CREATE INDEX "BalanceHistory_eventType_idx" ON "BalanceHistory"("eventType");

-- AddForeignKey
ALTER TABLE "BalanceHistory" ADD CONSTRAINT "BalanceHistory_userId_fkey" FOREIGN KEY ("userId") REFERENCES "Player"("id") ON DELETE RESTRICT ON UPDATE CASCADE;
