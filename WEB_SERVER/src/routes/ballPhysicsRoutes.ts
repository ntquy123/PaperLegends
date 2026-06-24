import { Router } from 'express';
import { getBallPhysics } from '../controllers/ballPhysicsController';

const router = Router();

router.post('/players/ball-physics', getBallPhysics);

export default router;
