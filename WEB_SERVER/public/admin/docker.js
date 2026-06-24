const sessionState = document.getElementById('session-state');
const adminName = document.getElementById('admin-name');
const adminMeta = document.getElementById('admin-meta');
const logoutButton = document.getElementById('logout');
const backDashboardButton = document.getElementById('back-dashboard');
const dockerGrid = document.getElementById('docker-grid');
const dockerCount = document.getElementById('docker-count');
const refreshButton = document.getElementById('refresh-docker');
const searchInput = document.getElementById('docker-search');
const dockerSystemGrid = document.getElementById('docker-system-grid');
const dockerSystemCount = document.getElementById('docker-system-count');
const refreshSystemButton = document.getElementById('refresh-docker-system');
const paginationInfo = document.getElementById('pagination-info');
const prevPageButton = document.getElementById('prev-page');
const nextPageButton = document.getElementById('next-page');
const roomGrid = document.getElementById('room-grid');
const roomCount = document.getElementById('room-count');
const refreshRoomsButton = document.getElementById('refresh-rooms');
const roomSearchInput = document.getElementById('room-search');
const roomPaginationInfo = document.getElementById('room-pagination-info');
const roomPrevPageButton = document.getElementById('room-prev-page');
const roomNextPageButton = document.getElementById('room-next-page');
const matchmakingGrid = document.getElementById('matchmaking-grid');
const matchmakingCount = document.getElementById('matchmaking-count');
const refreshMatchmakingButton = document.getElementById('refresh-matchmaking');
const matchmakingSearchInput = document.getElementById('matchmaking-search');
const matchmakingPaginationInfo = document.getElementById('matchmaking-pagination-info');
const matchmakingPrevPageButton = document.getElementById('matchmaking-prev-page');
const matchmakingNextPageButton = document.getElementById('matchmaking-next-page');
const loadingBackdrop = document.getElementById('loading-backdrop');
const loadingText = document.getElementById('loading-text');
const toast = document.getElementById('toast');
const logModal = document.getElementById('log-modal');
const logTitle = document.getElementById('log-title');
const logContent = document.getElementById('log-content');
const closeLogButton = document.getElementById('close-log');
const tabButtons = document.querySelectorAll('.tab-button');
const tabPanels = document.querySelectorAll('.tab-panel');

const TOKEN_KEY = 'admin_ui_token';
const ADMIN_API_BASE = window.location.pathname.startsWith('/paper-legends/')
  ? '/paper-legends/api/admin'
  : '/api/admin';
const adminApiUrl = (endpoint) => `${ADMIN_API_BASE}/${endpoint.toString().replace(/^\/+/, '')}`;
const PAGE_SIZE = 10;
const MATCHMAKING_REFRESH_MS = 2500;

let containers = [];
let searchTerm = '';
let currentPage = 1;
let rooms = [];
let roomSearchTerm = '';
let roomCurrentPage = 1;
let matchmakingPlayers = [];
let matchmakingSearchTerm = '';
let matchmakingCurrentPage = 1;
let activeTabId = 'containers';
let matchmakingRefreshTimer = null;

const HTML_ESCAPE_MAP = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
};

