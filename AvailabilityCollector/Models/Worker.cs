namespace AvailabilityCollector.Models;

public class Worker
{
    public int ID { get; set; }
    public string Ime { get; set; } = "";
    public string Priimek { get; set; } = "";
    public string DelovnoMesto { get; set; } = "";
    public string VrstaZaposlitve { get; set; } = ""; 
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Razpolozljivost>? Razpolozljivosti { get; set; }
    public ICollection<Status>? Statusi { get; set; }
}