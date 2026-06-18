using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;

namespace LDHub.Controllers;


public class ModuleController : Controller
{
    private readonly AppDbContext _db;

    public ModuleController(AppDbContext db)
    {
        _db = db;
    }

 

    // ── Auth helpers ──────────────────────────────────────────────
    // Check if user is CourseManager or ResourcePerson
    private bool IsCourseManager() =>
        HttpContext.Session.GetString("UserRole") == "CourseManager";

    private bool IsResourcePerson() =>
        HttpContext.Session.GetString("UserRole") == "ResourcePerson";

    private bool HasAccess() => IsCourseManager() || IsResourcePerson();

    private int GetUserId() =>
        int.Parse(HttpContext.Session.GetString("UserId") ?? "0");

    private void SetViewBag()
    {
        ViewBag.ActiveRole = HttpContext.Session.GetString("UserRole");
        ViewBag.UserName   = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto  = HttpContext.Session.GetString("UserPhoto");
    }

    // ── GET: /Module/Index ────────────────────────────────────────
    // Shows list of trainings
    // CourseManager → sees ALL trainings
    // ResourcePerson → sees only assigned trainings
  public async Task<IActionResult> Index()
{
    if (!HasAccess()) return RedirectToAction("Login", "Home");
    SetViewBag();

    var userId = GetUserId();
    List<Training> trainings;

    if (IsCourseManager())
    {
        trainings = await _db.Trainings
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }
    else
    {
        trainings = await _db.TrainingInstructors
            .Include(ti => ti.Training)
            .Where(ti => ti.UserId == userId)
            .Select(ti => ti.Training!)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    // DEBUG — remove after fixing
    System.Console.WriteLine($"Role: {HttpContext.Session.GetString("UserRole")}");
    System.Console.WriteLine($"UserId: {userId}");
    System.Console.WriteLine($"Trainings count: {trainings.Count}");

    return View("~/Views/Module/Index.cshtml", trainings);
}

    // ── GET: /Module/Modules/5 ────────────────────────────────────
    // Shows list of modules for a specific training
    public async Task<IActionResult> Modules(int id)
    {
        if (!HasAccess()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var training = await _db.Trainings.FindAsync(id);
        if (training == null) return NotFound();

        // Resource person can only access assigned trainings
        if (IsResourcePerson())
        {
            var userId   = GetUserId();
            var assigned = await _db.TrainingInstructors
                .AnyAsync(ti => ti.TrainingId == id && ti.UserId == userId);
            if (!assigned) return RedirectToAction("Index");
        }

        var modules = await _db.TrainingModules
            .Where(m => m.TrainingId == id)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        ViewBag.Training = training;
        return View("~/Views/CourseManager/Modules.cshtml", modules);
    }

    // ── POST: /Module/Create ──────────────────────────────────────
    // Create new module — called from modal
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TrainingModule model, IFormFile? file)
    {
        if (!HasAccess()) return RedirectToAction("Login", "Home");

        model.UploadedBy = GetUserId();
        model.UploadedAt = DateTime.UtcNow;
        model.UpdatedAt  = DateTime.UtcNow;

        // Handle file upload for Lecture type
        if (model.ModuleType == "Lecture" && 
            model.LectureSource == "file" && 
            file != null && file.Length > 0)
        {
            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "uploads", "modules");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            model.FilePath = $"/uploads/modules/{fileName}";
        }

        _db.TrainingModules.Add(model);
        await _db.SaveChangesAsync();

        // If Quiz type — create empty quiz
        if (model.ModuleType == "Quiz")
        {
            var quiz = new Quiz
            {
                ModuleId     = model.ModuleId,
                QuizTitle    = model.ModuleName,
                PassingScore = 70,
                CreatedAt    = DateTime.UtcNow
            };
            _db.Quizzes.Add(quiz);
            await _db.SaveChangesAsync();
        }

        // If Text type — create empty text content
        if (model.ModuleType == "Text")
        {
            var textContent = new ModuleTextContent
            {
                ModuleId  = model.ModuleId,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ModuleTextContents.Add(textContent);
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "Module added successfully!";
        return RedirectToAction(
            "Modules",
            "CourseManager",
            new { trainingId = model.TrainingId }
        );
    }

    // ── POST: /Module/Edit ────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TrainingModule model, IFormFile? file)
    {
        if (!HasAccess()) return RedirectToAction("Login", "Home");

        var existing = await _db.TrainingModules.FindAsync(model.ModuleId);
        if (existing == null) return NotFound();

        existing.ModuleName        = model.ModuleName;
        existing.ModuleDescription = model.ModuleDescription;
        existing.SortOrder         = model.SortOrder;
        existing.LinkUrl           = model.LinkUrl;
        existing.UpdatedAt         = DateTime.UtcNow;

        // Handle file upload
        if (file != null && file.Length > 0)
        {
            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "uploads", "modules");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            existing.FilePath = $"/uploads/modules/{fileName}";
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Module updated!";
                return RedirectToAction(
            "Modules",
            "CourseManager",
            new { trainingId = existing.TrainingId }
        );
    }

    // ── POST: /Module/Delete ──────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int moduleId, int trainingId)
    {
        if (!HasAccess()) return RedirectToAction("Login", "Home");

        var module = await _db.TrainingModules.FindAsync(moduleId);
        if (module != null)
        {
            _db.TrainingModules.Remove(module);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Module deleted!";
        }

        return RedirectToAction("Modules", "CourseManager", new { trainingId = trainingId });
    }

    // ── POST: /Module/SaveQuiz ────────────────────────────────────
    // Save quiz questions and choices
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQuiz(int quizId, int trainingId,
        List<string> questionTexts,
        List<List<string>> choices,
        List<int> correctChoices)
    {
        if (!HasAccess()) return RedirectToAction("Login", "Home");

        var quiz = await _db.Quizzes
            .Include(q => q.Questions)
                .ThenInclude(q => q.Choices)
            .FirstOrDefaultAsync(q => q.QuizId == quizId);

        if (quiz == null) return NotFound();

        // Remove existing questions
        _db.QuizQuestions.RemoveRange(quiz.Questions);
        await _db.SaveChangesAsync();

        // Add new questions
        for (int i = 0; i < questionTexts.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(questionTexts[i])) continue;

            var question = new QuizQuestion
            {
                QuizId       = quizId,
                QuestionText = questionTexts[i]
            };
            _db.QuizQuestions.Add(question);
            await _db.SaveChangesAsync();

            // Add choices
            if (choices.Count > i)
            {
                for (int j = 0; j < choices[i].Count; j++)
                {
                    if (string.IsNullOrWhiteSpace(choices[i][j])) continue;
                    _db.QuizChoices.Add(new QuizChoice
                    {
                        QuestionId = question.QuestionId,
                        ChoiceText = choices[i][j],
                        IsCorrect  = correctChoices.Count > i && correctChoices[i] == j
                    });
                }
                await _db.SaveChangesAsync();
            }
        }

        TempData["Success"] = "Quiz saved!";
        return RedirectToAction("Modules", "CourseManager", new { trainingId = trainingId });
    }

    // ── POST: /Module/SaveText ────────────────────────────────────
    // Save text module content and topics
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveText(int moduleId, int trainingId,
        string? introduction, string? referencesText,
        List<string> topicTitles, List<string> topicBodies)
    {
        if (!HasAccess()) return RedirectToAction("Login", "Home");

        var content = await _db.ModuleTextContents
            .Include(c => c.Topics)
            .FirstOrDefaultAsync(c => c.ModuleId == moduleId);

        if (content == null)
        {
            content = new ModuleTextContent { ModuleId = moduleId };
            _db.ModuleTextContents.Add(content);
            await _db.SaveChangesAsync();
        }

        content.Introduction    = introduction;
        content.ReferencesText  = referencesText;
        content.UpdatedAt       = DateTime.UtcNow;

        // Remove existing topics
        _db.ModuleTextTopics.RemoveRange(content.Topics);
        await _db.SaveChangesAsync();

        // Add new topics
        for (int i = 0; i < topicTitles.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(topicTitles[i])) continue;
            _db.ModuleTextTopics.Add(new ModuleTextTopic
            {
                ContentId  = content.ContentId,
                TopicTitle = topicTitles[i],
                TopicBody  = topicBodies.Count > i ? topicBodies[i] : null,
                SortOrder  = i
            });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Text content saved!";
        return RedirectToAction("Modules", "CourseManager", new { trainingId = trainingId });
    }

    // ── AJAX: GET: /Module/GetQuiz/5 ─────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetQuiz(int moduleId)
    {
        var quiz = await _db.Quizzes
            .Include(q => q.Questions)
                .ThenInclude(q => q.Choices)
            .FirstOrDefaultAsync(q => q.ModuleId == moduleId);

        if (quiz == null) return Json(null);

        return Json(new
        {
            quizId       = quiz.QuizId,
            quizTitle    = quiz.QuizTitle,
            passingScore = quiz.PassingScore,
            questions    = quiz.Questions.Select(q => new
            {
                questionId   = q.QuestionId,
                questionText = q.QuestionText,
                choices      = q.Choices.Select(c => new
                {
                    choiceId   = c.ChoiceId,
                    choiceText = c.ChoiceText,
                    isCorrect  = c.IsCorrect
                })
            })
        });
    }

    // ── AJAX: GET: /Module/GetTextContent/5 ──────────────────────
    [HttpGet]
    public async Task<IActionResult> GetTextContent(int moduleId)
    {
        var content = await _db.ModuleTextContents
            .Include(c => c.Topics)
            .FirstOrDefaultAsync(c => c.ModuleId == moduleId);

        if (content == null) return Json(null);

        return Json(new
        {
            contentId      = content.ContentId,
            introduction   = content.Introduction,
            referencesText = content.ReferencesText,
            topics         = content.Topics.OrderBy(t => t.SortOrder).Select(t => new
            {
                topicId    = t.TopicId,
                topicTitle = t.TopicTitle,
                topicBody  = t.TopicBody
            })
        });
    }
}