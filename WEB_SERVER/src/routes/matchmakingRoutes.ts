import express from 'express';
import {
  availableRooms,
  getEmptyRoomList,
  getPlayerRoom,
  joinRoom,
  joinRoomBatch,
  leaveRoomController,
  shutdownServers,
} from '../controllers/matchmakingController';

const router = express.Router();

router.get('/availableRooms', availableRooms);// Đảm bảo tồn tại số lượng phòng trống trước khi cho phép người chơi tham gia
router.post('/joinRoom', joinRoom);// 1 người chơi tham gia 1 phòng
router.post('/joinRooms', joinRoomBatch); // sự kiện ghép đôi thành công cho nhiều người chơi
router.post('/leaveRoom', leaveRoomController);// Người chơi rời khỏi phòng
router.get('/emptyRooms', getEmptyRoomList); // Lấy danh sách phòng trống
router.post('/shutdownServers', shutdownServers); // Tắt tất cả các máy chủ trò chơi
router.get('/playerRoom/:playerId', getPlayerRoom); // Lấy thông tin phòng của người chơi

export default router;
