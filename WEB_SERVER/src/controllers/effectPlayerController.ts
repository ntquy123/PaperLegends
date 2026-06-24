import { Request, Response } from 'express';
import { equipEffectPlayer, getByPlayerId, levelUpEffectPlayer } from '../services/effectPlayerService';
import { levelUpPlayerItem } from '../services/playerItemService';
import { recordApiErrorLog } from '../services/apiErrorLogService';
import { getClientIp, getUserAgent } from '../middleware/requestClientInfo';

const logMissingEffectPlayerData = (req: Request, playerId: number, message: string) => {
  void recordApiErrorLog({
    method: req.method,
    path: req.originalUrl,
    statusCode: 200,
    requestParams: {
      routeParams: req.params,
      query: req.query,
      body: req.body ?? {},
    },
    errorMessage: message,
    ipAddress: getClientIp(req),
    userAgent: getUserAgent(req),
  }).catch((error) => {
    console.error(`Failed to save missing effect-player log for player ${playerId}:`, error);
  });
};

export const getEffectPlayers = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.params.playerId);
    if (isNaN(playerId)) {
      res.status(400).json({ message: 'Invalid playerId' });
      return;
    }
    const effects = await getByPlayerId(playerId);
    if (effects.length === 0) {
      logMissingEffectPlayerData(req, playerId, `EffectPlayer data is empty for playerId=${playerId}`);
    } else if (!effects.some((effect) => effect.IsEquiped)) {
      logMissingEffectPlayerData(req, playerId, `No equipped EffectPlayer data for playerId=${playerId}`);
    }
    res.json(effects);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const levelUpEffect = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);
    const effectId = Number(req.body.effectId);

    if (isNaN(playerId) || isNaN(effectId)) {
      res.status(400).json({ message: 'Invalid playerId or effectId' });
      return;
    }

    const updated = await levelUpEffectPlayer(playerId, effectId);
    res.json(updated);
  } catch (error: any) {
 
    if (error.message === 'Không còn điểm TalentPoint để tăng cấp') {
      res.status(400).json({ message: error.message });
    } else {
      res.status(500).json({ message: error.message });
    }
 
  }
};

export const levelUpItem = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);
    const itemId = Number(req.body.itemId);
    const seq = Number(req.body.seq);
    const rawMaterials = Array.isArray(req.body.materials) ? req.body.materials : [];
    const materials = rawMaterials
      .map((m: any) => ({ id: Number(m.id), seq: Number(m.seq) }))
      .filter((m) => !isNaN(m.id) && !isNaN(m.seq));

    if (isNaN(playerId) || isNaN(itemId) || isNaN(seq)) {
      res.status(400).json({ message: 'Invalid parameters' });
      return;
    }

    const updated = await levelUpPlayerItem(playerId, itemId, seq, materials);
    res.json(updated);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const equipEffect = async (req: Request, res: Response) => {
  try {
    const playerId = Number(req.body.playerId);
    const oldEffectId = Number(req.body.oldEffectId);
    const newEffectId = Number(req.body.newEffectId);

    if (isNaN(playerId) || isNaN(oldEffectId) || isNaN(newEffectId)) {
      res.status(400).json({ message: 'Invalid parameters' });
      return;
    }

    const updated = await equipEffectPlayer(playerId, oldEffectId, newEffectId);
    res.json(updated);
  } catch (error: any) {
    if (
      error.message === 'Player not found or inactive' ||
      error.message === 'Skill not found for player' ||
      error.message === 'Chỉ có thể trang bị kỹ năng đang hoạt động' ||
      error.message === 'Người chơi chỉ có thể trang bị tối đa 3 kỹ năng'
    ) {
      res.status(400).json({ message: error.message });
    } else {
      res.status(500).json({ message: error.message });
    }
  }
};
