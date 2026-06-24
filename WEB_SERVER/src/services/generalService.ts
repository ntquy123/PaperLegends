import prisma from '../models/prismaClient';

export interface GeneralPayload {
  GenCode: number;
  GenCate: number;
  GenName: string;
  ParentCode?: number | null;
  description?: string | null;
}

export const getAllGenerals = async () => {
  return prisma.sysMasGeneral.findMany({
    orderBy: { GenCode: 'asc' },
  });
};

export const createGeneral = async (payload: GeneralPayload) => {
  return prisma.sysMasGeneral.create({ data: payload });
};

export const updateGeneral = async (GenCode: number, payload: Omit<GeneralPayload, 'GenCode'>) => {
  return prisma.sysMasGeneral.update({
    where: { GenCode },
    data: payload,
  });
};

export const deleteGeneral = async (GenCode: number) => {
  return prisma.sysMasGeneral.delete({
    where: { GenCode },
  });
};

export const getGeneralSelectOptions = async (GenCate: number) => {
  return prisma.sysMasGeneral.findMany({
    where: { GenCate },
    select: { GenCode: true, GenName: true },
    orderBy: { GenCode: 'asc' },
  });
};
