import { NextFunction, Request, Response } from 'express';
import { verifyAccessToken, VerifiedAccessToken } from '../services/authTokenService';

export interface AuthenticatedRequest extends Request {
  auth?: VerifiedAccessToken;
}

export const authMiddleware = async (
  req: AuthenticatedRequest,
  res: Response,
  next: NextFunction
) => {
  const authorization = req.headers.authorization;

  if (!authorization || !authorization.startsWith('Bearer ')) {
    res.status(401).json({ message: 'Missing Authorization header' });
    return;
  }

  const token = authorization.replace('Bearer ', '').trim();

  try {
    const payload = await verifyAccessToken(token);
    req.auth = payload;
    next();
  } catch (error: any) {
    res.status(401).json({ message: error.message || 'Invalid or expired access token' });
  }
};
