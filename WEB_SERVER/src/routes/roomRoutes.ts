import express from 'express';
import * as RoomController from '../controllers/roomController';

const router = express.Router();

//router.post('/createroom/', RoomController.createRoom);
router.delete('/deleteroom/:roomId', RoomController.deleteRoom);
router.put('/createroom', RoomController.createRoom);
router.post('/joinRoom',RoomController.joinRoom);
router.post('/leaveRoom',RoomController.leaveRoom);
router.post('/roomUserLeft',RoomController.markRoomUserLeft);
router.get('/getUserRooms',RoomController.getUserRoomsController);
router.put('/updateCreateId',RoomController.updateRoomCreator);
export default router;
