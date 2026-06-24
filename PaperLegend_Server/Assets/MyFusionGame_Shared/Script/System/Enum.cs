
using UnityEngine;

public static class ApiConfig
{
    private static ServerConfig _config;

    private static ServerConfig Config
    {
        get
        {
            if (_config == null)
            {
                _config = Resources.Load<ServerConfig>("ServerConfig");
                if (_config == null)
                {
                    Debug.LogError("ServerConfig asset not found in Resources folder.");
                    _config = ScriptableObject.CreateInstance<ServerConfig>();
                }
            }
            return _config;
        }
    }

    public static string BaseUrl => Config.baseUrl;
    public static string WebSocketUrl => Config.webSocketUrl;
    public static string BaseUrlPhoton => Config.baseUrlPhoton;
    public static string BaseUrlLocal => Config.baseUrlLocal;
    public static string BaseUrPhotonClound => Config.baseUrlPhotonCloud;
    public static string CatalogUrl => Config.catalogUrl;
    public static bool UsePinnedHttpsCertificates => Config.usePinnedHttpsCertificates;
    public static System.Collections.Generic.List<string> HttpsCertificatePins => Config.httpsCertificatePins;
    public static bool IsHttpsBaseUrl => !string.IsNullOrWhiteSpace(Config.baseUrl) && Config.baseUrl.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase);
}


public static class AddressablePaths
{
    public const string Root = "Assets/AddressableAsset";

    public static class Items
    {
        public const string RootItem                = "Assets/AddressableAsset/Items";
        public const string ImageItem               = "Assets/AddressableAsset/Items/ImageItem";
        public const string Culi                    = "Assets/AddressableAsset/Items/Culi";
        public const string CuliCateye              = "Assets/AddressableAsset/Items/Culi/Cateye";
        public const string CuliCateyeRingBall      = "Assets/AddressableAsset/Items/Culi/Cateye/RingBall";
        public const string CuliEffect              = "Assets/AddressableAsset/Items/Culi/EffectBall";
        public const string HairMeshes              = "Assets/AddressableAsset/Items/HairMeshes";
        public const string Clothes                 = "Assets/AddressableAsset/Items/Clothes";
        public const string DefaultCuliMaterial     = "Assets/AddressableAsset/Items/Culi/99000001.mat";
        public const string DefaultCateyeCuliMaterial = "Assets/AddressableAsset/Items/Culi/Cateye/RingBall/1.mat";
        public const string DefaultHairMesh         = "Assets/AddressableAsset/Items/HairMeshes/99000001.mat";
        public const string DefaultEffectBall       = "Assets/AddressableAsset/Items/Culi/EffectBall/99000001.prefab";
    }

    public static class Character
    {
        public const string Root            = "Assets/AddressableAsset/Character";
        public const string DefaultMaterial = "Assets/AddressableAsset/Character/Materials/DefaultPlayer.mat";
    }

    public static class PaperLegends
    {
        public const string Root = "Assets/AddressableAsset/PaperLegends";
        public const string HeroIcons = "Assets/AddressableAsset/PaperLegends/HeroIcons";
        public const string HeroPortraits = "Assets/AddressableAsset/PaperLegends/HeroPortraits";
        public const string HeroMaterials = "Assets/AddressableAsset/PaperLegends/HeroMaterial";
        public const string HeroPrefabs = "Assets/AddressableAsset/PaperLegends/HeroPrefabs";
        public const string SkillIcons = "Assets/AddressableAsset/PaperLegends/SkillIcons";
    }

    public static class Audio
    {
        public const string Root = "Assets/AddressableAsset/Audio";
    }
}

public static class ItemIdConfig
{
    public const int CuliSpriteId = 88000001;
}
public enum CameraFollowMode
{
    FixedOnHead,
    FollowFromDistance
}

public enum TurnState
{
    Exam,       // Thi xem thứ tự lượt chơi
    StartFromPoint,    // Bắt đầu tại mức
    Playing,
    End
}
public enum StatusPlayer
{
    Normal,
    ShootExam,
    StartPoint,
    MoveStartPoint,
    HoldingBall,
    Power,
    WaitingDestroy,
    Destroy
}
public enum SkillType
{
    Normal,
    [SkillIcon("Skills/fireball")] Ultimate,
    [SkillIcon("Skills/3Gang")] An3Gang,
}
public enum LocationItemGid
{
    Equipped = 0,
    Inventory =1,
    Shop = 2,
    Gif =3,
    Vip =4,
    CompanionBall = 5
}

