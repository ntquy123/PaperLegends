using Fusion;
using UnityEngine;

public enum PaperLegendTeam : byte
{
    None = 0,
    TeamA = 1,
    TeamB = 2
}

public enum PaperLegendCharacterState : byte
{
    Idle = 0,
    Flicked = 1,
    Airborne = 2,
    Grounded = 3,
    Eliminated = 4,
    Respawning = 5
}

public enum PaperLegendExperienceSource : byte
{
    Pickup = 0,
    Kill = 1,
    Assist = 2,
    Objective = 3
}

public enum PaperLegendHeroSkillId : int
{
    None = 0,

    Hero10000001DistanceLandingDamage = 11400001,
    Hero10000001ReservedSkill2 = 11400002,
    Hero10000001FlickForceBoost = 11400003,
    Hero10000001ReservedSkill4 = 11400004,

    Hero10000002ChildSkill1 = 11400021,
    Hero10000002ChildSkill2 = 11400022,
    Hero10000002HorseSkill1 = 11400023,
    Hero10000002HorseSkill2 = 11400024,
    Hero10000002HorseSkill3 = 11400025,
    Hero10000002HorseSkill4 = 11400026,

    Hero10000003WavePush = 11400031,
    Hero10000003ReservedSkill2 = 11400032,
    Hero10000003ReservedSkill3 = 11400033,
    Hero10000003ReservedSkill4 = 11400034,

    Hero10000004ReservedSkill1 = 11400041,
    Hero10000004ReservedSkill2 = 11400042,
    Hero10000004ReservedSkill3 = 11400043,
    Hero10000004ReservedSkill4 = 11400044,

    Hero10000005ReservedSkill1 = 11400051,
    Hero10000005ReservedSkill2 = 11400052,
    Hero10000005ReservedSkill3 = 11400053,
    Hero10000005ThunderStorm = 11400054
}

public struct PaperLegendPlayerInputData : INetworkInput
{
    public NetworkBool FlickRequested;
    public int FlickSequence;
    public Vector3 ContactWorldPosition;
    public Vector3 ContactSurfaceNormal;
    public Vector3 AimWorldDirection;
    public float Force01;
    public NetworkBool SkillRequested;
    public int SkillSlot;
    public NetworkBool SkillTargetWorldPositionSet;
    public Vector3 SkillTargetWorldPosition;
    public NetworkBool SkillUpgradeRequested;
    public int SkillUpgradeSlot;
}
