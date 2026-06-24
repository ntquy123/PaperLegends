using SQLite; // Đảm bảo import thư viện SQLite
using UnityEngine;

[Table("Item")] // Tên bảng trong database
public class Item
{
    [PrimaryKey]
    public int ID { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int Level { get; set; }
    [Column("Type_Gid")]
    public int TypeGid { get; set; }
    public int Price { get; set; }
    [Column("is_Level_Up")]
    public bool IsLevelUp { get; set; }
    public bool isOpen { get; set; }
    [Column("Location_Gid")]
    public int LocationGid { get; set; }
    [Ignore]
    public int Seq { get; set; }

    public float Mass { get; set; }
    public float GravityScale { get; set; }
    public float Drag { get; set; }
    public float Bounciness { get; set; }
    public float Elasticity { get; set; }
    public float ImpactResistance { get; set; }
    public Item() { }
    // Constructor nhận 5 tham số
    public Item(int id, string name, int level, int price, bool isLevelUp, string description, int typeGid, bool isOpen, int locationGid)
    {
        ID = id;
        Name = name;
        Level = level;
        Price = price;
        IsLevelUp = isLevelUp;
        Description = description;
        TypeGid = typeGid;
        this.isOpen = isOpen;
        LocationGid = locationGid;
    }
}
