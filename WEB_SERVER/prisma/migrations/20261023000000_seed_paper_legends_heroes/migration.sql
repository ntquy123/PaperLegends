-- Seed Paper Legends starter hero catalog.
-- Six heroes, one per role, each with four skills.

INSERT INTO "Hero" (
  "id", "code", "name", "role", "description",
  "hp", "attack", "defense", "speed",
  "weight", "bounce", "friction", "flickForce",
  "modelId", "prefabAddress", "iconUrl", "portraitUrl", "selectionImageUrl",
  "sortOrder", "isActive", "createdAt", "updatedAt"
)
VALUES
  (
    'hero_paper_10000001', 'THAN_DONG', 'Than Dong', 'WARRIOR',
    'Balanced paper warrior who wins by direct flick pressure and clean landing hits.',
    118, 16, 7, 1.00,
    1.05, 0.38, 0.48, 1.08,
    '10000001', 'Assets/Prefab/Chacter/10000001.prefab',
    '/paper-legends/assets/heroes/10000001/icon.png',
    '/paper-legends/assets/heroes/10000001/portrait.png',
    '/paper-legends/assets/heroes/10000001/selection.png',
    10, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
  ),
  (
    'hero_paper_10000002', 'LAC_VIET_SCOUT', 'Lac Viet Scout', 'ASSASSIN',
    'Light assassin paper hero with fast movement and strong finish potential.',
    88, 21, 3, 1.32,
    0.72, 0.52, 0.34, 1.24,
    '10000002', 'Assets/Prefab/Chacter/10000002.prefab',
    '/paper-legends/assets/heroes/10000002/icon.png',
    '/paper-legends/assets/heroes/10000002/portrait.png',
    '/paper-legends/assets/heroes/10000002/selection.png',
    20, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
  ),
  (
    'hero_paper_10000003', 'AU_CO_GUARDIAN', 'Au Co Guardian', 'TANK',
    'Heavy defensive paper hero that resists displacement and punishes close targets.',
    158, 11, 15, 0.78,
    1.55, 0.24, 0.68, 0.88,
    '10000003', 'Assets/Prefab/Chacter/10000003.prefab',
    '/paper-legends/assets/heroes/10000003/icon.png',
    '/paper-legends/assets/heroes/10000003/portrait.png',
    '/paper-legends/assets/heroes/10000003/selection.png',
    30, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
  ),
  (
    'hero_paper_10000004', 'SON_TINH_SHAMAN', 'Son Tinh Shaman', 'MAGE',
    'Mage paper hero with control effects and burst windows after precise flicks.',
    96, 24, 4, 0.95,
    0.92, 0.46, 0.42, 1.12,
    '10000004', 'Assets/Prefab/Chacter/10000004.prefab',
    '/paper-legends/assets/heroes/10000004/icon.png',
    '/paper-legends/assets/heroes/10000004/portrait.png',
    '/paper-legends/assets/heroes/10000004/selection.png',
    40, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
  ),
  (
    'hero_paper_10000005', 'MY_NUONG_ENVOY', 'My Nuong Envoy', 'SUPPORT',
    'Support paper hero that protects allies, grants assist value, and weakens enemies.',
    108, 12, 8, 1.08,
    0.88, 0.41, 0.44, 1.02,
    '10000005', 'Assets/Prefab/Chacter/10000005.prefab',
    '/paper-legends/assets/heroes/10000005/icon.png',
    '/paper-legends/assets/heroes/10000005/portrait.png',
    '/paper-legends/assets/heroes/10000005/selection.png',
    50, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
  ),
  (
    'hero_paper_10000006', 'NO_THAN_ARCHER', 'No Than Archer', 'MARKSMAN',
    'Marksman paper hero with long glide pressure and reliable poke before landing.',
    92, 20, 5, 1.16,
    0.82, 0.44, 0.36, 1.18,
    '10000006', 'Assets/Prefab/Chacter/10000006.prefab',
    '/paper-legends/assets/heroes/10000006/icon.png',
    '/paper-legends/assets/heroes/10000006/portrait.png',
    '/paper-legends/assets/heroes/10000006/selection.png',
    60, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
  )