public enum ItemInfoPopupTab
{
    Inventory = 1,
    Market = 2,
    Equipped = 3,
    CompanionSelection = 4,
    OnlyView = 5
}
public enum TypeItemGid
{
    All = 0,
    Culi = 1,
    Gem = 2,
    Clother = 3,
    Hair = 4,
    Other =5,
    Sale =6,
    PackageMoney = 7,
    PackageBall = 8
}
public enum StatusSold
{
    None = 0,
    Sale = 1,
    Sold = 2,
    NotSold = 3
}
public enum ItemCode
{
    CuliMatTroi = 1,
    CuliBangGia =2,
    AoThunBasic =3,
    AoBanhCam = 4,
    TocDaiLangTu = 5,
    DauTroc=6,
    TocChayNang = 7,
    TocChuanMen=8,
    DoTet=9,
    CuliBaKhiaVang = 10,
    CuliSuaDen = 11,
    CuliRing = 12,
}
public enum EffectPlayerType
{
    PowerSkill= 11000001,
    SpinSkill = 11000002,
    MentalitySkill= 11000003,
    HachDichSkill = 11000004,
    ViewSkill= 11000005,
    ChiemSkill= 11000006,
    CatAnTienSkill= 11000007,
    ChamCat = 11000008,
    BananaJumpSkill = 11000009,
    GrazeHit = 11000010,
    HeavyBallSkill = GrazeHit,
    BigBallSkill = 11000011,
    WindBlowSkill = 11000012,
    HuSkill = 11000013,
    SmallBallSkill = 11000014
}
public enum PlayerBodyType
{
    ChuBe = 1,
    HocSinh =2,
    GiangHo =3
}
public enum CharacterAnimState
{
    None,
    Idle,
    Running,
    SitToShoot,
    StandingUp,
    Shoot,
    Sleeping,
    Jumping,
    Falling,
    Dead,
    Changeball,
    Slipping,
    BlowWind,
    Hu,
    PickingUp,
    EmoteLaugh,
    EmoteTaunt,
    EmoteAngry,
    EmoteClap,
    HurtAfterSlip,
    LoseEmotion,
    EmoteSad
}
public enum StatusLoadingGame : byte
{
    None,
    LoadMenu,
    LoadMapGame,
    isExam,
    StartTurn,
    ContinueTurn,
    NextTurn,
    EndTurn,
    EndGame,
    DownloadData

}
public enum StatusWin
{
    Playing =0,
    Win = 1,
    Dickens = 2,
    Lose = 3,
    Surrender =4
}
public enum TypeMatchGid
{
    MatchRandomNormal =10000001,
    MatchRandomRank = 10000002,
    MatchRoom = 10000003,
}

public enum TimeOfDay
{
    Morning,
    Afternoon,
    Evening
}

public enum WeatherType
{
    Sunny,
    Rainy
}

public enum RewardType
{
    LuckyDraw = 11100001,
    WatchAds = 11100002,
    RollCallDaily = 11100003
}
public enum ButtonSfx
{
    Default,
    FindMatch,
    CancelMatch
}
public enum QuickMatchState
{
    Idle,
    Searching,
    MatchReady,
    EnteringMatch
}
public enum PlayerMovementRequestType
{
    TeleportExam, // di chuyển đến chỗ thi sau đó dòm vào mức thi
    TeleportStartPoint, // di chuyển đến mức để bắn sau đó nhìn vào vòng đậu bi (PlayArea)
    TeleportGatherPoint, // di chuyển về điểm tập kết đứng chờ, chưa ngồi và chưa cầm bi
    MoveToPlayArea // di chuyển đến chỗ viên bi của mình sau đó nhìn vào vòng đậu bi PlayArea
}
public enum GameInitializationFailureReason
{
    None,
    MissingRunner,
    RunnerNotRunning,
    MissingManager,
    SceneManagerUnavailable,
    SceneLoadFailed,
    NoPlayers,
    DataLoadFailed,
    SpawnFailed,
    ConnectionLost,
    Timeout,
    Unknown
}

public enum AuthenticationProviderType
{
    Anonymous,
    EmailPassword,
    Phone,
    Google,
    GooglePlayGames,
    Facebook,
    Twitter,
    GitHub,
    Microsoft,
    Yahoo,
    Apple,
    GameCenter,
    CustomToken
}
public enum MenuActionType
{
    None,
    Effect,
    Shop,
    Market,
    Rule,
    Settings,
    Friends,
    Messages,
    RewardAds,
    RewardDailyLogin,
    Inventory,
    MenuRoom,
    Uplevel,
    QuickMatch
}
public enum BallForgeSkillType
{
    BallShootingTechnique,
    Support
}
public enum GameMapId
{
    HometownHouse = 12000001,
    VillageRoad = 12000002,
    CoreShootingTutorial = 12000901
}
public enum ItemRarity
{
    Lowest = 11300001,
    Valuable = 11300002,
    Rare = 11300003,
    Epic = 11300004,
    Legendary = 11300005
}
