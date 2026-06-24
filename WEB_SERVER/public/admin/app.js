const loginCard = document.getElementById('login-card');
const dashboard = document.getElementById('dashboard');
const friendCodeInput = document.getElementById('friendCode');
const adminPasswordInput = document.getElementById('admin-password');
const sessionState = document.getElementById('session-state');
const adminName = document.getElementById('admin-name');
const adminFriendcode = document.getElementById('admin-friendcode');
const logoutButton = document.getElementById('logout');
const startButton = document.getElementById('start-btn');
const shutdownButton = document.getElementById('shutdown-btn');
const startRedisButton = document.getElementById('start-redis-btn');
const stopRedisButton = document.getElementById('stop-redis-btn');
const startMonitorButton = document.getElementById('start-monitor-btn');
const stopMonitorButton = document.getElementById('stop-monitor-btn');
const startTestButton = document.getElementById('start-test-btn');
const shutdownTestButton = document.getElementById('shutdown-test-btn');
const testRoomTypeSelect = document.getElementById('test-room-type');
const resultStatus = document.getElementById('result-status');
const resultMessage = document.getElementById('result-message');
const resultDetail = document.getElementById('result-detail');
const openLanguageConfigButton = document.getElementById('open-language-config');
const openDockerManagementButton = document.getElementById('open-docker-management');
const openServerLogsButton = document.getElementById('open-server-logs');
const openPlayerManagementButton = document.getElementById('open-player-management');
const openReadmeButton = document.getElementById('open-readme');
const openMarketDashboardButton = document.getElementById('open-market-dashboard');
const loadingBackdrop = document.getElementById('loading-backdrop');
const loadingText = document.getElementById('loading-text');
const toast = document.getElementById('toast');
const loginHint = document.getElementById('login-hint');

const TOKEN_KEY = 'admin_ui_token';
const DEFAULT_TEST_TYPE_MATCH_GID = 10000002;
const ADMIN_API_BASE = window.location.pathname.startsWith('/paper-legends/')
  ? '/paper-legends/api/admin'
  : '/api/admin';
const adminApiUrl = (endpoint) => `${ADMIN_API_BASE}/${endpoint.toString().replace(/^\/+/, '')}`;

const clearResult = () => {
  resultStatus.className = 'pill neutral';
  resultStatus.textContent = 'Đang chờ';
  resultMessage.textContent = 'Chưa có thao tác.';
  resultDetail.textContent = '';
};

const showToast = (message, type = 'success') => {
  toast.textContent = message;
  toast.className = `toast ${type}`;
  toast.classList.remove('hidden');

  setTimeout(() => {
    toast.classList.add('hidden');
  }, 2600);
};

const setLoading = (isLoading, message = 'Đang xử lý...') => {
  if (isLoading) {
    loadingText.textContent = message;
    loadingBackdrop.classList.remove('hidden');
    return;
  }
  loadingBackdrop.classList.add('hidden');
};

const HTML_ESCAPE_MAP = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
};

const escapeHtml = (value = '') =>
  value.toString().replace(/[&<>"']/g, (char) => HTML_ESCAPE_MAP[char] || char);

const saveToken = (token) => localStorage.setItem(TOKEN_KEY, token);
const getToken = () => localStorage.getItem(TOKEN_KEY);
const clearToken = () => localStorage.removeItem(TOKEN_KEY);

const updateSessionState = (isOnline, admin) => {
  sessionState.textContent = isOnline ? `Đăng nhập: ${admin?.friendCode ?? ''}` : 'Chưa đăng nhập';
  sessionState.classList.toggle('offline', !isOnline);
};

const setAuthenticatedUI = (admin) => {
  loginCard.classList.add('hidden');
  dashboard.classList.remove('hidden');
  adminName.textContent = admin?.friendCode ?? 'Admin';
  adminFriendcode.textContent = admin?.providerType
    ? `${admin.friendCode} · ${admin.providerType}`
    : admin?.friendCode ?? '';
  updateSessionState(true, admin);
  loginHint.textContent = 'Đăng nhập thành công! Bạn có thể bật/tắt server ngay bên dưới.';
  loadTestRoomOptions();
};

const setLoggedOutUI = () => {
  dashboard.classList.add('hidden');
  loginCard.classList.remove('hidden');
  updateSessionState(false);
  clearResult();
  loginHint.textContent = 'Sử dụng friendCode hệ thống và mật khẩu quản trị đã cấu hình.';
};

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
    setLoggedOutUI();
    throw new Error('Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.');
  }

  let body;
  try {
    body = await response.json();
  } catch (error) {
    body = {};
  }

  if (!response.ok) {
    const message = body?.error || 'Có lỗi xảy ra, vui lòng thử lại.';
    throw new Error(message);
  }

  return body;
};

