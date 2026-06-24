using SQLite;
 
public class EffectPlayer
{
    [PrimaryKey]
    public int ID { get; set; }

    public string Name { get; set; }
    public int Power { get; set; }
    public int Spin { get; set; }
    public int Mentality { get; set; }
    public int Level { get; set; }
    [Column("Is_Passive")]
    public bool IsPassive{ get; set; }
    public int Charges { get; set; }
    public string Description { get; set; }
    [Column("Parent_Id")]
    public int ParentId { get; set; }
    public int TalentPoint { get;  set; }
    public bool IsActive { get; set; }
    public bool IsEquiped { get; set; }

    public EffectPlayer() { }
}

