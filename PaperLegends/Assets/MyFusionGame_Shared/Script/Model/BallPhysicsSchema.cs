[System.Serializable]
public class BallPhysicsData
{
    public float Mass;
    public float GravityScale;
    public float Drag;
    public float Bounciness;
    public float Elasticity;
    public float ImpactResistance;
    public int level;
    public bool isCateye;
    public float damage;
}

[System.Serializable]
public class BallPhysicsInfo
{
    public int playerId;
    public BallPhysicsData physics;
}

[System.Serializable]
public class BallPhysicsWrapper
{
    public System.Collections.Generic.List<BallPhysicsInfo> physics;
}

[System.Serializable]
public class BallPhysicsItem
{
    public string name;
    public int itemId;
    public int seqItem;
    public int? SkillGid;
    public ActiveSkillSchema activeSkill;
    public float Mass;
    public float GravityScale;
    public float Drag;
    public float Bounciness;
    public float Elasticity;
    public float ImpactResistance;
    public int level;
    public bool isCateye;
    public float damage;
}

[System.Serializable]
public class PlayerBallPhysics
{
    public int playerId;
    public System.Collections.Generic.List<BallPhysicsItem> physics;
}

[System.Serializable]
public class PlayerBallPhysicsWrapper
{
    public System.Collections.Generic.List<PlayerBallPhysics> players;
}

[System.Serializable]
public class PlayerBallPhysicsRoot
{
    public System.Collections.Generic.List<PlayerBallPhysics> physics;
}
