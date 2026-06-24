import { RequestHandler } from 'express';
import { promises as fs } from 'fs';
import path from 'path';
import prisma from '../models/prismaClient';
import { buildWarmPoolSummary } from '../services/orchestratorWarmPool';
import { DockerOrchestrator, type DockerContainerInfo } from '../services/orchestrator';
import { forceShutdownAllRoomServers } from '../services/matchmakingService';
import { fetchContainerLogs, listRunningContainers, stopRunningContainer } from '../services/dockerService';
import { getServerLogs } from '../services/serverLogBuffer';
import { getApiErrorLogs } from '../services/apiErrorLogService';
import {
  AdminTokenPayload,
  createAdminToken,
  isAdminPasswordConfigured,
  verifyAdminPassword,
} from '../middleware/adminAuth';
import {
  getRedisContainerStatus,
  startRedisContainer,
  stopRedisContainer,
} from '../services/systemDockerService';
import { startMonitorProcess, stopMonitorProcess } from '../services/pm2Service';
import { getMarketOverview, getTopPriceMovers, getRangeDate } from '../services/marketAdminService';
import { isRoomContainerPoolEnabled } from '../config/roomContainerPool';
import { Matchmaker } from '../services/matchmaker';
const parseTypeMatchGid = (value: unknown) => {
  const typeMatchGid = Number(value);
  if (!Number.isInteger(typeMatchGid)) {
    return { error: 'TypeMatchGid không hợp lệ.' } as const;
  }
  return { typeMatchGid } as const;
};

const DEFAULT_REGION = process.env.DEFAULT_REGION || 'asia';
const DS_PORT_RANGE_START = Number(process.env.DS_PORT_START ?? process.env.ROOM_PORT_START) || 27200;
const DS_PORT_RANGE_END = Number(process.env.DS_PORT_END ?? process.env.ROOM_PORT_END) || 27299;
const DS_CONTAINER_PORT = Number(process.env.DS_CONTAINER_PORT) || 27015;

export const loginAdmin: RequestHandler = async (req, res) => {
  const { friendCode, password } = req.body as { friendCode?: unknown; password?: unknown };

  if (typeof friendCode !== 'string' || friendCode.trim() === '') {
    res.status(400).json({ error: 'Vui lòng nhập friendCode hợp lệ.' });
    return;
  }

  if (typeof password !== 'string' || password.length === 0) {
    res.status(400).json({ error: 'Vui lòng nhập mật khẩu quản trị.' });
    return;
  }

  if (!isAdminPasswordConfigured()) {
    res.status(503).json({ error: 'Server chưa cấu hình ADMIN_UI_PASSWORD cho tài khoản quản trị.' });
    return;
  }

  if (!verifyAdminPassword(password)) {
    res.status(401).json({ error: 'Thông tin đăng nhập quản trị không hợp lệ.' });
    return;
  }

  const normalizedFriendCode = friendCode.trim();

  try {
    const player = await prisma.player.findFirst({
      where: {
        friendCode: normalizedFriendCode,
        ProviderType: 'System',
      },
    });

    if (!player) {
      res.status(401).json({ error: 'FriendCode không tồn tại hoặc không phải tài khoản hệ thống.' });
      return;
    }

    await prisma.player.update({
      where: { id: player.id },
      data: { lastLoginAt: new Date() },
    });

    const token = createAdminToken({
      friendCode: player.friendCode,
      playerId: player.id,
      providerType: player.ProviderType ?? null,
      issuedAt: Date.now(),
    });

    res.json({
      token,
      player: {
        id: player.id,
        friendCode: player.friendCode,
        name: player.PlayerName ?? 'System Admin',
      },
    });
  } catch (error) {
    console.error('Lỗi khi đăng nhập admin:', error);
    res.status(500).json({ error: 'Không thể đăng nhập, vui lòng thử lại.' });
  }
};

export const getAdminSession: RequestHandler = async (_req, res) => {
  const admin = res.locals.admin as AdminTokenPayload | undefined;
  res.json({ admin });
};

