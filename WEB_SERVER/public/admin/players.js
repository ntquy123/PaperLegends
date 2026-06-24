import { createPopup, confirmPopup } from './popup.js';

const sessionState = document.getElementById('session-state');
const adminName = document.getElementById('admin-name');
const adminMeta = document.getElementById('admin-meta');
const logoutButton = document.getElementById('logout');
const backDashboardButton = document.getElementById('back-dashboard');
const playerGrid = document.getElementById('player-grid');
const playerCount = document.getElementById('player-count');
const searchInput = document.getElementById('player-search');
const searchButton = document.getElementById('search-player');
const resetButton = document.getElementById('reset-player');
const paginationInfo = document.getElementById('pagination-info');
const prevPageButton = document.getElementById('prev-page');
const nextPageButton = document.getElementById('next-page');
const loadingBackdrop = document.getElementById('loading-backdrop');
const loadingText = document.getElementById('loading-text');
const toast = document.getElementById('toast');
const playerDetailEmpty = document.getElementById('player-detail-empty');
const playerDetail = document.getElementById('player-detail');
const playerStatusPill = document.getElementById('player-status-pill');
const detailFriendCode = document.getElementById('detail-friendcode');
const detailName = document.getElementById('detail-name');
const detailProvider = document.getElementById('detail-provider');
const detailLevel = document.getElementById('detail-level');
const detailExp = document.getElementById('detail-exp');
const detailRingBall = document.getElementById('detail-ringball');
const detailMoney = document.getElementById('detail-money');
const detailTalent = document.getElementById('detail-talent');
const detailEmail = document.getElementById('detail-email');
const detailBody = document.getElementById('detail-body');
const detailActive = document.getElementById('detail-active');
const detailCreatedAt = document.getElementById('detail-created-at');
const detailLastLoginAt = document.getElementById('detail-last-login-at');
const viewLogsButton = document.getElementById('view-logs');
const toggleActiveButton = document.getElementById('toggle-active');
const toggleActiveLabel = document.getElementById('toggle-active-label');
const deletePlayerButton = document.getElementById('delete-player');
const sendAllCheckbox = document.getElementById('send-all');
const selectedCount = document.getElementById('selected-count');
const selectedList = document.getElementById('selected-list');
const useSelectedPlayerButton = document.getElementById('use-selected-player');
const clearSelectionButton = document.getElementById('clear-selection');
const messageContent = document.getElementById('message-content');
const messageRingBall = document.getElementById('message-ringball');
const messageMoney = document.getElementById('message-money');
const messageItem = document.getElementById('message-item');
const sendMessageButton = document.getElementById('send-message');

const TOKEN_KEY = 'admin_ui_token';
const ADMIN_API_BASE = window.location.pathname.startsWith('/paper-legends/')
  ? '/paper-legends/api/admin'
  : '/api/admin';
const adminApiUrl = (endpoint) => `${ADMIN_API_BASE}/${endpoint.toString().replace(/^\/+/, '')}`;
const PAGE_SIZE = 10;

let currentPage = 1;
let totalPages = 1;
let searchTerm = '';
let selectedPlayer = null;
let currentPlayers = new Map();
const selectedPlayerIds = new Set();
const selectedPlayers = new Map();

const HTML_ESCAPE_MAP = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
};

