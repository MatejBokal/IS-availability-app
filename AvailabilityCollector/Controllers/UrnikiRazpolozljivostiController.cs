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
    public class UrnikiRazpolozljivostiController : Controller
    {
        private readonly AppContextDb _context;

        public UrnikiRazpolozljivostiController(AppContextDb context)
        {
            _context = context;
        }

        // GET: UrnikiRazpolozljivosti
        public async Task<IActionResult> Index()
        {
            return View(await _context.UrnikiRazpolozljivosti.ToListAsync());
        }

        // GET: UrnikiRazpolozljivosti/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var urnikRazpolozljivost = await _context.UrnikiRazpolozljivosti
                .FirstOrDefaultAsync(m => m.ID == id);
            if (urnikRazpolozljivost == null)
            {
                return NotFound();
            }

            return View(urnikRazpolozljivost);
        }

        // GET: UrnikiRazpolozljivosti/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: UrnikiRazpolozljivosti/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,UrnikJSON,MesecLeto,Type,ZaporedniTeden")] UrnikRazpolozljivost urnikRazpolozljivost)
        {
            if (ModelState.IsValid)
            {
                _context.Add(urnikRazpolozljivost);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(urnikRazpolozljivost);
        }

        // GET: UrnikiRazpolozljivosti/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var urnikRazpolozljivost = await _context.UrnikiRazpolozljivosti.FindAsync(id);
            if (urnikRazpolozljivost == null)
            {
                return NotFound();
            }
            return View(urnikRazpolozljivost);
        }

        // POST: UrnikiRazpolozljivosti/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,UrnikJSON,MesecLeto,Type,ZaporedniTeden")] UrnikRazpolozljivost urnikRazpolozljivost)
        {
            if (id != urnikRazpolozljivost.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(urnikRazpolozljivost);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UrnikRazpolozljivostExists(urnikRazpolozljivost.ID))
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
            return View(urnikRazpolozljivost);
        }

        // GET: UrnikiRazpolozljivosti/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var urnikRazpolozljivost = await _context.UrnikiRazpolozljivosti
                .FirstOrDefaultAsync(m => m.ID == id);
            if (urnikRazpolozljivost == null)
            {
                return NotFound();
            }

            return View(urnikRazpolozljivost);
        }

        // POST: UrnikiRazpolozljivosti/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var urnikRazpolozljivost = await _context.UrnikiRazpolozljivosti.FindAsync(id);
            if (urnikRazpolozljivost != null)
            {
                _context.UrnikiRazpolozljivosti.Remove(urnikRazpolozljivost);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UrnikRazpolozljivostExists(int id)
        {
            return _context.UrnikiRazpolozljivosti.Any(e => e.ID == id);
        }
    }
}
