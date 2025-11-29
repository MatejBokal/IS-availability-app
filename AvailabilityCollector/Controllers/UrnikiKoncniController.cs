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
    public class UrnikiKoncniController : Controller
    {
        private readonly AppContextDb _context;

        public UrnikiKoncniController(AppContextDb context)
        {
            _context = context;
        }

        // GET: UrnikiKoncni
        public async Task<IActionResult> Index()
        {
            return View(await _context.UrnikiKoncni.ToListAsync());
        }

        // GET: UrnikiKoncni/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var urnikKoncni = await _context.UrnikiKoncni
                .FirstOrDefaultAsync(m => m.ID == id);
            if (urnikKoncni == null)
            {
                return NotFound();
            }

            return View(urnikKoncni);
        }

        // GET: UrnikiKoncni/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: UrnikiKoncni/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,UrnikJSON,MesecLeto,Type,ZaporedniTeden")] UrnikKoncni urnikKoncni)
        {
            if (ModelState.IsValid)
            {
                _context.Add(urnikKoncni);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(urnikKoncni);
        }

        // GET: UrnikiKoncni/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var urnikKoncni = await _context.UrnikiKoncni.FindAsync(id);
            if (urnikKoncni == null)
            {
                return NotFound();
            }
            return View(urnikKoncni);
        }

        // POST: UrnikiKoncni/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,UrnikJSON,MesecLeto,Type,ZaporedniTeden")] UrnikKoncni urnikKoncni)
        {
            if (id != urnikKoncni.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(urnikKoncni);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UrnikKoncniExists(urnikKoncni.ID))
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
            return View(urnikKoncni);
        }

        // GET: UrnikiKoncni/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var urnikKoncni = await _context.UrnikiKoncni
                .FirstOrDefaultAsync(m => m.ID == id);
            if (urnikKoncni == null)
            {
                return NotFound();
            }

            return View(urnikKoncni);
        }

        // POST: UrnikiKoncni/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var urnikKoncni = await _context.UrnikiKoncni.FindAsync(id);
            if (urnikKoncni != null)
            {
                _context.UrnikiKoncni.Remove(urnikKoncni);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UrnikKoncniExists(int id)
        {
            return _context.UrnikiKoncni.Any(e => e.ID == id);
        }
    }
}
