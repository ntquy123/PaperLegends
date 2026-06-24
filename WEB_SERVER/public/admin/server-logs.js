const sessionState = document.getElementById('session-state');
const adminName = document.getElementById('admin-name');
const adminMeta = document.getElementById('admin-meta');
const logoutButton = document.getElementById('logout');
const backDashboardButton = document.getElementById('back-dashboard');
const tabButtons = document.querySelectorAll('.tab-button');
const tabPanels = document.querySelectorAll('.tab-panel');
const refreshButton = document.getElementById('refresh-logs');
const runtimeLogCount = document.getElementById('runtime-log-count');
const runtimeLogContent = document.getElementById('runtime-log-content');
const logTailInput = document.getElementById('log-tail');
const autoRefreshInput = document.getElementById('auto-refresh');
const toggleAutoButton = document.getElementById('toggle-auto');
const apiErrorCount = document.getElementById('api-error-count');
const apiErrorContent = document.getElementById('api-error-content');
const apiErrorSearch = document.getElementById('api-error-search');
const apiErrorFrom = document.getElementById('api-error-from');
const apiErrorTo = document.getElementById('api-error-to');
const apiErrorPageSize = document.getElementById('api-error-page-size');
const apiErrorPrev = document.getElementById('api-error-prev');
const apiErrorNext = document.getElementById('api-error-next');
const apiErrorPagination = document.getElementById('api-error-pagination');
const refreshApiErrorLogsButton = document.getElementById('refresh-api-error-logs');
const toast = document.getElementById('toast');

const TOKEN_KEY = 'admin_ui_token';
const ADMIN_API_BASE = window.location.pathname.startsWith('/paper-legends/')
  ? '/paper-legends/api/admin'
  : '/api/admin';
const adminApiUrl = (endpoint) => `${ADMIN_API_BASE}/${endpoint.toString().replace(/^\/+/, '')}`;

let autoRefreshTimer = null;
let isAutoRefreshPaused = false;
let activeTabId = 'runtime';
let apiErrorCurrentPage = 1;
let apiErrorTotalPages = 1;

const HTML_ESCAPE_MAP = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
};

const escapeHtml = (value = '') => value.toString().replace(/[&<>"']/g, (char) => HTML_ESCAPE_MAP[char] || char);

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
    throw new Error('Login session expired.');
  }

  let body;
  try {
    body = await response.json();
  } catch (error) {
    body = {};
  }

  if (!response.ok) {
    const message = body?.message || body?.error || 'Request failed.';
    throw new Error(message);
  }

  return body;
};

const setSessionState = (admin) => {
  if (!admin) return;
  adminName.textContent = admin.friendCode || 'Admin';
  adminMeta.textContent = admin.providerType ? `${admin.friendCode} - ${admin.providerType}` : admin.friendCode;
  sessionState.textContent = `Logged in: ${admin.friendCode || ''}`;
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
      throw new Error('Invalid session.');
    }
    setSessionState(admin);
  } catch (error) {
    clearToken();
    window.location.href = './index.html';
  }
};

const classifyLogLine = (entry) => {
  const level = (entry?.level || 'log').toString().toLowerCase();
  const message = (entry?.message || '').toString().toLowerCase();

  if (level === 'error' || message.includes(' failed') || message.includes(' error')) {
    return 'error';
  }

  if (level === 'warn' || message.includes('warn')) {
    return 'warn';
  }

  if (level === 'info' || message.includes('connected') || message.includes('ready')) {
    return 'success';
  }

  return 'normal';
};

const formatLogLine = (entry) => {
  const timestamp = entry?.timestamp ? new Date(entry.timestamp).toLocaleString('vi-VN') : '-';
  const level = entry?.level?.toUpperCase?.() ?? 'LOG';
  const message = escapeHtml(entry?.message ?? '');
  const lineClass = classifyLogLine(entry);
  return `<div class="log-line log-line--${lineClass}">[${timestamp}] [${level}] ${message}</div>`;
};

const loadRuntimeLogs = async () => {
  const tail = Number.parseInt(logTailInput.value, 10);
  const safeTail = Number.isFinite(tail) ? Math.min(Math.max(tail, 10), 2000) : 200;

  try {
    const data = await apiFetch(`server-logs?tail=${safeTail}`);
    const logs = Array.isArray(data.logs) ? data.logs : [];
    runtimeLogCount.textContent = `${logs.length} dong`;
    runtimeLogContent.innerHTML = logs.length > 0
      ? logs.map(formatLogLine).join('')
      : '<div class="log-line log-line--normal">Chua co log de hien thi.</div>';
  } catch (error) {
    runtimeLogContent.innerHTML = '<div class="log-line log-line--error">Khong the tai runtime log.</div>';
    showToast(error.message, 'error');
  }
};

const toIsoDate = (input) => {
  if (!input?.value) return '';
  const date = new Date(input.value);
  return Number.isNaN(date.getTime()) ? '' : date.toISOString();
};

const formatJson = (value) => {
  if (value === null || value === undefined) return '';
  try {
    return JSON.stringify(value, null, 2);
  } catch (error) {
    return String(value);
  }
};

