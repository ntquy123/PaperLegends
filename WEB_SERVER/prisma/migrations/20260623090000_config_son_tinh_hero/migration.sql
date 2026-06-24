-- Configure hero 10000003 as Son Tinh and seed the first gameplay skill set.

UPDATE "Hero"
SET
  "isActive" = false,
  "sortOrder" = 999,
  "updatedAt" = CURRENT_TIMESTAMP
WHERE "code" = 'SON_TINH_SHAMAN'
  AND "modelId" = '10000004';

UPDATE "Hero"
SET
  "code" = 'SON_TINH',
  "name" = 'Son Tinh',
  "role" = 'MAGE',
  "description" = 'Control paper hero who uses waves to push enemies out of position.',
  "hp" = 106,
  "attack" = 15,
  "defense" = 6,
  "speed" = 1.02,
  "weight" = 0.96,
  "bounce" = 0.46,
  "friction" = 0.44,
  "flickForce" = 1.08,
  "prefabAddress" = 'Assets/AddressableAsset/PaperLegends/HeroPrefabs/10000003.prefab',
  "iconUrl" = 'Assets/AddressableAsset/PaperLegends/HeroIcons/10000003.png',
  "portraitUrl" = 'Assets/AddressableAsset/PaperLegends/HeroPortraits/10000003.png',
  "selectionImageUrl" = 'Assets/AddressableAsset/PaperLegends/HeroPortraits/10000003.png',
  "sortOrder" = 30,
  "isActive" = true,
  "updatedAt" = CURRENT_TIMESTAMP
WHERE "id" = 'hero_paper_10000003'
   OR "modelId" = '10000003';

INSERT INTO "Hero" (
  "id", "code", "name", "role", "description",
  "hp", "attack", "defense", "speed",
  "weight", "bounce", "friction", "flickForce",
  "modelId", "prefabAddress", "iconUrl", "portraitUrl", "selectionImageUrl",
  "sortOrder", "isActive", "createdAt", "updatedAt"
)
SELECT
  'hero_paper_10000003', 'SON_TINH', 'Son Tinh', 'MAGE',
  'Control paper hero who uses waves to push enemies out of position.',
  106, 15, 6, 1.02,
  0.96, 0.46, 0.44, 1.08,
  '10000003',
  'Assets/AddressableAsset/PaperLegends/HeroPrefabs/10000003.prefab',
  'Assets/AddressableAsset/PaperLegends/HeroIcons/10000003.png',
  'Assets/AddressableAsset/PaperLegends/HeroPortraits/10000003.png',
  'Assets/AddressableAsset/PaperLegends/HeroPortraits/10000003.png',
  30, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
WHERE NOT EXISTS (
  SELECT 1 FROM "Hero" WHERE "modelId" = '10000003'
);

DELETE FROM "HeroSkill"
WHERE "heroId" IN (
  SELECT "id" FROM "Hero" WHERE "modelId" = '10000003'
);

INSERT INTO "HeroSkill" (
  "id", "heroId", "slot", "code", "name", "description",
  "cooldown", "manaCost", "damage", "range", "duration",
  "config", "iconUrl", "vfxCode", "sfxCode", "isPassive", "isActive"
)
SELECT
  skill."id",
  h."id" AS "heroId",
  skill."slot",
  skill."code",
  skill."name",
  skill."description",
  skill."cooldown",
  skill."manaCost",
  skill."damage",
  skill."range",
  skill."duration",
  skill."config",
  skill."iconUrl",
  skill."vfxCode",
  skill."sfxCode",
  skill."isPassive",
  skill."isActive"
FROM (
  VALUES
    (
      'skill_son_tinh_01',
      1,
      '11400031',
      'Song Day',
      'After casting, the next flick direction releases a wave that pushes paper heroes hit by the wave.',
      8.0,
      0,
      0.0,
      5.5,
      0.9,
      '{"waveLength":5.5,"waveHalfWidth":1.15,"baseHorizontalImpulse":4.25,"horizontalImpulsePerLevel":0.7,"baseUpwardImpulse":0.55}'::jsonb,
      'Assets/AddressableAsset/PaperLegends/SkillIcons/11400031.png',
      'son_tinh_wave_push',
      'son_tinh_wave_push',
      false,
      true
    ),
    (
      'skill_son_tinh_02',
      2,
      '11400032',
      'Reserved Skill 2',
      'Reserved gameplay slot for Son Tinh.',
      0.0,
      0,
      0.0,
      0.0,
      0.0,
      '{}'::jsonb,
      'Assets/AddressableAsset/PaperLegends/SkillIcons/11400032.png',
      'son_tinh_skill_2',
      'son_tinh_skill_2',
      false,
      true
    ),
    (
      'skill_son_tinh_03',
      3,
      '11400033',
      'Reserved Skill 3',
      'Reserved gameplay slot for Son Tinh.',
      0.0,
      0,
      0.0,
      0.0,
      0.0,
      '{}'::jsonb,
      'Assets/AddressableAsset/PaperLegends/SkillIcons/11400033.png',
      'son_tinh_skill_3',
      'son_tinh_skill_3',
      false,
      true
    ),
    (
      'skill_son_tinh_04',
      4,
      '11400034',
      'Reserved Skill 4',
      'Reserved gameplay slot for Son Tinh.',
      0.0,
      0,
      0.0,
      0.0,
      0.0,
      '{}'::jsonb,
      'Assets/AddressableAsset/PaperLegends/SkillIcons/11400034.png',
      'son_tinh_skill_4',
      'son_tinh_skill_4',
      false,
      true
    )
) AS skill(
  "id", "slot", "code", "name", "description",
  "cooldown", "manaCost", "damage", "range", "duration",
  "config", "iconUrl", "vfxCode", "sfxCode", "isPassive", "isActive"
)
JOIN "Hero" h ON h."modelId" = '10000003'
ON CONFLICT ("heroId", "code") DO UPDATE SET
  "slot" = EXCLUDED."slot",
  "name" = EXCLUDED."name",
  "description" = EXCLUDED."description",
  "cooldown" = EXCLUDED."cooldown",
  "manaCost" = EXCLUDED."manaCost",
  "damage" = EXCLUDED."damage",
  "range" = EXCLUDED."range",
  "duration" = EXCLUDED."duration",
  "config" = EXCLUDED."config",
  "iconUrl" = EXCLUDED."iconUrl",
  "vfxCode" = EXCLUDED."vfxCode",
  "sfxCode" = EXCLUDED."sfxCode",
  "isPassive" = EXCLUDED."isPassive",
  "isActive" = EXCLUDED."isActive";
