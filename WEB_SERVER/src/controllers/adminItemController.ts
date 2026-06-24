import { Request, Response } from 'express';
import {
  createItem as createItemService,
  deleteItem as deleteItemService,
  getAllItems,
  getItemSelectOptions,
  ItemPayload,
  saveItemImagePng,
  updateItem as updateItemService,
} from '../services/adminItemService';

function parseNumber(value: any, field: string, allowNull: true): number | null | { readonly error: string };
function parseNumber(value: any, field: string, allowNull?: false): number | { readonly error: string };
function parseNumber(value: any, field: string, allowNull?: boolean): number | null | { readonly error: string } {
  if (value === undefined || value === null || value === '') {
    return allowNull ? null : ({ error: `${field} khong duoc de trong.` } as const);
  }

  const parsed = Number(value);
  if (Number.isNaN(parsed)) {
    return { error: `${field} phai la so.` } as const;
  }

  return parsed;
}

const parseBoolean = (value: any, field: string) => {
  if (value === undefined || value === null || value === '') {
    return { error: `${field} khong duoc de trong.` } as const;
  }

  if (value === true || value === false) return value;
  if (value === 'true' || value === '1' || value === 1) return true;
  if (value === 'false' || value === '0' || value === 0) return false;

  return { error: `${field} phai la true/false.` } as const;
};

const isParseError = (v: unknown): v is { error: string } =>
  v !== null && typeof v === 'object' && 'error' in (v as object);

type ParsedItemPayload = Omit<ItemPayload, 'id'> & { id: number | null };

const parseImageBase64 = (raw: unknown): Buffer | null | { error: string } => {
  if (raw === undefined || raw === null || raw === '') {
    return null;
  }

  const value = String(raw).trim();
  if (!value) return null;

  const matched = value.match(/^data:image\/png;base64,(.+)$/i);
  const base64 = matched ? matched[1] : value;
  const normalized = base64.replace(/\s+/g, '');
  if (!/^[A-Za-z0-9+/=]+$/.test(normalized)) {
    return { error: 'Anh item khong hop le (base64).' };
  }

  try {
    const buffer = Buffer.from(normalized, 'base64');
    if (!buffer.length) {
      return { error: 'Anh item rong.' };
    }
    return buffer;
  } catch (_error) {
    return { error: 'Khong the giai ma anh item.' };
  }
};

const parseItemPayload = (req: Request, options?: { requireId?: boolean }): { error: string } | ParsedItemPayload => {
  const requireId = options?.requireId ?? true;
  const rawId = req.body?.id;
  const id =
    rawId === undefined || rawId === null || rawId === ''
      ? (requireId ? ({ error: 'ID khong duoc de trong.' } as const) : null)
      : parseNumber(rawId, 'ID');
  if (isParseError(id)) return id;

  const name = (req.body?.name ?? '').toString().trim();
  if (!name) return { error: 'Ten item khong duoc de trong.' } as const;

  const description = (req.body?.description ?? '').toString().trim();
  if (!description) return { error: 'Mo ta khong duoc de trong.' } as const;

  const level = parseNumber(req.body?.level, 'Level');
  if (isParseError(level)) return level;

  const typeGid = parseNumber(req.body?.typeGid, 'TypeGid');
  if (isParseError(typeGid)) return typeGid;

  const skillGid = parseNumber(req.body?.SkillGid, 'SkillGid', true);
  if (isParseError(skillGid)) return skillGid;

  const rarityGidRaw = req.body?.rarityGid;
  const rarityGidParsed = parseNumber(rarityGidRaw, 'RarityGid', true);
  if (isParseError(rarityGidParsed)) return rarityGidParsed;
  const rarityGid = rarityGidParsed ?? 11300001;

  const price = parseNumber(req.body?.price, 'Gia');
  if (isParseError(price)) return price;

  const locationGid = parseNumber(req.body?.locationGid, 'LocationGid');
  if (isParseError(locationGid)) return locationGid;

  const parsedMaxPurchasePerDay = parseNumber(req.body?.maxPurchasePerDay, 'maxPurchasePerDay', true);
  if (isParseError(parsedMaxPurchasePerDay)) return parsedMaxPurchasePerDay;
  const maxPurchasePerDay: number | null =
    typeof parsedMaxPurchasePerDay === 'number' ? parsedMaxPurchasePerDay : null;

  const normalizedMaxPurchasePerDay =
    locationGid === 2 && maxPurchasePerDay !== null
      ? Math.max(0, Math.floor(maxPurchasePerDay))
      : null;

  const isLevelUp = parseBoolean(req.body?.isLevelUp, 'isLevelUp');
  if (isParseError(isLevelUp)) return isLevelUp;

  const isOpen = parseBoolean(req.body?.isOpen, 'isOpen');
  if (isParseError(isOpen)) return isOpen;

  let isCateye = true;
  if (req.body?.isCateye !== undefined && req.body?.isCateye !== null && req.body?.isCateye !== '') {
    const parsedIsCateye = parseBoolean(req.body?.isCateye, 'isCateye');
    if (isParseError(parsedIsCateye)) return parsedIsCateye;
    isCateye = parsedIsCateye;
  }

  const ElementType = parseNumber(req.body?.ElementType, 'ElementType', true);
  if (isParseError(ElementType)) return ElementType;

  const priceByBall = parseNumber(req.body?.priceByBall, 'priceByBall', true);
  if (isParseError(priceByBall)) return priceByBall;

  const Levelrequired = parseNumber(req.body?.Levelrequired, 'Levelrequired', true);
  if (isParseError(Levelrequired)) return Levelrequired;

  const Mass = parseNumber(req.body?.Mass, 'Mass', true);
  if (isParseError(Mass)) return Mass;

  const GravityScale = parseNumber(req.body?.GravityScale, 'GravityScale', true);
  if (isParseError(GravityScale)) return GravityScale;

  const Drag = parseNumber(req.body?.Drag, 'Drag', true);
  if (isParseError(Drag)) return Drag;

  const Bounciness = parseNumber(req.body?.Bounciness, 'Bounciness', true);
  if (isParseError(Bounciness)) return Bounciness;

  const Elasticity = parseNumber(req.body?.Elasticity, 'Elasticity', true);
  if (isParseError(Elasticity)) return Elasticity;

  const ImpactResistance = parseNumber(req.body?.ImpactResistance, 'ImpactResistance', true);
  if (isParseError(ImpactResistance)) return ImpactResistance;

  return {
    id,
    name,
    SkillGid: skillGid,
    ElementType,
    description,
    level,
    typeGid,
    rarityGid,
    price,
    priceByBall,
    maxPurchasePerDay: normalizedMaxPurchasePerDay,
    isLevelUp,
    isOpen,
    isCateye,
    locationGid,
    Levelrequired,
    Mass,
    GravityScale,
    Drag,
    Bounciness,
    Elasticity,
    ImpactResistance,
  } as unknown as ParsedItemPayload;
};

