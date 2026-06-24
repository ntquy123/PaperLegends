import { Router } from 'express';
import {
  checkAccountController,
  socialLoginController,
  confirmSocialLoginNameController,
  loginController,
  refreshTokenController,
  logoutController,
  logoutAllController,
} from '../controllers/accountController';

const router = Router();

router.post('/check-account', checkAccountController);
router.post('/social-login', socialLoginController);
router.post('/social-login/confirm-name', confirmSocialLoginNameController);
router.post('/auth/login', loginController);
router.post('/auth/refresh', refreshTokenController);
router.post('/auth/logout', logoutController);
router.post('/auth/logout-all', logoutAllController);

export default router;
