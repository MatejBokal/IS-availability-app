namespace AvailabilityCollector.Models;

public class UrnikRazpolozljivost
{
    public int ID { get; set; }
    public string UrnikJSON { get; set; } = "";
    public string MesecLeto { get; set; } = "";
    public string Type { get; set; } = "";
    public int? ZaporedniTeden { get; set; }
}
