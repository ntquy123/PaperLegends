import { Router } from 'express';
import { ugsToFirebase } from '../controllers/authController';

const router = Router();

router.post('/auth/ugs-to-firebase', ugsToFirebase);
export default router;
