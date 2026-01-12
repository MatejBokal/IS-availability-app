using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Controllers
{
    /// <summary>
    /// REST API controller for Workers
    /// Used by Android mobile application with Volley library
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class WorkersApiController : ControllerBase
    {
        private readonly AppContextDb _context;

        public WorkersApiController(AppContextDb context)
        {
            _context = context;
        }

        /// <summary>
        /// GET: api/workers - READ operation
        /// Returns all workers as JSON
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkerDto>>> GetWorkers()
        {
            var workers = await _context.Workers
                .Select(w => new WorkerDto
                {
                    Id = w.ID,
                    Ime = w.Ime,
                    Priimek = w.Priimek,
                    DelovnoMesto = w.DelovnoMesto,
                    VrstaZaposlitve = w.VrstaZaposlitve,
                    CreatedAt = w.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    IsActive = w.IsActive
                })
                .ToListAsync();

            return Ok(workers);
        }

        /// <summary>
        /// GET: api/workers/{id} - READ operation
        /// Returns a single worker by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<WorkerDto>> GetWorker(int id)
        {
            var worker = await _context.Workers.FindAsync(id);

            if (worker == null)
            {
                return NotFound(new { message = "Worker not found" });
            }

            return Ok(new WorkerDto
            {
                Id = worker.ID,
                Ime = worker.Ime,
                Priimek = worker.Priimek,
                DelovnoMesto = worker.DelovnoMesto,
                VrstaZaposlitve = worker.VrstaZaposlitve,
                CreatedAt = worker.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                IsActive = worker.IsActive
            });
        }

        /// <summary>
        /// POST: api/workers - CREATE operation
        /// Creates a new worker
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<WorkerDto>> CreateWorker([FromBody] WorkerCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Ime) || string.IsNullOrWhiteSpace(dto.Priimek))
            {
                return BadRequest(new { message = "Ime and Priimek are required" });
            }

            var worker = new Worker
            {
                Ime = dto.Ime,
                Priimek = dto.Priimek,
                DelovnoMesto = dto.DelovnoMesto ?? "",
                VrstaZaposlitve = dto.VrstaZaposlitve ?? "",
                CreatedAt = DateTime.Now,
                IsActive = dto.IsActive ?? true
            };

            _context.Workers.Add(worker);
            await _context.SaveChangesAsync();

            var result = new WorkerDto
            {
                Id = worker.ID,
                Ime = worker.Ime,
                Priimek = worker.Priimek,
                DelovnoMesto = worker.DelovnoMesto,
                VrstaZaposlitve = worker.VrstaZaposlitve,
                CreatedAt = worker.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                IsActive = worker.IsActive
            };

            return CreatedAtAction(nameof(GetWorker), new { id = worker.ID }, result);
        }

        /// <summary>
        /// PUT: api/workers/{id} - UPDATE operation
        /// Updates an existing worker
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<WorkerDto>> UpdateWorker(int id, [FromBody] WorkerCreateDto dto)
        {
            var worker = await _context.Workers.FindAsync(id);
            if (worker == null)
            {
                return NotFound(new { message = "Worker not found" });
            }

            if (!string.IsNullOrWhiteSpace(dto.Ime))
                worker.Ime = dto.Ime;
            if (!string.IsNullOrWhiteSpace(dto.Priimek))
                worker.Priimek = dto.Priimek;
            if (!string.IsNullOrWhiteSpace(dto.DelovnoMesto))
                worker.DelovnoMesto = dto.DelovnoMesto;
            if (!string.IsNullOrWhiteSpace(dto.VrstaZaposlitve))
                worker.VrstaZaposlitve = dto.VrstaZaposlitve;
            if (dto.IsActive.HasValue)
                worker.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();

            return Ok(new WorkerDto
            {
                Id = worker.ID,
                Ime = worker.Ime,
                Priimek = worker.Priimek,
                DelovnoMesto = worker.DelovnoMesto,
                VrstaZaposlitve = worker.VrstaZaposlitve,
                CreatedAt = worker.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                IsActive = worker.IsActive
            });
        }

        /// <summary>
        /// DELETE: api/workers/{id} - DELETE operation
        /// Deletes a worker
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteWorker(int id)
        {
            var worker = await _context.Workers.FindAsync(id);
            if (worker == null)
            {
                return NotFound(new { message = "Worker not found" });
            }

            _context.Workers.Remove(worker);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    /// <summary>
    /// DTO for returning worker data
    /// </summary>
    public class WorkerDto
    {
        public int Id { get; set; }
        public string Ime { get; set; } = "";
        public string Priimek { get; set; } = "";
        public string DelovnoMesto { get; set; } = "";
        public string VrstaZaposlitve { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// DTO for creating/updating workers
    /// </summary>
    public class WorkerCreateDto
    {
        public string Ime { get; set; } = "";
        public string Priimek { get; set; } = "";
        public string? DelovnoMesto { get; set; }
        public string? VrstaZaposlitve { get; set; }
        public bool? IsActive { get; set; }
    }
}
