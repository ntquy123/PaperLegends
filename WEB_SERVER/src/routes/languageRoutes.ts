import { Router } from 'express';
import * as LanguageController from '../controllers/languageController';

const router = Router();

router.get('/languages', LanguageController.getLanguages);

export default router;
