import { RequestHandler } from 'express';
import * as RoomService from '../services/roomService';
import { createWebSocketEmitter } from '../websocket/registry';

const broadcastRoomUsersUpdate = async (roomId: number) => {
  try {
    const users = await RoomService.getRoomUsersSnapshot(roomId);
    const emitter = createWebSocketEmitter();
    const playerIds = users
      .map((user) => user.userId)
      .filter((id): id is number => Number.isFinite(id));

    playerIds.forEach((userId) => {
      emitter.to(`user:${userId}`).emit('room_users', { roomId, users });
    });
  } catch (error) {
    console.warn('Không thể broadcast danh sách người chơi trong phòng:', error);
  }
};

export const createRoom: RequestHandler = async (req, res) => {
  try {
    const { userId, bet, maxPlayer, mapId, roomName, maxRound, rounds } = req.body;
    const parsedMaxRound = typeof maxRound === 'number'
      ? maxRound
      : Number(maxRound ?? rounds) || undefined;
    const room = await RoomService.createRoom({ userId, bet, maxPlayer, mapId, roomName, maxRound: parsedMaxRound });

    res.json(room);
    if (room?.roomId) {
      void broadcastRoomUsersUpdate(room.roomId);
    }
  } catch (error) {
    console.error('💥 Lỗi trong createRoom:', error);
    if (error instanceof Error && error.message === 'NO_AVAILABLE_PORT') {
      res.status(503).json({ error: 'Không còn cổng trống để tạo phòng' });
      return;
    }
    if (error instanceof Error && error.message === 'ROOM_NOT_READY') {
      res.status(404).json({ error: 'Không tìm thấy phòng đã được chuẩn bị sẵn' });
      return;
    }
    if (error instanceof Error && error.message === 'ROOM_TYPE_MISMATCH') {
      res.status(400).json({ error: 'Phòng không thuộc loại MatchRoom' });
      return;
    }
    res.status(500).json({ error: error.message || 'Database error' });
  }
};

export const getRooms: RequestHandler = async (_req, res) => {
  try {
    const rooms = await RoomService.getActiveRooms();
    res.json(rooms);
  } catch (error) {
    res.status(500).json({ error: 'Database error' });
  }
};

export const updateRoomCreator: RequestHandler = async (req, res) => {
  try {
    const { roomId, userId } = req.body;

    const roomIdNumber = Number(roomId);
    const userIdNumber = Number(userId);

    if (Number.isNaN(roomIdNumber) || Number.isNaN(userIdNumber)) {
      res.status(400).json({ error: 'Invalid roomId or userId' });
      return;
    }

    const result = await RoomService.updateRoomCreator(roomIdNumber, userIdNumber);

    res.json(result);
    return;
  } catch (error) {
    console.error('💥 Lỗi trong updateRoomCreator:', error);
    if (error instanceof Error && error.message === 'ROOM_NOT_FOUND') {
      res.status(404).json({ error: 'Room not found' });
      return;
    }

    if (error instanceof Error && error.message === 'USER_NOT_IN_ROOM') {
      res.status(400).json({ error: 'User is not in the room' });
      return;
    }

    res.status(500).json({ error: error.message || 'Database error' });
  }
};

export const deleteRoom: RequestHandler = async (
  req,
  res
): Promise<void> => {
  try {
    const roomId = Number(req.params.roomId);
    if (isNaN(roomId)) {
      res.status(400).json({ error: 'Invalid roomId' });
      return;
    }
    await RoomService.deleteRoom(roomId);
    res.json({ message: 'Room deleted' });
    return;
  } catch (error) {
    res.status(500).json({ error: 'Room not found or error' });
    return;
  }
};