const escapeHtml = (value = '') => value.toString().replace(/[&<>"']/g, (char) => HTML_ESCAPE_MAP[char] || char);
const formatTimestamp = (value) => (value ? new Date(value).toLocaleString('vi-VN') : '—');

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

const updatePaginationControls = () => {
  const hasData = totalPages > 0;
  const displayCurrentPage = hasData ? currentPage : 0;
  const displayTotalPages = hasData ? totalPages : 0;
  paginationInfo.textContent = `Trang ${displayCurrentPage}/${displayTotalPages}`;
  prevPageButton.disabled = currentPage <= 1;
  nextPageButton.disabled = currentPage >= totalPages;
};

const renderPlayers = (players = []) => {
  currentPlayers = new Map(players.map((player) => [player.id, player]));

  if (!players.length) {
    playerGrid.innerHTML = `<div class="docker-empty">Không tìm thấy tài khoản phù hợp.</div>`;
    return;
  }

  playerGrid.innerHTML = players
    .map(
      (player) => `
        <article class="data-row player-row" data-id="${player.id}">
          <div class="row-main player-row-main">
            <div class="code-badge">${escapeHtml(player.friendCode)}</div>
            <div class="row-text">
              <p class="row-label">Tên hiển thị</p>
              <p class="row-value">${escapeHtml(player.PlayerName || 'Chưa đặt tên')}</p>
              <p class="row-note">Provider: ${escapeHtml(player.ProviderType || '—')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Cấp độ</p>
              <p class="row-value">Level ${escapeHtml(player.Level ?? '0')}</p>
              <p class="row-note">RingBall: ${escapeHtml(player.RingBall ?? '0')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Trạng thái</p>
              <p class="row-value ${player.IsActive ? 'status-active' : 'status-locked'}">
                ${player.IsActive ? 'Đang hoạt động' : 'Đang bị khóa'}
              </p>
              <p class="row-note">Money: ${escapeHtml(player.Money ?? '0')}</p>
              <p class="row-note">Last login: ${escapeHtml(formatTimestamp(player.lastLoginAt))}</p>
            </div>
          </div>
          <div class="row-actions">
            <label class="row-select">
              <input
                type="checkbox"
                data-action="select-message"
                data-id="${player.id}"
                ${selectedPlayerIds.has(player.id) ? 'checked' : ''}
                ${sendAllCheckbox?.checked ? 'disabled' : ''}
              />
              <span>Chọn gửi</span>
            </label>
            <button class="chip-action chip-button" data-action="select" data-id="${player.id}">
              Xem chi tiết
            </button>
          </div>
        </article>
      `,
    )
    .join('');
};

const updatePlayerCount = (totalItems = 0) => {
  playerCount.textContent = `${totalItems} tài khoản`;
};

const loadPlayers = async (page = 1) => {
  setLoading(true, 'Đang tải danh sách người chơi...');
  try {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: PAGE_SIZE.toString(),
    });
    if (searchTerm) {
      params.append('friendCode', searchTerm);
    }

    const data = await apiFetch(`players?${params.toString()}`);
    currentPage = data.pagination.page;
    totalPages = data.pagination.totalPages;
    updatePlayerCount(data.pagination.totalItems);
    renderPlayers(data.players);
    updatePaginationControls();
  } catch (error) {
    showToast(error.message, 'error');
  } finally {
    setLoading(false);
  }
};

const updateSelectedDetail = (player) => {
  if (!player) {
    playerDetail.classList.add('hidden');
    playerDetailEmpty.classList.remove('hidden');
    playerStatusPill.className = 'pill neutral';
    playerStatusPill.textContent = 'Chưa chọn';
    return;
  }

  playerDetailEmpty.classList.add('hidden');
  playerDetail.classList.remove('hidden');
  playerStatusPill.className = `pill ${player.IsActive ? 'success' : 'danger'}`;
  playerStatusPill.textContent = player.IsActive ? 'Hoạt động' : 'Đang khóa';
  detailFriendCode.textContent = player.friendCode || '—';
  detailName.textContent = player.PlayerName || 'Chưa đặt tên';
  detailProvider.textContent = player.ProviderType || '—';
  detailLevel.textContent = player.Level ?? '0';
  detailExp.textContent = player.Exp ?? '0';
  detailRingBall.textContent = player.RingBall ?? '0';
  detailMoney.textContent = player.Money ?? '0';
  detailTalent.textContent = player.TalentPoint ?? '0';
  detailEmail.textContent = player.Email || '—';
  detailBody.textContent = player.Body ?? '—';
  detailActive.textContent = player.IsActive ? 'Hoạt động' : 'Đang khóa';
  detailCreatedAt.textContent = formatTimestamp(player.createdAt);
  detailLastLoginAt.textContent = formatTimestamp(player.lastLoginAt);
  toggleActiveLabel.textContent = player.IsActive ? 'Khóa tài khoản' : 'Mở khóa tài khoản';
  toggleActiveButton.className = player.IsActive ? 'cta danger' : 'cta';
};