const formatDetail = (data) => JSON.stringify(data, null, 2);
const buildQuery = (params) => new URLSearchParams(params).toString();

const handleAction = async (endpoint, method, loadingLabel, payload) => {
  const targetEndpoint =
    method === 'GET' && payload && Object.keys(payload).length
      ? `${endpoint}?${buildQuery(payload)}`
      : endpoint;

  setLoading(true, loadingLabel);
  try {
    const result = await apiFetch(targetEndpoint, {
      method,
      body: method !== 'GET' && payload ? JSON.stringify(payload) : undefined,
    });
    resultStatus.className = 'pill success';
    resultStatus.textContent = 'Thành công';
    resultMessage.textContent = result.message || 'Thao tác hoàn tất.';
    resultDetail.textContent = formatDetail(result);
    showToast('Thao tác thành công.', 'success');
  } catch (error) {
    resultStatus.className = 'pill danger';
    resultStatus.textContent = 'Thất bại';
    resultMessage.textContent = error.message;
    resultDetail.textContent = '';
    showToast(error.message, 'error');
  } finally {
    setLoading(false);
  }
};

const loadTestRoomOptions = async () => {
  if (!testRoomTypeSelect) {
    return;
  }

  testRoomTypeSelect.innerHTML = '<option value="">Đang tải loại phòng...</option>';
  testRoomTypeSelect.disabled = true;

  try {
    const data = await apiFetch('generals/match-type-options');
    const options = data.options ?? [];
    if (!options.length) {
      testRoomTypeSelect.innerHTML = `<option value="${DEFAULT_TEST_TYPE_MATCH_GID}">Paper Legends Test (${DEFAULT_TEST_TYPE_MATCH_GID})</option>`;
      return;
    }

    testRoomTypeSelect.innerHTML = options
      .map(
        (option) =>
          `<option value="${escapeHtml(option.value)}">${escapeHtml(
            `${option.label} (${option.value})`,
          )}</option>`,
      )
      .join('');

    if (!options.some((option) => Number(option.value) === DEFAULT_TEST_TYPE_MATCH_GID)) {
      testRoomTypeSelect.insertAdjacentHTML(
        'afterbegin',
        `<option value="${DEFAULT_TEST_TYPE_MATCH_GID}">Paper Legends Test (${DEFAULT_TEST_TYPE_MATCH_GID})</option>`,
      );
    }
    testRoomTypeSelect.value = String(DEFAULT_TEST_TYPE_MATCH_GID);
  } catch (error) {
    testRoomTypeSelect.innerHTML = `<option value="${DEFAULT_TEST_TYPE_MATCH_GID}">Paper Legends Test (${DEFAULT_TEST_TYPE_MATCH_GID})</option>`;
    showToast(error.message || 'Không thể tải loại phòng thử nghiệm.', 'error');
  } finally {
    testRoomTypeSelect.disabled = false;
  }
};

const getSelectedTestRoomType = () => {
  const value = testRoomTypeSelect?.value ?? '';
  const typeMatchGid = Number(value);
  return Number.isInteger(typeMatchGid) ? typeMatchGid : DEFAULT_TEST_TYPE_MATCH_GID;
};

