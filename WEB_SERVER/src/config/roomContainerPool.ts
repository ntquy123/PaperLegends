export function parseBooleanEnv(value: string | undefined, defaultValue: boolean) {
  if (value == null) return defaultValue;

  const normalized = value.trim().toLowerCase();
  if (['1', 'true', 'yes', 'on'].includes(normalized)) return true;
  if (['0', 'false', 'no', 'off'].includes(normalized)) return false;

  return defaultValue;
}

export function isRoomContainerPoolEnabled() {
  return parseBooleanEnv(process.env.ROOM_CONTAINER_POOL_ENABLED, true);
}