const updateSelectedList = () => {
  if (sendAllCheckbox?.checked) {
    selectedCount.textContent = '0';
    selectedList.textContent = 'Gửi toàn server (tất cả người chơi).';
    return;
  }

  selectedCount.textContent = selectedPlayerIds.size.toString();
  if (selectedPlayerIds.size === 0) {
    selectedList.textContent = 'Chưa chọn người chơi.';
    return;
  }

  const names = Array.from(selectedPlayerIds).map((id) => {
    const info = selectedPlayers.get(id);
    if (info) {
      return `${info.friendCode}${info.PlayerName ? ` · ${info.PlayerName}` : ''}`;
    }
    return `#${id}`;
  });
  selectedList.textContent = names.join(', ');
};

const updateSelectionControls = () => {
  const isSendAll = sendAllCheckbox?.checked;
  useSelectedPlayerButton.disabled = !!isSendAll;
  clearSelectionButton.disabled = !!isSendAll;
  updateSelectedList();
};

const togglePlayerSelection = (playerId, isSelected) => {
  if (!Number.isInteger(playerId)) return;
  const player = currentPlayers.get(playerId);

  if (isSelected) {
    selectedPlayerIds.add(playerId);
    if (player) {
      selectedPlayers.set(playerId, player);
    }
  } else {
    selectedPlayerIds.delete(playerId);
    selectedPlayers.delete(playerId);
  }
  updateSelectedList();
};

const loadItems = async () => {
  try {
    const items = await apiFetch('items');
    if (!Array.isArray(items)) return;
    messageItem.innerHTML =
      '<option value="">Không đính kèm</option>' +
      items
        .map((item) => `<option value="${item.id}">${escapeHtml(item.name || `Item ${item.id}`)}</option>`)
        .join('');
  } catch (error) {
    showToast(error.message, 'error');
  }
};

