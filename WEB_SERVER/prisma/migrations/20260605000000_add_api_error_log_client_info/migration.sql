ALTER TABLE "ApiErrorLog"
ADD COLUMN "ipAddress" TEXT,
ADD COLUMN "userAgent" TEXT;

CREATE INDEX "ApiErrorLog_ipAddress_createdAt_idx" ON "ApiErrorLog"("ipAddress", "createdAt");
