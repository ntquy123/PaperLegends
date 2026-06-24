const sessionState = document.getElementById('session-state');
const adminName = document.getElementById('admin-name');
const adminMeta = document.getElementById('admin-meta');
const logoutButton = document.getElementById('logout');
const backDashboardButton = document.getElementById('back-dashboard');
const tabButtons = document.querySelectorAll('.tab-button');
const tabPanels = document.querySelectorAll('.tab-panel');

const languageGrid = document.getElementById('language-grid');
const languageCount = document.getElementById('language-count');
const languageForm = document.getElementById('language-form');
const codeInput = document.getElementById('lang-code');
const languageFormatSelect = document.getElementById('language-format');
const languagePlainFields = document.getElementById('language-plain-fields');
const languageRichFields = document.getElementById('language-rich-fields');
const viPlainInput = document.getElementById('lang-vi-plain');
const enPlainInput = document.getElementById('lang-en-plain');
const viInput = document.getElementById('lang-vi');
const enInput = document.getElementById('lang-en');
const richToolbars = document.querySelectorAll('.rich-toolbar');
const cancelEditButton = document.getElementById('cancel-edit');
const submitButton = document.getElementById('submit-language');
const formTitle = document.getElementById('form-title');
const formMode = document.getElementById('form-mode');
const searchInput = document.getElementById('language-search');
const paginationInfo = document.getElementById('pagination-info');
const prevPageButton = document.getElementById('prev-page');
const nextPageButton = document.getElementById('next-page');

const generalGrid = document.getElementById('general-grid');
const generalCount = document.getElementById('general-count');
const generalForm = document.getElementById('general-form');
const generalCodeInput = document.getElementById('general-code');
const generalCateInput = document.getElementById('general-cate');
const generalNameInput = document.getElementById('general-name');
const generalParentInput = document.getElementById('general-parent');
const generalDescriptionInput = document.getElementById('general-description');
const generalCancelEdit = document.getElementById('cancel-general-edit');
const generalSubmitButton = document.getElementById('submit-general');
const generalFormTitle = document.getElementById('general-form-title');
const generalFormMode = document.getElementById('general-form-mode');
const generalSearchInput = document.getElementById('general-search');
const generalPaginationInfo = document.getElementById('general-pagination');
const generalPrevButton = document.getElementById('general-prev');
const generalNextButton = document.getElementById('general-next');

const itemGrid = document.getElementById('item-grid');
const itemCount = document.getElementById('item-count');
const itemForm = document.getElementById('item-form');
const itemIdInput = document.getElementById('item-id');
const itemNameInput = document.getElementById('item-name');
const itemDescriptionInput = document.getElementById('item-description');
const itemImageInput = document.getElementById('item-image');
const itemLevelInput = document.getElementById('item-level');
const itemTypeInput = document.getElementById('item-type');
const itemRarityInput = document.getElementById('item-rarity');
const itemLocationInput = document.getElementById('item-location');
const itemPriceInput = document.getElementById('item-price');
const itemPriceBallInput = document.getElementById('item-price-ball');
const itemMaxPurchasePerDayInput = document.getElementById('item-max-purchase-day');
const itemElementInput = document.getElementById('item-element');
const itemLevelRequiredInput = document.getElementById('item-level-required');
const itemIsLevelUpSelect = document.getElementById('item-is-level-up');
const itemIsOpenSelect = document.getElementById('item-is-open');
const itemIsCateyeSelect = document.getElementById('item-is-cateye');
const itemMassInput = document.getElementById('item-mass');
const itemGravityInput = document.getElementById('item-gravity');
const itemDragInput = document.getElementById('item-drag');
const itemBouncinessInput = document.getElementById('item-bounciness');
const itemElasticityInput = document.getElementById('item-elasticity');
const itemImpactInput = document.getElementById('item-impact');
const itemCuliOnlyFields = Array.from(document.querySelectorAll('.culi-only-field'));
const itemCuliSliderSection = document.getElementById('culi-stat-sliders');
const itemMassSlider = document.getElementById('item-mass-slider');
const itemMassSliderPercent = document.getElementById('item-mass-slider-percent');
const itemMassSliderValue = document.getElementById('item-mass-slider-value');
const itemGravitySlider = document.getElementById('item-gravity-slider');
const itemGravitySliderPercent = document.getElementById('item-gravity-slider-percent');
const itemGravitySliderValue = document.getElementById('item-gravity-slider-value');
const itemDragSlider = document.getElementById('item-drag-slider');
const itemDragSliderPercent = document.getElementById('item-drag-slider-percent');
const itemDragSliderValue = document.getElementById('item-drag-slider-value');
const itemBouncinessSlider = document.getElementById('item-bounciness-slider');
const itemBouncinessSliderPercent = document.getElementById('item-bounciness-slider-percent');
const itemBouncinessSliderValue = document.getElementById('item-bounciness-slider-value');
const itemElasticitySlider = document.getElementById('item-elasticity-slider');
const itemElasticitySliderPercent = document.getElementById('item-elasticity-slider-percent');
const itemElasticitySliderValue = document.getElementById('item-elasticity-slider-value');
const itemImpactSlider = document.getElementById('item-impact-slider');
const itemImpactSliderPercent = document.getElementById('item-impact-slider-percent');
const itemImpactSliderValue = document.getElementById('item-impact-slider-value');
const itemCancelEdit = document.getElementById('cancel-item-edit');
const itemSubmitButton = document.getElementById('submit-item');
const itemFormTitle = document.getElementById('item-form-title');
const itemFormMode = document.getElementById('item-form-mode');
const itemFormModal = document.getElementById('item-form-modal');
const itemFormBackdrop = document.getElementById('item-form-backdrop');
const openItemCreateButton = document.getElementById('open-item-create');
const closeItemModalButton = document.getElementById('close-item-modal');
const itemSearchInput = document.getElementById('item-search');
const itemTypeFilterSelect = document.getElementById('item-type-filter');
const itemLocationFilterSelect = document.getElementById('item-location-filter');
const itemPaginationInfo = document.getElementById('item-pagination');
const itemPrevButton = document.getElementById('item-prev');
const itemNextButton = document.getElementById('item-next');

const achievementGrid = document.getElementById('achievement-grid');
const achievementCount = document.getElementById('achievement-count');
const achievementForm = document.getElementById('achievement-form');
const achievementRewardTypeInput = document.getElementById('achievement-reward-type');
const achievementSeqInput = document.getElementById('achievement-seq');
const achievementLocationInput = document.getElementById('achievement-location');
const achievementAmountInput = document.getElementById('achievement-amount');
const achievementItemInput = document.getElementById('achievement-item');
const achievementCountInput = document.getElementById('achievement-count-gif');
const achievementIsUsedSelect = document.getElementById('achievement-is-used');
const achievementDateInput = document.getElementById('achievement-date');
const achievementCancelEdit = document.getElementById('cancel-achievement-edit');
const achievementSubmitButton = document.getElementById('submit-achievement');
const achievementFormTitle = document.getElementById('achievement-form-title');
const achievementFormMode = document.getElementById('achievement-form-mode');
const achievementSearchInput = document.getElementById('achievement-search');
const achievementPaginationInfo = document.getElementById('achievement-pagination');
const achievementPrevButton = document.getElementById('achievement-prev');
const achievementNextButton = document.getElementById('achievement-next');

const loadingBackdrop = document.getElementById('loading-backdrop');
const loadingText = document.getElementById('loading-text');
const toast = document.getElementById('toast');

const TOKEN_KEY = 'admin_ui_token';
const ADMIN_API_BASE = window.location.pathname.startsWith('/paper-legends/')
  ? '/paper-legends/api/admin'
  : '/api/admin';
const adminApiUrl = (endpoint) => `${ADMIN_API_BASE}/${endpoint.toString().replace(/^\/+/, '')}`;
const PAGE_SIZE = 10;

let languages = [];
let editingCode = null;
let searchTerm = '';
let currentPage = 1;

let generals = [];
let editingGeneral = null;
let generalSearchTerm = '';
let generalPage = 1;

let items = [];
let editingItem = null;
let itemSearchTerm = '';
let itemTypeFilter = 'all';
let itemLocationFilter = 'all';
let itemPage = 1;
let itemRarityOptions = [];

