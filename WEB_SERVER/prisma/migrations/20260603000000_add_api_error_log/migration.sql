CREATE TABLE "ApiErrorLog" (
    "logId" SERIAL NOT NULL,
    "method" TEXT NOT NULL,
    "path" TEXT NOT NULL,
    "statusCode" INTEGER,
    "requestParams" JSONB,
    "errorMessage" TEXT NOT NULL,
    "errorStack" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "ApiErrorLog_pkey" PRIMARY KEY ("logId")
);

CREATE INDEX "ApiErrorLog_createdAt_idx" ON "ApiErrorLog"("createdAt");
CREATE INDEX "ApiErrorLog_method_path_idx" ON "ApiErrorLog"("method", "path");
