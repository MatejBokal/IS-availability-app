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
    public class StatusiController : Controller
    {
        private readonly AppContextDb _context;

        public StatusiController(AppContextDb context)
        {
            _context = context;
        }

        // GET: Statusi
        public async Task<IActionResult> Index()
        {
            var appContextDb = _context.Statusi.Include(s => s.Worker);
            return View(await appContextDb.ToListAsync());
        }

        // GET: Statusi/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var status = await _context.Statusi
                .Include(s => s.Worker)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (status == null)
            {
                return NotFound();
            }

            return View(status);
        }

        // GET: Statusi/Create
        public IActionResult Create()
        {
            ViewData["WorkerID"] = new SelectList(_context.Workers, "ID", "ID");
            return View();
        }

        // POST: Statusi/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,WorkerID,StUr,StDelovnihDni")] Status status)
        {
            if (ModelState.IsValid)
            {
                _context.Add(status);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["WorkerID"] = new SelectList(_context.Workers, "ID", "ID", status.WorkerID);
            return View(status);
        }

        // GET: Statusi/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var status = await _context.Statusi.FindAsync(id);
            if (status == null)
            {
                return NotFound();
            }
            ViewData["WorkerID"] = new SelectList(_context.Workers, "ID", "ID", status.WorkerID);
            return View(status);
        }

        // POST: Statusi/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,WorkerID,StUr,StDelovnihDni")] Status status)
        {
            if (id != status.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(status);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StatusExists(status.ID))
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
            ViewData["WorkerID"] = new SelectList(_context.Workers, "ID", "ID", status.WorkerID);
            return View(status);
        }

        // GET: Statusi/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var status = await _context.Statusi
                .Include(s => s.Worker)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (status == null)
            {
                return NotFound();
            }

            return View(status);
        }

        // POST: Statusi/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var status = await _context.Statusi.FindAsync(id);
            if (status != null)
            {
                _context.Statusi.Remove(status);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool StatusExists(int id)
        {
            return _context.Statusi.Any(e => e.ID == id);
        }
    }
}
