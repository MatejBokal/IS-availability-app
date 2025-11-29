namespace AvailabilityCollector.Models;

public class Razpolozljivost
{
    public int ID { get; set; }
    public string RazpolozljivostJSON { get; set; } = "";
    public string MesecLeto { get; set; } = "";
    public string Type { get; set; } = "";  
    public int? ZaporedniTeden { get; set; }

    public int WorkerID { get; set; }
    public Worker? Worker { get; set; }
}