const sendSystemMessage = async () => {
  const message = messageContent.value.trim();
  if (!message) {
    showToast('Vui lòng nhập nội dung tin nhắn.', 'error');
    return;
  }

  const ringBallReward = Number(messageRingBall.value || 0);
  const moneyReward = Number(messageMoney.value || 0);
  if (!Number.isFinite(ringBallReward) || ringBallReward < 0) {
    showToast('RingBall phải là số không âm.', 'error');
    return;
  }
  if (!Number.isFinite(moneyReward) || moneyReward < 0) {
    showToast('Money phải là số không âm.', 'error');
    return;
  }

  const sendAll = sendAllCheckbox.checked;
  const targetIds = Array.from(selectedPlayerIds);
  if (!sendAll && targetIds.length === 0) {
    showToast('Vui lòng chọn ít nhất một người chơi.', 'error');
    return;
  }

  const itemRewardId = messageItem.value ? Number(messageItem.value) : null;

  setLoading(true, 'Đang gửi tin nhắn...');
  try {
    const payload = {
      message,
      sendAll,
      playerIds: targetIds,
      ringBallReward: Math.floor(ringBallReward),
      moneyReward: Math.floor(moneyReward),
      itemRewardId,
    };
    const data = await apiFetch('players/messages', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    showToast(data.message || 'Đã gửi tin nhắn.', 'success');
    messageContent.value = '';
    messageRingBall.value = '0';
    messageMoney.value = '0';
    messageItem.value = '';
    if (!sendAll) {
      selectedPlayerIds.clear();
      selectedPlayers.clear();
      updateSelectedList();
      renderPlayers(Array.from(currentPlayers.values()));
    }
  } catch (error) {
    showToast(error.message, 'error');
  } finally {
    setLoading(false);
  }
};

const selectPlayer = async (playerId) => {
  setLoading(true, 'Đang tải thông tin người chơi...');
  try {
    const { player } = await apiFetch(`players/${playerId}`);
    selectedPlayer = player;
    updateSelectedDetail(player);
  } catch (error) {
    showToast(error.message, 'error');
  } finally {
    setLoading(false);
  }
};

const renderHistoryList = (histories = []) => {
  if (!histories.length) {
    return '<div class="popup-empty">Không có lịch sử trận đấu.</div>';
  }
  return histories
    .map(
      (history) => `
        <div class="popup-row">
          <div>
            <p class="popup-row-title">Trans #${escapeHtml(history.transno)}</p>
            <p class="popup-row-sub">Map: ${escapeHtml(history.mapGame || '—')} · Type: ${escapeHtml(
              history.typeMatchGid ?? '—',
            )}</p>
          </div>
          <div class="popup-row-meta">
            <span>${history.statusWin === 1 ? 'Thắng' : 'Thua'}</span>
            <span>+${escapeHtml(history.expGained ?? 0)} EXP</span>
            <span>${escapeHtml(history.rankPoints ?? 0)} RP</span>
          </div>
        </div>
      `,
    )
    .join('');
};

const renderBalanceList = (histories = []) => {
  if (!histories.length) {
    return '<div class="popup-empty">Không có lịch sử trừ tiền.</div>';
  }
  return histories
    .map(
      (history) => `
        <div class="popup-row">
          <div>
            <p class="popup-row-title">Sự kiện: ${escapeHtml(history.eventType)}</p>
            <p class="popup-row-sub">${escapeHtml(history.description || 'Không có mô tả')}</p>
          </div>
          <div class="popup-row-meta">
            <span>RingBall: ${escapeHtml(history.ringBall ?? 0)}</span>
            <span>Money: ${escapeHtml(history.money ?? 0)}</span>
          </div>
        </div>
      `,
    )
    .join('');
};

const renderTradeList = (trades = []) => {
  if (!trades.length) {
    return '<div class="popup-empty">Không có lịch sử buôn bán.</div>';
  }
  return trades
    .map((trade) => {
      const buyer = trade.buyer ? `${trade.buyer.name} (${trade.buyer.friendCode})` : 'Không rõ';
      const seller = trade.seller ? `${trade.seller.name} (${trade.seller.friendCode})` : 'Không rõ';
      return `
        <div class="popup-row">
          <div>
            <p class="popup-row-title">${escapeHtml(trade.itemName)}</p>
            <p class="popup-row-sub">Mua: ${escapeHtml(buyer)} · Bán: ${escapeHtml(seller)}</p>
          </div>
          <div class="popup-row-meta">
            <span>Seq: ${escapeHtml(trade.seq)}</span>
            <span>${new Date(trade.createdAt).toLocaleString()}</span>
          </div>
        </div>
      `;
    })
    .join('');
};

const showLogsPopup = async () => {
  if (!selectedPlayer) {
    showToast('Vui lòng chọn người chơi trước.', 'error');
    return;
  }

  setLoading(true, 'Đang tải log lịch sử...');
  try {
    const [historyData, balanceData, tradeData] = await Promise.all([
      apiFetch(`players/${selectedPlayer.id}/histories?limit=20`),
      apiFetch(`players/${selectedPlayer.id}/balance-histories?limit=20`),
      apiFetch(`players/${selectedPlayer.id}/item-trade-histories?limit=20`),
    ]);

    const content = document.createElement('div');
    content.innerHTML = `
      <div class="popup-tab-nav">
        <button class="tab-button active" data-tab="history">History</button>
        <button class="tab-button" data-tab="balance">BalanceHistory</button>
        <button class="tab-button" data-tab="trade">ItemTradeHistory</button>
      </div>
      <div class="popup-tab-panel active" data-panel="history">
        ${renderHistoryList(historyData.histories)}
      </div>
      <div class="popup-tab-panel" data-panel="balance">
        ${renderBalanceList(balanceData.balanceHistories)}
      </div>
      <div class="popup-tab-panel" data-panel="trade">
        ${renderTradeList(tradeData.itemTradeHistories)}
      </div>
    `;

    const popup = createPopup({
      title: `Log lịch sử · ${selectedPlayer.friendCode}`,
      subtitle: 'Theo dõi trận đấu, trừ tiền và giao dịch item gần nhất.',
      content,
      tone: 'success',
    });

    const tabButtons = content.querySelectorAll('.tab-button');
    const panels = content.querySelectorAll('.popup-tab-panel');
    tabButtons.forEach((button) => {
      button.addEventListener('click', () => {
        const tab = button.dataset.tab;
        tabButtons.forEach((btn) => btn.classList.toggle('active', btn === button));
        panels.forEach((panel) => {
          panel.classList.toggle('active', panel.dataset.panel === tab);
        });
      });
    });

    popup.body.scrollTop = 0;
  } catch (error) {
    showToast(error.message, 'error');
  } finally {
    setLoading(false);
  }
};

const toggleActiveStatus = async () => {
  if (!selectedPlayer) {
    showToast('Vui lòng chọn người chơi trước.', 'error');
    return;
  }

  const isCurrentlyActive = selectedPlayer.IsActive;
  const confirmed = await confirmPopup({
    title: isCurrentlyActive ? 'Khóa tài khoản người chơi?' : 'Mở khóa tài khoản người chơi?',
    message: isCurrentlyActive
      ? 'Tài khoản sẽ bị khóa và không thể đăng nhập.'
      : 'Tài khoản sẽ được mở khóa và có thể hoạt động lại.',
    confirmText: isCurrentlyActive ? 'Khóa tài khoản' : 'Mở khóa',
  });

  if (!confirmed) {
    return;
  }

  setLoading(true, 'Đang cập nhật trạng thái tài khoản...');
  try {
    const data = await apiFetch(`players/${selectedPlayer.id}/active`, {
      method: 'PUT',
      body: JSON.stringify({ isActive: !isCurrentlyActive }),
    });
    selectedPlayer = { ...selectedPlayer, IsActive: data.player.IsActive };
    updateSelectedDetail(selectedPlayer);
    showToast(data.message, 'success');
    loadPlayers(currentPage);
  } catch (error) {
    showToast(error.message, 'error');
  } finally {
    setLoading(false);
  }
};

const showDeletePlayerPopup = () => {
  if (!selectedPlayer) {
    showToast('Vui lòng chọn người chơi trước.', 'error');
    return;
  }

  const playerToDelete = selectedPlayer;
  const content = document.createElement('div');
  content.className = 'popup-confirm delete-player-confirm';

  const warning = document.createElement('p');
  warning.className = 'popup-message delete-player-warning';
  warning.textContent =
    `Xóa vĩnh viễn người chơi ${playerToDelete.friendCode}. ` +
    'Toàn bộ vật phẩm, lịch sử, bạn bè, tin nhắn và dữ liệu liên quan sẽ bị xóa.';

  const passwordField = document.createElement('label');
  passwordField.className = 'field';
  const passwordLabel = document.createElement('span');
  passwordLabel.textContent = 'Nhập lại mật khẩu quản trị';
  const passwordInput = document.createElement('input');
  passwordInput.type = 'password';
  passwordInput.autocomplete = 'current-password';
  passwordInput.placeholder = 'Mật khẩu quản trị';
  passwordField.append(passwordLabel, passwordInput);
  content.append(warning, passwordField);

  let popup;
  const deleteConfirmedPlayer = async () => {
    const password = passwordInput.value;
    if (!password) {
      showToast('Vui lòng nhập mật khẩu quản trị để xóa.', 'error');
      passwordInput.focus();
      return;
    }

    popup.close();
    setLoading(true, 'Đang xóa người chơi và dữ liệu liên quan...');
    try {
      const data = await apiFetch(`players/${playerToDelete.id}`, {
        method: 'DELETE',
        body: JSON.stringify({ password }),
      });

      selectedPlayerIds.delete(playerToDelete.id);
      selectedPlayers.delete(playerToDelete.id);
      if (selectedPlayer?.id === playerToDelete.id) {
        selectedPlayer = null;
        updateSelectedDetail(null);
      }
      updateSelectedList();

      await loadPlayers(currentPage);
      if (currentPage > totalPages) {
        await loadPlayers(totalPages);
      }
      showToast(data.message || 'Đã xóa người chơi.', 'success');
    } catch (error) {
      showToast(error.message, 'error');
    } finally {
      setLoading(false);
    }
  };

  popup = createPopup({
    title: 'Xóa vĩnh viễn người chơi?',
    subtitle: 'Thao tác này không thể hoàn tác và yêu cầu xác nhận mật khẩu.',
    content,
    tone: 'warning',
    actions: [
      {
        label: 'Hủy',
        className: 'chip-action chip-button muted',
        onClick: () => popup.close(),
      },
      {
        label: 'Xóa vĩnh viễn',
        className: 'cta danger',
        onClick: deleteConfirmedPlayer,
      },
    ],
  });

  passwordInput.addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      deleteConfirmedPlayer();
    }
  });
  passwordInput.focus();
};