export const startServers: RequestHandler = async (_req, res) => {
  try {
    const summary = await buildWarmPoolSummary();
    res.json({
      message: 'Đã bật/tăng nhiệt server và phòng chờ.',
      ...summary,
    });
  } catch (error) {
    if (error instanceof Error && error.message.startsWith('Docker start error')) {
      res.status(500).json({ error: 'Không thể khởi động container phòng.', detail: error.message });
      return;
    }

    if (error instanceof Error && error.message === 'DOCKER_NOT_AVAILABLE') {
      res.status(503).json({ error: 'Docker chưa sẵn sàng để khởi động server.' });
      return;
    }

    console.error('Lỗi khi bật server:', error);
    res.status(500).json({ error: 'Không thể bật server.' });
  }
};

export const startTestServer: RequestHandler = async (req, res) => {
  const parsed = parseTypeMatchGid(req.query?.typeMatchGid);
  if ('error' in parsed) {
    res.status(400).json({ error: parsed.error });
    return;
  }
  const region = typeof req.query?.region === 'string' && req.query.region.trim()
    ? req.query.region.trim()
    : DEFAULT_REGION;

  try {
    const containers = await DockerOrchestrator.listManagedContainers({ region });
    const sameType = containers.filter(
      (container) => container.labels.typeMatchGid === String(parsed.typeMatchGid),
    );
    const idleContainers = sameType.filter((container) => container.labels.mode === 'IDLE');
    const busyContainers = sameType.filter((container) => container.labels.mode === 'MATCH');

    if (busyContainers.length > 0 && idleContainers.length === 0) {
      res.status(409).json({ error: 'Phòng test đang bận, vui lòng thử lại sau khi trống.' });
      return;
    }

    let created = false;
    let startedContainer: { containerId: string; name: string; hostPort: number } | null = null;
    if (idleContainers.length === 0) {
      // if (!isRoomContainerPoolEnabled()) {
      //   res.status(503).json({ error: 'Room container pool is disabled.' });
      //   return;
      // }

      startedContainer = await DockerOrchestrator.startDedicatedServer({
        mode: 'IDLE',
        region,
        typeMatchGid: parsed.typeMatchGid,
      });
      created = true;
    }

    res.json({
      message: created
        ? `Đã bật server test (1 phòng trống) với TypeMatchGid = ${parsed.typeMatchGid}.`
        : `Server test đã sẵn sàng với TypeMatchGid = ${parsed.typeMatchGid}.`,
      typeMatchGid: parsed.typeMatchGid,
      region,
      created,
      portRange: {
        start: DS_PORT_RANGE_START,
        end: DS_PORT_RANGE_END,
        containerPort: DS_CONTAINER_PORT,
      },
      container: startedContainer
        ? {
            id: startedContainer.containerId,
            name: startedContainer.name,
            hostPort: startedContainer.hostPort,
          }
        : null,
    });
  } catch (error) {
    if (error instanceof Error && error.message.startsWith('Docker start error')) {
      res
        .status(500)
        .json({ error: 'Không thể khởi động container server test.', detail: error.message });
      return;
    }

    if (error instanceof Error && error.message === 'DOCKER_NOT_AVAILABLE') {
      res.status(503).json({ error: 'Docker chưa sẵn sàng để khởi động server test.' });
      return;
    }

    console.error('Lỗi khi bật server test:', error);
    const detail = error instanceof Error
      ? { message: error.message, stack: error.stack }
      : String(error);
    res.status(500).json({ error: 'Không thể bật server test.', detail });
  }
};

export const shutdownServersAdmin: RequestHandler = async (_req, res) => {
  try {
    let containers: DockerContainerInfo[] = [];

    try {
      containers = await DockerOrchestrator.listManagedContainers({});
    } catch (error) {
      console.warn('Không thể truy vấn container để tắt warm pool, vẫn tiếp tục shutdown:', error);
    }

    const warmPoolResults = await Promise.all(
      containers.map((container) => DockerOrchestrator.tryStopContainerById(container.id)),
    );
    const roomResults = await forceShutdownAllRoomServers();
    const result = {
      ...roomResults,
      stoppedContainers: roomResults.stoppedContainers + warmPoolResults.filter(Boolean).length,
    };
    res.json({
      message: 'Đã dừng toàn bộ server',
      ...result,
    });
  } catch (error) {
    console.error('Lỗi khi tắt server:', error);
    const detail = error instanceof Error ? error.message : 'Unknown error';
    res.status(500).json({ error: 'Không thể tắt server', detail });
  }
};

