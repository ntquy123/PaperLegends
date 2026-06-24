import prisma from '../models/prismaClient';

 

export interface BallPhysics {
  itemId: number
  name: string
  seqItem: number
  SkillGid: number | null;
  activeSkill: {
    GenCode: number;
    GenName: string;
    description: string | null;
  } | null;
  Mass: number | null;
  GravityScale: number | null;
  Drag: number | null;
  Bounciness: number | null;
  Elasticity: number | null;
  ImpactResistance: number | null;
  level: number;
  damage: number;
  isCateye: boolean;
}

 export const getBallPhysicsByPlayer = async (
  playerId: number
): Promise<BallPhysics[]> => {
  // Lấy danh sách các quả bóng từ EquipPlayer với điều kiện playerId và locationId trong [1, 2, 3]
  const equips = await prisma.equipPlayer.findMany({
    where: { playerId, locationId: { in: [1, 2, 3] } },
    select: { itemId: true, seqItem: true },
    orderBy: { locationId: 'asc' },
  });

  if (!equips || equips.length === 0) {
    return [];
  }

  // Lấy thông tin vật lý cho từng quả bóng
  const results = await Promise.all(
    equips.map(async (equip) => {
      const playerItem = await prisma.playerItem.findUnique({
        where: {
          playerId_itemId_seq: {
            playerId,
            itemId: equip.itemId,
            seq: equip.seqItem,
          },
        },
        select: {
          level: true,
          damage: true,
          SkillGid: true,
          activeSkill: {
            select: {
              GenCode: true,
              GenName: true,
              description: true,
            },
          },
        },
      });

      if (!playerItem) {
        return null;
      }

      const item = await prisma.item.findUnique({
        where: { id: equip.itemId },
        select: {
          name: true,
          Mass: true,
          GravityScale: true,
          Drag: true,
          Bounciness: true,
          Elasticity: true,
          ImpactResistance: true,
          isCateye: true,
        },
      });

      if (!item) {
        return null;
      }

      const level = playerItem.level;
      const factor = 1 + 0.05 * (level - 1); // Tăng 5% mỗi cấp

      return {
        itemId: equip.itemId,
        seqItem: equip.seqItem,
        name: item.name,
        SkillGid: playerItem.SkillGid ?? null,
        activeSkill: playerItem.activeSkill ?? null,
        Mass: item.Mass !== null ? item.Mass * factor : null,
        GravityScale: item.GravityScale !== null ? item.GravityScale : null,
        Drag: item.Drag !== null ? item.Drag / factor : null,
        Bounciness: item.Bounciness !== null ? item.Bounciness * factor : null,
        Elasticity: item.Elasticity !== null ? item.Elasticity : null,
        ImpactResistance: item.ImpactResistance !== null ? item.ImpactResistance * factor : null,
        level,
        damage: Number(playerItem.damage ?? 0),
        isCateye: item.isCateye
      };
    })
  );

  // Loại bỏ các giá trị null (nếu có)
  return results.filter((result) => result !== null) as BallPhysics[];
};

 export const getBallPhysicsByPlayers = async (
  playerIds: number[]
): Promise<{ playerId: number; physics: BallPhysics[] }[]> => {
  const results = await Promise.all(
    playerIds.map(async (id) => ({
      playerId: id,
      physics: await getBallPhysicsByPlayer(id), // Trả về danh sách các quả bóng
    }))
  );
  return results;
};
