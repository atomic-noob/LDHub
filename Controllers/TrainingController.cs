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
                // DEBUG - see what's failing
                if (!ModelState.IsValid)
                {
                    ModelState.Remove("TrainingCode");

                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .Select(x => new { x.Key, x.Value.Errors })
                        .ToList();
                    
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"Field: {error.Key}");
                        foreach (var e in error.Errors)
                        {
                            Console.WriteLine($"Error: {e.ErrorMessage}");
                        }
                    }
                }

                if (ModelState.IsValid)
                {
                    
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

                Console.WriteLine($"Saving training: {training.TrainingTitle} - {training.TrainingCode}");

                _context.Add(training);
                await _context.SaveChangesAsync();

                Console.WriteLine("Saved successfully!");

                return RedirectToAction(nameof(Index));
                }

                ViewBag.Regions = _context.Regions.ToList();
                ViewBag.Proficiencies = new List<SelectListItem>
                {
                    new SelectListItem { Value = "1", Text = "Basic" },
                    new SelectListItem { Value = "2", Text = "Intermediate" },
                    new SelectListItem { Value = "3", Text = "Advanced" },
                    new SelectListItem { Value = "4", Text = "Superior" }
                };
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
        // GET: Training/Details/5
public async Task<IActionResult> Details(int? id)
{
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
    if (id == null) return NotFound();

    var training = await _context.Trainings
        .FirstOrDefaultAsync(t => t.TrainingId == id);

    if (training == null) return NotFound();

    // Get already assigned participant IDs
    var assignedIds = await _context.TrainingParticipants
        .Where(tp => tp.TrainingId == id)
        .Select(tp => tp.UserId)
        .ToListAsync();

    // Get employees not yet assigned
    var employees = await _context.Users
        .Include(u => u.UserRoles)
        .Where(u => u.UserRoles.Any(r => r.Role == "Employee")
                 && !assignedIds.Contains(u.UserId))
        .OrderBy(u => u.LastName)
        .ToListAsync();

    ViewBag.Training = training;
    ViewBag.Employees = employees;
    return View();
}

// POST: Training/AssignParticipant
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AssignParticipant(int trainingId, List<int> userIds)
{
    if (userIds == null || userIds.Count == 0)
    {
        TempData["Error"] = "Please select at least one employee!";
        return RedirectToAction("AssignParticipant", new { id = trainingId });
    }

    foreach (var userId in userIds)
    {
        // Skip if already assigned
        var exists = await _context.TrainingParticipants
            .AnyAsync(tp => tp.TrainingId == trainingId && tp.UserId == userId);
        if (exists) continue;

        _context.TrainingParticipants.Add(new TrainingParticipant
        {
            TrainingId      = trainingId,
            UserId          = userId,
            EnrollmentDate  = DateTime.UtcNow,
            CompletionStatus = "Enrolled"
        });
    }

    await _context.SaveChangesAsync();
    TempData["Success"] = "Participants assigned successfully!";
    return RedirectToAction("Details", new { id = trainingId });
}

// POST: Training/RemoveParticipant
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RemoveParticipant(int trainingId, int userId)
{
    var participant = await _context.TrainingParticipants
        .FirstOrDefaultAsync(tp => tp.TrainingId == trainingId 
                                && tp.UserId == userId);
    if (participant != null)
    {
        _context.TrainingParticipants.Remove(participant);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Participant removed!";
    }
    return RedirectToAction("Details", new { id = trainingId });
}

// GET: Training/AssignResourcePerson/5
public async Task<IActionResult> AssignResourcePerson(int? id)
{
    if (id == null) return NotFound();

    var training = await _context.Trainings
        .FirstOrDefaultAsync(t => t.TrainingId == id);

    if (training == null) return NotFound();

    // Get already assigned resource person IDs
    var assignedIds = await _context.TrainingInstructors
        .Where(ti => ti.TrainingId == id)
        .Select(ti => ti.UserId)
        .ToListAsync();

    // Get resource persons not yet assigned
    var resourcePersons = await _context.Users
        .Include(u => u.UserRoles)
        .Where(u => u.UserRoles.Any(r => r.Role == "ResourcePerson")
                 && !assignedIds.Contains(u.UserId))
        .OrderBy(u => u.LastName)
        .ToListAsync();

    ViewBag.Training  = training;
    ViewBag.ResourcePersons = resourcePersons;
    return View();
}

// POST: Training/AssignResourcePerson
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AssignResourcePerson(int trainingId, List<int> userIds)
{
    if (userIds == null || userIds.Count == 0)
    {
        TempData["Error"] = "Please select at least one resource person!";
        return RedirectToAction("AssignResourcePerson", new { id = trainingId });
    }

    foreach (var userId in userIds)
    {
        var exists = await _context.TrainingInstructors
            .AnyAsync(ti => ti.TrainingId == trainingId && ti.UserId == userId);
        if (exists) continue;

        _context.TrainingInstructors.Add(new TrainingInstructor
        {
            TrainingId   = trainingId,
            UserId       = userId,
            AssignedDate = DateTime.UtcNow
        });
    }

    await _context.SaveChangesAsync();
    TempData["Success"] = "Resource persons assigned successfully!";
    return RedirectToAction("Details", new { id = trainingId });
}

// POST: Training/RemoveResourcePerson
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RemoveResourcePerson(int trainingId, int userId)
{
    var instructor = await _context.TrainingInstructors
        .FirstOrDefaultAsync(ti => ti.TrainingId == trainingId 
                                && ti.UserId == userId);
    if (instructor != null)
    {
        _context.TrainingInstructors.Remove(instructor);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Resource person removed!";
    }
    return RedirectToAction("Details", new { id = trainingId });
}
    }
}