export const shutdownTestServerController: RequestHandler = async (req, res) => {
  const parsed = parseTypeMatchGid(req.body?.typeMatchGid);
  if ('error' in parsed) {
    res.status(400).json({ error: parsed.error });
    return;
  }

  try {
    const region = typeof req.body?.region === 'string' && req.body.region.trim()
      ? req.body.region.trim()
      : DEFAULT_REGION;

    const containers = await DockerOrchestrator.listManagedContainers({ region });
    const sameType = containers.filter(
      (container) => container.labels.typeMatchGid === String(parsed.typeMatchGid),
    );
    const busyContainers = sameType.filter((container) => container.labels.mode === 'MATCH');
    if (busyContainers.length > 0) {
      res.status(400).json({ error: 'Không thể tắt server test khi phòng đang bận.' });
      return;
    }

    const idleContainers = sameType.filter((container) => container.labels.mode === 'IDLE');
    const stopResults = await Promise.all(
      idleContainers.map((container) => DockerOrchestrator.tryStopContainerById(container.id)),
    );
    const result = {
      deletedRecords: stopResults.filter(Boolean).length,
      stoppedContainers: stopResults.filter(Boolean).length,
    };
    res.json({
      message:
        result.deletedRecords > 0
          ? `Đã tắt server test TypeMatchGid = ${parsed.typeMatchGid} (${region}).`
          : `Không có server test TypeMatchGid = ${parsed.typeMatchGid} nào đang chạy.`,
      typeMatchGid: parsed.typeMatchGid,
      region,
      ...result,
    });
  } catch (error) {
    console.error('Lỗi khi tắt server test:', error);
    const detail = error instanceof Error ? error.message : 'Unknown error';
    res.status(500).json({ error: 'Không thể tắt server test.', detail });
  }
};

export const startRedisServer: RequestHandler = async (_req, res) => {
  try {
    const result = await startRedisContainer();
    res.json(result);
  } catch (error) {
    console.error('Lỗi khi bật Redis:', error);
    const detail = error instanceof Error ? error.message : 'Unknown error';
    res.status(500).json({ error: 'Không thể bật Redis.', detail });
  }
};

export const stopRedisServer: RequestHandler = async (_req, res) => {
  try {
    const result = await stopRedisContainer();
    res.json(result);
  } catch (error) {
    console.error('Lỗi khi tắt Redis:', error);
    const detail = error instanceof Error ? error.message : 'Unknown error';
    res.status(500).json({ error: 'Không thể tắt Redis.', detail });
  }
};

export const getRedisStatus: RequestHandler = async (_req, res) => {
  try {
    const status = await getRedisContainerStatus();
    res.json(status);
  } catch (error) {
    console.error('Lỗi khi kiểm tra Redis:', error);
    const detail = error instanceof Error ? error.message : 'Unknown error';
    res.status(500).json({ error: 'Không thể kiểm tra Redis.', detail });
  }
};

export const startMonitorServer: RequestHandler = async (_req, res) => {
  try {
    const result = await startMonitorProcess();
    res.json(result);
  } catch (error) {
    console.error('Lỗi khi bật paper-legends-monitor:', error);
    const detail = error instanceof Error ? error.message : 'Unknown error';
    res.status(500).json({ error: 'Không thể bật paper-legends-monitor.', detail });
  }
};

export const stopMonitorServer: RequestHandler = async (_req, res) => {
  try {
    const result = await stopMonitorProcess();
    res.json(result);
  } catch (error) {
    console.error('Lỗi khi tắt paper-legends-monitor:', error);
    const detail = error instanceof Error ? error.message : 'Unknown error';
    res.status(500).json({ error: 'Không thể tắt paper-legends-monitor.', detail });
  }
};

export const getReadmeContent: RequestHandler = async (_req, res) => {
  const readmePath = path.join(process.cwd(), 'ReadMe');

  try {
    const content = await fs.readFile(readmePath, 'utf8');
    res.json({ content });
  } catch (error) {
    console.error('Lỗi khi đọc ReadMe:', error);
    res.status(404).json({ error: 'Không tìm thấy nội dung ReadMe.' });
  }
};

