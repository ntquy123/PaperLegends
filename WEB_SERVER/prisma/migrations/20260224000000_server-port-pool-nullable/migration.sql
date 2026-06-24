-- Make server port pool columns nullable to allow released rooms
ALTER TABLE "ServerPortPool" ALTER COLUMN "containerId" DROP NOT NULL;
ALTER TABLE "ServerPortPool" ALTER COLUMN "roomNameRef" DROP NOT NULL;
