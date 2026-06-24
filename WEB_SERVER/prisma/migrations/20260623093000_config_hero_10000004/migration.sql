-- Configure hero 10000004 with a placeholder four-skill kit.

UPDATE "Hero"
SET
  "code" = 'SON_TINH_10000004',
  "name" = 'Son Tinh',
  "role" = 'MAGE',
  "description" = 'Placeholder Son Tinh hero profile. Gameplay skills will be implemented later.',
  "hp" = 100,
  "attack" = 14,
  "defense" = 6,
  "speed" = 1.00,
  "weight" = 1.00,
  "bounce" = 0.42,
  "friction" = 0.46,
  "flickForce" = 1.05,
  "prefabAddress" = 'Assets/AddressableAsset/PaperLegends/HeroPrefabs/10000004.prefab',
  "iconUrl" = 'Assets/AddressableAsset/PaperLegends/HeroIcons/10000004.png',
  "portraitUrl" = 'Assets/AddressableAsset/PaperLegends/HeroPortraits/10000004.png',
  "selectionImageUrl" = 'Assets/AddressableAsset/PaperLegends/HeroPortraits/10000004.png',
  "sortOrder" = 40,
  "isActive" = true,
  "updatedAt" = CURRENT_TIMESTAMP
WHERE "modelId" = '10000004';

INSERT INTO "Hero" (
  "id", "code", "name", "role", "description",
  "hp", "attack", "defense", "speed",
  "weight", "bounce", "friction", "flickForce",
  "modelId", "prefabAddress", "iconUrl", "portraitUrl", "selectionImageUrl",
  "sortOrder", "isActive", "createdAt", "updatedAt"
)
SELECT
  'hero_paper_10000004', 'SON_TINH_10000004', 'Son Tinh', 'MAGE',
  'Placeholder Son Tinh hero profile. Gameplay skills will be implemented later.',
  100, 14, 6, 1.00,
  1.00, 0.42, 0.46, 1.05,
  '10000004',
  'Assets/AddressableAsset/PaperLegends/HeroPrefabs/10000004.prefab',
  'Assets/AddressableAsset/PaperLegends/HeroIcons/10000004.png',
  'Assets/AddressableAsset/PaperLegends/HeroPortraits/10000004.png',
  'Assets/AddressableAsset/PaperLegends/HeroPortraits/10000004.png',
  40, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
WHERE NOT EXISTS (
  SELECT 1 FROM "Hero" WHERE "modelId" = '10000004'
);

DELETE FROM "HeroSkill"
WHERE "heroId" IN (
  SELECT "id" FROM "Hero" WHERE "modelId" = '10000004'
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
      'skill_10000004_01',
      1,
      '11400041',
      'Reserved Skill 1',
      'Reserved gameplay slot for hero 10000004.',
      0.0,
      0,
      0.0,
      0.0,
      0.0,
      '{}'::jsonb,
      'Assets/AddressableAsset/PaperLegends/SkillIcons/11400041.png',
      'hero_10000004_skill_1',
      'hero_10000004_skill_1',
      false,
      true
    ),
    (
      'skill_10000004_02',
      2,
      '11400042',
      'Reserved Skill 2',
      'Reserved gameplay slot for hero 10000004.',
      0.0,
      0,
      0.0,
      0.0,
      0.0,
      '{}'::jsonb,
      'Assets/AddressableAsset/PaperLegends/SkillIcons/11400042.png',
      'hero_10000004_skill_2',
      'hero_10000004_skill_2',
      false,
      true
    ),
    (
      'skill_10000004_03',
      3,
      '11400043',
      'Reserved Skill 3',
      'Reserved gameplay slot for hero 10000004.',
      0.0,
      0,
      0.0,
      0.0,
      0.0,
      '{}'::jsonb,
      'Assets/AddressableAsset/PaperLegends/SkillIcons/11400043.png',
      'hero_10000004_skill_3',
      'hero_10000004_skill_3',
      false,
      true
    ),
    (
      'skill_10000004_04',
      4,
      '11400044',
      'Reserved Skill 4',
      'Reserved gameplay slot for hero 10000004.',
      0.0,
      0,
      0.0,
      0.0,
      0.0,
      '{}'::jsonb,
      'Assets/AddressableAsset/PaperLegends/SkillIcons/11400044.png',
      'hero_10000004_skill_4',
      'hero_10000004_skill_4',
      false,
      true
    )
) AS skill(
  "id", "slot", "code", "name", "description",
  "cooldown", "manaCost", "damage", "range", "duration",
  "config", "iconUrl", "vfxCode", "sfxCode", "isPassive", "isActive"
)
JOIN "Hero" h ON h."modelId" = '10000004'
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
