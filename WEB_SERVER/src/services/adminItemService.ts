import prisma from '../models/prismaClient';
import { mkdir, writeFile } from 'fs/promises';

const DEFAULT_ITEM_IMAGE_DIR = '/var/www/BanCuLi_Backend/public/images/items';
const ITEM_IMAGE_DIR = process.env.ITEM_IMAGE_DIR || DEFAULT_ITEM_IMAGE_DIR;

export interface ItemPayload {
  id: number;
  name: string;
  SkillGid?: number | null;
  ElementType?: number | null;
  description: string;
  level: number;
  typeGid: number;
  rarityGid: number;
  price: number;
  priceByBall?: number | null;
  maxPurchasePerDay?: number | null;
  isLevelUp: boolean;
  isOpen: boolean;
  isCateye: boolean;
  locationGid: number;
  Levelrequired?: number | null;
  Mass?: number | null;
  GravityScale?: number | null;
  Drag?: number | null;
  Bounciness?: number | null;
  Elasticity?: number | null;
  ImpactResistance?: number | null;
}

export const getAllItems = async () => {
  return prisma.item.findMany({ orderBy: { id: 'asc' } });
};

export const createItem = async (payload: ItemPayload) => {
  return prisma.item.create({ data: payload });
};

export const updateItem = async (id: number, payload: Partial<ItemPayload>) => {
  const data = Object.fromEntries(
    Object.entries(payload).filter(([, value]) => value !== undefined && value !== null)
  ) as Partial<ItemPayload>;

  if (Object.keys(data).length === 0) {
    return prisma.item.findUniqueOrThrow({ where: { id } });
  }

  return prisma.item.update({
    where: { id },
    data,
  });
};

export const deleteItem = async (id: number) => {
  return prisma.item.delete({ where: { id } });
};

export const getItemSelectOptions = async () => {
  return prisma.item.findMany({
    select: { id: true, name: true },
    orderBy: { id: 'asc' },
  });
};

export const saveItemImagePng = async (id: number, imageBuffer: Buffer) => {
  await mkdir(ITEM_IMAGE_DIR, { recursive: true });
  await writeFile(`${ITEM_IMAGE_DIR}/${id}.png`, imageBuffer);
};
