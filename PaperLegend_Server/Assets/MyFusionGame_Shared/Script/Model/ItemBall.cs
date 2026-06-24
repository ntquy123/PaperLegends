[System.Serializable]
public class ItemBall
{
    public int ID { get; set; }
    public string Name { get; set; }
    public int Level { get; set; }
    public float Price { get; set; }
    public bool IsLevelUp { get; set; }
    public ElementalType element;
}
