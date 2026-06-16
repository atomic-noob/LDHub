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

        // ── Auth helpers ──────────────────────────────────────────
        private bool CanAccessTraining()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return role == "Admin" || role == "CourseManager" || role == "Raam";
        }

        private IActionResult RedirectAfterSave()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role == "CourseManager")
                return RedirectToAction("Trainings", "CourseManager");
            if (role == "Raam")
                return RedirectToAction("Trainings", "Raam");
            return RedirectToAction(nameof(Index));
        }

        private void SetProficiencyAndRegions()
        {
            ViewBag.Regions = _context.Regions.ToList();
            ViewBag.Proficiencies = new List<SelectListItem>
            {
                new SelectListItem { Value = "1", Text = "Basic" },
                new SelectListItem { Value = "2", Text = "Intermediate" },
                new SelectListItem { Value = "3", Text = "Advanced" },
                new SelectListItem { Value = "4", Text = "Superior" }
            };
        }

        // GET: Training
        public async Task<IActionResult> Index()
        {
            if (!CanAccessTraining()) return RedirectToAction("Index", "Admin");
            var trainings = await _context.Trainings.ToListAsync();
            return View(trainings);
        }

        // GET: Training/Create
        public IActionResult Create()
        {
            if (!CanAccessTraining()) return RedirectToAction("Index", "Admin");
            SetProficiencyAndRegions();
            return View();
        }

        // POST: Training/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Training training)
        {
            ModelState.Remove("TrainingCode");

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => new { x.Key, x.Value.Errors })
                    .ToList();

                foreach (var error in errors)
                {
                    Console.WriteLine($"Field: {error.Key}");
                    foreach (var e in error.Errors)
                        Console.WriteLine($"Error: {e.ErrorMessage}");
                }

                SetProficiencyAndRegions();
                return View(training);
            }

            var region = await _context.Regions
                .FirstOrDefaultAsync(r => r.RegionId == training.RegionId);

            string regionCode = region?.RegionCode ?? "DENR";

            int count = await _context.Trainings
                .Where(t => t.RegionId == training.RegionId
                    && t.CreatedAt.Year == DateTime.Now.Year)
                .CountAsync();

            training.TrainingCode = $"{regionCode}-{DateTime.Now.Year}-{(count + 1):D4}";
            training.CreatedAt = DateTime.UtcNow;
            training.UpdatedAt = DateTime.UtcNow;
            training.CreatedBy = HttpContext.Session.GetInt32("UserId") ?? 0;

            _context.Add(training);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Training created successfully!";
            return RedirectAfterSave();
        }

        // GET: Training/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (!CanAccessTraining()) return RedirectToAction("Index", "Admin");
            if (id == null) return NotFound();

            var training = await _context.Trainings.FindAsync(id);
            if (training == null) return NotFound();

            SetProficiencyAndRegions();
            return View(training);
        }

        // POST: Training/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Training training)
        {
            if (id != training.TrainingId) return NotFound();

            ModelState.Remove("TrainingCode");

            if (!ModelState.IsValid)
            {
                SetProficiencyAndRegions();
                return View(training);
            }

            training.UpdatedAt = DateTime.UtcNow;
            _context.Update(training);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Training updated successfully!";
            return RedirectAfterSave();
        }

        // GET: Training/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (!CanAccessTraining()) return RedirectToAction("Index", "Admin");
            if (id == null) return NotFound();

            var training = await _context.Trainings
                .Include(t => t.TrainingParticipants)
                    .ThenInclude(tp => tp.User)
                .Include(t => t.TrainingInstructors)
                    .ThenInclude(ti => ti.User)
                .FirstOrDefaultAsync(t => t.TrainingId == id);

            if (training == null) return NotFound();

            return View(training);
        }

        // GET: Training/AssignParticipant/5
        public async Task<IActionResult> AssignParticipant(int? id)
        {
            if (!CanAccessTraining()) return RedirectToAction("Index", "Admin");
            if (id == null) return NotFound();

            var training = await _context.Trainings
                .FirstOrDefaultAsync(t => t.TrainingId == id);
            if (training == null) return NotFound();

            var assignedIds = await _context.TrainingParticipants
                .Where(tp => tp.TrainingId == id)
                .Select(tp => tp.UserId)
                .ToListAsync();

            var availableUsers = await _context.Users
                .Where(u => u.Status == "Active" && !assignedIds.Contains(u.UserId))
                .ToListAsync();

            var assignedParticipants = await _context.TrainingParticipants
                .Where(tp => tp.TrainingId == id)
                .Include(tp => tp.User)
                .ToListAsync();

            ViewBag.Training = training;
            ViewBag.AvailableUsers = availableUsers;
            ViewBag.AssignedParticipants = assignedParticipants;

            return View();
        }

        // POST: Training/AssignParticipant
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignParticipant(int trainingId, List<int> userIds)
        {
            if (userIds == null || userIds.Count == 0)
            {
                TempData["Error"] = "Please select at least one participant.";
                return RedirectToAction("AssignParticipant", new { id = trainingId });
            }

            foreach (var userId in userIds)
            {
                var exists = await _context.TrainingParticipants
                    .AnyAsync(tp => tp.TrainingId == trainingId && tp.UserId == userId);

                if (!exists)
                {
                    _context.TrainingParticipants.Add(new TrainingParticipant
                    {
                        TrainingId = trainingId,
                        UserId = userId,
                        EnrollmentDate = DateTime.UtcNow,
                        CompletionStatus = "Enrolled"
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Participants assigned successfully!";
            return RedirectToAction("AssignParticipant", new { id = trainingId });
        }

        // GET: Training/AssignResourcePerson/5
        public async Task<IActionResult> AssignResourcePerson(int? id)
        {
            if (!CanAccessTraining()) return RedirectToAction("Index", "Admin");
            if (id == null) return NotFound();

            var training = await _context.Trainings
                .FirstOrDefaultAsync(t => t.TrainingId == id);
            if (training == null) return NotFound();

            var assignedIds = await _context.TrainingInstructors
                .Where(ti => ti.TrainingId == id)
                .Select(ti => ti.UserId)
                .ToListAsync();

            var availableUsers = await _context.Users
                .Where(u => u.Status == "Active" && !assignedIds.Contains(u.UserId))
                .ToListAsync();

            var assignedInstructors = await _context.TrainingInstructors
                .Where(ti => ti.TrainingId == id)
                .Include(ti => ti.User)
                .ToListAsync();

            ViewBag.Training = training;
            ViewBag.AvailableUsers = availableUsers;
            ViewBag.AssignedInstructors = assignedInstructors;

            return View();
        }

        // POST: Training/AssignResourcePerson
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignResourcePerson(int trainingId, List<int> userIds)
        {
            if (userIds == null || userIds.Count == 0)
            {
                TempData["Error"] = "Please select at least one resource person.";
                return RedirectToAction("AssignResourcePerson", new { id = trainingId });
            }

            foreach (var userId in userIds)
            {
                var exists = await _context.TrainingInstructors
                    .AnyAsync(ti => ti.TrainingId == trainingId && ti.UserId == userId);

                if (!exists)
                {
                    _context.TrainingInstructors.Add(new TrainingInstructor
                    {
                        TrainingId = trainingId,
                        UserId = userId,
                        AssignedDate = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Resource persons assigned successfully!";
            return RedirectToAction("AssignResourcePerson", new { id = trainingId });
        }
    }
}