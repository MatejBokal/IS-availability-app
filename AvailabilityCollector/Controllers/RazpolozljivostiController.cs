using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Controllers
{
    public class RazpolozljivostiController : Controller
    {
        private readonly AppContextDb _context;

        public RazpolozljivostiController(AppContextDb context)
        {
            _context = context;
        }

        // GET: Razpolozljivosti
        public async Task<IActionResult> Index()
        {
            var appContextDb = _context.Razpolozljivosti.Include(r => r.Worker);
            return View(await appContextDb.ToListAsync());
        }

        // GET: Razpolozljivosti/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var razpolozljivost = await _context.Razpolozljivosti
                .Include(r => r.Worker)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (razpolozljivost == null)
            {
                return NotFound();
            }

            return View(razpolozljivost);
        }

        // GET: Razpolozljivosti/Create
        public IActionResult Create()
        {
            ViewData["WorkerID"] = new SelectList(_context.Workers, "ID", "ID");
            return View();
        }

        // POST: Razpolozljivosti/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,RazpolozljivostJSON,MesecLeto,Type,ZaporedniTeden,WorkerID")] Razpolozljivost razpolozljivost)
        {
            if (ModelState.IsValid)
            {
                _context.Add(razpolozljivost);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["WorkerID"] = new SelectList(_context.Workers, "ID", "ID", razpolozljivost.WorkerID);
            return View(razpolozljivost);
        }

        // GET: Razpolozljivosti/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var razpolozljivost = await _context.Razpolozljivosti.FindAsync(id);
            if (razpolozljivost == null)
            {
                return NotFound();
            }
            ViewData["WorkerID"] = new SelectList(_context.Workers, "ID", "ID", razpolozljivost.WorkerID);
            return View(razpolozljivost);
        }

        // POST: Razpolozljivosti/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,RazpolozljivostJSON,MesecLeto,Type,ZaporedniTeden,WorkerID")] Razpolozljivost razpolozljivost)
        {
            if (id != razpolozljivost.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(razpolozljivost);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RazpolozljivostExists(razpolozljivost.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["WorkerID"] = new SelectList(_context.Workers, "ID", "ID", razpolozljivost.WorkerID);
            return View(razpolozljivost);
        }

        // GET: Razpolozljivosti/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var razpolozljivost = await _context.Razpolozljivosti
                .Include(r => r.Worker)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (razpolozljivost == null)
            {
                return NotFound();
            }

            return View(razpolozljivost);
        }

        // POST: Razpolozljivosti/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var razpolozljivost = await _context.Razpolozljivosti.FindAsync(id);
            if (razpolozljivost != null)
            {
                _context.Razpolozljivosti.Remove(razpolozljivost);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RazpolozljivostExists(int id)
        {
            return _context.Razpolozljivosti.Any(e => e.ID == id);
        }
    }
}