export const getRoomOverview: RequestHandler = async (_req, res) => {
  try {
    const [rooms, portPools] = await Promise.all([
      prisma.room.findMany({
        select: {
          id: true,
          roomName: true,
          maxPlayers: true,
          currentPlayers: true,
          bet: true,
          createId: true,
          createDate: true,
          typeMatchGid: true,
          mapId: true,
          roomUsers: {
            select: {
              userId: true,
              joinedAt: true,
              player: {
                select: {
                  PlayerName: true,
                },
              },
            },
            orderBy: { joinedAt: 'desc' },
          },
        },
      }),
      prisma.serverPortPool.findMany({
        select: {
          portNo: true,
          isBusy: true,
          lastUpdate: true,
          containerId: true,
          roomNameRef: true,
          typeMatchGid: true,
        },
      }),
    ]);

    const typeMatchGids = [
      ...new Set([
        ...rooms.map((room) => room.typeMatchGid),
        ...portPools.map((pool) => pool.typeMatchGid),
      ]),
    ];

    const creatorIds = [...new Set(rooms.map((room) => room.createId))];

    const [generalTypes, creators] = await Promise.all([
      typeMatchGids.length
        ? prisma.sysMasGeneral.findMany({
            where: { GenCode: { in: typeMatchGids } },
            select: { GenCode: true, GenName: true },
          })
        : Promise.resolve([]),
      creatorIds.length
        ? prisma.player.findMany({
            where: { id: { in: creatorIds } },
            select: { id: true, PlayerName: true },
          })
        : Promise.resolve([]),
    ]);

    const poolMap = new Map(
      portPools
        .filter((pool) => pool.roomNameRef)
        .map((pool) => [pool.roomNameRef as string, pool]),
    );

    const typeMatchMap = new Map(generalTypes.map((type) => [type.GenCode, type.GenName]));
    const creatorMap = new Map(creators.map((player) => [player.id, player.PlayerName]));

    const overview = rooms.map((room) => {
      const pool = poolMap.get(room.roomName) ?? null;

      return {
        roomId: room.id,
        roomName: room.roomName,
        maxPlayers: room.maxPlayers,
        currentPlayers: room.currentPlayers,
        bet: room.bet,
        createId: room.createId,
        createPlayerName: creatorMap.get(room.createId) ?? null,
        createDate: room.createDate,
        roomTypeMatchGid: room.typeMatchGid,
        roomTypeName: typeMatchMap.get(room.typeMatchGid) ?? null,
        mapId: room.mapId,
        roomUsers: room.roomUsers.map((roomUser) => ({
          playerId: roomUser.userId,
          playerName: roomUser.player?.PlayerName ?? null,
          joinedAt: roomUser.joinedAt,
        })),
        portNo: pool?.portNo ?? null,
        isBusy: pool?.isBusy ?? null,
        lastUpdate: pool?.lastUpdate ?? null,
        containerId: pool?.containerId ?? null,
        roomNameRef: pool?.roomNameRef ?? null,
        poolTypeMatchGid: pool?.typeMatchGid ?? null,
        poolTypeName: pool ? typeMatchMap.get(pool.typeMatchGid) ?? null : null,
      };
    });

    res.json({ rooms: overview });
  } catch (error) {
    console.error('Lỗi khi lấy danh sách phòng:', error);
    const detail = error instanceof Error ? error.message : 'Unknown error';
    res.status(500).json({ error: 'Không thể lấy danh sách phòng.', detail });
  }
};

