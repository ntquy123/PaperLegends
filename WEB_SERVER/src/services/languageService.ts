import prisma from '../models/prismaClient';

export const getAllLanguages = async () => {
  return prisma.sysMasLanguage.findMany();
};

export const createLanguage = async (language: { code: string; vietnamText: string; englishText: string }) => {
  return prisma.sysMasLanguage.create({
    data: language,
  });
};

export const updateLanguage = async (
  code: string,
  language: { code?: string; vietnamText?: string; englishText?: string }
) => {
  return prisma.sysMasLanguage.update({
    where: { code },
    data: language,
  });
};

export const deleteLanguage = async (code: string) => {
  return prisma.sysMasLanguage.delete({
    where: { code },
  });
};
