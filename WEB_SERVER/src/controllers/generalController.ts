import { Request, Response } from 'express';
import {
  createGeneral as createGeneralService,
  deleteGeneral as deleteGeneralService,
  getAllGenerals,
  getGeneralSelectOptions,
  updateGeneral as updateGeneralService,
} from '../services/generalService';
import { SysMasGeneralCate } from '../config/generalCategory';

const deriveGenCate = (GenCode: number) => {
  const normalized = Math.abs(GenCode).toString();
  return Number.parseInt(normalized.slice(0, 3), 10);
};

const parseGeneralPayload = (req: Request) => {
  const GenCode = Number(req.body?.GenCode);
  const GenName = (req.body?.GenName ?? '').toString().trim();
  const ParentCodeRaw = req.body?.ParentCode;
  const descriptionRaw = req.body?.description;

  if (!Number.isInteger(GenCode)) {
    return { error: 'GenCode phải là số nguyên.' } as const;
  }

  if (!GenName) {
    return { error: 'GenName không được để trống.' } as const;
  }

  const ParentCode =
    ParentCodeRaw === undefined || ParentCodeRaw === null || ParentCodeRaw === ''
      ? null
      : Number(ParentCodeRaw);

  if (ParentCode !== null && !Number.isInteger(ParentCode)) {
    return { error: 'ParentCode phải là số nguyên.' } as const;
  }

  const description = descriptionRaw === undefined ? null : String(descriptionRaw);
  const GenCate = deriveGenCate(GenCode);

  return { GenCode, GenCate, GenName, ParentCode, description } as const;
};

export const getGenerals = async (_req: Request, res: Response): Promise<void> => {
  try {
    const generals = await getAllGenerals();
    res.json(generals);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const createGeneral = async (req: Request, res: Response): Promise<void> => {
  const payload = parseGeneralPayload(req);

  if ('error' in payload) {
    res.status(400).json({ message: payload.error });
    return;
  }

  try {
    const general = await createGeneralService(payload);
    res.status(201).json({ message: 'Thêm cấu hình hệ thống thành công.', general });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const updateGeneral = async (req: Request, res: Response): Promise<void> => {
  const targetCode = Number(req.params.GenCode);
  const payload = parseGeneralPayload(req);

  if (!Number.isInteger(targetCode)) {
    res.status(400).json({ message: 'Thiếu GenCode cần cập nhật.' });
    return;
  }

  if ('error' in payload) {
    res.status(400).json({ message: payload.error });
    return;
  }

  try {
    const general = await updateGeneralService(targetCode, {
      GenCate: payload.GenCate,
      GenName: payload.GenName,
      ParentCode: payload.ParentCode,
      description: payload.description,
    });
    res.json({ message: 'Cập nhật cấu hình hệ thống thành công.', general });
  } catch (error: any) {
    if (error?.code === 'P2025') {
      res.status(404).json({ message: 'Không tìm thấy cấu hình hệ thống cần cập nhật.' });
      return;
    }
    res.status(500).json({ message: error.message });
  }
};

export const getRewardTypeOptions = async (_req: Request, res: Response): Promise<void> => {
  try {
    const options = await getGeneralSelectOptions(SysMasGeneralCate.PlayerAchievementRewardType);
    res.json({
      options: options.map((option) => ({
        value: option.GenCode,
        label: option.GenName,
      })),
    });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getItemRarityOptions = async (_req: Request, res: Response): Promise<void> => {
  try {
    const options = await getGeneralSelectOptions(SysMasGeneralCate.ItemRarity);
    res.json({
      options: options.map((option) => ({
        value: option.GenCode,
        label: option.GenName,
      })),
    });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const getMatchTypeOptions = async (_req: Request, res: Response): Promise<void> => {
  try {
    const options = await getGeneralSelectOptions(SysMasGeneralCate.MatchType);
    res.json({
      options: options.map((option) => ({
        value: option.GenCode,
        label: option.GenName,
      })),
    });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const deleteGeneral = async (req: Request, res: Response): Promise<void> => {
  const targetCode = Number(req.params.GenCode);

  if (!Number.isInteger(targetCode)) {
    res.status(400).json({ message: 'Thiếu GenCode cần xóa.' });
    return;
  }

  try {
    await deleteGeneralService(targetCode);
    res.json({ message: 'Xóa cấu hình hệ thống thành công.' });
  } catch (error: any) {
    if (error?.code === 'P2025') {
      res.status(404).json({ message: 'Không tìm thấy cấu hình hệ thống cần xóa.' });
      return;
    }
    res.status(500).json({ message: error.message });
  }
};
