const sessionState = document.getElementById('session-state');
const adminName = document.getElementById('admin-name');
const adminMeta = document.getElementById('admin-meta');
const logoutButton = document.getElementById('logout');
const backDashboardButton = document.getElementById('back-dashboard');
const readmeContent = document.getElementById('readme-content');
const loadingBackdrop = document.getElementById('loading-backdrop');
const loadingText = document.getElementById('loading-text');
const toast = document.getElementById('toast');

const TOKEN_KEY = 'admin_ui_token';
const ADMIN_API_BASE = window.location.pathname.startsWith('/paper-legends/')
  ? '/paper-legends/api/admin'
  : '/api/admin';
const adminApiUrl = (endpoint) => `${ADMIN_API_BASE}/${endpoint.toString().replace(/^\/+/, '')}`;

const HTML_ESCAPE_MAP = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
};

const escapeHtml = (value = '') => value.toString().replace(/[&<>"']/g, (char) => HTML_ESCAPE_MAP[char] || char);

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

const parseMarkdown = (content = '') => {
  const lines = content.split(/\r?\n/);
  let html = '';
  let inList = false;
  let inCode = false;
  let codeBuffer = [];

  const closeList = () => {
    if (inList) {
      html += '</ul>';
      inList = false;
    }
  };

  const flushCode = () => {
    if (codeBuffer.length) {
      html += `<pre><code>${escapeHtml(codeBuffer.join('\n'))}</code></pre>`;
      codeBuffer = [];
    }
  };

  lines.forEach((rawLine) => {
    const line = rawLine.trimEnd();

    if (inCode) {
      if (line.startsWith('```')) {
        inCode = false;
        flushCode();
      } else {
        codeBuffer.push(rawLine);
      }
      return;
    }

    if (line.startsWith('```')) {
      closeList();
      inCode = true;
      return;
    }

    if (!line.trim()) {
      closeList();
      return;
    }

    if (line.startsWith('### ')) {
      closeList();
      html += `<h4>${escapeHtml(line.replace('### ', ''))}</h4>`;
      return;
    }

    if (line.startsWith('## ')) {
      closeList();
      html += `<h3>${escapeHtml(line.replace('## ', ''))}</h3>`;
      return;
    }

    if (line.startsWith('# ')) {
      closeList();
      html += `<h2>${escapeHtml(line.replace('# ', ''))}</h2>`;
      return;
    }

    if (line.startsWith('- ')) {
      if (!inList) {
        html += '<ul>';
        inList = true;
      }
      html += `<li>${escapeHtml(line.replace('- ', ''))}</li>`;
      return;
    }

    closeList();
    html += `<p>${escapeHtml(line)}</p>`;
  });

  closeList();
  if (inCode) {
    flushCode();
  }

  return html || '<p>Không có nội dung ReadMe.</p>';
};

const loadReadme = async () => {
  setLoading(true, 'Đang tải ReadMe...');
  try {
    const data = await apiFetch('readme');
    const content = data?.content || '';
    readmeContent.innerHTML = parseMarkdown(content);
  } catch (error) {
    readmeContent.innerHTML = '<p>Không thể tải nội dung ReadMe.</p>';
    showToast(error.message || 'Không thể tải ReadMe.', 'error');
  } finally {
    setLoading(false);
  }
};

logoutButton.addEventListener('click', () => {
  clearToken();
  window.location.href = './index.html';
});

backDashboardButton.addEventListener('click', () => {
  window.location.href = './index.html';
});

ensureSession().then(loadReadme);
