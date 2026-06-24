import { Request, Response } from 'express';
import {
  createLanguage as createLanguageService,
  deleteLanguage as deleteLanguageService,
  getAllLanguages,
  updateLanguage as updateLanguageService,
} from '../services/languageService';

export const getLanguages = async (_req: Request, res: Response): Promise<void> => {
  try {
    const languages = await getAllLanguages();
    res.json(languages);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

const validatePayload = (req: Request) => {
  const code = (req.body?.code ?? '').toString().trim();
  const vietnamText = (req.body?.vietnamText ?? '').toString().trim();
  const englishText = (req.body?.englishText ?? '').toString().trim();

  if (!code || !vietnamText || !englishText) {
    return { error: 'Vui lòng nhập đủ Code, Tiếng Việt và Tiếng Anh.' };
  }

  return { code, vietnamText, englishText };
};

export const createLanguage = async (req: Request, res: Response): Promise<void> => {
  const payload = validatePayload(req);
  if ('error' in payload) {
    res.status(400).json({ message: payload.error });
    return;
  }

  try {
    const language = await createLanguageService(payload);
    res.status(201).json({ message: 'Thêm mới cấu hình ngôn ngữ thành công.', language });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const updateLanguage = async (req: Request, res: Response): Promise<void> => {
  const targetCode = req.params.code;
  const payload = validatePayload(req);

  if (!targetCode) {
    res.status(400).json({ message: 'Thiếu mã cấu hình cần cập nhật.' });
    return;
  }

  if ('error' in payload) {
    res.status(400).json({ message: payload.error });
    return;
  }

  try {
    const language = await updateLanguageService(targetCode, payload);
    res.json({ message: 'Cập nhật cấu hình ngôn ngữ thành công.', language });
  } catch (error: any) {
    if (error?.code === 'P2025') {
      res.status(404).json({ message: 'Không tìm thấy cấu hình ngôn ngữ cần cập nhật.' });
      return;
    }
    res.status(500).json({ message: error.message });
  }
};

export const deleteLanguage = async (req: Request, res: Response): Promise<void> => {
  const code = req.params.code;

  if (!code) {
    res.status(400).json({ message: 'Thiếu mã cấu hình cần xóa.' });
    return;
  }

  try {
    await deleteLanguageService(code);
    res.json({ message: 'Xóa cấu hình ngôn ngữ thành công.' });
  } catch (error: any) {
    if (error?.code === 'P2025') {
      res.status(404).json({ message: 'Không tìm thấy cấu hình ngôn ngữ cần xóa.' });
      return;
    }
    res.status(500).json({ message: error.message });
  }
};