export const getItems = async (_req: Request, res: Response): Promise<void> => {
  try {
    const items = await getAllItems();
    res.json(items);
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const createItem = async (req: Request, res: Response): Promise<void> => {
  const payload = parseItemPayload(req, { requireId: true });
  if ('error' in payload) {
    res.status(400).json({ message: payload.error });
    return;
  }

  if (payload.id === null) {
    res.status(400).json({ message: 'ID khong duoc de trong.' });
    return;
  }

  const imageBuffer = parseImageBase64(req.body?.imageBase64);
  if (isParseError(imageBuffer)) {
    res.status(400).json({ message: imageBuffer.error });
    return;
  }

  try {
    const item = await createItemService(payload as unknown as ItemPayload);
    if (imageBuffer) {
      await saveItemImagePng(payload.id, imageBuffer);
    }
    res.status(201).json({ message: 'Them item thanh cong.', item });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};

export const updateItem = async (req: Request, res: Response): Promise<void> => {
  const targetId = Number(req.params.id);
  if (!Number.isInteger(targetId)) {
    res.status(400).json({ message: 'Thieu ID item can cap nhat.' });
    return;
  }

  const payload = parseItemPayload(req, { requireId: false });
  if ('error' in payload) {
    res.status(400).json({ message: payload.error });
    return;
  }

  if (typeof payload.id === 'number' && payload.id !== targetId) {
    res.status(400).json({ message: 'ID trong body khong khop voi ID tren URL.' });
    return;
  }

  const imageBuffer = parseImageBase64(req.body?.imageBase64);
  if (isParseError(imageBuffer)) {
    res.status(400).json({ message: imageBuffer.error });
    return;
  }

  try {
    const item = await updateItemService(targetId, {
      name: payload.name,
      SkillGid: payload.SkillGid,
      ElementType: payload.ElementType,
      description: payload.description,
      level: payload.level,
      typeGid: payload.typeGid,
      rarityGid: payload.rarityGid,
      price: payload.price,
      priceByBall: payload.priceByBall,
      maxPurchasePerDay: payload.maxPurchasePerDay,
      isLevelUp: payload.isLevelUp,
      isOpen: payload.isOpen,
      isCateye: payload.isCateye,
      locationGid: payload.locationGid,
      Levelrequired: payload.Levelrequired,
      Mass: payload.Mass,
      GravityScale: payload.GravityScale,
      Drag: payload.Drag,
      Bounciness: payload.Bounciness,
      Elasticity: payload.Elasticity,
      ImpactResistance: payload.ImpactResistance,
    });
    if (imageBuffer) {
      await saveItemImagePng(targetId, imageBuffer);
    }
    res.json({ message: 'Cap nhat item thanh cong.', item });
  } catch (error: any) {
    if (error?.code === 'P2025' || /record to update not found|no .* record/i.test(error?.message ?? '')) {
      res.status(404).json({ message: 'Khong tim thay item can cap nhat.' });
      return;
    }
    res.status(500).json({ message: error.message });
  }
};

export const deleteItem = async (req: Request, res: Response): Promise<void> => {
  const targetId = Number(req.params.id);
  if (!Number.isInteger(targetId)) {
    res.status(400).json({ message: 'Thieu ID item can xoa.' });
    return;
  }

  try {
    await deleteItemService(targetId);
    res.json({ message: 'Xoa item thanh cong.' });
  } catch (error: any) {
    if (error?.code === 'P2025') {
      res.status(404).json({ message: 'Khong tim thay item can xoa.' });
      return;
    }
    res.status(500).json({ message: error.message });
  }
};

export const getItemOptions = async (_req: Request, res: Response): Promise<void> => {
  try {
    const options = await getItemSelectOptions();
    res.json({
      options: options.map((option) => ({
        value: option.id,
        label: option.name,
      })),
    });
  } catch (error: any) {
    res.status(500).json({ message: error.message });
  }
};