export const getMatchmakingQueueOverview: RequestHandler = async (_req, res) => {
  try {
    const snapshot = Matchmaker.instance.getSearchingPlayersSnapshot();
    const playerIds = [
      ...new Set([
        ...snapshot.queued.map((entry) => entry.playerId),
        ...snapshot.activeMatches.map((entry) => entry.playerId),
      ]),
    ];
    const typeMatchGids = [
      ...new Set([
        ...snapshot.queued.map((entry) => entry.typeMatchGid),
        ...snapshot.activeMatches.map((entry) => entry.typeMatchGid),
      ].filter((value) => Number.isFinite(value))),
    ];

    const [players, generalTypes] = await Promise.all([
      playerIds.length
        ? prisma.player.findMany({
            where: { id: { in: playerIds } },
            select: { id: true, PlayerName: true, friendCode: true, ProviderType: true, AvatarUrl: true },
          })
        : Promise.resolve([]),
      typeMatchGids.length
        ? prisma.sysMasGeneral.findMany({
            where: { GenCode: { in: typeMatchGids } },
            select: { GenCode: true, GenName: true },
          })
        : Promise.resolve([]),
    ]);

    const playerMap = new Map(players.map((player) => [player.id, player]));
    const typeMatchMap = new Map(generalTypes.map((type) => [type.GenCode, type.GenName]));
    const enrichPlayer = (playerId: number) => {
      const player = playerMap.get(playerId);
      return {
        playerId,
        playerName: player?.PlayerName ?? null,
        friendCode: player?.friendCode ?? null,
        providerType: player?.ProviderType ?? null,
        avatarUrl: player?.AvatarUrl ?? null,
        isOnline: true,
      };
    };

    const queued = snapshot.queued.map((entry) => ({
      ...entry,
      ...enrichPlayer(entry.playerId),
      typeMatchName: typeMatchMap.get(entry.typeMatchGid) ?? null,
      startedAt: entry.queuedAt,
    }));

    const activeMatches = snapshot.activeMatches.map((entry) => ({
      ...entry,
      ...enrichPlayer(entry.playerId),
      typeMatchName: typeMatchMap.get(entry.typeMatchGid) ?? null,
      startedAt: entry.createdAt,
    }));

    res.json({
      queued,
      activeMatches,
      totalSearchingPlayers: snapshot.totalSearchingPlayers,
      queueCount: queued.length,
      activeMatchPlayerCount: activeMatches.length,
      bucketCount: snapshot.bucketCount,
      activeMatchCount: snapshot.activeMatchCount,
      serverTime: Date.now(),
    });
  } catch (error) {
    console.error('Lỗi khi lấy danh sách người chơi đang tìm trận:', error);
    const detail = error instanceof Error ? error.message : 'Unknown error';
    res.status(500).json({ error: 'Không thể lấy danh sách người chơi đang tìm trận.', detail });
  }
};

export const getAdminMarketOverview: RequestHandler = async (req, res) => {
  try {
    const range = String(req.query.range ?? '24h');
    const data = await getMarketOverview(range);
    res.json(data);
  } catch (error) {
    res.status(500).json({ error: 'Không thể tải market overview.' });
  }
};

export const getAdminMarketTopGainers: RequestHandler = async (req, res) => {
  try {
    const range = String(req.query.range ?? '24h');
    const limit = Math.min(Number(req.query.limit ?? 10), 50);
    res.json({ items: await getTopPriceMovers(range, limit, 'gainers') });
  } catch {
    res.status(500).json({ error: 'Không thể tải top gainers.' });
  }
};

export const getAdminMarketTopLosers: RequestHandler = async (req, res) => {
  try {
    const range = String(req.query.range ?? '24h');
    const limit = Math.min(Number(req.query.limit ?? 10), 50);
    res.json({ items: await getTopPriceMovers(range, limit, 'losers') });
  } catch {
    res.status(500).json({ error: 'Không thể tải top losers.' });
  }
};

export const getAdminItemHistory: RequestHandler = async (req, res) => {
  try {
    const itemId = Number(req.params.itemId);
    const range = String(req.query.range ?? '24h');
    const from = getRangeDate(range);
    const history = await prisma.itemPriceHistory.findMany({
      where: { itemId, createdAt: { gte: from } },
      orderBy: { createdAt: 'asc' },
      take: 1000,
    });
    res.json({ itemId, history });
  } catch {
    res.status(500).json({ error: 'Không thể tải lịch sử giá item.' });
  }
};

export const getAdminRecentMarketEvents: RequestHandler = async (req, res) => {
  try {
    const limit = Math.min(Number(req.query.limit ?? 20), 100);
    const events = await prisma.itemTradeHistory.findMany({
      orderBy: { createdAt: 'desc' },
      take: limit,
    });
    const playerIds = [...new Set(events.flatMap((e) => [e.playerIdBuy, e.playerIdSold]))];
    const itemIds = [...new Set(events.map((e) => e.itemId))];
    const [players, items] = await Promise.all([
      prisma.player.findMany({ where: { id: { in: playerIds } }, select: { id: true, PlayerName: true } }),
      prisma.item.findMany({ where: { id: { in: itemIds } }, select: { id: true, name: true } }),
    ]);
    const pMap = new Map(players.map((p) => [p.id, p.PlayerName ?? `#${p.id}`]));
    const iMap = new Map(items.map((i) => [i.id, i.name]));
    res.json({
      events: events.map((e) => ({
        ...e,
        buyerName: pMap.get(e.playerIdBuy) ?? `#${e.playerIdBuy}`,
        sellerName: pMap.get(e.playerIdSold) ?? `#${e.playerIdSold}`,
        itemName: iMap.get(e.itemId) ?? `Item ${e.itemId}`,
      })),
    });
  } catch {
    res.status(500).json({ error: 'Không thể tải recent market transactions.' });
  }
};

