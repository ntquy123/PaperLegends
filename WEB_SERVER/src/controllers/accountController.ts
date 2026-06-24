import { Request, Response } from 'express';
import {
  getPlayerByAccountId,
  loginOrCreateSocialAccount,
  confirmPlayerName,
  updatePlayerLastLoginAt,
} from '../services/playerService';
import {
  issueTokensForDevice,
  refreshTokensForDevice,
  revokeAllRefreshTokens,
  revokeRefreshToken,
  revokeTokensForOtherDevices,
} from '../services/authTokenService';
import { isPlayerOnline } from '../websocket/registry';

const VALID_PROVIDER_TYPES = [
  'Anonymous',
  'EmailPassword',
  'Phone',
  'Google',
  'GooglePlayGames',
  'Facebook',
  'Twitter',
  'GitHub',
  'Microsoft',
  'Yahoo',
  'Apple',
  'GameCenter',
  'CustomToken',
] as const;

export const checkAccountController = async (req: Request, res: Response) => {
  try {
    const { idToken } = req.body;

    if (typeof idToken !== 'string') {
      res.status(400).json({ message: 'Invalid idToken' });
      return;
    }

    const player = await getPlayerByAccountId(idToken);

    if (player) {
      res.json(player);
    } else {
      res.json({ player: null, message: 'Chưa có tài khoản' });
    }
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

const isValidProviderType = (
  providerType: string
): providerType is (typeof VALID_PROVIDER_TYPES)[number] =>
  VALID_PROVIDER_TYPES.includes(providerType as (typeof VALID_PROVIDER_TYPES)[number]);

export const socialLoginController = async (req: Request, res: Response) => {
  try {
    const { firebaseUid, email, providerType, deviceId, avatarUrl } = req.body ?? {};

    if (typeof firebaseUid !== 'string' || !firebaseUid.trim()) {
      res.status(400).json({ message: 'Invalid firebaseUid' });
      return;
    }

    if (typeof email !== 'string') {
      res.status(400).json({ message: 'Invalid email' });
      return;
    }

    if (typeof providerType !== 'string' || !isValidProviderType(providerType.trim())) {
      res.status(400).json({ message: 'Invalid providerType' });
      return;
    }

    if (typeof deviceId !== 'string' || !deviceId.trim()) {
      res.status(400).json({ message: 'Invalid deviceId' });
      return;
    }

    if (avatarUrl !== undefined && avatarUrl !== null && typeof avatarUrl !== 'string') {
      res.status(400).json({ message: 'Invalid avatarUrl' });
      return;
    }

    const normalizedFirebaseUid = firebaseUid.trim();
    const normalizedEmail = email.trim();
    const normalizedProviderType = providerType.trim();
    const normalizedDeviceId = deviceId.trim();
    const normalizedAvatarUrl = typeof avatarUrl === 'string' && avatarUrl.trim()
      ? avatarUrl.trim()
      : undefined;

    const player = await loginOrCreateSocialAccount(
      normalizedFirebaseUid,
      normalizedEmail,
      normalizedProviderType,
      normalizedAvatarUrl
    );

    if (isPlayerOnline(player.id)) {
      res.status(409).json({ message: 'Player is already logged in' });
      return;
    }

    await revokeTokensForOtherDevices(player.id, normalizedDeviceId);
    const tokens = await issueTokensForDevice(player.id, normalizedDeviceId);
    const loggedInPlayer = await updatePlayerLastLoginAt(player.id);

    res.json({ player: loggedInPlayer, tokens });
  } catch (error: any) {
    const status = error.message === 'Player is already logged in' ? 409 : 500;
    res.status(status).json({ message: error.message });
  }
};

export const confirmSocialLoginNameController = async (
  req: Request,
  res: Response
) => {
  try {
    const {
      id,
      PlayerName,
      playerName,
      CompanionBallItemId,
      companionBallItemId,
    } = req.body ?? {};

    const parsedId =
      typeof id === 'string' ? Number.parseInt(id, 10) : Number(id);

    if (!Number.isInteger(parsedId) || parsedId <= 0) {
      res.status(400).json({ message: 'Invalid id' });
      return;
    }

    const nameCandidate =
      typeof PlayerName === 'string' && PlayerName.trim()
        ? PlayerName
        : typeof playerName === 'string'
        ? playerName
        : undefined;

    if (typeof nameCandidate !== 'string' || !nameCandidate.trim()) {
      res.status(400).json({ message: 'Invalid PlayerName' });
      return;
    }

    const parsedCompanionBallItemId = (() => {
      const rawValue =
        typeof CompanionBallItemId === 'string' && CompanionBallItemId.trim()
          ? CompanionBallItemId
          : typeof companionBallItemId === 'string' && companionBallItemId.trim()
          ? companionBallItemId
          : typeof CompanionBallItemId === 'number'
          ? CompanionBallItemId
          : typeof companionBallItemId === 'number'
          ? companionBallItemId
          : undefined;

      return typeof rawValue === 'string'
        ? Number.parseInt(rawValue, 10)
        : rawValue;
    })();

    if (
      !Number.isInteger(parsedCompanionBallItemId) ||
      parsedCompanionBallItemId <= 0
    ) {
      res.status(400).json({ message: 'Invalid CompanionBallItemId' });
      return;
    }

    const updatedPlayer = await confirmPlayerName(
      parsedId,
      nameCandidate.trim(),
      parsedCompanionBallItemId
    );

    res.json(updatedPlayer);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const loginController = async (req: Request, res: Response) => {
  try {
    const { firebaseUid, email, providerType, deviceId, avatarUrl } = req.body ?? {};

    if (typeof firebaseUid !== 'string' || !firebaseUid.trim()) {
      res.status(400).json({ message: 'Invalid firebaseUid' });
      return;
    }

    if (typeof email !== 'string') {
      res.status(400).json({ message: 'Invalid email' });
      return;
    }

    if (typeof providerType !== 'string' || !isValidProviderType(providerType.trim())) {
      res.status(400).json({ message: 'Invalid providerType' });
      return;
    }

    if (typeof deviceId !== 'string' || !deviceId.trim()) {
      res.status(400).json({ message: 'Invalid deviceId' });
      return;
    }

    if (avatarUrl !== undefined && avatarUrl !== null && typeof avatarUrl !== 'string') {
      res.status(400).json({ message: 'Invalid avatarUrl' });
      return;
    }

    const normalizedFirebaseUid = firebaseUid.trim();
    const normalizedEmail = email.trim();
    const normalizedProviderType = providerType.trim();
    const normalizedDeviceId = deviceId.trim();
    const normalizedAvatarUrl = typeof avatarUrl === 'string' && avatarUrl.trim()
      ? avatarUrl.trim()
      : undefined;

    const player = await loginOrCreateSocialAccount(
      normalizedFirebaseUid,
      normalizedEmail,
      normalizedProviderType,
      normalizedAvatarUrl
    );

    if (isPlayerOnline(player.id)) {
      res.status(409).json({ message: 'Player is already logged in' });
      return;
    }

    await revokeTokensForOtherDevices(player.id, normalizedDeviceId);
    const tokens = await issueTokensForDevice(player.id, normalizedDeviceId);
    const loggedInPlayer = await updatePlayerLastLoginAt(player.id);

    res.json({ player: loggedInPlayer, tokens });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const refreshTokenController = async (req: Request, res: Response) => {
  try {
    const { refreshToken, deviceId } = req.body ?? {};

    if (typeof refreshToken !== 'string' || !refreshToken.trim()) {
      res.status(400).json({ message: 'Invalid refreshToken' });
      return;
    }

    if (typeof deviceId !== 'string' || !deviceId.trim()) {
      res.status(400).json({ message: 'Invalid deviceId' });
      return;
    }

    const tokens = await refreshTokensForDevice(refreshToken.trim(), deviceId.trim());

    res.json(tokens);
  } catch (error: any) {
    const status = error.message.includes('device') ? 403 : 401;
    res.status(status).json({ message: error.message });
  }
};

export const logoutController = async (req: Request, res: Response) => {
  try {
    const { refreshToken } = req.body ?? {};

    if (typeof refreshToken !== 'string' || !refreshToken.trim()) {
      res.status(400).json({ message: 'Invalid refreshToken' });
      return;
    }

    await revokeRefreshToken(refreshToken.trim());

    res.json({ message: 'Logged out' });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const logoutAllController = async (req: Request, res: Response) => {
  try {
    const { refreshToken } = req.body ?? {};

    if (typeof refreshToken !== 'string' || !refreshToken.trim()) {
      res.status(400).json({ message: 'Invalid refreshToken' });
      return;
    }

    await revokeAllRefreshTokens(refreshToken.trim());

    res.json({ message: 'Logged out from all devices' });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};
