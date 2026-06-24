-- Add IsActive and IsEquiped flags to EffectPlayer
ALTER TABLE "EffectPlayer" ADD COLUMN "IsActive" BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE "EffectPlayer" ADD COLUMN "IsEquiped" BOOLEAN NOT NULL DEFAULT false;