export const getActiveContainers: RequestHandler = async (_req, res) => {
  try {
    const containers = await listRunningContainers();
    const isSystemContainer = (container: { name?: string; image?: string }) => {
      const signature = `${container.name ?? ''} ${container.image ?? ''}`.toLowerCase();
      return signature.includes('redis');
    };
    const parseUdpPorts = (ports: string): number[] => {
      if (!ports) return [];
      return ports
        .split(',')
        .map((segment) => segment.trim())
        .filter((segment) => segment.includes('/udp'))
        .map((segment) => {
          if (segment.includes('->')) {
            const [hostPart] = segment.split('->');
            const hostPortMatch = hostPart.trim().match(/:(\d+)$/);
            const portNo = hostPortMatch ? Number(hostPortMatch[1]) : Number.NaN;
            return Number.isFinite(portNo) ? portNo : null;
          }

          const directMatch = segment.match(/(\d+)\/udp$/);
          const portNo = directMatch ? Number(directMatch[1]) : Number.NaN;
          return Number.isFinite(portNo) ? portNo : null;
        })
        .filter((portNo): portNo is number => portNo !== null);
    };

    const containerUdpPorts = containers.map((container) => ({
      container,
      udpPorts: parseUdpPorts(container.ports),
    }));

    const portNos = [
      ...new Set(containerUdpPorts.flatMap(({ udpPorts }) => udpPorts).filter((portNo) => portNo)),
    ];

    const portPools = portNos.length
      ? await prisma.serverPortPool.findMany({
          where: { portNo: { in: portNos } },
          select: {
            portNo: true,
            isBusy: true,
            roomNameRef: true,
            typeMatchGid: true,
          },
        })
      : [];

    const typeMatchGids = [...new Set(portPools.map((pool) => pool.typeMatchGid))];
    const roomNameRefs = [...new Set(portPools.map((pool) => pool.roomNameRef).filter(Boolean))];

    const [generalTypes, rooms] = await Promise.all([
      typeMatchGids.length
        ? prisma.sysMasGeneral.findMany({
            where: { GenCode: { in: typeMatchGids } },
            select: { GenCode: true, GenName: true },
          })
        : Promise.resolve([]),
      roomNameRefs.length
        ? prisma.room.findMany({
            where: { roomName: { in: roomNameRefs } },
            select: { roomName: true },
          })
        : Promise.resolve([]),
    ]);

    const portPoolMap = new Map<number, (typeof portPools)[number]>();
    portPools.forEach((pool) => {
      portPoolMap.set(pool.portNo, pool);
    });

    const typeMap = new Map(generalTypes.map((type) => [type.GenCode, type.GenName]));
    const roomNameSet = new Set(rooms.map((room) => room.roomName));

    const enhancedContainers = containerUdpPorts.map(({ container, udpPorts }) => {
      const pool = udpPorts.map((portNo) => portPoolMap.get(portNo)).find(Boolean);
      const roomTypeName = pool ? typeMap.get(pool.typeMatchGid) ?? 'Không có trong config' : 'Không có data pool';
      const isBusy = pool ? pool.isBusy === 1 : null;
      const hasStarted = pool?.roomNameRef ? roomNameSet.has(pool.roomNameRef) : false;
      const category = isSystemContainer(container) ? 'system' : 'game';

      return {
        ...container,
        roomTypeName,
        isBusy,
        hasStarted,
        typeMatchGid: pool?.typeMatchGid ?? null,
        roomNameRef: pool?.roomNameRef ?? null,
        category,
      };
    });

    res.json({ containers: enhancedContainers });
  } catch (error) {
    console.error('Lỗi khi lấy danh sách docker đang chạy:', error);
    const detail = error instanceof Error ? error.message : 'Unknown error';
    res.status(500).json({ error: 'Không thể lấy danh sách docker đang chạy.', detail });
  }
};

