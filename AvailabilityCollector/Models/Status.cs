namespace AvailabilityCollector.Models;

public class Status
{
    public int ID { get; set; }
    public int WorkerID { get; set; }
    public Worker? Worker { get; set; }

    public int StUr { get; set; }
    public int StDelovnihDni { get; set; }
}
