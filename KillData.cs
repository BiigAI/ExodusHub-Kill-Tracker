// Model class for kill data
public class KillData
{
    public DateTime Timestamp { get; set; }
    public string Killer { get; set; }
    public string Victim { get; set; }
    public string Weapon { get; set; }
    public string Location { get; set; }
    public string KillType { get; set; }
}