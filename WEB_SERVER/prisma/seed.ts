import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

async function main() {
  // const items = [
  //   { name: 'Potion', description: 'Restore health', level: 1, typeGid: 1, price: 50, isLevelUp: false, isOpen: true, locationGid: 0 },
  //   { name: 'Elixir', description: 'Restore mana', level: 1, typeGid: 2, price: 75, isLevelUp: false, isOpen: true, locationGid: 0 }
  // ];

  // for (const data of items) {
  //   await prisma.item.upsert({
  //     where: { id: data.typeGid },
  //     update: {},
  //     create: data
  //   });
  // }

  const generals = [
    { genCode: 11000001, genName: 'power_skill', genParent: 0 },
    { genCode: 11000002, genName: 'cham_cat', genParent: 0 }
  ];

  for (const data of generals) {
    await (prisma as any).sysMasGeneral.upsert({
      where: { genCode: data.genCode },
      update: {},
      create: data
    });
  }

  await (prisma as any).sysMasLanguage.upsert({
    where: { code: 'power_skill' },
    update: {},
    create: { code: 'power_skill', vietnamText: 'Sức mạnh', englishText: 'Power' }
  });

  const player = await prisma.player.findFirst();
  if (player) {
    const firstItem = await prisma.item.findFirst();
    if (firstItem) {
      await prisma.playerItem.upsert({
        where: { playerId_itemId: { playerId: player.id, itemId: firstItem.id } },
        update: { quantity: 1 },
        create: { playerId: player.id, itemId: firstItem.id, quantity: 1 }
      });
    }

    await (prisma as any).effectPlayer.create({
      data: {
        playerId: player.id,
        effectId: 11000001,
        power: 10,
        spin: 5,
        level: 1,
        isPassive: false,
        charges: 3,
        description: 'Sample effect',
        parentId: 0,
      }
    });
  }
}

main()
  .catch((e) => {
    console.error(e);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
