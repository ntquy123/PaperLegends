import { Router } from 'express';
import * as ItemController from '../controllers/itemController';

const itemRoutes = Router();

itemRoutes.get('/items', ItemController.getItems);

export default itemRoutes;
