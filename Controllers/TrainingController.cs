using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Data;
using LDHub.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace LDHub.Controllers
{
    public class TrainingController : Controller
    {
        private readonly AppDbContext _context;

        public TrainingController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Training
        public async Task<IActionResult> Index()
        {
            var trainings = await _context.Trainings.ToListAsync();
            return View(trainings);
        }

        // GET: Training/Create
        // GET: Training/Create
        public IActionResult Create()
{
    ViewBag.Regions = _context.Regions.ToList();
    ViewBag.Proficiencies = new List<SelectListItem>
    {
        new SelectListItem { Value = "1", Text = "Basic" },
        new SelectListItem { Value = "2", Text = "Intermediate" },
        new SelectListItem { Value = "3", Text = "Advanced" },
        new SelectListItem { Value = "4", Text = "Superior" }
    };
    return View();
}

        // POST: Training/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
               public async Task<IActionResult> Create(Training training)
        {
            if (ModelState.IsValid)
            {
                // Get region code
                var region = await _context.Regions
                    .FirstOrDefaultAsync(r => r.RegionId == training.RegionId);

                string regionCode = region?.RegionCode ?? "DENR";

                // Count existing trainings for this region this year
                int count = await _context.Trainings
                    .Where(t => t.RegionId == training.RegionId
                        && t.CreatedAt.Year == DateTime.Now.Year)
                    .CountAsync();

                // Auto-generate training code
                training.TrainingCode = $"{regionCode}-{DateTime.Now.Year}-{(count + 1):D4}";
                training.CreatedAt = DateTime.UtcNow;
                training.UpdatedAt = DateTime.UtcNow;

                _context.Add(training);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(training);
        }

        // GET: Training/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var training = await _context.Trainings.FindAsync(id);
            if (training == null) return NotFound();

            return View(training);
        }

        // POST: Training/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Training training)
        {
            if (id != training.TrainingId) return NotFound();

            if (ModelState.IsValid)
            {
                training.UpdatedAt = DateTime.UtcNow;
                _context.Update(training);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(training);
        }

        // GET: Training/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var training = await _context.Trainings
                .FirstOrDefaultAsync(m => m.TrainingId == id);
            if (training == null) return NotFound();

            return View(training);
        }

        // POST: Training/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var training = await _context.Trainings.FindAsync(id);
            if (training != null)
            {
                _context.Trainings.Remove(training);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}