ON CONFLICT ("code") DO UPDATE SET
  "name" = EXCLUDED."name",
  "role" = EXCLUDED."role",
  "description" = EXCLUDED."description",
  "hp" = EXCLUDED."hp",
  "attack" = EXCLUDED."attack",
  "defense" = EXCLUDED."defense",
  "speed" = EXCLUDED."speed",
  "weight" = EXCLUDED."weight",
  "bounce" = EXCLUDED."bounce",
  "friction" = EXCLUDED."friction",
  "flickForce" = EXCLUDED."flickForce",
  "modelId" = EXCLUDED."modelId",
  "prefabAddress" = EXCLUDED."prefabAddress",
  "iconUrl" = EXCLUDED."iconUrl",
  "portraitUrl" = EXCLUDED."portraitUrl",
  "selectionImageUrl" = EXCLUDED."selectionImageUrl",
  "sortOrder" = EXCLUDED."sortOrder",
  "isActive" = EXCLUDED."isActive",
  "updatedAt" = CURRENT_TIMESTAMP;

DELETE FROM "HeroSkill"
WHERE "heroId" IN (
  SELECT "id"
  FROM "Hero"
  WHERE "code" IN (
    'THAN_DONG',
    'LAC_VIET_SCOUT',
    'AU_CO_GUARDIAN',
    'SON_TINH_SHAMAN',
    'MY_NUONG_ENVOY',
    'NO_THAN_ARCHER'
  )
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
    ('skill_td_01', 'THAN_DONG', 1, 'TD_TRONG_DONG_STEP', 'Trong Dong Step', 'A steady flick gains extra landing damage after a short hop.', 3.0, 0, 18.0, 2.2, 1.0, '{"landingDamageBonus":0.12,"minForce":0.35}'::jsonb, '/paper-legends/assets/heroes/10000001/skills/1.png', 'td_step', 'paper_flick_heavy', false, true),
    ('skill_td_02', 'THAN_DONG', 2, 'TD_SHIELD_EDGE', 'Shield Edge', 'After being hit, reduce the next displacement and counter on contact.', 8.0, 0, 10.0, 1.4, 3.5, '{"displacementReduction":0.22,"counterWindow":3.5}'::jsonb, '/paper-legends/assets/heroes/10000001/skills/2.png', 'td_shield', 'paper_block', false, true),
    ('skill_td_03', 'THAN_DONG', 3, 'TD_WAR_DRUM', 'War Drum', 'Temporarily increases base flick force and assist experience.', 14.0, 0, 0.0, 4.0, 5.0, '{"flickForceMultiplier":1.12,"assistExpBonus":0.15}'::jsonb, '/paper-legends/assets/heroes/10000001/skills/3.png', 'td_drum', 'drum_hit', false, true),
    ('skill_td_04', 'THAN_DONG', 4, 'TD_KING_LANDING', 'King Landing', 'Ultimate landing hit grants bonus kill experience and briefly slows nearby enemies.', 38.0, 0, 42.0, 2.8, 2.5, '{"bonusKillExp":35,"slowPercent":0.25}'::jsonb, '/paper-legends/assets/heroes/10000001/skills/4.png', 'td_ultimate', 'paper_impact_big', false, true),

    ('skill_lvs_01', 'LAC_VIET_SCOUT', 1, 'LVS_QUICK_FLICK', 'Quick Flick', 'A light fast flick with reduced cooldown when grounded quickly.', 2.2, 0, 14.0, 2.8, 0.8, '{"cooldownRefundOnFastLand":0.45,"speedBonus":0.18}'::jsonb, '/paper-legends/assets/heroes/10000002/skills/1.png', 'lvs_quick', 'paper_swish', false, true),
    ('skill_lvs_02', 'LAC_VIET_SCOUT', 2, 'LVS_SHADOW_SLIP', 'Shadow Slip', 'Briefly lowers friction to slide around a target after landing.', 9.0, 0, 6.0, 3.0, 2.0, '{"frictionMultiplier":0.72,"duration":2.0}'::jsonb, '/paper-legends/assets/heroes/10000002/skills/2.png', 'lvs_slip', 'paper_slide', false, true),
    ('skill_lvs_03', 'LAC_VIET_SCOUT', 3, 'LVS_MARK_PREY', 'Mark Prey', 'Marks the nearest lower-level enemy; landing on them grants extra damage.', 12.0, 0, 22.0, 5.0, 6.0, '{"preferLowerLevel":true,"bonusDamage":0.2}'::jsonb, '/paper-legends/assets/heroes/10000002/skills/3.png', 'lvs_mark', 'target_mark', false, true),
    ('skill_lvs_04', 'LAC_VIET_SCOUT', 4, 'LVS_BAMBOO_AMBUSH', 'Bamboo Ambush', 'Ultimate leap gains high bounce and executes low-health paper targets.', 34.0, 0, 38.0, 3.6, 1.5, '{"bounceMultiplier":1.25,"executeHpPercent":0.25}'::jsonb, '/paper-legends/assets/heroes/10000002/skills/4.png', 'lvs_ultimate', 'paper_cut_fast', false, true),

    ('skill_acg_01', 'AU_CO_GUARDIAN', 1, 'ACG_STONE_WEIGHT', 'Stone Weight', 'Passive weight reduces knockback from enemy landings.', 0.0, 0, 0.0, 0.0, 0.0, '{"incomingImpulseMultiplier":0.82}'::jsonb, '/paper-legends/assets/heroes/10000003/skills/1.png', 'acg_weight', 'stone_guard', true, true),
    ('skill_acg_02', 'AU_CO_GUARDIAN', 2, 'ACG_MOTHER_WALL', 'Mother Wall', 'Creates a short defensive window after landing.', 10.0, 0, 8.0, 1.8, 4.0, '{"defenseBonus":8,"duration":4.0}'::jsonb, '/paper-legends/assets/heroes/10000003/skills/2.png', 'acg_wall', 'paper_guard', false, true),
    ('skill_acg_03', 'AU_CO_GUARDIAN', 3, 'ACG_ROOT_GRIP', 'Root Grip', 'High friction stance that stops sliding and resists being pushed away.', 13.0, 0, 0.0, 2.0, 3.5, '{"frictionMultiplier":1.35,"pushResistance":0.3}'::jsonb, '/paper-legends/assets/heroes/10000003/skills/3.png', 'acg_root', 'root_hold', false, true),
    ('skill_acg_04', 'AU_CO_GUARDIAN', 4, 'ACG_MOUNTAIN_PRESS', 'Mountain Press', 'Ultimate heavy landing deals large area crush damage.', 42.0, 0, 46.0, 2.4, 1.2, '{"areaRadius":2.4,"weightMultiplier":1.18}'::jsonb, '/paper-legends/assets/heroes/10000003/skills/4.png', 'acg_ultimate', 'paper_impact_heavy', false, true),

    ('skill_sts_01', 'SON_TINH_SHAMAN', 1, 'STS_RIVER_CURVE', 'River Curve', 'A curved flick improves side approach angle and poke damage.', 3.5, 0, 17.0, 3.0, 1.0, '{"curveStrength":0.2,"sideHitBonus":0.1}'::jsonb, '/paper-legends/assets/heroes/10000004/skills/1.png', 'sts_curve', 'water_flick', false, true),
    ('skill_sts_02', 'SON_TINH_SHAMAN', 2, 'STS_MIST_SNARE', 'Mist Snare', 'Landing near an enemy reduces their next flick force.', 11.0, 0, 9.0, 3.2, 4.0, '{"enemyFlickForceMultiplier":0.82,"duration":4.0}'::jsonb, '/paper-legends/assets/heroes/10000004/skills/2.png', 'sts_mist', 'mist_snare', false, true),
    ('skill_sts_03', 'SON_TINH_SHAMAN', 3, 'STS_THUNDER_MARK', 'Thunder Mark', 'Marks a target; the next landing hit deals bonus burst.', 15.0, 0, 28.0, 5.0, 6.0, '{"bonusBurst":18,"markDuration":6.0}'::jsonb, '/paper-legends/assets/heroes/10000004/skills/3.png', 'sts_mark', 'thunder_mark', false, true),
    ('skill_sts_04', 'SON_TINH_SHAMAN', 4, 'STS_STORM_FOLD', 'Storm Fold', 'Ultimate launches with extra bounce and applies area slow on landing.', 40.0, 0, 44.0, 3.5, 3.0, '{"bounceMultiplier":1.3,"slowPercent":0.35}'::jsonb, '/paper-legends/assets/heroes/10000004/skills/4.png', 'sts_ultimate', 'storm_fold', false, true),

    ('skill_mne_01', 'MY_NUONG_ENVOY', 1, 'MNE_SILK_STEP', 'Silk Step', 'Smooth controlled flick with reduced landing drift.', 2.8, 0, 11.0, 2.4, 1.0, '{"landingDriftMultiplier":0.8}'::jsonb, '/paper-legends/assets/heroes/10000005/skills/1.png', 'mne_step', 'silk_move', false, true),
    ('skill_mne_02', 'MY_NUONG_ENVOY', 2, 'MNE_LAC_CHARM', 'Lac Charm', 'Weakens a nearby enemy and grants assist credit on takedown.', 12.0, 0, 7.0, 4.0, 5.0, '{"damageTakenBonus":0.1,"assistWindow":5.0}'::jsonb, '/paper-legends/assets/heroes/10000005/skills/2.png', 'mne_charm', 'charm_cast', false, true),
    ('skill_mne_03', 'MY_NUONG_ENVOY', 3, 'MNE_PAPER_BLESSING', 'Paper Blessing', 'Protects self after respawn and gives bonus experience from pickups.', 18.0, 0, 0.0, 0.0, 5.0, '{"spawnShieldSeconds":3.0,"pickupExpMultiplier":1.15}'::jsonb, '/paper-legends/assets/heroes/10000005/skills/3.png', 'mne_blessing', 'paper_bless', false, true),
    ('skill_mne_04', 'MY_NUONG_ENVOY', 4, 'MNE_ROYAL_DECREE', 'Royal Decree', 'Ultimate grants a short team-wide flick force bonus.', 45.0, 0, 0.0, 8.0, 6.0, '{"teamFlickForceMultiplier":1.1,"duration":6.0}'::jsonb, '/paper-legends/assets/heroes/10000005/skills/4.png', 'mne_ultimate', 'royal_decree', false, true),

    ('skill_nta_01', 'NO_THAN_ARCHER', 1, 'NTA_ARROW_LINE', 'Arrow Line', 'Straight precise flick deals bonus poke when landing near the target.', 3.0, 0, 16.0, 4.2, 1.0, '{"straightnessBonus":0.14,"nearMissRadius":0.7}'::jsonb, '/paper-legends/assets/heroes/10000006/skills/1.png', 'nta_arrow', 'arrow_line', false, true),
    ('skill_nta_02', 'NO_THAN_ARCHER', 2, 'NTA_FEATHER_GLIDE', 'Feather Glide', 'Longer glide with lower friction after a medium force flick.', 9.0, 0, 8.0, 4.5, 2.2, '{"frictionMultiplier":0.75,"minForce":0.45}'::jsonb, '/paper-legends/assets/heroes/10000006/skills/2.png', 'nta_glide', 'feather_glide', false, true),
    ('skill_nta_03', 'NO_THAN_ARCHER', 3, 'NTA_WEAK_SPOT', 'Weak Spot', 'Deals extra damage to high-level targets to keep snowballing under control.', 14.0, 0, 26.0, 5.5, 5.0, '{"bonusVsHigherLevel":0.18,"duration":5.0}'::jsonb, '/paper-legends/assets/heroes/10000006/skills/3.png', 'nta_weakspot', 'weak_spot', false, true),
    ('skill_nta_04', 'NO_THAN_ARCHER', 4, 'NTA_CROSSBOW_RAIN', 'Crossbow Rain', 'Ultimate marks a landing area and damages enemies hit by the paper crash.', 39.0, 0, 43.0, 5.0, 2.0, '{"areaRadius":3.0,"markDelay":0.8}'::jsonb, '/paper-legends/assets/heroes/10000006/skills/4.png', 'nta_ultimate', 'crossbow_rain', false, true)
) AS skill(
  "id", "heroCode", "slot", "code", "name", "description",
  "cooldown", "manaCost", "damage", "range", "duration",
  "config", "iconUrl", "vfxCode", "sfxCode", "isPassive", "isActive"
)
JOIN "Hero" h ON h."code" = skill."heroCode"
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