let achievements = [];
let editingAchievement = null;
let achievementSearchTerm = '';
let achievementPage = 1;
let rewardTypeOptions = [];
let itemSelectOptions = [];

const HTML_ESCAPE_MAP = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
};

const ITEM_LOCATION_LABELS = {
  0: 'Equipped',
  1: 'Inventory',
  2: 'Shop',
  3: 'Gif',
  4: 'Vip',
  5: 'CompanionBall',
};

const ITEM_TYPE_LABELS = {
  0: 'All',
  1: 'Culi',
  2: 'Gem',
  3: 'Clother',
  4: 'Hair',
  5: 'Other',
  6: 'Sale',
  7: 'PackageMoney',
  8: 'PackageBall',
};

const DEFAULT_ITEM_RARITY = 11300001;
const ITEM_IMAGE_BASE_URL = '/images/items';

const escapeHtml = (value = '') => value.toString().replace(/[&<>"']/g, (char) => HTML_ESCAPE_MAP[char] || char);
const stripHtml = (value = '') => value.toString().replace(/<[^>]*>/g, '');
const normalizeWhitespace = (value = '') => value.replace(/\s+/g, ' ').trim();
const ALLOWED_RICH_TAGS = new Set(['BR', 'B', 'STRONG', 'I', 'EM', 'U', 'SPAN', 'DIV', 'P', 'FONT']);
const ALLOWED_COLOR_NAMES = new Set(['red', 'blue', 'green', 'black']);

const hasRichMarkup = (value = '') => /<\/?[a-z][\s\S]*>/i.test(value);

const isAllowedColor = (color = '') => {
  const normalized = color.trim().toLowerCase();
  if (!normalized) return false;
  const isHex = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(normalized);
  const isRgb = /^rgba?\((\s*\d+\s*,){2}\s*\d+\s*(,\s*(0(\.\d+)?|1(\.0+)?)\s*)?\)$/i.test(
    normalized
  );
  return isHex || isRgb || ALLOWED_COLOR_NAMES.has(normalized);
};

const sanitizeRichText = (html = '') => {
  const parser = new DOMParser();
  const doc = parser.parseFromString(`<div>${html}</div>`, 'text/html');
  const container = doc.body.firstChild;

  const cleanNode = (node) => {
    if (node.nodeType === Node.TEXT_NODE) return;
    if (node.nodeType !== Node.ELEMENT_NODE) {
      node.remove();
      return;
    }

    if (!ALLOWED_RICH_TAGS.has(node.tagName)) {
      const textNode = doc.createTextNode(node.textContent || '');
      node.replaceWith(textNode);
      return;
    }

    if (node.tagName === 'FONT') {
      const span = doc.createElement('span');
      const color = node.getAttribute('color') || '';
      if (isAllowedColor(color)) {
        span.style.color = color.trim();
      }
      while (node.firstChild) {
        span.appendChild(node.firstChild);
      }
      node.replaceWith(span);
      cleanNode(span);
      return;
    }

    if (node.tagName === 'SPAN') {
      const color = node.style.color || '';
      if (!isAllowedColor(color)) {
        node.style.color = '';
      }
      Array.from(node.attributes).forEach((attr) => {
        if (attr.name !== 'style') node.removeAttribute(attr.name);
      });
    } else {
      Array.from(node.attributes).forEach((attr) => node.removeAttribute(attr.name));
    }

    Array.from(node.childNodes).forEach(cleanNode);
  };

  Array.from(container.childNodes).forEach(cleanNode);
  return container.innerHTML;
};

const getRichValue = (element) => sanitizeRichText(element.innerHTML);
const getRichPlain = (element) => normalizeWhitespace(stripHtml(element.innerHTML));
const getPlainFromRich = (element) => normalizeWhitespace(element.innerText || element.textContent || '');
const htmlToPlainText = (value = '') => {
  const parser = new DOMParser();
  const doc = parser.parseFromString(`<div>${sanitizeRichText(value)}</div>`, 'text/html');
  return normalizeWhitespace(doc.body.textContent || '');
};
const setRichEditorValue = (editor, value = '') => {
  editor.innerHTML = escapeHtml(value).replace(/\n/g, '<br>');
};
const getItemLabel = (mapping, value) => {
  if (value === null || value === undefined || Number.isNaN(Number(value))) {
    return '—';
  }
  const label = mapping[Number(value)];
  return label ? `${label} (${value})` : `${value}`;
};
const getOptionLabel = (options, value) => {
  if (value === null || value === undefined || Number.isNaN(Number(value))) {
    return '—';
  }
  const found = options.find((option) => Number(option.value) === Number(value));
  return found ? `${found.label} (${value})` : `${value}`;
};
const getItemImageUrl = (id) => `${ITEM_IMAGE_BASE_URL}/${encodeURIComponent(id)}.png`;
const fileToDataUrl = (file) =>
  new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.onerror = () => reject(new Error('Khong the doc anh.'));
    reader.readAsDataURL(file);
  });
