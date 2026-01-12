using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Controllers
{
    /// <summary>
    /// REST API controller for Razpolozljivost (Availability)
    /// Used by Android mobile application with Volley library
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class RazpolozljivostiApiController : ControllerBase
    {
        private readonly AppContextDb _context;

        public RazpolozljivostiApiController(AppContextDb context)
        {
            _context = context;
        }

        /// <summary>
        /// GET: api/razpolozljivostiapi - READ operation
        /// Returns all availability entries as JSON
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RazpolozljivostDto>>> GetRazpolozljivosti()
        {
            var razpolozljivosti = await _context.Razpolozljivosti
                .Include(r => r.Worker)
                .Select(r => new RazpolozljivostDto
                {
                    Id = r.ID,
                    RazpolozljivostJSON = r.RazpolozljivostJSON,
                    MesecLeto = r.MesecLeto,
                    Type = r.Type,
                    ZaporedniTeden = r.ZaporedniTeden,
                    WorkerId = r.WorkerID,
                    WorkerName = r.Worker != null ? $"{r.Worker.Ime} {r.Worker.Priimek}" : null
                })
                .ToListAsync();

            return Ok(razpolozljivosti);
        }

        /// <summary>
        /// GET: api/razpolozljivostiapi/{id} - READ operation
        /// Returns a single availability entry by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<RazpolozljivostDto>> GetRazpolozljivost(int id)
        {
            var razpolozljivost = await _context.Razpolozljivosti
                .Include(r => r.Worker)
                .FirstOrDefaultAsync(r => r.ID == id);

            if (razpolozljivost == null)
            {
                return NotFound(new { message = "Availability not found" });
            }

            return Ok(new RazpolozljivostDto
            {
                Id = razpolozljivost.ID,
                RazpolozljivostJSON = razpolozljivost.RazpolozljivostJSON,
                MesecLeto = razpolozljivost.MesecLeto,
                Type = razpolozljivost.Type,
                ZaporedniTeden = razpolozljivost.ZaporedniTeden,
                WorkerId = razpolozljivost.WorkerID,
                WorkerName = razpolozljivost.Worker != null ? $"{razpolozljivost.Worker.Ime} {razpolozljivost.Worker.Priimek}" : null
            });
        }

        /// <summary>
        /// GET: api/razpolozljivostiapi/worker/{workerId} - READ operation
        /// Returns all availability entries for a specific worker
        /// </summary>
        [HttpGet("worker/{workerId}")]
        public async Task<ActionResult<IEnumerable<RazpolozljivostDto>>> GetRazpolozljivostiByWorker(int workerId)
        {
            var razpolozljivosti = await _context.Razpolozljivosti
                .Include(r => r.Worker)
                .Where(r => r.WorkerID == workerId)
                .Select(r => new RazpolozljivostDto
                {
                    Id = r.ID,
                    RazpolozljivostJSON = r.RazpolozljivostJSON,
                    MesecLeto = r.MesecLeto,
                    Type = r.Type,
                    ZaporedniTeden = r.ZaporedniTeden,
                    WorkerId = r.WorkerID,
                    WorkerName = r.Worker != null ? $"{r.Worker.Ime} {r.Worker.Priimek}" : null
                })
                .ToListAsync();

            return Ok(razpolozljivosti);
        }

        /// <summary>
        /// POST: api/razpolozljivostiapi - CREATE operation
        /// Creates a new availability entry
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<RazpolozljivostDto>> CreateRazpolozljivost([FromBody] RazpolozljivostCreateDto dto)
        {
            // Validate that the worker exists
            var worker = await _context.Workers.FindAsync(dto.WorkerId);
            if (worker == null)
            {
                return BadRequest(new { message = "Worker not found" });
            }

            var razpolozljivost = new Razpolozljivost
            {
                RazpolozljivostJSON = dto.RazpolozljivostJSON ?? "{}",
                MesecLeto = dto.MesecLeto ?? "",
                Type = dto.Type ?? "weekly",
                ZaporedniTeden = dto.ZaporedniTeden,
                WorkerID = dto.WorkerId
            };

            _context.Razpolozljivosti.Add(razpolozljivost);
            await _context.SaveChangesAsync();

            var result = new RazpolozljivostDto
            {
                Id = razpolozljivost.ID,
                RazpolozljivostJSON = razpolozljivost.RazpolozljivostJSON,
                MesecLeto = razpolozljivost.MesecLeto,
                Type = razpolozljivost.Type,
                ZaporedniTeden = razpolozljivost.ZaporedniTeden,
                WorkerId = razpolozljivost.WorkerID,
                WorkerName = $"{worker.Ime} {worker.Priimek}"
            };

            return CreatedAtAction(nameof(GetRazpolozljivost), new { id = razpolozljivost.ID }, result);
        }

        /// <summary>
        /// PUT: api/razpolozljivostiapi/{id} - UPDATE operation
        /// Updates an existing availability entry
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<RazpolozljivostDto>> UpdateRazpolozljivost(int id, [FromBody] RazpolozljivostCreateDto dto)
        {
            var razpolozljivost = await _context.Razpolozljivosti
                .Include(r => r.Worker)
                .FirstOrDefaultAsync(r => r.ID == id);

            if (razpolozljivost == null)
            {
                return NotFound(new { message = "Availability not found" });
            }

            if (!string.IsNullOrWhiteSpace(dto.RazpolozljivostJSON))
                razpolozljivost.RazpolozljivostJSON = dto.RazpolozljivostJSON;
            if (!string.IsNullOrWhiteSpace(dto.MesecLeto))
                razpolozljivost.MesecLeto = dto.MesecLeto;
            if (!string.IsNullOrWhiteSpace(dto.Type))
                razpolozljivost.Type = dto.Type;
            if (dto.ZaporedniTeden.HasValue)
                razpolozljivost.ZaporedniTeden = dto.ZaporedniTeden;

            await _context.SaveChangesAsync();

            return Ok(new RazpolozljivostDto
            {
                Id = razpolozljivost.ID,
                RazpolozljivostJSON = razpolozljivost.RazpolozljivostJSON,
                MesecLeto = razpolozljivost.MesecLeto,
                Type = razpolozljivost.Type,
                ZaporedniTeden = razpolozljivost.ZaporedniTeden,
                WorkerId = razpolozljivost.WorkerID,
                WorkerName = razpolozljivost.Worker != null ? $"{razpolozljivost.Worker.Ime} {razpolozljivost.Worker.Priimek}" : null
            });
        }

        /// <summary>
        /// DELETE: api/razpolozljivostiapi/{id} - DELETE operation
        /// Deletes an availability entry
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteRazpolozljivost(int id)
        {
            var razpolozljivost = await _context.Razpolozljivosti.FindAsync(id);
            if (razpolozljivost == null)
            {
                return NotFound(new { message = "Availability not found" });
            }

            _context.Razpolozljivosti.Remove(razpolozljivost);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    /// <summary>
    /// DTO for returning availability data
    /// </summary>
    public class RazpolozljivostDto
    {
        public int Id { get; set; }
        public string RazpolozljivostJSON { get; set; } = "";
        public string MesecLeto { get; set; } = "";
        public string Type { get; set; } = "";
        public int? ZaporedniTeden { get; set; }
        public int WorkerId { get; set; }
        public string? WorkerName { get; set; }
    }

    /// <summary>
    /// DTO for creating/updating availability
    /// </summary>
    public class RazpolozljivostCreateDto
    {
        public string? RazpolozljivostJSON { get; set; }
        public string? MesecLeto { get; set; }
        public string? Type { get; set; }
        public int? ZaporedniTeden { get; set; }
        public int WorkerId { get; set; }
    }
}