playerGrid.addEventListener('click', (event) => {
  const target = event.target.closest('button[data-action="select"]');
  if (!target) return;
  const id = Number(target.dataset.id);
  if (Number.isInteger(id)) {
    selectPlayer(id);
  }
});

playerGrid.addEventListener('change', (event) => {
  const target = event.target;
  if (!(target instanceof HTMLInputElement)) return;
  if (target.dataset.action !== 'select-message') return;
  const id = Number(target.dataset.id);
  if (Number.isInteger(id)) {
    togglePlayerSelection(id, target.checked);
  }
});

searchButton.addEventListener('click', () => {
  searchTerm = searchInput.value.trim();
  currentPage = 1;
  loadPlayers(currentPage);
});

searchInput.addEventListener('keydown', (event) => {
  if (event.key === 'Enter') {
    event.preventDefault();
    searchButton.click();
  }
});

resetButton.addEventListener('click', () => {
  searchTerm = '';
  searchInput.value = '';
  currentPage = 1;
  loadPlayers(currentPage);
});

prevPageButton.addEventListener('click', () => {
  if (currentPage > 1) {
    currentPage -= 1;
    loadPlayers(currentPage);
  }
});

nextPageButton.addEventListener('click', () => {
  if (currentPage < totalPages) {
    currentPage += 1;
    loadPlayers(currentPage);
  }
});