const deriveGenCate = (GenCode) => {
  if (!Number.isInteger(GenCode)) return '';
  const normalized = Math.abs(GenCode).toString();
  return Number.parseInt(normalized.slice(0, 3), 10);
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
  const isFormDataBody = options.body instanceof FormData;
  const headers = {
    ...(isFormDataBody ? {} : { 'Content-Type': 'application/json' }),
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

const buildSelectOption = (value, label) => {
  const option = document.createElement('option');
  option.value = value;
  option.textContent = label;
  return option;
};

const populateSelectOptions = (select, options, { placeholder = '', allowEmpty = false } = {}) => {
  const currentValue = select.value;
  select.innerHTML = '';

  if (allowEmpty) {
    select.appendChild(buildSelectOption('', placeholder || '—'));
  } else if (placeholder) {
    const placeholderOption = buildSelectOption('', placeholder);
    placeholderOption.disabled = true;
    placeholderOption.selected = true;
    select.appendChild(placeholderOption);
  }

  options.forEach((option) => {
    select.appendChild(buildSelectOption(option.value, option.label));
  });

  if (currentValue) {
    select.value = currentValue;
  }
};

const ensureSelectOption = (select, value, label) => {
  const normalizedValue = value === null || value === undefined ? '' : String(value);
  if (!normalizedValue) return;
  const exists = Array.from(select.options).some((option) => option.value === normalizedValue);
  if (exists) return;
  select.appendChild(buildSelectOption(normalizedValue, label || normalizedValue));
};

const applyItemRarityOptions = (selectedValue) => {
  populateSelectOptions(itemRarityInput, itemRarityOptions, { placeholder: 'Chọn độ hiếm' });
  const fallbackValue =
    itemRarityOptions.find((option) => Number(option.value) === DEFAULT_ITEM_RARITY)?.value ??
    itemRarityOptions[0]?.value ??
    DEFAULT_ITEM_RARITY;
  itemRarityInput.value = selectedValue ?? fallbackValue;
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

const activateTab = (tabId) => {
  tabButtons.forEach((btn) => btn.classList.toggle('active', btn.dataset.tab === tabId));
  tabPanels.forEach((panel) => panel.classList.toggle('active', panel.dataset.tabPanel === tabId));
};

const applyRichCommand = (editor, command, value) => {
  if (!editor) return;
  editor.focus();
  if (command === 'removeFormat') {
    document.execCommand('removeFormat');
    return;
  }
  document.execCommand(command, false, value);
};

richToolbars.forEach((toolbar) => {
  toolbar.addEventListener('click', (event) => {
    const button = event.target.closest('.rich-button');
    if (!button) return;
    const editor = toolbar.closest('.rich-editor')?.querySelector('.rich-input');
    const { command, value } = button.dataset;
    applyRichCommand(editor, command, value);
  });
});

let languageFormat = 'plain';

const setLanguageFormat = (mode, { syncValues = false } = {}) => {
  const nextMode = mode === 'rich' ? 'rich' : 'plain';
  if (syncValues && nextMode !== languageFormat) {
    if (nextMode === 'rich') {
      setRichEditorValue(viInput, viPlainInput.value);
      setRichEditorValue(enInput, enPlainInput.value);
    } else {
      viPlainInput.value = getPlainFromRich(viInput);
      enPlainInput.value = getPlainFromRich(enInput);
    }
  }

  languageFormat = nextMode;
  languageFormatSelect.value = languageFormat;
  languageRichFields.classList.toggle('hidden', languageFormat !== 'rich');
  languagePlainFields.classList.toggle('hidden', languageFormat !== 'plain');
};

// Language helpers
const updateLanguageCount = (filteredLength = languages.length) => {
  if (filteredLength !== languages.length) {
    languageCount.textContent = `${filteredLength}/${languages.length} bản ghi`;
    return;
  }
  languageCount.textContent = `${languages.length} bản ghi`;
};

const formatLanguageValue = (value = '') => {
  if (hasRichMarkup(value)) {
    return { html: sanitizeRichText(value), isRich: true };
  }
  return { html: escapeHtml(value).replace(/\n/g, '<br>'), isRich: false };
};

const getFilteredLanguages = () => {
  const keyword = searchTerm.trim().toLowerCase();
  if (!keyword) return [...languages];

  return languages.filter((language) => {
    const { code = '', vietnamText = '', englishText = '' } = language;
    return [code, stripHtml(vietnamText), stripHtml(englishText)].some((value) =>
      value.toString().toLowerCase().includes(keyword)
    );
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

const renderLanguages = () => {
  const filtered = getFilteredLanguages();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  currentPage = filtered.length ? Math.min(currentPage, totalPages) : 1;

  updateLanguageCount(filtered.length);
  updatePaginationControls(filtered.length, totalPages);

  if (!filtered.length) {
    const message = searchTerm.trim()
      ? `Không tìm thấy cấu hình cho từ khóa "${escapeHtml(searchTerm)}".`
      : 'Chưa có cấu hình ngôn ngữ nào.';
    languageGrid.innerHTML = `<div class="docker-empty">${message}</div>`;
    return;
  }

  const startIndex = (currentPage - 1) * PAGE_SIZE;
  const pageItems = filtered.slice(startIndex, startIndex + PAGE_SIZE);

  languageGrid.innerHTML = pageItems
    .map((language) => {
      const vietnamDisplay = formatLanguageValue(language.vietnamText);
      const englishDisplay = formatLanguageValue(language.englishText);
      return `
        <article class="data-row">
          <div class="row-main">
            <div class="code-badge">${escapeHtml(language.code)}</div>
            <div class="row-text">
              <p class="row-label">Tiếng Việt</p>
              <div class="row-value${vietnamDisplay.isRich ? ' rich-text' : ''}">${vietnamDisplay.html}</div>
            </div>
            <div class="row-text">
              <p class="row-label">Tiếng Anh</p>
              <div class="row-value${englishDisplay.isRich ? ' rich-text' : ''}">${englishDisplay.html}</div>
            </div>
          </div>
          <div class="row-actions">
            <button class="chip-action chip-button" data-action="edit" data-code="${escapeHtml(
              language.code
            )}">Sửa</button>
            <button class="chip-action chip-button danger" data-action="delete" data-code="${escapeHtml(
              language.code
            )}">Xóa</button>
          </div>
        </article>
      `;
    })
    .join('');
};

const fetchLanguages = async () => {
  setLoading(true, 'Đang tải grid ngôn ngữ...');
  try {
    const data = await apiFetch('languages');
    languages = Array.isArray(data) ? data : data?.languages || [];
    currentPage = 1;
    renderLanguages();
  } catch (error) {
    languageGrid.innerHTML = `<div class="docker-error">${escapeHtml(
      error.message || 'Không thể tải dữ liệu.'
    )}</div>`;
    showToast(error.message || 'Không thể tải dữ liệu.', 'error');
  } finally {
    setLoading(false);
  }
};

const resetForm = () => {
  languageForm.reset();
  editingCode = null;
  submitButton.textContent = 'Thêm cấu hình';
  formTitle.textContent = 'Thêm mới config';
  formMode.textContent = 'Thêm mới';
  formMode.className = 'pill neutral';
  cancelEditButton.classList.add('hidden');
  codeInput.removeAttribute('readonly');
  viInput.innerHTML = '';
  enInput.innerHTML = '';
  viPlainInput.value = '';
  enPlainInput.value = '';
  setLanguageFormat('plain');
};

const startEdit = (code) => {
  const target = languages.find((lang) => lang.code === code);
  if (!target) return;

  const detectedFormat =
    hasRichMarkup(target.vietnamText) || hasRichMarkup(target.englishText) ? 'rich' : 'plain';

  editingCode = target.code;
  codeInput.value = target.code;
  codeInput.setAttribute('readonly', 'readonly');
  if (detectedFormat === 'rich') {
    viInput.innerHTML = sanitizeRichText(target.vietnamText);
    enInput.innerHTML = sanitizeRichText(target.englishText);
    viPlainInput.value = htmlToPlainText(target.vietnamText);
    enPlainInput.value = htmlToPlainText(target.englishText);
  } else {
    viPlainInput.value = target.vietnamText || '';
    enPlainInput.value = target.englishText || '';
    setRichEditorValue(viInput, viPlainInput.value);
    setRichEditorValue(enInput, enPlainInput.value);
  }

  setLanguageFormat(detectedFormat);
  submitButton.textContent = 'Lưu thay đổi';
  formTitle.textContent = 'Chỉnh sửa config';
  formMode.textContent = 'Chỉnh sửa';
  formMode.className = 'pill warm';
  cancelEditButton.classList.remove('hidden');
  window.scrollTo({ top: 0, behavior: 'smooth' });
};

const submitLanguage = async (event) => {
  event.preventDefault();
  const code = codeInput.value.trim();
  const isRich = languageFormat === 'rich';
  const vietnamText = isRich ? getRichValue(viInput).trim() : viPlainInput.value.trim();
  const englishText = isRich ? getRichValue(enInput).trim() : enPlainInput.value.trim();
  const vietnamCheck = isRich ? getRichPlain(viInput) : vietnamText;
  const englishCheck = isRich ? getRichPlain(enInput) : englishText;

  const payload = {
    code,
    vietnamText,
    englishText,
  };

  if (!payload.code || !vietnamCheck || !englishCheck) {
    showToast('Vui lòng nhập đầy đủ thông tin.', 'error');
    return;
  }

  const method = editingCode ? 'PUT' : 'POST';
  const endpoint = editingCode ? `languages/${encodeURIComponent(editingCode)}` : 'languages';
  const loadingLabel = editingCode ? 'Đang lưu chỉnh sửa...' : 'Đang thêm cấu hình...';

  setLoading(true, loadingLabel);
  try {
    const result = await apiFetch(endpoint, {
      method,
      body: JSON.stringify(payload),
    });

    showToast(result?.message || 'Thao tác thành công.', 'success');
    await fetchLanguages();
    resetForm();
  } catch (error) {
    showToast(error.message || 'Không thể lưu cấu hình.', 'error');
  } finally {
    setLoading(false);
  }
};

const deleteLanguage = async (code) => {
  const confirmDelete = confirm(`Bạn chắc chắn muốn xóa config "${code}"?`);
  if (!confirmDelete) return;

  setLoading(true, 'Đang xóa cấu hình...');
  try {
    const result = await apiFetch(`languages/${encodeURIComponent(code)}`, {
      method: 'DELETE',
    });
    showToast(result?.message || 'Đã xóa cấu hình.', 'success');
    await fetchLanguages();
    if (editingCode === code) {
      resetForm();
    }
  } catch (error) {
    showToast(error.message || 'Không thể xóa cấu hình.', 'error');
  } finally {
    setLoading(false);
  }
};

// SysMasGeneral helpers
const updateGeneralCount = (filteredLength = generals.length) => {
  generalCount.textContent =
    filteredLength !== generals.length
      ? `${filteredLength}/${generals.length} bản ghi`
      : `${generals.length} bản ghi`;
};

const getFilteredGenerals = () => {
  const keyword = generalSearchTerm.trim().toLowerCase();
  if (!keyword) return [...generals];

  return generals.filter((item) => {
    const { GenCode = '', GenCate = '', GenName = '', description = '' } = item;
    return [GenCode, GenCate, GenName, description || '']
      .map((value) => value?.toString().toLowerCase())
      .some((value) => value.includes(keyword));
  });
};

const updateGeneralPagination = (filteredLength, totalPages) => {
  const hasData = filteredLength > 0;
  const displayTotalPages = hasData ? totalPages : 0;
  const displayCurrentPage = hasData ? generalPage : 0;

  generalPaginationInfo.textContent = `Trang ${displayCurrentPage}/${displayTotalPages}`;
  generalPrevButton.disabled = !hasData || generalPage === 1;
  generalNextButton.disabled = !hasData || generalPage === totalPages;
};

const renderGenerals = () => {
  const filtered = getFilteredGenerals();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  generalPage = filtered.length ? Math.min(generalPage, totalPages) : 1;

  updateGeneralCount(filtered.length);
  updateGeneralPagination(filtered.length, totalPages);

  if (!filtered.length) {
    const message = generalSearchTerm.trim()
      ? `Không tìm thấy cấu hình cho từ khóa "${escapeHtml(generalSearchTerm)}".`
      : 'Chưa có cấu hình SysMasGeneral nào.';
    generalGrid.innerHTML = `<div class="docker-empty">${message}</div>`;
    return;
  }

  const startIndex = (generalPage - 1) * PAGE_SIZE;
  const pageItems = filtered.slice(startIndex, startIndex + PAGE_SIZE);

  generalGrid.innerHTML = pageItems
    .map(
      (item) => `
        <article class="data-row">
          <div class="row-main">
            <div class="code-badge">${escapeHtml(item.GenCode)}</div>
            <div class="row-text">
              <p class="row-label">GenName</p>
              <p class="row-value">${escapeHtml(item.GenName)}</p>
            </div>
            <div class="row-text">
              <p class="row-label">GenCate</p>
              <p class="row-value">${item.GenCate ?? deriveGenCate(Number(item.GenCode)) ?? '—'}</p>
            </div>
            <div class="row-text">
              <p class="row-label">ParentCode</p>
              <p class="row-value">${item.ParentCode ?? '—'}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Mô tả</p>
              <p class="row-value">${escapeHtml(item.description || '—')}</p>
            </div>
          </div>
          <div class="row-actions">
            <button class="chip-action chip-button" data-action="edit-general" data-code="${escapeHtml(
              item.GenCode
            )}">Sửa</button>
            <button class="chip-action chip-button danger" data-action="delete-general" data-code="${escapeHtml(
              item.GenCode
            )}">Xóa</button>
          </div>
        </article>
      `
    )
    .join('');
};

const fetchGenerals = async () => {
  setLoading(true, 'Đang tải SysMasGeneral...');
  try {
    const data = await apiFetch('generals');
    generals = Array.isArray(data) ? data : data?.generals || [];
    generalPage = 1;
    renderGenerals();
  } catch (error) {
    generalGrid.innerHTML = `<div class="docker-error">${escapeHtml(error.message || 'Không thể tải dữ liệu.')}</div>`;
    showToast(error.message || 'Không thể tải dữ liệu.', 'error');
  } finally {
    setLoading(false);
  }
};

const resetGeneralForm = () => {
  generalForm.reset();
  editingGeneral = null;
  generalSubmitButton.textContent = 'Thêm SysMasGeneral';
  generalFormTitle.textContent = 'Thêm mới SysMasGeneral';
  generalFormMode.textContent = 'Thêm mới';
  generalFormMode.className = 'pill neutral';
  generalCancelEdit.classList.add('hidden');
  generalCodeInput.removeAttribute('readonly');
  generalCateInput.value = '';
};

const syncGeneralCate = () => {
  const GenCode = Number(generalCodeInput.value);
  const GenCate = deriveGenCate(GenCode);
  generalCateInput.value = Number.isInteger(GenCate) ? GenCate : '';
};

const startGeneralEdit = (GenCode) => {
  const target = generals.find((item) => Number(item.GenCode) === Number(GenCode));
  if (!target) return;

  editingGeneral = target.GenCode;
  generalCodeInput.value = target.GenCode;
  generalCodeInput.setAttribute('readonly', 'readonly');
  generalCateInput.value = target.GenCate ?? deriveGenCate(Number(target.GenCode));
  generalNameInput.value = target.GenName;
  generalParentInput.value = target.ParentCode ?? '';
  generalDescriptionInput.value = target.description ?? '';
  generalSubmitButton.textContent = 'Lưu thay đổi';
  generalFormTitle.textContent = 'Chỉnh sửa SysMasGeneral';
  generalFormMode.textContent = 'Chỉnh sửa';
  generalFormMode.className = 'pill warm';
  generalCancelEdit.classList.remove('hidden');
  window.scrollTo({ top: 0, behavior: 'smooth' });
};

const buildGeneralPayload = () => {
  const GenCode = Number(generalCodeInput.value);
  const GenName = generalNameInput.value.trim();
  const ParentCodeValue = generalParentInput.value;
  const ParentCode = ParentCodeValue === '' ? null : Number(ParentCodeValue);
  const description = generalDescriptionInput.value.trim() || null;

  if (!Number.isInteger(GenCode) || !GenName) {
    return { error: 'Vui lòng nhập GenCode (số nguyên) và GenName.' };
  }

  if (ParentCodeValue !== '' && !Number.isInteger(ParentCode)) {
    return { error: 'ParentCode phải là số nguyên.' };
  }

  return { GenCode, GenName, ParentCode, description };
};

const submitGeneral = async (event) => {
  event.preventDefault();
  const payload = buildGeneralPayload();

  if ('error' in payload) {
    showToast(payload.error, 'error');
    return;
  }

  const method = editingGeneral ? 'PUT' : 'POST';
  const endpoint = editingGeneral ? `generals/${encodeURIComponent(editingGeneral)}` : 'generals';
  const loadingLabel = editingGeneral ? 'Đang lưu chỉnh sửa...' : 'Đang thêm cấu hình...';

  setLoading(true, loadingLabel);
  try {
    const result = await apiFetch(endpoint, {
      method,
      body: JSON.stringify(payload),
    });
    showToast(result?.message || 'Thao tác thành công.', 'success');
    await fetchGenerals();
    resetGeneralForm();
  } catch (error) {
    showToast(error.message || 'Không thể lưu cấu hình.', 'error');
  } finally {
    setLoading(false);
  }
};

const deleteGeneral = async (GenCode) => {
  if (!confirm(`Bạn chắc chắn muốn xóa GenCode "${GenCode}"?`)) return;
  setLoading(true, 'Đang xóa cấu hình...');
  try {
    const result = await apiFetch(`generals/${encodeURIComponent(GenCode)}`, { method: 'DELETE' });
    showToast(result?.message || 'Đã xóa cấu hình.', 'success');
    await fetchGenerals();
    if (editingGeneral === Number(GenCode)) {
      resetGeneralForm();
    }
  } catch (error) {
    showToast(error.message || 'Không thể xóa cấu hình.', 'error');
  } finally {
    setLoading(false);
  }
};

// Item helpers
const updateItemCount = (filteredLength = items.length) => {
  itemCount.textContent =
    filteredLength !== items.length ? `${filteredLength}/${items.length} bản ghi` : `${items.length} bản ghi`;
};

const parseItemFilterValue = (value) => {
  if (!value || value === 'all') return null;
  const parsed = Number(value);
  return Number.isNaN(parsed) ? null : parsed;
};

const getFilteredItems = () => {
  const keyword = itemSearchTerm.trim().toLowerCase();
  const typeFilterValue = parseItemFilterValue(itemTypeFilter);
  const locationFilterValue = parseItemFilterValue(itemLocationFilter);

  return items.filter((item) => {
    if (typeFilterValue !== null && Number(item.typeGid) !== typeFilterValue) {
      return false;
    }

    if (locationFilterValue !== null && Number(item.locationGid) !== locationFilterValue) {
      return false;
    }

    if (!keyword) return true;

    const fields = [item.id, item.name || '', item.description || '', item.rarityGid ?? ''];
    return fields
      .map((value) => value?.toString().toLowerCase())
      .some((value) => value.includes(keyword));
  });
};

const updateItemPagination = (filteredLength, totalPages) => {
  const hasData = filteredLength > 0;
  const displayTotalPages = hasData ? totalPages : 0;
  const displayCurrentPage = hasData ? itemPage : 0;

  itemPaginationInfo.textContent = `Trang ${displayCurrentPage}/${displayTotalPages}`;
  itemPrevButton.disabled = !hasData || itemPage === 1;
  itemNextButton.disabled = !hasData || itemPage === totalPages;
};

const renderItems = () => {
  const filtered = getFilteredItems();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  itemPage = filtered.length ? Math.min(itemPage, totalPages) : 1;

  updateItemCount(filtered.length);
  updateItemPagination(filtered.length, totalPages);

  if (!filtered.length) {
    const message = itemSearchTerm.trim()
      ? `Không tìm thấy item cho từ khóa "${escapeHtml(itemSearchTerm)}".`
      : 'Chưa có item nào.';
    itemGrid.innerHTML = `<div class="docker-empty">${message}</div>`;
    return;
  }

  const startIndex = (itemPage - 1) * PAGE_SIZE;
  const pageItems = filtered.slice(startIndex, startIndex + PAGE_SIZE);

  itemGrid.innerHTML = pageItems
    .map(
      (item) => `
        <article class="data-row item-row">
          <div class="row-main">
            <div class="code-badge">#${escapeHtml(item.id)}</div>
            <div class="row-text item-image-block">
              <p class="row-label">Anh</p>
              <img
                class="item-thumb"
                src="${getItemImageUrl(item.id)}"
                alt="item-${escapeHtml(item.id)}"
                loading="lazy"
                onerror="this.style.display='none'; this.nextElementSibling.style.display='block';"
              />
              <p class="row-value item-thumb-empty" style="display:none;">Khong co anh</p>
            </div>
            <div class="row-text">
              <p class="row-label">Tên</p>
              <p class="row-value">${escapeHtml(item.name)}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Loại · Level</p>
              <p class="row-value">${escapeHtml(getItemLabel(ITEM_TYPE_LABELS, item.typeGid))} · Level ${escapeHtml(
                item.level
              )}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Độ hiếm</p>
              <p class="row-value">${escapeHtml(getOptionLabel(itemRarityOptions, item.rarityGid))}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Vị trí</p>
              <p class="row-value">${escapeHtml(getItemLabel(ITEM_LOCATION_LABELS, item.locationGid))}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Giá</p>
              <p class="row-value">${escapeHtml(item.price)} / Ball ${escapeHtml(item.priceByBall ?? '—')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Giới hạn mua/ngày</p>
              <p class="row-value">${Number(item.locationGid) === 2 ? escapeHtml(item.maxPurchasePerDay ?? '—') : '—'}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Trạng thái</p>
              <p class="row-value">${item.isOpen ? 'Đang mở' : 'Đóng'} · ${
                item.isLevelUp ? 'Nâng cấp' : 'Không nâng cấp'
              } · ${item.isCateye ? 'Cateye' : 'Không Cateye'}</p>
            </div>
          </div>
          <div class="row-actions">
            <button class="chip-action chip-button" data-action="edit-item" data-id="${escapeHtml(item.id)}">Sửa</button>
            <button class="chip-action chip-button danger" data-action="delete-item" data-id="${escapeHtml(
              item.id
            )}">Xóa</button>
          </div>
        </article>
      `
    )
    .join('');
};

const fetchItems = async () => {
  setLoading(true, 'Đang tải Item...');
  try {
    const data = await apiFetch('items');
    items = Array.isArray(data) ? data : data?.items || [];
    itemPage = 1;
    renderItems();
  } catch (error) {
    itemGrid.innerHTML = `<div class="docker-error">${escapeHtml(error.message || 'Không thể tải dữ liệu.')}</div>`;
    showToast(error.message || 'Không thể tải dữ liệu.', 'error');
  } finally {
    setLoading(false);
  }
};

const CULI_STAT_SLIDERS = [
  {
    key: 'Mass',
    input: itemMassInput,
    slider: itemMassSlider,
    percentLabel: itemMassSliderPercent,
    valueLabel: itemMassSliderValue,
    min: 0.1,
    max: 2,
    decimals: 2,
  },
  {
    key: 'GravityScale',
    input: itemGravityInput,
    slider: itemGravitySlider,
    percentLabel: itemGravitySliderPercent,
    valueLabel: itemGravitySliderValue,
    min: 0.5,
    max: 2,
    decimals: 2,
  },
  {
    key: 'Drag',
    input: itemDragInput,
    slider: itemDragSlider,
    percentLabel: itemDragSliderPercent,
    valueLabel: itemDragSliderValue,
    min: 0.05,
    max: 0.4,
    decimals: 2,
  },
  {
    key: 'Bounciness',
    input: itemBouncinessInput,
    slider: itemBouncinessSlider,
    percentLabel: itemBouncinessSliderPercent,
    valueLabel: itemBouncinessSliderValue,
    min: 0.1,
    max: 1,
    decimals: 2,
  },
  {
    key: 'Elasticity',
    input: itemElasticityInput,
    slider: itemElasticitySlider,
    percentLabel: itemElasticitySliderPercent,
    valueLabel: itemElasticitySliderValue,
    min: 0.1,
    max: 1,
    decimals: 2,
  },
  {
    key: 'ImpactResistance',
    input: itemImpactInput,
    slider: itemImpactSlider,
    percentLabel: itemImpactSliderPercent,
    valueLabel: itemImpactSliderValue,
    min: 0.1,
    max: 2,
    decimals: 2,
  },
];

const clampValue = (value, min, max) => Math.min(Math.max(value, min), max);
const percentToValue = (percent, min, max) => min + (max - min) * (percent / 100);
const valueToPercent = (value, min, max) => ((value - min) / (max - min)) * 100;
const isCuliType = () => Number(itemTypeInput.value) === 1;
const isShopLocation = () => Number(itemLocationInput.value) === 2;

const formatStatValue = (value, decimals = 2) => {
  if (!Number.isFinite(value)) return '';
  return value.toFixed(decimals);
};

const updateStatLabels = (config, percent, value) => {
  config.percentLabel.textContent = `${Math.round(percent)}%`;
  config.valueLabel.textContent = Number.isFinite(value) ? value.toFixed(config.decimals) : '—';
};

const syncStatSliderFromInput = (config) => {
  const rawValue = parseFloat(config.input.value);
  if (!Number.isFinite(rawValue)) {
    config.slider.value = 0;
    updateStatLabels(config, 0, null);
    return;
  }
  const clamped = clampValue(rawValue, config.min, config.max);
  const percent = clampValue(valueToPercent(clamped, config.min, config.max), 0, 100);
  config.slider.value = percent;
  config.input.value = formatStatValue(clamped, config.decimals);
  updateStatLabels(config, percent, clamped);
};

const syncStatInputFromSlider = (config) => {
  const percent = Number(config.slider.value) || 0;
  const value = percentToValue(percent, config.min, config.max);
  config.input.value = formatStatValue(value, config.decimals);
  updateStatLabels(config, percent, value);
};

const syncAllCuliSliders = () => {
  CULI_STAT_SLIDERS.forEach((config) => syncStatSliderFromInput(config));
};

const updateCuliSliderVisibility = () => {
  const shouldShow = isCuliType();
  itemCuliOnlyFields.forEach((field) => {
    field.classList.toggle('hidden', !shouldShow);
    field.querySelectorAll('input, select').forEach((input) => {
      input.disabled = !shouldShow;
    });
  });
  itemCuliSliderSection.classList.toggle('hidden', !shouldShow);
  if (!shouldShow) {
    itemIsCateyeSelect.value = 'false';
    CULI_STAT_SLIDERS.forEach((config) => {
      config.input.value = '0';
      config.slider.value = 0;
      updateStatLabels(config, 0, null);
    });
    return;
  }
  syncAllCuliSliders();
};

const updateMaxPurchasePerDayVisibility = () => {
  const enableInput = isShopLocation();
  itemMaxPurchasePerDayInput.disabled = !enableInput;
  if (!enableInput) {
    itemMaxPurchasePerDayInput.value = '';
  }
};

const parseOptionalNumber = (value) => {
  if (value === '' || value === undefined || value === null) return null;
  const parsed = Number(value);
  return Number.isNaN(parsed) ? null : parsed;
};

const openItemFormModal = () => {
  if (!itemFormModal || !itemFormBackdrop) return;
  itemFormModal.classList.remove('hidden');
  itemFormBackdrop.classList.remove('hidden');
};

const closeItemFormModal = () => {
  if (!itemFormModal || !itemFormBackdrop) return;
  itemFormModal.classList.add('hidden');
  itemFormBackdrop.classList.add('hidden');
};

const toDateTimeLocalValue = (value) => {
  if (!value) return '';
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.valueOf())) return '';
  const offset = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
};

const buildItemPayload = () => {
  const id = Number(itemIdInput.value);
  const name = itemNameInput.value.trim();
  const description = itemDescriptionInput.value.trim();
  const level = Number(itemLevelInput.value);
  const typeGid = Number(itemTypeInput.value);
  const rarityGid = Number(itemRarityInput.value);
  const locationGid = Number(itemLocationInput.value);
  const price = Number(itemPriceInput.value);
  const priceByBall = parseOptionalNumber(itemPriceBallInput.value);
  const maxPurchasePerDay = parseOptionalNumber(itemMaxPurchasePerDayInput.value);
  const ElementType = parseOptionalNumber(itemElementInput.value);
  const Levelrequired = parseOptionalNumber(itemLevelRequiredInput.value);
  const isLevelUp = itemIsLevelUpSelect.value === 'true';
  const isOpen = itemIsOpenSelect.value === 'true';
  const isCuli = typeGid === 1;
  const isCateye = isCuli ? itemIsCateyeSelect.value === 'true' : false;
  const Mass = isCuli ? parseOptionalNumber(itemMassInput.value) : 0;
  const GravityScale = isCuli ? parseOptionalNumber(itemGravityInput.value) : 0;
  const Drag = isCuli ? parseOptionalNumber(itemDragInput.value) : 0;
  const Bounciness = isCuli ? parseOptionalNumber(itemBouncinessInput.value) : 0;
  const Elasticity = isCuli ? parseOptionalNumber(itemElasticityInput.value) : 0;
  const ImpactResistance = isCuli ? parseOptionalNumber(itemImpactInput.value) : 0;

  const requiredNumbers = [id, level, typeGid, rarityGid, price, locationGid];
  if (requiredNumbers.some((num) => Number.isNaN(num))) {
    return { error: 'ID, Level, TypeGid, Độ hiếm, Giá và LocationGid phải là số.' };
  }

  if (!name || !description) {
    return { error: 'Tên và mô tả không được để trống.' };
  }

  return {
    id,
    name,
    description,
    level,
    typeGid,
    rarityGid,
    price,
    priceByBall,
    maxPurchasePerDay: locationGid === 2 ? maxPurchasePerDay : null,
    locationGid,
    isLevelUp,
    isOpen,
    isCateye,
    ElementType,
    Levelrequired,
    Mass,
    GravityScale,
    Drag,
    Bounciness,
    Elasticity,
    ImpactResistance,
  };
};

const resetItemForm = () => {
  itemForm.reset();
  editingItem = null;
  itemSubmitButton.textContent = 'Thêm Item';
  itemFormTitle.textContent = 'Thêm mới Item';
  itemFormMode.textContent = 'Thêm mới';
  itemFormMode.className = 'pill neutral';
  itemCancelEdit.classList.add('hidden');
  itemIdInput.removeAttribute('readonly');
  itemImageInput.value = '';
  applyItemRarityOptions();
  updateCuliSliderVisibility();
  updateMaxPurchasePerDayVisibility();
};

const startItemEdit = (id) => {
  const target = items.find((item) => Number(item.id) === Number(id));
  if (!target) return;

  editingItem = target.id;
  itemIdInput.value = target.id;
  itemIdInput.setAttribute('readonly', 'readonly');
  itemImageInput.value = '';
  itemNameInput.value = target.name;
  itemDescriptionInput.value = target.description;
  itemLevelInput.value = target.level;
  itemTypeInput.value = target.typeGid;
  const rarityValue = target.rarityGid ?? DEFAULT_ITEM_RARITY;
  ensureSelectOption(itemRarityInput, rarityValue, getOptionLabel(itemRarityOptions, rarityValue));
  itemRarityInput.value = rarityValue;
  itemLocationInput.value = target.locationGid;
  itemPriceInput.value = target.price;
  itemPriceBallInput.value = target.priceByBall ?? '';
  itemMaxPurchasePerDayInput.value = target.maxPurchasePerDay ?? '';
  itemElementInput.value = target.ElementType ?? '';
  itemLevelRequiredInput.value = target.Levelrequired ?? '';
  itemIsLevelUpSelect.value = target.isLevelUp ? 'true' : 'false';
  itemIsOpenSelect.value = target.isOpen ? 'true' : 'false';
  itemIsCateyeSelect.value = target.isCateye ? 'true' : 'false';
  itemMassInput.value = target.Mass ?? '';
  itemGravityInput.value = target.GravityScale ?? '';
  itemDragInput.value = target.Drag ?? '';
  itemBouncinessInput.value = target.Bounciness ?? '';
  itemElasticityInput.value = target.Elasticity ?? '';
  itemImpactInput.value = target.ImpactResistance ?? '';
  itemSubmitButton.textContent = 'Lưu thay đổi';
  itemFormTitle.textContent = 'Chỉnh sửa Item';
  itemFormMode.textContent = 'Chỉnh sửa';
  itemFormMode.className = 'pill warm';
  itemCancelEdit.classList.remove('hidden');
  updateCuliSliderVisibility();
  updateMaxPurchasePerDayVisibility();
  openItemFormModal();
};

// PlayerAchievement helpers
const updateAchievementCount = (filteredLength = achievements.length) => {
  achievementCount.textContent =
    filteredLength !== achievements.length
      ? `${filteredLength}/${achievements.length} bản ghi`
      : `${achievements.length} bản ghi`;
};

const getFilteredAchievements = () => {
  const keyword = achievementSearchTerm.trim().toLowerCase();
  if (!keyword) return [...achievements];

  return achievements.filter((achievement) => {
    const fields = [
      achievement.rewardType,
      achievement.rewardTypeName || '',
      achievement.seq,
      achievement.itemId ?? '',
      achievement.itemName || '',
    ];
    return fields
      .map((value) => value?.toString().toLowerCase())
      .some((value) => value.includes(keyword));
  });
};

const updateAchievementPagination = (filteredLength, totalPages) => {
  const hasData = filteredLength > 0;
  const displayTotalPages = hasData ? totalPages : 0;
  const displayCurrentPage = hasData ? achievementPage : 0;

  achievementPaginationInfo.textContent = `Trang ${displayCurrentPage}/${displayTotalPages}`;
  achievementPrevButton.disabled = !hasData || achievementPage === 1;
  achievementNextButton.disabled = !hasData || achievementPage === totalPages;
};

const renderAchievements = () => {
  const filtered = getFilteredAchievements();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  achievementPage = filtered.length ? Math.min(achievementPage, totalPages) : 1;

  updateAchievementCount(filtered.length);
  updateAchievementPagination(filtered.length, totalPages);

  if (!filtered.length) {
    const message = achievementSearchTerm.trim()
      ? `Không tìm thấy PlayerAchievement cho từ khóa "${escapeHtml(achievementSearchTerm)}".`
      : 'Chưa có PlayerAchievement nào.';
    achievementGrid.innerHTML = `<div class="docker-empty">${message}</div>`;
    return;
  }

  const startIndex = (achievementPage - 1) * PAGE_SIZE;
  const pageItems = filtered.slice(startIndex, startIndex + PAGE_SIZE);

  achievementGrid.innerHTML = pageItems
    .map((achievement) => {
      const rewardTypeLabel = achievement.rewardTypeName
        ? `${achievement.rewardType} · ${achievement.rewardTypeName}`
        : achievement.rewardType;
      return `
        <article class="data-row">
          <div class="row-main">
            <div class="code-badge">${escapeHtml(rewardTypeLabel)}</div>
            <div class="row-text">
              <p class="row-label">Seq</p>
              <p class="row-value">${escapeHtml(achievement.seq)}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Item</p>
              <p class="row-value">${escapeHtml(
                achievement.itemName ? `${achievement.itemId} · ${achievement.itemName}` : achievement.itemId ?? '—'
              )}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Reward</p>
              <p class="row-value">${escapeHtml(achievement.rewardAmount ?? '—')}</p>
            </div>
            <div class="row-text">
              <p class="row-label">Trạng thái</p>
              <p class="row-value">${achievement.isUsed ? 'Đã dùng' : 'Chưa dùng'}</p>
            </div>
          </div>
          <div class="row-actions">
            <button class="chip-action chip-button" data-action="edit-achievement" data-reward-type="${escapeHtml(
              achievement.rewardType
            )}" data-seq="${escapeHtml(achievement.seq)}">Sửa</button>
            <button class="chip-action chip-button danger" data-action="delete-achievement" data-reward-type="${escapeHtml(
              achievement.rewardType
            )}" data-seq="${escapeHtml(achievement.seq)}">Xóa</button>
          </div>
        </article>
      `;
    })
    .join('');
};

const fetchAchievements = async () => {
  setLoading(true, 'Đang tải PlayerAchievement...');
  try {
    const data = await apiFetch('player-achievements');
    achievements = Array.isArray(data) ? data : data?.achievements || [];
    achievementPage = 1;
    renderAchievements();
  } catch (error) {
    achievementGrid.innerHTML = `<div class="docker-error">${escapeHtml(
      error.message || 'Không thể tải dữ liệu.'
    )}</div>`;
    showToast(error.message || 'Không thể tải dữ liệu.', 'error');
  } finally {
    setLoading(false);
  }
};

const fetchRewardTypeOptions = async () => {
  try {
    const data = await apiFetch('generals/reward-type-options');
    rewardTypeOptions = Array.isArray(data?.options) ? data.options : [];
    populateSelectOptions(achievementRewardTypeInput, rewardTypeOptions, { placeholder: 'Chọn reward type' });
  } catch (error) {
    showToast(error.message || 'Không thể tải RewardType.', 'error');
  }
};

const fetchItemRarityOptions = async () => {
  try {
    const data = await apiFetch('generals/item-rarity-options');
    itemRarityOptions = Array.isArray(data?.options) ? data.options : [];
    const selectedValue = itemRarityInput.value ? Number(itemRarityInput.value) : undefined;
    applyItemRarityOptions(Number.isNaN(selectedValue) ? undefined : selectedValue);
  } catch (error) {
    showToast(error.message || 'Không thể tải độ hiếm item.', 'error');
  }
};

const fetchItemOptions = async () => {
  try {
    const data = await apiFetch('items/options');
    itemSelectOptions = Array.isArray(data?.options) ? data.options : [];
    populateSelectOptions(achievementItemInput, itemSelectOptions, {
      placeholder: '— Không chọn item —',
      allowEmpty: true,
    });
  } catch (error) {
    showToast(error.message || 'Không thể tải danh sách item.', 'error');
  }
};

const buildAchievementPayload = () => {
  const rewardType = achievementRewardTypeInput.value.trim();
  const seq = Number(achievementSeqInput.value);
  const locationId = parseOptionalNumber(achievementLocationInput.value);
  const rewardAmount = parseOptionalNumber(achievementAmountInput.value);
  const itemId = parseOptionalNumber(achievementItemInput.value);
  const countGif = parseOptionalNumber(achievementCountInput.value);
  const isUsed = achievementIsUsedSelect.value === 'true';
  const achievedAt = achievementDateInput.value ? new Date(achievementDateInput.value).toISOString() : null;

  if (!rewardType) {
    return { error: 'RewardType không được để trống.' };
  }

  if (!Number.isInteger(seq)) {
    return { error: 'Seq phải là số nguyên.' };
  }

  return {
    rewardType,
    seq,
    locationId,
    rewardAmount,
    itemId,
    countGif,
    isUsed,
    achievedAt,
  };
};

const resetAchievementForm = () => {
  achievementForm.reset();
  editingAchievement = null;
  achievementSubmitButton.textContent = 'Thêm PlayerAchievement';
  achievementFormTitle.textContent = 'Thêm mới PlayerAchievement';
  achievementFormMode.textContent = 'Thêm mới';
  achievementFormMode.className = 'pill neutral';
  achievementCancelEdit.classList.add('hidden');
  achievementRewardTypeInput.removeAttribute('disabled');
  achievementRewardTypeInput.value = '';
  achievementItemInput.value = '';
  populateSelectOptions(achievementRewardTypeInput, rewardTypeOptions, { placeholder: 'Chọn reward type' });
  populateSelectOptions(achievementItemInput, itemSelectOptions, {
    placeholder: '— Không chọn item —',
    allowEmpty: true,
  });
  achievementSeqInput.removeAttribute('readonly');
};

const startAchievementEdit = (rewardType, seq) => {
  const target = achievements.find(
    (achievement) => achievement.rewardType === rewardType && Number(achievement.seq) === Number(seq)
  );
  if (!target) return;

  editingAchievement = { rewardType, seq: Number(seq) };
  ensureSelectOption(
    achievementRewardTypeInput,
    target.rewardType,
    target.rewardTypeName ? `${target.rewardType} · ${target.rewardTypeName}` : target.rewardType,
  );
  achievementRewardTypeInput.value = target.rewardType;
  achievementRewardTypeInput.setAttribute('disabled', 'disabled');
  achievementSeqInput.value = target.seq;
  achievementSeqInput.setAttribute('readonly', 'readonly');
  achievementLocationInput.value = target.locationId ?? '';
  achievementAmountInput.value = target.rewardAmount ?? '';
  if (Number.isInteger(target.itemId)) {
    ensureSelectOption(
      achievementItemInput,
      target.itemId,
      target.itemName ? `${target.itemId} · ${target.itemName}` : target.itemId,
    );
  }
  achievementItemInput.value = target.itemId ?? '';
  achievementCountInput.value = target.countGif ?? '';
  achievementIsUsedSelect.value = target.isUsed ? 'true' : 'false';
  achievementDateInput.value = toDateTimeLocalValue(target.achievedAt);
  achievementSubmitButton.textContent = 'Lưu thay đổi';
  achievementFormTitle.textContent = 'Chỉnh sửa PlayerAchievement';
  achievementFormMode.textContent = 'Chỉnh sửa';
  achievementFormMode.className = 'pill warm';
  achievementCancelEdit.classList.remove('hidden');
  window.scrollTo({ top: 0, behavior: 'smooth' });
};

const submitAchievement = async (event) => {
  event.preventDefault();
  const payload = buildAchievementPayload();

  if ('error' in payload) {
    showToast(payload.error, 'error');
    return;
  }

  const isEditing = Boolean(editingAchievement);
  const endpoint = isEditing
    ? `player-achievements/${encodeURIComponent(editingAchievement.rewardType)}/${encodeURIComponent(
        editingAchievement.seq
      )}`
    : 'player-achievements';
  const method = isEditing ? 'PUT' : 'POST';
  const loadingLabel = isEditing ? 'Đang lưu PlayerAchievement...' : 'Đang thêm PlayerAchievement...';

  setLoading(true, loadingLabel);
  try {
    const result = await apiFetch(endpoint, {
      method,
      body: JSON.stringify(payload),
    });
    showToast(result?.message || 'Thao tác thành công.', 'success');
    await fetchAchievements();
    resetAchievementForm();
  } catch (error) {
    showToast(error.message || 'Không thể lưu PlayerAchievement.', 'error');
  } finally {
    setLoading(false);
  }
};

const deleteAchievement = async (rewardType, seq) => {
  if (!confirm(`Bạn chắc chắn muốn xóa rewardType "${rewardType}" seq ${seq}?`)) return;
  setLoading(true, 'Đang xóa PlayerAchievement...');
  try {
    const result = await apiFetch(
      `player-achievements/${encodeURIComponent(rewardType)}/${encodeURIComponent(seq)}`,
      { method: 'DELETE' }
    );
    showToast(result?.message || 'Đã xóa PlayerAchievement.', 'success');
    await fetchAchievements();
    if (editingAchievement && editingAchievement.rewardType === rewardType && editingAchievement.seq === Number(seq)) {
      resetAchievementForm();
    }
  } catch (error) {
    showToast(error.message || 'Không thể xóa PlayerAchievement.', 'error');
  } finally {
    setLoading(false);
  }
};

const submitItem = async (event) => {
  event.preventDefault();
  const payload = buildItemPayload();

  if ('error' in payload) {
    showToast(payload.error, 'error');
    return;
  }

  const method = editingItem ? 'PUT' : 'POST';
  const endpoint = editingItem ? `items/${encodeURIComponent(editingItem)}` : 'items';
  const loadingLabel = editingItem ? 'Dang luu item...' : 'Dang them item...';
  const selectedImage = itemImageInput.files?.[0] || null;

  setLoading(true, loadingLabel);
  try {
    const requestPayload = { ...payload };
    if (selectedImage) {
      const imageDataUrl = await fileToDataUrl(selectedImage);
      requestPayload.imageBase64 = imageDataUrl;
    }
    const result = await apiFetch(endpoint, {
      method,
      body: JSON.stringify(requestPayload),
    });
    showToast(result?.message || 'Thao tác thành công.', 'success');
    await fetchItems();
    resetItemForm();
    closeItemFormModal();
  } catch (error) {
    showToast(error.message || 'Không thể lưu item.', 'error');
  } finally {
    setLoading(false);
  }
};

const deleteItem = async (id) => {
  if (!confirm(`Bạn chắc chắn muốn xóa item #${id}?`)) return;
  setLoading(true, 'Đang xóa item...');
  try {
    const result = await apiFetch(`items/${encodeURIComponent(id)}`, { method: 'DELETE' });
    showToast(result?.message || 'Đã xóa item.', 'success');
    await fetchItems();
    if (editingItem === Number(id)) {
      resetItemForm();
    }
  } catch (error) {
    showToast(error.message || 'Không thể xóa item.', 'error');
  } finally {
    setLoading(false);
  }
};

// Event bindings
languageGrid.addEventListener('click', (event) => {
  const button = event.target.closest('button[data-action]');
  if (!button) return;

  const code = button.getAttribute('data-code');
  const action = button.getAttribute('data-action');

  if (action === 'edit') {
    startEdit(code);
  }

  if (action === 'delete') {
    deleteLanguage(code);
  }
});

generalGrid.addEventListener('click', (event) => {
  const button = event.target.closest('button[data-action]');
  if (!button) return;
  const code = button.getAttribute('data-code');
  const action = button.getAttribute('data-action');

  if (action === 'edit-general') {
    startGeneralEdit(code);
  }

  if (action === 'delete-general') {
    deleteGeneral(code);
  }
});

itemGrid.addEventListener('click', (event) => {
  const button = event.target.closest('button[data-action]');
  if (!button) return;
  const id = button.getAttribute('data-id');
  const action = button.getAttribute('data-action');

  if (action === 'edit-item') {
    startItemEdit(id);
  }

  if (action === 'delete-item') {
    deleteItem(id);
  }
});

achievementGrid.addEventListener('click', (event) => {
  const button = event.target.closest('button[data-action]');
  if (!button) return;
  const rewardType = button.getAttribute('data-reward-type');
  const seq = button.getAttribute('data-seq');
  const action = button.getAttribute('data-action');

  if (action === 'edit-achievement') {
    startAchievementEdit(rewardType, seq);
  }

  if (action === 'delete-achievement') {
    deleteAchievement(rewardType, seq);
  }
});

languageForm.addEventListener('submit', submitLanguage);
languageFormatSelect.addEventListener('change', (event) => {
  setLanguageFormat(event.target.value, { syncValues: true });
});
itemForm.addEventListener('submit', submitItem);
generalForm.addEventListener('submit', submitGeneral);
achievementForm.addEventListener('submit', submitAchievement);

cancelEditButton.addEventListener('click', resetForm);
generalCancelEdit.addEventListener('click', resetGeneralForm);
itemCancelEdit.addEventListener('click', () => {
  resetItemForm();
  closeItemFormModal();
});
achievementCancelEdit.addEventListener('click', resetAchievementForm);

if (openItemCreateButton) {
  openItemCreateButton.addEventListener('click', () => {
    resetItemForm();
    openItemFormModal();
  });
}

if (closeItemModalButton) {
  closeItemModalButton.addEventListener('click', () => {
    closeItemFormModal();
  });
}

if (itemFormBackdrop) {
  itemFormBackdrop.addEventListener('click', () => {
    closeItemFormModal();
  });
}

generalCodeInput.addEventListener('input', syncGeneralCate);

itemTypeInput.addEventListener('change', updateCuliSliderVisibility);
itemLocationInput.addEventListener('change', updateMaxPurchasePerDayVisibility);
CULI_STAT_SLIDERS.forEach((config) => {
  config.slider.addEventListener('input', () => {
    if (!isCuliType()) return;
    syncStatInputFromSlider(config);
  });
  config.input.addEventListener('input', () => {
    if (!isCuliType()) return;
    syncStatSliderFromInput(config);
  });
});

logoutButton.addEventListener('click', () => {
  clearToken();
  window.location.href = './index.html';
});

backDashboardButton.addEventListener('click', () => {
  window.location.href = './index.html';
});

searchInput.addEventListener('input', (event) => {
  searchTerm = event.target.value;
  currentPage = 1;
  renderLanguages();
});

generalSearchInput.addEventListener('input', (event) => {
  generalSearchTerm = event.target.value;
  generalPage = 1;
  renderGenerals();
});

itemSearchInput.addEventListener('input', (event) => {
  itemSearchTerm = event.target.value;
  itemPage = 1;
  renderItems();
});

itemTypeFilterSelect.addEventListener('change', (event) => {
  itemTypeFilter = event.target.value;
  itemPage = 1;
  renderItems();
});

itemLocationFilterSelect.addEventListener('change', (event) => {
  itemLocationFilter = event.target.value;
  itemPage = 1;
  renderItems();
});

achievementSearchInput.addEventListener('input', (event) => {
  achievementSearchTerm = event.target.value;
  achievementPage = 1;
  renderAchievements();
});

prevPageButton.addEventListener('click', () => {
  if (currentPage === 1) return;
  currentPage -= 1;
  renderLanguages();
});

nextPageButton.addEventListener('click', () => {
  const filtered = getFilteredLanguages();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  if (currentPage >= totalPages) return;
  currentPage += 1;
  renderLanguages();
});

generalPrevButton.addEventListener('click', () => {
  if (generalPage === 1) return;
  generalPage -= 1;
  renderGenerals();
});

generalNextButton.addEventListener('click', () => {
  const filtered = getFilteredGenerals();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  if (generalPage >= totalPages) return;
  generalPage += 1;
  renderGenerals();
});

itemPrevButton.addEventListener('click', () => {
  if (itemPage === 1) return;
  itemPage -= 1;
  renderItems();
});

itemNextButton.addEventListener('click', () => {
  const filtered = getFilteredItems();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  if (itemPage >= totalPages) return;
  itemPage += 1;
  renderItems();
});

achievementPrevButton.addEventListener('click', () => {
  if (achievementPage === 1) return;
  achievementPage -= 1;
  renderAchievements();
});

achievementNextButton.addEventListener('click', () => {
  const filtered = getFilteredAchievements();
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  if (achievementPage >= totalPages) return;
  achievementPage += 1;
  renderAchievements();
});

Array.from(tabButtons).forEach((button) => {
  button.addEventListener('click', () => {
    const targetTab = button.dataset.tab;
    activateTab(targetTab);
  });
});

updateCuliSliderVisibility();
updateMaxPurchasePerDayVisibility();
setLanguageFormat('plain');

ensureSession()
  .then(async () => {
    await fetchLanguages();
    await fetchGenerals();
    await fetchItemRarityOptions();
    await fetchItems();
    await fetchRewardTypeOptions();
    await fetchItemOptions();
    await fetchAchievements();
  })
  .catch(() => {});