const escapeHtml = (value = '') => value.toString().replace(/[&<>"']/g, (char) => HTML_ESCAPE_MAP[char] || char);
const formatDateTime = (value) => {
  if (!value) return '—';
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return value.toString();
  return new Intl.DateTimeFormat('vi-VN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(date);
};
const formatDuration = (startedAt, serverTime = Date.now()) => {
  if (!startedAt) return 'â€”';
  const elapsedMs = Math.max(0, Number(serverTime) - Number(startedAt));
  const totalSeconds = Math.floor(elapsedMs / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return minutes > 0 ? `${minutes}p ${seconds}s` : `${seconds}s`;
};
const formatRoomUsers = (roomUsers = []) => {
  if (!Array.isArray(roomUsers) || roomUsers.length === 0) return '—';
  return roomUsers
    .map((user) => {
      const playerId = escapeHtml(user?.playerId ?? '—');
      const playerName = user?.playerName ? ` · ${escapeHtml(user.playerName)}` : '';
      const joinedAt = user?.joinedAt ? ` (${escapeHtml(formatDateTime(user.joinedAt))})` : '';
      return `${playerId}${playerName}${joinedAt}`;
    })
    .join('<br />');
};

const parsePortMapping = (mapping = '') => {
  const value = mapping.trim();
  if (!value) return null;

  const hostToContainerMatch = value.match(/^(.+)->(.+)$/);
  if (hostToContainerMatch) {
    return {
      type: 'mapped',
      host: hostToContainerMatch[1].trim(),
      container: hostToContainerMatch[2].trim(),
      raw: value,
    };
  }

  return {
    type: 'internal',
    container: value,
    raw: value,
  };
};

const formatContainerPorts = (ports = '') => {
  const rawPorts = ports?.toString().trim();
  if (!rawPorts) return '—';

  const mappings = rawPorts
    .split(',')
    .map((item) => parsePortMapping(item))
    .filter(Boolean);

  if (!mappings.length) return escapeHtml(rawPorts);

  return mappings
    .map((mapping) => {
      if (mapping.type === 'mapped') {
        return `<span class="port-item"><strong>Ngoài VPS:</strong> ${escapeHtml(
          mapping.host,
        )} → <strong>Trong Docker:</strong> ${escapeHtml(mapping.container)}</span>`;
      }

      return `<span class="port-item"><strong>Trong Docker:</strong> ${escapeHtml(mapping.container)} <em>(chưa map ra ngoài VPS)</em></span>`;
    })
    .join('<br />');
};

const setLoading = (isLoading, message = 'Đang xử lý...') => {
  if (isLoading) {
    loadingText.textContent = message;
    loadingBackdrop.classList.remove('hidden');
    return;
  }
  loadingBackdrop.classList.add('hidden');
};

const showToast = (message, type = 'success') => {
  toast.textContent = message;
  toast.className = `toast ${type}`;
  toast.classList.remove('hidden');

  setTimeout(() => {
    toast.classList.add('hidden');
  }, 2600);
};

const getToken = () => localStorage.getItem(TOKEN_KEY);
const clearToken = () => localStorage.removeItem(TOKEN_KEY);

const apiFetch = async (endpoint, options = {}) => {
  const token = getToken();
  const headers = {
    'Content-Type': 'application/json',
    ...(options.headers || {}),
  };

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(adminApiUrl(endpoint), {
    ...options,
    headers,
  });

  if (response.status === 401) {
    clearToken();
    window.location.href = './index.html';
    throw new Error('Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.');
  }

  let body;
  try {
    body = await response.json();
  } catch (error) {
    body = {};
  }

  if (!response.ok) {
    const message = body?.message || body?.error || 'Có lỗi xảy ra, vui lòng thử lại.';
    throw new Error(message);
  }

  return body;
};

const setSessionState = (admin) => {
  if (!admin) return;
  adminName.textContent = admin.friendCode || 'Admin';
  adminMeta.textContent = admin.providerType ? `${admin.friendCode} · ${admin.providerType}` : admin.friendCode;
  sessionState.textContent = `Đăng nhập: ${admin.friendCode || ''}`;
  sessionState.classList.remove('offline');
};

const ensureSession = async () => {
  const token = getToken();
  if (!token) {
    window.location.href = './index.html';
    return;
  }

  try {
    const { admin } = await apiFetch('session');
    if (!admin) {
      throw new Error('Phiên đăng nhập không hợp lệ.');
    }
    setSessionState(admin);
  } catch (error) {
    clearToken();
    window.location.href = './index.html';
  }
};

const getGameContainers = () => containers.filter((container) => container.category !== 'system');
const getSystemContainers = () => containers.filter((container) => container.category === 'system');

const updateDockerCount = (filteredLength = getGameContainers().length) => {
  const total = getGameContainers().length;
  if (filteredLength !== total) {
    dockerCount.textContent = `${filteredLength}/${total} container`;
    return;
  }
  dockerCount.textContent = `${total} container`;
};

const updateSystemCount = (filteredLength = getSystemContainers().length) => {
  if (!dockerSystemCount) return;
  const total = getSystemContainers().length;
  if (filteredLength !== total) {
    dockerSystemCount.textContent = `${filteredLength}/${total} container`;
    return;
  }
  dockerSystemCount.textContent = `${total} container`;
};

const getFilteredContainers = () => {
  const keyword = searchTerm.trim().toLowerCase();
  const gameContainers = getGameContainers();
  if (!keyword) return [...gameContainers];

  return gameContainers.filter((container) => {
    const values = [
      container.name,
      container.id,
      container.image,
      container.ports,
      container.status,
      container.roomTypeName,
      container.isBusy === true ? 'bận' : container.isBusy === false ? 'trống' : '',
      container.hasStarted === true ? 'đã bắt đầu' : container.hasStarted === false ? 'chưa bắt đầu' : '',
    ];
    return values.some((value) => value?.toString().toLowerCase().includes(keyword));
  });
};

const updatePaginationControls = (filteredLength, totalPages) => {
  const hasData = filteredLength > 0;
  const displayTotalPages = hasData ? totalPages : 0;
  const displayCurrentPage = hasData ? currentPage : 0;

  paginationInfo.textContent = `Trang ${displayCurrentPage}/${displayTotalPages}`;
  prevPageButton.disabled = !hasData || currentPage === 1;
  nextPageButton.disabled = !hasData || currentPage === totalPages;
};

const renderContainers = () => {
  const filtered = getFilteredContainers();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  currentPage = filtered.length ? Math.min(currentPage, totalPages) : 1;

  updateDockerCount(filtered.length);
  updatePaginationControls(filtered.length, totalPages);

  if (!filtered.length) {
    const message = searchTerm.trim()
      ? `Không tìm thấy container cho từ khóa "${escapeHtml(searchTerm)}".`
      : 'Không có container nào đang chạy.';
    dockerGrid.innerHTML = `<div class="docker-empty">${message}</div>`;
    return;
  }

  const startIndex = (currentPage - 1) * PAGE_SIZE;
  const pageItems = filtered.slice(startIndex, startIndex + PAGE_SIZE);

  dockerGrid.innerHTML = renderContainerRows(pageItems, true);
};

const renderContainerRows = (items, includeRoomDetails) =>
  items
    .map(
      (container) => `
        <article class="data-row">
          <div class="row-main docker-row">
            <div class="code-badge">${escapeHtml(container.name || container.id)}</div>
            <div class="row-text">
              <p class="row-label">Image</p>
              <p class="row-value">${escapeHtml(container.image || 'Không rõ')}</p>
              <p class="row-note">ID: ${escapeHtml(container.id || '—')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Trạng thái</p>
              <p class="row-value">${escapeHtml(container.status || 'Unknown')}</p>
              <p class="row-note port-note">Ports:<br />${formatContainerPorts(container.ports)}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Tài nguyên</p>
              <p class="row-value">CPU: ${escapeHtml(container.cpu || '—')}</p>
              <p class="row-note">RAM: ${escapeHtml(container.memory || '—')}</p>
            </div>
            ${
              includeRoomDetails
                ? `
            <div class="row-text">
              <p class="row-label">Loại phòng</p>
              <p class="row-value">${escapeHtml(container.roomTypeName || 'Không rõ')}</p>
              <p class="row-note">TypeMatchGid: ${escapeHtml(container.typeMatchGid ?? '—')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Trạng thái phòng</p>
              <p class="row-value">${
                container.isBusy === true
                  ? 'Đang bận'
                  : container.isBusy === false
                    ? 'Đang trống'
                    : 'Không rõ'
              }</p>
              <p class="row-note">isBusy: ${escapeHtml(container.isBusy ?? '—')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Bắt đầu game</p>
              <p class="row-value">${container.hasStarted ? 'Đã bắt đầu' : 'Chưa bắt đầu'}</p>
              <p class="row-note">Room: ${escapeHtml(container.roomNameRef || '—')}</p>
            </div>
            `
                : `
            <div class="row-text">
              <p class="row-label">Service</p>
              <p class="row-value">${escapeHtml(container.category || 'system')}</p>
              <p class="row-note">System container</p>
            </div>
            `
            }
          </div>
          <div class="row-actions">
            <button
              class="chip-action chip-button"
              data-action="log"
              data-id="${escapeHtml(container.id)}"
              data-name="${escapeHtml(container.name)}"
              type="button"
            >
              Xem log
            </button>
            <button
              class="chip-action chip-button danger"
              data-action="stop"
              data-id="${escapeHtml(container.id)}"
              data-name="${escapeHtml(container.name)}"
              type="button"
            >
              Stop
            </button>
          </div>
        </article>
      `,
    )
    .join('');

const renderSystemContainers = () => {
  if (!dockerSystemGrid) return;
  const systemContainers = getSystemContainers();
  updateSystemCount(systemContainers.length);

  if (!systemContainers.length) {
    dockerSystemGrid.innerHTML = '<div class="docker-empty">Không có container system.</div>';
    return;
  }

  dockerSystemGrid.innerHTML = renderContainerRows(systemContainers, false);
};

const updateRoomCount = (filteredLength = rooms.length) => {
  if (!roomCount) return;
  if (filteredLength !== rooms.length) {
    roomCount.textContent = `${filteredLength}/${rooms.length} phòng`;
    return;
  }
  roomCount.textContent = `${rooms.length} phòng`;
};

const getFilteredRooms = () => {
  const keyword = roomSearchTerm.trim().toLowerCase();
  if (!keyword) return [...rooms];

  return rooms.filter((room) => {
    const values = [
      room.roomName,
      room.roomNameRef,
      room.portNo,
      room.containerId,
      room.isBusy === 1 ? 'bận' : room.isBusy === 0 ? 'trống' : '',
      room.roomTypeMatchGid,
      room.poolTypeMatchGid,
      room.roomTypeName,
      room.poolTypeName,
      room.createId,
      room.createPlayerName,
      room.currentPlayers,
      room.maxPlayers,
      room.mapId,
      ...(Array.isArray(room.roomUsers)
        ? room.roomUsers.flatMap((user) => [user?.playerId, user?.playerName, user?.joinedAt])
        : []),
    ];
    return values.some((value) => value?.toString().toLowerCase().includes(keyword));
  });
};

const updateRoomPaginationControls = (filteredLength, totalPages) => {
  if (!roomPaginationInfo || !roomPrevPageButton || !roomNextPageButton) return;
  const hasData = filteredLength > 0;
  const displayTotalPages = hasData ? totalPages : 0;
  const displayCurrentPage = hasData ? roomCurrentPage : 0;

  roomPaginationInfo.textContent = `Trang ${displayCurrentPage}/${displayTotalPages}`;
  roomPrevPageButton.disabled = !hasData || roomCurrentPage === 1;
  roomNextPageButton.disabled = !hasData || roomCurrentPage === totalPages;
};

const renderRooms = () => {
  if (!roomGrid) return;
  const filtered = getFilteredRooms();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  roomCurrentPage = filtered.length ? Math.min(roomCurrentPage, totalPages) : 1;

  updateRoomCount(filtered.length);
  updateRoomPaginationControls(filtered.length, totalPages);

  if (!filtered.length) {
    const message = roomSearchTerm.trim()
      ? `Không tìm thấy phòng cho từ khóa "${escapeHtml(roomSearchTerm)}".`
      : 'Không có phòng nào.';
    roomGrid.innerHTML = `<div class="docker-empty">${message}</div>`;
    return;
  }

  const startIndex = (roomCurrentPage - 1) * PAGE_SIZE;
  const pageItems = filtered.slice(startIndex, startIndex + PAGE_SIZE);

  roomGrid.innerHTML = pageItems
    .map(
      (room) => `
        <article class="data-row">
          <div class="row-main room-row">
            <div class="code-badge">${escapeHtml(room.roomName || `#${room.roomId}`)}</div>
            <div class="row-text">
              <p class="row-label">Room</p>
              <p class="row-value">${escapeHtml(room.roomName || '—')}</p>
              <p class="row-note">ID: ${escapeHtml(room.roomId ?? '—')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Người chơi</p>
              <p class="row-value">${escapeHtml(room.currentPlayers ?? '—')} / ${escapeHtml(
                room.maxPlayers ?? '—',
              )}</p>
              <p class="row-note">Bet: ${escapeHtml(room.bet ?? '—')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Port</p>
              <p class="row-value">${escapeHtml(room.portNo ?? '—')}</p>
              <p class="row-note">Container: ${escapeHtml(room.containerId ?? '—')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Trạng thái</p>
              <p class="row-value">${
                room.isBusy === 1 ? 'Đang bận' : room.isBusy === 0 ? 'Đang trống' : 'Không rõ'
              }</p>
              <p class="row-note">RoomNameRef: ${escapeHtml(room.roomNameRef ?? '—')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">TypeMatchGid</p>
              <p class="row-value">${escapeHtml(
                room.roomTypeMatchGid != null
                  ? `${room.roomTypeName ? `${room.roomTypeName} · ` : ''}${room.roomTypeMatchGid}`
                  : '—',
              )}</p>
              <p class="row-note">Pool: ${escapeHtml(
                room.poolTypeMatchGid != null
                  ? `${room.poolTypeName ? `${room.poolTypeName} · ` : ''}${room.poolTypeMatchGid}`
                  : '—',
              )}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Tạo phòng</p>
              <p class="row-value">${escapeHtml(
                room.createId != null
                  ? `${room.createId}${room.createPlayerName ? ` · ${room.createPlayerName}` : ''}`
                  : '—',
              )}</p>
              <p class="row-note">${escapeHtml(formatDateTime(room.createDate))}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Người chơi tham gia</p>
              <p class="row-value">${formatRoomUsers(room.roomUsers)}</p>
              <p class="row-note">Tổng: ${escapeHtml(room.roomUsers?.length ?? 0)}</p>
            </div>
          </div>
        </article>
      `,
    )
    .join('');
};

const getMatchmakingStatusLabel = (status) => {
  switch (status) {
    case 'QUEUED':
      return 'Đang tìm trận';
    case 'MATCH_PROPOSED':
      return 'Đã tìm thấy, chờ xác nhận';
    case 'MATCH_CONFIRMED':
      return 'Đã xác nhận';
    case 'SERVER_CREATING':
      return 'Đang tạo server';
    case 'READY':
      return 'Server sẵn sàng';
    default:
      return status || 'Không rõ';
  }
};

const getFilteredMatchmakingPlayers = () => {
  const keyword = matchmakingSearchTerm.trim().toLowerCase();
  if (!keyword) return [...matchmakingPlayers];

  return matchmakingPlayers.filter((entry) => {
    const values = [
      entry.playerId,
      entry.playerName,
      entry.friendCode,
      entry.providerType,
      entry.bucket,
      entry.region,
      entry.typeMatchGid,
      entry.typeMatchName,
      entry.bet,
      entry.status,
      getMatchmakingStatusLabel(entry.status),
      entry.matchId,
      entry.sessionName,
      entry.hostPort,
    ];
    return values.some((value) => value?.toString().toLowerCase().includes(keyword));
  });
};

const updateMatchmakingCount = (filteredLength = matchmakingPlayers.length) => {
  if (!matchmakingCount) return;
  const total = matchmakingPlayers.length;
  matchmakingCount.textContent =
    filteredLength !== total ? `${filteredLength}/${total} người` : `${total} người`;
};

const updateMatchmakingPaginationControls = (filteredLength, totalPages) => {
  if (!matchmakingPaginationInfo || !matchmakingPrevPageButton || !matchmakingNextPageButton) return;
  const hasData = filteredLength > 0;
  matchmakingPaginationInfo.textContent = `Trang ${hasData ? matchmakingCurrentPage : 0}/${hasData ? totalPages : 0}`;
  matchmakingPrevPageButton.disabled = !hasData || matchmakingCurrentPage === 1;
  matchmakingNextPageButton.disabled = !hasData || matchmakingCurrentPage === totalPages;
};

const renderMatchmakingPlayers = () => {
  if (!matchmakingGrid) return;

  const filtered = getFilteredMatchmakingPlayers();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  matchmakingCurrentPage = filtered.length ? Math.min(matchmakingCurrentPage, totalPages) : 1;

  updateMatchmakingCount(filtered.length);
  updateMatchmakingPaginationControls(filtered.length, totalPages);

  if (!filtered.length) {
    const message = matchmakingSearchTerm.trim()
      ? `Không tìm thấy người chơi cho từ khóa "${escapeHtml(matchmakingSearchTerm)}".`
      : 'Chưa có người chơi nào đang tìm trận.';
    matchmakingGrid.innerHTML = `<div class="docker-empty">${message}</div>`;
    return;
  }

  const startIndex = (matchmakingCurrentPage - 1) * PAGE_SIZE;
  const pageItems = filtered.slice(startIndex, startIndex + PAGE_SIZE);

  matchmakingGrid.innerHTML = pageItems
    .map((entry) => {
      const displayName = entry.playerName || entry.friendCode || `User ${entry.playerId}`;
      const typeLabel = entry.typeMatchName
        ? `${entry.typeMatchName} · ${entry.typeMatchGid}`
        : entry.typeMatchGid ?? 'â€”';
      const waitingText = formatDuration(entry.startedAt, entry.serverTime);
      const groupText = entry.source === 'match'
        ? `${entry.playerCount ?? 0}/${entry.requiredPlayers ?? 'â€”'} người`
        : `${entry.bucketSize ?? 0} trong bucket`;

      return `
        <article class="data-row">
          <div class="row-main matchmaking-row">
            <div class="searching-badge" title="Đang tìm trận">
              <span class="searching-glasses">◉-◉</span>
              <span>${escapeHtml(entry.playerId)}</span>
            </div>
            <div class="row-text">
              <p class="row-label">Người chơi</p>
              <p class="row-value">${escapeHtml(displayName)}</p>
              <p class="row-note">FriendCode: ${escapeHtml(entry.friendCode || 'â€”')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Trạng thái</p>
              <p class="row-value">${escapeHtml(getMatchmakingStatusLabel(entry.status))}</p>
              <p class="row-note">Đã chờ: ${escapeHtml(waitingText)}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Loại trận</p>
              <p class="row-value">${escapeHtml(typeLabel)}</p>
              <p class="row-note">Bet: ${escapeHtml(entry.bet ?? 'â€”')} · ${escapeHtml(entry.region || 'â€”')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Bucket / Match</p>
              <p class="row-value">${escapeHtml(entry.bucket || entry.matchId || 'â€”')}</p>
              <p class="row-note">${escapeHtml(groupText)}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Server</p>
              <p class="row-value">${escapeHtml(entry.sessionName || 'â€”')}</p>
              <p class="row-note">Port: ${escapeHtml(entry.hostPort ?? 'â€”')}</p>
            </div>
          </div>
        </article>
      `;
    })
    .join('');
};

const fetchContainers = async () => {
  setLoading(true, 'Đang tải danh sách container...');
  try {
    const data = await apiFetch('containers');
    containers = Array.isArray(data?.containers) ? data.containers : data || [];
    currentPage = 1;
    renderContainers();
    renderSystemContainers();
  } catch (error) {
    dockerGrid.innerHTML = `<div class="docker-error">${escapeHtml(
      error.message || 'Không thể tải danh sách container.',
    )}</div>`;
    if (dockerSystemGrid) {
      dockerSystemGrid.innerHTML = `<div class="docker-error">${escapeHtml(
        error.message || 'Không thể tải danh sách container system.',
      )}</div>`;
    }
    showToast(error.message || 'Không thể tải danh sách container.', 'error');
  } finally {
    setLoading(false);
  }
};

const fetchRooms = async () => {
  if (!roomGrid) return;
  setLoading(true, 'Đang tải danh sách phòng...');
  try {
    const data = await apiFetch('rooms/overview');
    rooms = Array.isArray(data?.rooms) ? data.rooms : data || [];
    roomCurrentPage = 1;
    renderRooms();
  } catch (error) {
    roomGrid.innerHTML = `<div class="docker-error">${escapeHtml(
      error.message || 'Không thể tải danh sách phòng.',
    )}</div>`;
    showToast(error.message || 'Không thể tải danh sách phòng.', 'error');
  } finally {
    setLoading(false);
  }
};

const fetchMatchmakingPlayers = async ({ silent = false } = {}) => {
  if (!matchmakingGrid) return;
  if (!silent) setLoading(true, 'Đang tải danh sách người chơi đang tìm trận...');

  try {
    const data = await apiFetch('matchmaking/searching-players');
    const serverTime = data?.serverTime || Date.now();
    const queued = Array.isArray(data?.queued)
      ? data.queued.map((entry) => ({ ...entry, source: 'queue', serverTime }))
      : [];
    const activeMatches = Array.isArray(data?.activeMatches)
      ? data.activeMatches.map((entry) => ({ ...entry, source: 'match', serverTime }))
      : [];

    matchmakingPlayers = [...queued, ...activeMatches].sort((a, b) => {
      const aTime = Number(a.startedAt || 0);
      const bTime = Number(b.startedAt || 0);
      return aTime - bTime;
    });
    matchmakingCurrentPage = Math.min(matchmakingCurrentPage, Math.max(1, Math.ceil(matchmakingPlayers.length / PAGE_SIZE)));
    renderMatchmakingPlayers();
  } catch (error) {
    matchmakingGrid.innerHTML = `<div class="docker-error">${escapeHtml(
      error.message || 'Không thể tải danh sách người chơi đang tìm trận.',
    )}</div>`;
    if (!silent) showToast(error.message || 'Không thể tải danh sách người chơi đang tìm trận.', 'error');
  } finally {
    if (!silent) setLoading(false);
  }
};

const startMatchmakingAutoRefresh = () => {
  if (matchmakingRefreshTimer != null) return;
  matchmakingRefreshTimer = setInterval(() => {
    if (activeTabId === 'matchmaking') {
      fetchMatchmakingPlayers({ silent: true });
    }
  }, MATCHMAKING_REFRESH_MS);
};

const stopMatchmakingAutoRefresh = () => {
  if (matchmakingRefreshTimer == null) return;
  clearInterval(matchmakingRefreshTimer);
  matchmakingRefreshTimer = null;
};

const activateTab = (tabId) => {
  activeTabId = tabId;
  tabButtons.forEach((btn) => btn.classList.toggle('active', btn.dataset.tab === tabId));
  tabPanels.forEach((panel) => panel.classList.toggle('active', panel.dataset.tabPanel === tabId));

  if (tabId === 'matchmaking') {
    fetchMatchmakingPlayers({ silent: matchmakingPlayers.length > 0 });
    startMatchmakingAutoRefresh();
  } else {
    stopMatchmakingAutoRefresh();
  }
};

const openLogModal = (title) => {
  logTitle.textContent = title;
  logModal.classList.remove('hidden');
};

const closeLogModal = () => {
  logModal.classList.add('hidden');
  logContent.textContent = '';
};

const loadContainerLog = async (containerId, name) => {
  openLogModal(name ? `Log · ${name}` : 'Log container');
  logContent.textContent = 'Đang tải log...';
  try {
    const { logs } = await apiFetch(`containers/${encodeURIComponent(containerId)}/logs`);
    logContent.textContent = logs || 'Không có log để hiển thị.';
  } catch (error) {
    logContent.textContent = error.message || 'Không thể tải log.';
    showToast(error.message || 'Không thể tải log.', 'error');
  }
};

const stopContainer = async (containerId, name) => {
  const label = name || containerId;
  const confirmed = window.confirm(`Stop container "${label}"?`);
  if (!confirmed) return;

  setLoading(true, `Dang stop container ${label}...`);
  try {
    const result = await apiFetch(`containers/${encodeURIComponent(containerId)}/stop`, {
      method: 'POST',
    });

    showToast(result.message || `Da stop container ${label}.`);
    await fetchContainers();
  } catch (error) {
    showToast(error.message || 'Khong the stop container.', 'error');
  } finally {
    setLoading(false);
  }
};

backDashboardButton?.addEventListener('click', () => {
  window.location.href = './index.html';
});

logoutButton.addEventListener('click', () => {
  clearToken();
  window.location.href = './index.html';
});

refreshButton.addEventListener('click', fetchContainers);
refreshSystemButton?.addEventListener('click', fetchContainers);
refreshRoomsButton?.addEventListener('click', fetchRooms);
refreshMatchmakingButton?.addEventListener('click', () => fetchMatchmakingPlayers());

searchInput.addEventListener('input', (event) => {
  searchTerm = event.target.value;
  currentPage = 1;
  renderContainers();
});

roomSearchInput?.addEventListener('input', (event) => {
  roomSearchTerm = event.target.value;
  roomCurrentPage = 1;
  renderRooms();
});

matchmakingSearchInput?.addEventListener('input', (event) => {
  matchmakingSearchTerm = event.target.value;
  matchmakingCurrentPage = 1;
  renderMatchmakingPlayers();
});

prevPageButton.addEventListener('click', () => {
  if (currentPage > 1) {
    currentPage -= 1;
    renderContainers();
  }
});

nextPageButton.addEventListener('click', () => {
  const filtered = getFilteredContainers();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  if (currentPage < totalPages) {
    currentPage += 1;
    renderContainers();
  }
});

roomPrevPageButton?.addEventListener('click', () => {
  if (roomCurrentPage > 1) {
    roomCurrentPage -= 1;
    renderRooms();
  }
});

roomNextPageButton?.addEventListener('click', () => {
  const filtered = getFilteredRooms();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  if (roomCurrentPage < totalPages) {
    roomCurrentPage += 1;
    renderRooms();
  }
});

matchmakingPrevPageButton?.addEventListener('click', () => {
  if (matchmakingCurrentPage > 1) {
    matchmakingCurrentPage -= 1;
    renderMatchmakingPlayers();
  }
});

matchmakingNextPageButton?.addEventListener('click', () => {
  const filtered = getFilteredMatchmakingPlayers();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  if (matchmakingCurrentPage < totalPages) {
    matchmakingCurrentPage += 1;
    renderMatchmakingPlayers();
  }
});

closeLogButton.addEventListener('click', closeLogModal);

logModal.addEventListener('click', (event) => {
  if (event.target === logModal) {
    closeLogModal();
  }
});

const handleContainerActionClick = (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;

  const action = target.dataset.action;
  const containerId = target.dataset.id;
  const name = target.dataset.name;
  if (!containerId) return;

  if (action === 'log') {
    loadContainerLog(containerId, name);
    return;
  }

  if (action === 'stop') {
    stopContainer(containerId, name);
  }
};

dockerGrid.addEventListener('click', handleContainerActionClick);
dockerSystemGrid?.addEventListener('click', handleContainerActionClick);

Array.from(tabButtons).forEach((button) => {
  button.addEventListener('click', () => {
    const targetTab = button.dataset.tab;
    if (targetTab) {
      activateTab(targetTab);
    }
  });
});

(async () => {
  await ensureSession();
  await fetchContainers();
  await fetchRooms();
  await fetchMatchmakingPlayers({ silent: true });
})();
