const createElement = (tag, className) => {
  const element = document.createElement(tag);
  if (className) {
    element.className = className;
  }
  return element;
};

export const createPopup = ({ title, subtitle, content, actions = [], tone = 'default' }) => {
  const backdrop = createElement('div', 'popup-backdrop');
  const card = createElement('div', `popup-card popup-${tone}`);
  const header = createElement('div', 'popup-header');
  const headerText = createElement('div', 'popup-header-text');
  const titleEl = createElement('h3', 'popup-title');
  const subtitleEl = createElement('p', 'popup-subtitle');
  const closeButton = createElement('button', 'chip-action chip-button popup-close');

  titleEl.textContent = title;
  subtitleEl.textContent = subtitle || '';
  closeButton.type = 'button';
  closeButton.textContent = '✕';

  headerText.append(titleEl);
  if (subtitle) {
    headerText.append(subtitleEl);
  }

  header.append(headerText, closeButton);

  const body = createElement('div', 'popup-body');
  if (typeof content === 'string') {
    body.innerHTML = content;
  } else if (content instanceof HTMLElement) {
    body.append(content);
  }

  const footer = createElement('div', 'popup-actions');
  actions.forEach((action) => {
    const button = createElement('button', action.className || 'cta');
    button.type = 'button';
    button.textContent = action.label;
    button.addEventListener('click', () => {
      action.onClick?.();
    });
    footer.append(button);
  });

  card.append(header, body);
  if (actions.length) {
    card.append(footer);
  }
  backdrop.append(card);

  const close = () => {
    backdrop.classList.add('closing');
    setTimeout(() => {
      backdrop.remove();
    }, 160);
  };

  closeButton.addEventListener('click', close);
  backdrop.addEventListener('click', (event) => {
    if (event.target === backdrop) {
      close();
    }
  });

  document.body.append(backdrop);
  return { close, backdrop, body };
};

export const confirmPopup = ({
  title,
  message,
  confirmText = 'Xác nhận',
  cancelText = 'Hủy',
  tone = 'warning',
}) =>
  new Promise((resolve) => {
    const content = createElement('div', 'popup-confirm');
    const messageEl = createElement('p', 'popup-message');
    messageEl.textContent = message;
    content.append(messageEl);

    const popup = createPopup({
      title,
      subtitle: 'Vui lòng xác nhận thao tác trước khi tiếp tục.',
      content,
      tone,
      actions: [
        {
          label: cancelText,
          className: 'chip-action chip-button muted',
          onClick: () => {
            popup.close();
            resolve(false);
          },
        },
        {
          label: confirmText,
          className: 'cta danger',
          onClick: () => {
            popup.close();
            resolve(true);
          },
        },
      ],
    });
  });