const formatApiErrorRow = (entry) => {
  const createdAt = entry?.createdAt ? new Date(entry.createdAt).toLocaleString('vi-VN') : '-';
  const method = escapeHtml(entry?.method ?? '-');
  const status = entry?.statusCode ?? '-';
  const path = escapeHtml(entry?.path ?? '-');
  const message = escapeHtml(entry?.errorMessage ?? '');
  const ipAddress = escapeHtml(entry?.ipAddress ?? '-');
  const userAgent = escapeHtml(entry?.userAgent ?? '-');
  const params = escapeHtml(formatJson(entry?.requestParams));
  const stack = escapeHtml(entry?.errorStack ?? '');

  return `
    <article class="api-error-row">
      <div class="api-error-meta">
        <span>#${escapeHtml(entry?.logId ?? '-')}</span>
        <span>${createdAt}</span>
        <span class="api-error-status">${method} ${status}</span>
      </div>
      <div class="api-error-path">${path}</div>
      <div class="api-error-message">${message || 'No error message'}</div>
      <div class="api-error-client">IP: ${ipAddress} | UA: ${userAgent}</div>
      <details>
        <summary>Request params</summary>
        <pre>${params || '{}'}</pre>
      </details>
      ${stack ? `<details><summary>Error stack</summary><pre>${stack}</pre></details>` : ''}
    </article>
  `;
};

const updateApiErrorPagination = (pagination = {}) => {
  apiErrorCurrentPage = pagination.page ?? apiErrorCurrentPage;
  apiErrorTotalPages = pagination.totalPages ?? apiErrorTotalPages;
  const totalItems = pagination.totalItems ?? 0;

  apiErrorCount.textContent = `${totalItems} loi`;
  apiErrorPagination.textContent = `Page ${apiErrorCurrentPage}/${apiErrorTotalPages}`;
  apiErrorPrev.disabled = apiErrorCurrentPage <= 1;
  apiErrorNext.disabled = apiErrorCurrentPage >= apiErrorTotalPages;
};

const buildApiErrorQuery = () => {
  const pageSize = Number.parseInt(apiErrorPageSize.value, 10);
  const safePageSize = Number.isFinite(pageSize) ? Math.min(Math.max(pageSize, 10), 200) : 50;
  const params = new URLSearchParams({
    page: String(apiErrorCurrentPage),
    pageSize: String(safePageSize),
  });

  const search = apiErrorSearch.value.trim();
  const from = toIsoDate(apiErrorFrom);
  const to = toIsoDate(apiErrorTo);

  if (search) params.set('search', search);
  if (from) params.set('from', from);
  if (to) params.set('to', to);

  return params.toString();
};

const loadApiErrorLogs = async () => {
  try {
    const data = await apiFetch(`api-error-logs?${buildApiErrorQuery()}`);
    const logs = Array.isArray(data.logs) ? data.logs : [];
    updateApiErrorPagination(data.pagination);
    apiErrorContent.innerHTML = logs.length > 0
      ? logs.map(formatApiErrorRow).join('')
      : '<div class="empty-state">Chua co API error log.</div>';
  } catch (error) {
    apiErrorContent.innerHTML = '<div class="log-line log-line--error">Khong the tai API error log.</div>';
    showToast(error.message, 'error');
  }
};

const loadActiveLogs = () => {
  if (activeTabId === 'api-errors') {
    return loadApiErrorLogs();
  }

  return loadRuntimeLogs();
};

const activateTab = (tabId) => {
  activeTabId = tabId;
  tabButtons.forEach((btn) => btn.classList.toggle('active', btn.dataset.tab === tabId));
  tabPanels.forEach((panel) => panel.classList.toggle('active', panel.dataset.tabPanel === tabId));
  void loadActiveLogs();
};

const resetAndLoadApiErrors = () => {
  apiErrorCurrentPage = 1;
  void loadApiErrorLogs();
};

const updateAutoRefreshState = () => {
  const intervalSeconds = Number.parseInt(autoRefreshInput.value, 10);
  const safeInterval = Number.isFinite(intervalSeconds) ? Math.max(intervalSeconds, 0) : 0;

  if (autoRefreshTimer) {
    clearInterval(autoRefreshTimer);
    autoRefreshTimer = null;
  }

  if (safeInterval > 0 && !isAutoRefreshPaused) {
    autoRefreshTimer = setInterval(loadActiveLogs, safeInterval * 1000);
    toggleAutoButton.textContent = 'Pause';
  } else {
    toggleAutoButton.textContent = 'Resume';
  }
};

tabButtons.forEach((button) => {
  button.addEventListener('click', () => activateTab(button.dataset.tab));
});

refreshButton.addEventListener('click', () => loadRuntimeLogs());
refreshApiErrorLogsButton.addEventListener('click', () => loadApiErrorLogs());
logTailInput.addEventListener('change', () => loadRuntimeLogs());
autoRefreshInput.addEventListener('change', () => updateAutoRefreshState());
toggleAutoButton.addEventListener('click', () => {
  isAutoRefreshPaused = !isAutoRefreshPaused;
  updateAutoRefreshState();
});

apiErrorSearch.addEventListener('change', resetAndLoadApiErrors);
apiErrorFrom.addEventListener('change', resetAndLoadApiErrors);
apiErrorTo.addEventListener('change', resetAndLoadApiErrors);
apiErrorPageSize.addEventListener('change', resetAndLoadApiErrors);
apiErrorPrev.addEventListener('click', () => {
  if (apiErrorCurrentPage <= 1) return;
  apiErrorCurrentPage -= 1;
  void loadApiErrorLogs();
});
apiErrorNext.addEventListener('click', () => {
  if (apiErrorCurrentPage >= apiErrorTotalPages) return;
  apiErrorCurrentPage += 1;
  void loadApiErrorLogs();
});

logoutButton.addEventListener('click', () => {
  clearToken();
  window.location.href = './index.html';
});

backDashboardButton.addEventListener('click', () => {
  window.location.href = './index.html';
});

ensureSession()
  .then(loadRuntimeLogs)
  .finally(updateAutoRefreshState);