export const getContainerLogs: RequestHandler = async (req, res) => {
  const containerId = req.params.id;
  const tail = Number.parseInt((req.query.tail as string) ?? '200', 10);

  if (!containerId) {
    res.status(400).json({ error: 'Thiếu containerId để xem log.' });
    return;
  }

  try {
    const logs = await fetchContainerLogs(containerId, tail);
    res.json({ logs });
  } catch (error) {
    const detail = error instanceof Error ? error.message : 'Unknown error';
    const isNotFound = detail.includes("No such container") || detail.includes("không tồn tại");
    if (!isNotFound) {
      console.error(`Lỗi khi lấy log cho container ${containerId}:`, error);
    }
    res.status(isNotFound ? 404 : 500).json({ error: 'Không thể lấy log container.', detail });
  }
};

const parseContainerHostPorts = (ports: string): number[] => {
  if (!ports) return [];

  return ports
    .split(',')
    .map((segment) => segment.trim())
    .map((segment) => {
      const mappedMatch = segment.match(/:(\d+)->\d+\/(?:tcp|udp)/i);
      if (mappedMatch?.[1]) return Number(mappedMatch[1]);

      const directMatch = segment.match(/^(\d+)\/(?:tcp|udp)$/i);
      if (directMatch?.[1]) return Number(directMatch[1]);

      return null;
    })
    .filter((portNo): portNo is number => Number.isInteger(portNo));
};

export const stopContainerController: RequestHandler = async (req, res) => {
  const containerIdOrName = req.params.id;

  if (!containerIdOrName) {
    res.status(400).json({ error: 'Thieu containerId de stop.' });
    return;
  }

  try {
    const containers = await listRunningContainers();
    const matchedContainer = containers.find(
      (container) => container.id === containerIdOrName || container.name === containerIdOrName,
    );

    if (!matchedContainer) {
      res.status(404).json({ error: 'Container khong ton tai hoac khong con chay.' });
      return;
    }

    const hostPorts = parseContainerHostPorts(matchedContainer.ports);
    const poolWhere = {
      OR: [
        { containerId: matchedContainer.id },
        { roomNameRef: matchedContainer.name },
        ...(hostPorts.length ? [{ portNo: { in: hostPorts } }] : []),
      ],
    };

    const affectedPools = await prisma.serverPortPool.findMany({
      where: poolWhere,
      select: { roomNameRef: true },
    });
    const roomNames = [
      ...new Set(
        affectedPools
          .map((pool) => pool.roomNameRef)
          .filter((roomNameRef): roomNameRef is string => Boolean(roomNameRef)),
      ),
    ];

    const stopResult = await stopRunningContainer(matchedContainer.id);

    await prisma.$transaction([
      ...(roomNames.length
        ? [
            prisma.roomUser.deleteMany({ where: { room: { roomName: { in: roomNames } } } }),
            prisma.room.deleteMany({ where: { roomName: { in: roomNames } } }),
          ]
        : []),
      prisma.serverPortPool.deleteMany({ where: poolWhere }),
    ]);

    res.json({
      stopped: true,
      message: `Da stop container ${matchedContainer.name || matchedContainer.id}.`,
      container: matchedContainer,
      dockerOutput: stopResult.output,
      releasedPorts: hostPorts,
      deletedRooms: roomNames,
    });
  } catch (error) {
    const detail = error instanceof Error ? error.message : 'Unknown error';
    console.error(`Loi khi stop container ${containerIdOrName}:`, error);
    res.status(500).json({ error: 'Khong the stop container.', detail });
  }
};

export const getServerLogsController: RequestHandler = (req, res) => {
  const tail = Number.parseInt((req.query.tail as string) ?? '200', 10);
  const safeTail = Number.isFinite(tail) && tail > 0 ? Math.min(tail, 2000) : 200;

  res.json({ logs: getServerLogs(safeTail) });
};

export const getApiErrorLogsController: RequestHandler = async (req, res) => {
  const page = Number.parseInt((req.query.page as string) ?? '1', 10);
  const pageSize = Number.parseInt((req.query.pageSize as string) ?? '50', 10);
  const search = typeof req.query.search === 'string' ? req.query.search : '';
  const from = typeof req.query.from === 'string' && req.query.from ? new Date(req.query.from) : undefined;
  const to = typeof req.query.to === 'string' && req.query.to ? new Date(req.query.to) : undefined;

  try {
    const result = await getApiErrorLogs({
      page,
      pageSize,
      search,
      from,
      to,
    });

    res.json(result);
  } catch (error) {
    console.error('Failed to load API error logs:', error);
    res.status(500).json({ error: 'Cannot load API error logs.' });
  }
};
