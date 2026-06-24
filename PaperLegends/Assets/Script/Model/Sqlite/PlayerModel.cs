using SQLite; // Đảm bảo import thư viện SQLite
using UnityEngine;

[Table("Player")] // Tên bảng trong database
public class PlayerModel
{
    [PrimaryKey, AutoIncrement]
    public int ID { get; set; }

    [Column("player_name")]
    public string PlayerName { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int Ball { get; set; }
    public int Shirt { get; set; }
    public int Pant { get; set; }
    public int RingBall { get; set; }
    public int Money { get; set; }
    public int Hair { get; set; }

    [Column("Talent_Point")]
    public int TalentPoint { get; set; }
    public string Id_Account { get; set; }
    // Constructor mặc định
    public PlayerModel() { }

    // Constructor có tham số
    public PlayerModel(int id, string playerName, int level, int exp, int ball, int shirt, int pant, int ringBall, int money, int hair, string id_Account)
    {
        ID = id;
        PlayerName = playerName;
        Level = level;
        Exp = exp;
        Ball = ball;
        Shirt = shirt;
        Pant = pant;
        RingBall = ringBall;
        Money = money;
        Hair = hair;
        Id_Account = id_Account;
    }
}

public class PlayerModelDetail
{
    public int ID { get; set; }
    public string Player_Name { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int Ball { get; set; }
    public int Shirt { get; set; }
    public int Pant { get; set; }
    public int RingBall { get; set; }
    public int? Money { get; set; }  // Thay int thành int? để có thể chứa null
    public int Hair { get; set; }
    public int Talent_Point { get; set; }
    public string Id_Account { get; set; }
}

