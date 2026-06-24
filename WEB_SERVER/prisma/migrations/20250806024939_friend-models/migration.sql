-- -- CreateTable
-- CREATE TABLE "FriendRequest" (
--     "senderId" INTEGER NOT NULL,
--     "receiverId" INTEGER NOT NULL,
--     "status" TEXT NOT NULL DEFAULT 'PENDING',
--     "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

--     CONSTRAINT "FriendRequest_pkey" PRIMARY KEY ("senderId","receiverId")
-- );

-- -- CreateTable
-- CREATE TABLE "Friendship" (
--     "playerId" INTEGER NOT NULL,
--     "friendId" INTEGER NOT NULL,
--     "since" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

--     CONSTRAINT "Friendship_pkey" PRIMARY KEY ("playerId","friendId")
-- );

-- -- CreateTable
-- CREATE TABLE "FriendMessage" (
--     "id" SERIAL NOT NULL,
--     "senderId" INTEGER NOT NULL,
--     "receiverId" INTEGER NOT NULL,
--     "message" TEXT NOT NULL,
--     "itemId" INTEGER,
--     "seqId" INTEGER,
--     "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

--     CONSTRAINT "FriendMessage_pkey" PRIMARY KEY ("id")
-- );

-- -- AddForeignKey
-- ALTER TABLE "FriendRequest" ADD CONSTRAINT "FriendRequest_senderId_fkey" FOREIGN KEY ("senderId") REFERENCES "Player"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- -- AddForeignKey
-- ALTER TABLE "FriendRequest" ADD CONSTRAINT "FriendRequest_receiverId_fkey" FOREIGN KEY ("receiverId") REFERENCES "Player"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- -- AddForeignKey
-- ALTER TABLE "Friendship" ADD CONSTRAINT "Friendship_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- -- AddForeignKey
-- ALTER TABLE "Friendship" ADD CONSTRAINT "Friendship_friendId_fkey" FOREIGN KEY ("friendId") REFERENCES "Player"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- -- AddForeignKey
-- ALTER TABLE "FriendMessage" ADD CONSTRAINT "FriendMessage_senderId_fkey" FOREIGN KEY ("senderId") REFERENCES "Player"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- -- AddForeignKey
-- ALTER TABLE "FriendMessage" ADD CONSTRAINT "FriendMessage_receiverId_fkey" FOREIGN KEY ("receiverId") REFERENCES "Player"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

