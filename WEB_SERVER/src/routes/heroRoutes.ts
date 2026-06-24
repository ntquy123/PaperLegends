import { Router } from 'express';
import { getHeroes, getSelectedHeroes } from '../controllers/heroController';

const router = Router();

router.get('/heroes', getHeroes);
router.post('/heroes/selected', getSelectedHeroes);

export default router;
