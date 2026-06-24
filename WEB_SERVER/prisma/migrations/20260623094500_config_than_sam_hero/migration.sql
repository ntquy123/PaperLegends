-- Configure hero 10000005 as Than Sam and seed the first targeted ultimate skill.

UPDATE "Hero"
SET
  "code" = 'THAN_SAM',
  "name" = 'Than Sam',
  "role" = 'MAGE',
  "description" = 'Thunder paper hero who controls an area with delayed lightning strikes.',
  "hp" = 98,
  "attack" = 18,
  "defense" = 4,
  "speed" = 1.04,
  "weight" = 0.9,
  "bounce" = 0.48,
  "friction" = 0.4,
  "flickForce" = 1.1,
  "prefabAddress" = 'Assets/AddressableAsset/PaperLegends/HeroPrefabs/10000005.prefab',
  "iconUrl" = 'Assets/AddressableAsset/PaperLegends/HeroIcons/10000005.png',
  "portraitUrl" = 'Assets/AddressableAsset/PaperLegends/HeroPortraits/10000005.png',
  "selectionImageUrl" = 'Assets/AddressableAsset/PaperLegends/HeroPortraits/10000005.png',
  "sortOrder" = 50,
  "isActive" = true,
  "updatedAt" = CURRENT_TIMESTAMP
WHERE "modelId" = '10000005';

INSERT INTO "Hero" (
  "id", "code", "name", "role", "description",
  "hp", "attack", "defense", "speed",
  "weight", "bounce", "friction", "flickForce",
  "modelId", "prefabAddress", "iconUrl", "portraitUrl", "selectionImageUrl",
  "sortOrder", "isActive", "createdAt", "updatedAt"
)
SELECT
  'hero_paper_10000005', 'THAN_SAM', 'Than Sam', 'MAGE',
  'Thunder paper hero who controls an area with delayed lightning strikes.',
  98, 18, 4, 1.04,
  0.9, 0.48, 0.4, 1.1,
  '10000005',
  'Assets/AddressableAsset/PaperLegends/HeroPrefabs/10000005.prefab',
  'Assets/AddressableAsset/PaperLegends/HeroIcons/10000005.png',
  'Assets/AddressableAsset/PaperLegends/HeroPortraits/10000005.png',
  'Assets/AddressableAsset/PaperLegends/HeroPortraits/10000005.png',
  50, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
WHERE NOT EXISTS (
  SELECT 1 FROM "Hero" WHERE "modelId" = '10000005'
);

DELETE FROM "HeroSkill"
WHERE "heroId" IN (
  SELECT "id" FROM "Hero" WHERE "modelId" = '10000005'
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
      'skill_than_sam_01',
      1,
      '11400051',
      'Reserved Skill 1',
      'Reserved gameplay slot for Than Sam.',
      0.0,
      0,
      0.0,
      0.0,
      0.0,
      '{}'::jsonb,
      'Assets/AddressableAsset/PaperLegends/SkillIcons/11400051.png',
      'than_sam_skill_1',
      'than_sam_skill_1',
      false,
      true
    ),
    (
      'skill_than_sam_02',
      2,
      '11400052',
      'Reserved Skill 2',
      'Reserved gameplay slot for Than Sam.',
      0.0,
      0,
      0.0,
      0.0,
      0.0,
      '{}'::jsonb,
      'Assets/AddressableAsset/PaperLegends/SkillIcons/11400052.png',
      'than_sam_skill_2',
      'than_sam_skill_2',
      false,
      true
    ),
    (
      'skill_than_sam_03',
      3,
      '11400053',
      'Reserved Skill 3',
      'Reserved gameplay slot for Than Sam.',
      0.0,
      0,
      0.0,
      0.0,
      0.0,
      '{}'::jsonb,
      'Assets/AddressableAsset/PaperLegends/SkillIcons/11400053.png',
      'than_sam_skill_3',
      'than_sam_skill_3',
      false,
      true
    ),
    (
      'skill_than_sam_04',
      4,
      '11400054',
      'Thunder Storm',
      'Select an area, channel for 1 second, then call lightning strikes for 4/5/6 seconds by skill level.',
      38.0,
      0,
      16.0,
      3.2,
      4.0,
      '{"radius":3.2,"strikeRadius":0.85,"castDelay":1.0,"durationsByLevel":[4,5,6],"maxSkillLevel":3}'::jsonb,
      'Assets/AddressableAsset/PaperLegends/SkillIcons/11400054.png',
      'than_sam_thunder_storm',
      'than_sam_thunder_storm',
      false,
      true
    )
) AS skill(
  "id", "slot", "code", "name", "description",
  "cooldown", "manaCost", "damage", "range", "duration",
  "config", "iconUrl", "vfxCode", "sfxCode", "isPassive", "isActive"
)
JOIN "Hero" h ON h."modelId" = '10000005'
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
