import loadEnv from '../config/loadEnv';
import { Request, Response } from 'express';
import { createRemoteJWKSet, jwtVerify } from 'jose';
import admin from '../config/firebaseAdmin';

loadEnv();

/**
 * Unity Authentication (UGS) → Firebase custom token
 * - Verifies the UGS JWT with Unity's JWKS
 * - Checks the Project ID claim matches env UGS_PROJECT_ID
 * - Mints a Firebase Custom Token for the player
 */

const {
  UNITY_ISSUER = 'https://player-auth.services.api.unity.com',
  UNITY_JWKS_URL = 'https://player-auth.services.api.unity.com/.well-known/jwks.json',
  UGS_PROJECT_ID,
  UGS_ENVIRONMENT_NAME = 'production',
} = process.env;

// Fail fast if project id is missing
if (!UGS_PROJECT_ID) {
  // Throwing here makes the problem obvious at boot time
  // If you prefer runtime check, move this into the handler.
  // eslint-disable-next-line no-throw-literal
  throw new Error('UGS_PROJECT_ID is not configured (set it in environment variables).');
}

const jwks = createRemoteJWKSet(new URL(UNITY_JWKS_URL));

export const ugsToFirebase = async (req: Request, res: Response): Promise<void> => {
  const { ugsToken } = req.body ?? {};

  if (typeof ugsToken !== 'string' || !ugsToken.trim()) {
    res.status(400).json({ message: 'ugsToken is required.' });
    return;
  }

  try {
    // 1) Verify signature & issuer using Unity JWKS
    const verification = await jwtVerify(ugsToken, jwks, {
      issuer: UNITY_ISSUER,
      // audience: you may add if you enforce an audience
    });
    const payload: any = verification.payload;

    // 2) Extract project id from token claims (pid / projectId / project_id)
    const tokenProjectId: string | undefined =
      payload?.pid ?? payload?.projectId ?? payload?.project_id;

    if (!tokenProjectId) {
      res.status(400).json({ message: 'UGS token missing project claim.' });
      return;
    }
    if (tokenProjectId !== UGS_PROJECT_ID) {
      res.status(401).json({ message: 'UGS project mismatch.' });
      return;
    }

    // 3) PlayerId is the subject (sub)
    const playerId: string | undefined = payload?.sub;
    if (!playerId) {
      res.status(400).json({ message: 'UGS token missing subject claim.' });
      return;
    }

    // 4) Mint Firebase custom token
    const uid = `ugs:${playerId}`;
    const developerClaims = {
      provider: 'ugs',
      ugsProjectId: tokenProjectId,
      ugsEnv: UGS_ENVIRONMENT_NAME,
    };

    const customToken = await admin.auth().createCustomToken(uid, developerClaims);
    res.json({ customToken });
  } catch (err) {
    // jose throws RequestFailed, JWSSignatureVerificationFailed, JWTInvalid, etc.
    console.error('UGS token verify/mint failed:', err);
    res.status(401).json({ message: 'Invalid UGS token.' });
  }
};