viewLogsButton.addEventListener('click', showLogsPopup);
toggleActiveButton.addEventListener('click', toggleActiveStatus);
deletePlayerButton.addEventListener('click', showDeletePlayerPopup);

sendAllCheckbox.addEventListener('change', () => {
  updateSelectionControls();
  renderPlayers(Array.from(currentPlayers.values()));
});

useSelectedPlayerButton.addEventListener('click', () => {
  if (!selectedPlayer) {
    showToast('Vui lòng chọn người chơi trước.', 'error');
    return;
  }
  selectedPlayerIds.add(selectedPlayer.id);
  selectedPlayers.set(selectedPlayer.id, selectedPlayer);
  updateSelectedList();
  renderPlayers(Array.from(currentPlayers.values()));
});

clearSelectionButton.addEventListener('click', () => {
  selectedPlayerIds.clear();
  selectedPlayers.clear();
  updateSelectedList();
  renderPlayers(Array.from(currentPlayers.values()));
});

sendMessageButton.addEventListener('click', sendSystemMessage);

logoutButton.addEventListener('click', () => {
  clearToken();
  window.location.href = './index.html';
});

backDashboardButton.addEventListener('click', () => {
  window.location.href = './index.html';
});

ensureSession()
  .then(() => Promise.all([loadPlayers(currentPage), loadItems()]))
  .then(updateSelectionControls);