export const leaveRoom: RequestHandler = async (
  req,
  res
): Promise<void> => {
  try {
    // Lấy dữ liệu từ body
    const { roomId, userId } = req.body;

    const roomIdNumber = Number(roomId);
    const userIds = Array.isArray(userId) ? userId : [];
    const parsedUserIds = userIds.map((id) => Number(id));

    // Kiểm tra nếu roomId hoặc userId không hợp lệ
    if (
      Number.isNaN(roomIdNumber) ||
      parsedUserIds.length === 0 ||
      parsedUserIds.some((id) => Number.isNaN(id))
    ) {
      res.status(400).json({ error: 'Invalid roomId or userId' });
      return;
    }

    // Gọi service để xóa người dùng khỏi phòng
    const result = await RoomService.leaveRoom(roomIdNumber, parsedUserIds);

    // Trả về kết quả thành công
    res.json(result);
    void broadcastRoomUsersUpdate(roomIdNumber);
    return;
  } catch (error) {
    console.error('💥 Lỗi trong leaveRoom:', error);
    if (error instanceof Error && error.message === 'ROOM_NOT_FOUND') {
      res.status(404).json({ error: 'Room not found' });
      return;
    }

    res.status(500).json({ error: error.message || 'Room not found or error' });
    return;
  }
};

export const joinRoom: RequestHandler = async (
  req,
  res
): Promise<void> => {
  try {
     // Lấy dữ liệu từ body
     const { roomId, userId } = req.body;
     const roomIdNumber = Number(roomId);
     const userIdNumber = Number(userId);

    // Kiểm tra nếu roomId hoặc userId không hợp lệ
    if (Number.isNaN(roomIdNumber) || Number.isNaN(userIdNumber)) {
      res.status(400).json({ error: 'Invalid roomId or userId' });
      return;
    }
    const room = await RoomService.joinRoom(roomIdNumber, userIdNumber);

    // Trả về kết quả thành công
    res.json(room);
    void broadcastRoomUsersUpdate(roomIdNumber);
    return;
  } catch (error) {
    console.error('💥 Lỗi :', error);
    if (error instanceof Error && error.message === 'ROOM_NOT_FOUND') {
      res.status(404).json({ error: 'Room not found' });
      return;
    }

    if (error instanceof Error && error.message === 'ROOM_FULL') {
      res.status(409).json({ error: 'Room is full' });
      return;
    }

    if (error instanceof Error && error.message === 'NOT_ENOUGH_RINGBALL') {
      res.status(400).json({ error: 'Not enough RingBall to join the room' });
      return;
    }

    res.status(500).json({ error: error.message || 'Room not found or error' });
    return;
  }
};

export const markRoomUserLeft: RequestHandler = async (
  req,
  res
): Promise<void> => {
  try {
    const { roomId, userId } = req.body;
    const roomIdNumber = Number(roomId);
    const userIdNumber = Number(userId);

    if (Number.isNaN(roomIdNumber) || Number.isNaN(userIdNumber)) {
      res.status(400).json({ error: 'Invalid roomId or userId' });
      return;
    }

    const result = await RoomService.markRoomUserLeft(roomIdNumber, userIdNumber);
    res.json(result);
    if (result.updated) {
      void broadcastRoomUsersUpdate(roomIdNumber);
    }
    return;
  } catch (error) {
    console.error('💥 Lỗi trong markRoomUserLeft:', error);
    res.status(500).json({ error: error instanceof Error ? error.message : 'Database error' });
    return;
  }
};

export const getUserRoomsController: RequestHandler = async (
  req,
  res
): Promise<void> => {
  try {
    const roomId = Number(req.query.roomId);   

    // Kiểm tra nếu roomId không hợp lệ
    if (isNaN(roomId)) {
      res.status(400).json({ error: 'Invalid roomId' });
      return;
    }

    // Gọi service để lấy danh sách người dùng trong phòng
    const users = await RoomService.getUserRooms(roomId);

    // Trả về danh sách người dùng
    res.json(users);
    return;
  } catch (error) {
    console.error('💥 Lỗi trong getUserRoomsController:', error);
    res.status(500).json({ error: error.message || 'Internal server error' });
    return;
  }
};
