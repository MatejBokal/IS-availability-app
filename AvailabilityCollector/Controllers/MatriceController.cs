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
    public class MatriceController : Controller
    {
        private readonly AppContextDb _context;

        public MatriceController(AppContextDb context)
        {
            _context = context;
        }

        // GET: Matrice
        public async Task<IActionResult> Index()
        {
            return View(await _context.Matrice.ToListAsync());
        }

        // GET: Matrice/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var matrica = await _context.Matrice
                .FirstOrDefaultAsync(m => m.ID == id);
            if (matrica == null)
            {
                return NotFound();
            }

            return View(matrica);
        }

        // GET: Matrice/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Matrice/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,MesecLeto,MatricaJSON")] Matrica matrica)
        {
            if (ModelState.IsValid)
            {
                _context.Add(matrica);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(matrica);
        }

        // GET: Matrice/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var matrica = await _context.Matrice.FindAsync(id);
            if (matrica == null)
            {
                return NotFound();
            }
            return View(matrica);
        }

        // POST: Matrice/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,MesecLeto,MatricaJSON")] Matrica matrica)
        {
            if (id != matrica.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(matrica);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MatricaExists(matrica.ID))
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
            return View(matrica);
        }

        // GET: Matrice/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var matrica = await _context.Matrice
                .FirstOrDefaultAsync(m => m.ID == id);
            if (matrica == null)
            {
                return NotFound();
            }

            return View(matrica);
        }

        // POST: Matrice/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var matrica = await _context.Matrice.FindAsync(id);
            if (matrica != null)
            {
                _context.Matrice.Remove(matrica);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MatricaExists(int id)
        {
            return _context.Matrice.Any(e => e.ID == id);
        }
    }
}