const handleLogin = async (event) => {
  event.preventDefault();
  const friendCode = friendCodeInput.value.trim();
  const password = adminPasswordInput.value;

  if (!friendCode) {
    showToast('Vui lòng nhập friendCode.', 'error');
    return;
  }

  if (!password) {
    showToast('Vui lòng nhập mật khẩu quản trị.', 'error');
    return;
  }

  setLoading(true, 'Đang xác thực...');
  try {
    const data = await apiFetch('login', {
      method: 'POST',
      body: JSON.stringify({ friendCode, password }),
    });

    saveToken(data.token);
    adminPasswordInput.value = '';
    setAuthenticatedUI(data.player);
    showToast('Đăng nhập thành công.', 'success');
  } catch (error) {
    showToast(error.message, 'error');
  } finally {
    setLoading(false);
  }
};

const restoreSession = async () => {
  const token = getToken();
  if (!token) {
    return;
  }

  try {
    const { admin } = await apiFetch('session');
    if (admin) {
      setAuthenticatedUI(admin);
    }
  } catch (error) {
    clearToken();
    setLoggedOutUI();
  }
};

document.getElementById('login-form').addEventListener('submit', handleLogin);

logoutButton.addEventListener('click', () => {
  clearToken();
  setLoggedOutUI();
  showToast('Đã đăng xuất.', 'success');
});

startButton.addEventListener('click', () => handleAction('start', 'GET', 'Đang bật server và phòng chờ...'));
shutdownButton.addEventListener('click', () => handleAction('shutdown', 'POST', 'Đang tắt server...'));
startRedisButton?.addEventListener('click', () =>
  handleAction('redis/start', 'POST', 'Đang bật Redis...'),
);
stopRedisButton?.addEventListener('click', () => handleAction('redis/stop', 'POST', 'Đang tắt Redis...'));
startMonitorButton?.addEventListener('click', () =>
  handleAction('monitor/start', 'POST', 'Đang bật paper-legends-monitor...'),
);
stopMonitorButton?.addEventListener('click', () =>
  handleAction('monitor/stop', 'POST', 'Đang tắt paper-legends-monitor...'),
);
startTestButton?.addEventListener('click', () => {
  const typeMatchGid = getSelectedTestRoomType();
  if (!typeMatchGid) {
    showToast('Vui lòng chọn loại phòng thử nghiệm.', 'error');
    return;
  }
  handleAction('test-server/start', 'GET', 'Đang bật server test...', { typeMatchGid });
});
shutdownTestButton?.addEventListener('click', () => {
  const typeMatchGid = getSelectedTestRoomType();
  if (!typeMatchGid) {
    showToast('Vui lòng chọn loại phòng thử nghiệm.', 'error');
    return;
  }
  handleAction('test-server/shutdown', 'POST', 'Đang tắt server test...', { typeMatchGid });
});
openLanguageConfigButton?.addEventListener('click', () => {
  const token = getToken();
  if (!token) {
    showToast('Vui lòng đăng nhập trước khi truy cập cấu hình.', 'error');
    return;
  }

  window.location.href = './config.html';
});

openDockerManagementButton?.addEventListener('click', () => {
  const token = getToken();
  if (!token) {
    showToast('Vui lòng đăng nhập trước khi truy cập Docker.', 'error');
    return;
  }

  window.location.href = './docker.html';
});

openServerLogsButton?.addEventListener('click', () => {
  const token = getToken();
  if (!token) {
    showToast('Vui lòng đăng nhập trước khi xem log API.', 'error');
    return;
  }

  window.location.href = './server-logs.html';
});

openPlayerManagementButton?.addEventListener('click', () => {
  const token = getToken();
  if (!token) {
    showToast('Vui lòng đăng nhập trước khi truy cập quản lý người chơi.', 'error');
    return;
  }

  window.location.href = './players.html';
});

openReadmeButton?.addEventListener('click', () => {
  const token = getToken();
  if (!token) {
    showToast('Vui lòng đăng nhập trước khi truy cập ReadMe.', 'error');
    return;
  }

  window.location.href = './readme.html';
});

openMarketDashboardButton?.addEventListener('click', () => {
  const token = getToken();
  if (!token) {
    showToast('Vui lòng đăng nhập trước khi truy cập Market Dashboard.', 'error');
    return;
  }
  window.location.href = './market-dashboard.html';
});

restoreSession();
