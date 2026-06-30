using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;

namespace LDHub.Controllers;

public class ContentController : Controller
{
    private readonly AppDbContext _db;

    public ContentController(AppDbContext db)
    {
        _db = db;
    }

    // ── Auth check — shared by CourseManager, ResourcePerson, Raam ──
    private bool CanManageContent()
    {
        var role = HttpContext.Session.GetString("UserRole");
        return role == "CourseManager" || role == "ResourcePerson" || role == "Raam";
    }

    private int GetUserId()
    {
        var str = HttpContext.Session.GetString("UserId");
        return int.TryParse(str, out int id) ? id : 0;
    }

    private void SetViewBag()
    {
        ViewBag.ActiveRole = HttpContext.Session.GetString("UserRole");
        ViewBag.UserName   = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto  = HttpContext.Session.GetString("UserPhoto");
    }

    // ════════════════════════════════════════════════════════════
    //  MODULES
    // ════════════════════════════════════════════════════════════

    // ── GET: /Content/Modules ──────────────────────────────────
    public async Task<IActionResult> Modules(int? id, int? trainingId)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var selectedTrainingId = id ?? trainingId;

        if (selectedTrainingId == null)
        {
            var trainings = await _db.Trainings
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            ViewBag.Trainings  = trainings;
            ViewBag.TrainingId = null;
            return View("~/Views/Content/Modules.cshtml");
        }

        var training = await _db.Trainings
            .FirstOrDefaultAsync(t => t.TrainingId == selectedTrainingId);

        if (training == null) return NotFound();

        var modules = await _db.TrainingModules
            .Where(m => m.TrainingId == selectedTrainingId)
            .Include(m => m.Quiz)
            .Include(m => m.TextContent)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        var groupedModules = modules
            .GroupBy(m => m.ModuleName)
            .ToDictionary(g => g.Key, g => g.ToList());

        ViewBag.Training       = training;
        ViewBag.GroupedModules = groupedModules;
        ViewBag.TrainingId     = selectedTrainingId;
        return View("~/Views/Content/Modules.cshtml");
    }

    // ── POST: /Content/CreateModule ────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateModule(
        int trainingId, string moduleName, string moduleDescription,
        string moduleType, string? lectureSource, string? linkUrl,
        IFormFile? file)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");

        if (moduleType == "Activity" || moduleType == "Quiz")
        {
            var exists = await _db.TrainingModules
                .AnyAsync(m => m.TrainingId == trainingId
                    && m.ModuleName == moduleName
                    && (m.ModuleType == "Activity" || m.ModuleType == "Quiz"));
            if (exists)
            {
                TempData["Error"] = "Only 1 Activity or Quiz is allowed per module!";
                return RedirectToAction("Modules", new { trainingId });
            }
        }

        string? filePath = null;

        if (file != null && file.Length > 0)
        {
            if (file.Length > 41943040)
            {
                TempData["Error"] = "File size exceeds 40MB limit!";
                return RedirectToAction("Modules", new { trainingId });
            }

            var allowedExt = new[] {
                "pdf","doc","docx","ppt","pptx","xls","xlsx","csv",
                "mp4","avi","mov","wmv","flv","mkv","webm",
                "jpg","jpeg","png","gif","bmp","webp"
            };

            var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLower();
            if (!allowedExt.Contains(ext))
            {
                TempData["Error"] = $"File type '{ext}' is not allowed!";
                return RedirectToAction("Modules", new { trainingId });
            }

            var uploadDir = Path.Combine("wwwroot", "uploads", "modules");
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var fullPath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            filePath = $"/uploads/modules/{fileName}";
        }

        var module = new TrainingModule
        {
            TrainingId         = trainingId,
            ModuleName          = moduleName,
            ModuleDescription   = moduleDescription,
            ModuleType          = moduleType,
            LectureSource       = lectureSource,
            LinkUrl             = linkUrl,
            FilePath            = filePath,
            UploadedBy          = GetUserId(),
            UploadedAt          = DateTime.UtcNow,
            UpdatedAt           = DateTime.UtcNow,
            SortOrder           = await _db.TrainingModules
                                    .CountAsync(m => m.TrainingId == trainingId)
        };

        _db.TrainingModules.Add(module);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Module added successfully!";
        return RedirectToAction("Modules", new { trainingId });
    }

    // ── POST: /Content/EditModule ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditModule(
        int moduleId, int trainingId, string moduleDescription,
        string? linkUrl, IFormFile? file)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");

        var module = await _db.TrainingModules.FindAsync(moduleId);
        if (module == null) return NotFound();

        module.ModuleDescription = moduleDescription;
        module.LinkUrl           = linkUrl;
        module.UpdatedAt         = DateTime.UtcNow;

        if (file != null && file.Length > 0)
        {
            var uploadDir = Path.Combine("wwwroot", "uploads", "modules");
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var fullPath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            module.FilePath = $"/uploads/modules/{fileName}";
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = "Module updated successfully!";
        return RedirectToAction("Modules", new { trainingId });
    }

    // ── POST: /Content/DeleteModule ────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteModule(int moduleId, int trainingId)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");

        var module = await _db.TrainingModules.FindAsync(moduleId);
        if (module != null)
        {
            _db.TrainingModules.Remove(module);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Module deleted successfully!";
        }

        return RedirectToAction("Modules", new { trainingId });
    }

    // ════════════════════════════════════════════════════════════
    //  QUIZ BUILDER
    // ════════════════════════════════════════════════════════════

    // ── GET: /Content/QuizBuilder ───────────────────────────────
    public async Task<IActionResult> QuizBuilder(int moduleId)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var module = await _db.TrainingModules
            .Include(m => m.Training)
            .Include(m => m.Quiz)
                .ThenInclude(q => q.Questions)
                    .ThenInclude(q => q.Choices)
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module == null) return NotFound();

        return View("~/Views/Content/QuizBuilder.cshtml", module);
    }

    // ── POST: /Content/SaveQuizSettings ────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQuizSettings(
        int moduleId, int? quizId, string quizTitle, int passingScore)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");

        if (quizId.HasValue && quizId > 0)
        {
            var quiz = await _db.Quizzes.FindAsync(quizId);
            if (quiz != null)
            {
                quiz.QuizTitle    = quizTitle;
                quiz.PassingScore = passingScore;
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            _db.Quizzes.Add(new Quiz
            {
                ModuleId     = moduleId,
                QuizTitle    = quizTitle,
                PassingScore = passingScore,
                CreatedAt    = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "Quiz settings saved!";
        return RedirectToAction("QuizBuilder", new { moduleId });
    }

    // ── POST: /Content/AddQuestion ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuestion(
        int moduleId, int quizId, string questionText,
        List<string> choices, int correctChoice)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");

        var question = new QuizQuestion
        {
            QuizId       = quizId,
            QuestionText = questionText
        };

        _db.QuizQuestions.Add(question);
        await _db.SaveChangesAsync();

        for (int i = 0; i < choices.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(choices[i])) continue;
            _db.QuizChoices.Add(new QuizChoice
            {
                QuestionId = question.QuestionId,
                ChoiceText = choices[i],
                IsCorrect  = (i == correctChoice)
            });
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = "Question added!";
        return RedirectToAction("QuizBuilder", new { moduleId });
    }

    // ── POST: /Content/DeleteQuestion ──────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(int questionId, int moduleId)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");

        var question = await _db.QuizQuestions
            .Include(q => q.Choices)
            .FirstOrDefaultAsync(q => q.QuestionId == questionId);

        if (question != null)
        {
            _db.QuizChoices.RemoveRange(question.Choices);
            _db.QuizQuestions.Remove(question);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Question deleted!";
        }

        return RedirectToAction("QuizBuilder", new { moduleId });
    }

    // ════════════════════════════════════════════════════════════
    //  TEXT EDITOR
    // ════════════════════════════════════════════════════════════

    // ── GET: /Content/TextEditor ────────────────────────────────
    public async Task<IActionResult> TextEditor(int moduleId)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var module = await _db.TrainingModules
            .Include(m => m.Training)
            .Include(m => m.TextContent)
                .ThenInclude(tc => tc.Topics)
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

        if (module == null) return NotFound();

        return View("~/Views/Content/TextEditor.cshtml", module);
    }

    // ── POST: /Content/SaveTextContent ──────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTextContent(
        int moduleId, int? contentId, string? introduction,
        string? referencesText, List<string> topicTitles,
        List<string> topicBodies)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");

        ModuleTextContent? content;

        if (contentId.HasValue && contentId > 0)
        {
            content = await _db.ModuleTextContents
                .Include(c => c.Topics)
                .FirstOrDefaultAsync(c => c.ContentId == contentId);

            if (content != null)
            {
                content.Introduction   = introduction;
                content.ReferencesText = referencesText;
                content.UpdatedAt      = DateTime.UtcNow;
                _db.ModuleTextTopics.RemoveRange(content.Topics);
            }
            else
            {
                content = new ModuleTextContent
                {
                    ModuleId       = moduleId,
                    Introduction   = introduction,
                    ReferencesText = referencesText,
                    UpdatedAt      = DateTime.UtcNow
                };
                _db.ModuleTextContents.Add(content);
            }
        }
        else
        {
            content = new ModuleTextContent
            {
                ModuleId       = moduleId,
                Introduction   = introduction,
                ReferencesText = referencesText,
                UpdatedAt      = DateTime.UtcNow
            };
            _db.ModuleTextContents.Add(content);
        }

        await _db.SaveChangesAsync();

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
        return RedirectToAction("TextEditor", new { moduleId });
    }

    // ════════════════════════════════════════════════════════════
    //  TEST MAKER (Pre/Post Test Questions)
    // ════════════════════════════════════════════════════════════

    // ── GET: /Content/TestMaker ─────────────────────────────────
  
public async Task<IActionResult> TestMaker(int? trainingId)
{
    if (!CanManageContent()) return RedirectToAction("Login", "Home");
    SetViewBag();
 
    var trainings = await _db.Trainings.OrderBy(t => t.TrainingTitle).ToListAsync();
    ViewBag.Trainings = trainings;
 
    if (trainingId.HasValue && trainingId > 0)
    {
        var training = await _db.Trainings.FindAsync(trainingId);
        var questions = await _db.Questions
            .Where(q => q.TrainingId == trainingId)
            .OrderBy(q => q.Id)
            .ToListAsync();
 
        ViewBag.SelectedTraining = training;
        ViewBag.Questions        = questions;
        ViewBag.TrainingId       = trainingId;
 
        // ── Build Grades ──────────────────────────────────────────
        var totalPoints = questions.Sum(q => q.Points);
 
        // Get all participants assigned to this training
        var participants = await _db.TrainingParticipants
            .Include(tp => tp.User)
            .Where(tp => tp.TrainingId == trainingId)
            .ToListAsync();
 
        // Get all test attempts for this training
        var attempts = await _db.ParticipantTestAttempts
            .Where(a => a.TrainingId == trainingId)
            .ToListAsync();
 
        var gradeRows = participants.Select(tp =>
        {
            var userAttempts = attempts.Where(a => a.UserId == tp.UserId).ToList();
 
            var preTest = userAttempts
                .Where(a => a.TestType == "PreTest")
                .OrderByDescending(a => a.TakenAt)
                .FirstOrDefault();
 
            var postTests = userAttempts
                .Where(a => a.TestType == "PostTest")
                .OrderByDescending(a => a.Score)
                .ToList();
 
            var bestPost = postTests.FirstOrDefault();
 
            return new GradeRow
            {
                UserId           = tp.UserId,
                EmployeeName     = tp.User != null
                                    ? $"{tp.User.FirstName} {tp.User.LastName}"
                                    : "Unknown",
                PreTestScore     = preTest?.Score,
                PostTestScore    = bestPost?.Score,
                TotalPoints      = totalPoints,
                PostTestAttempts = postTests.Count,
                PostTestPassed   = bestPost?.IsPassed ?? false
            };
        }).ToList();
 
        ViewBag.Grades = gradeRows;
    }
 
    return View("~/Views/Content/TestMaker.cshtml");
}

    // ── POST: /Content/SaveQuestion ─────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQuestion(
        int? questionId, int trainingId, string questionType,
        string questionText, string? choiceA, string? choiceB,
        string? choiceC, string? choiceD, string? correctAnswer,
        string? correctAnswerTf, string? correctAnswerText, decimal points)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");

        string? finalAnswer = questionType switch
        {
            "true_false"      => correctAnswerTf,
            "multiple_choice" => correctAnswer,
            _                 => correctAnswerText
        };

        if (questionId.HasValue && questionId > 0)
        {
            var q = await _db.Questions.FindAsync(questionId);
            if (q != null)
            {
                q.QuestionType  = questionType;
                q.QuestionText  = questionText;
                q.ChoiceA       = choiceA;
                q.ChoiceB       = choiceB;
                q.ChoiceC       = choiceC;
                q.ChoiceD       = choiceD;
                q.CorrectAnswer = finalAnswer;
                q.Points        = points;
            }
        }
        else
        {
            _db.Questions.Add(new Question
            {
                TrainingId    = trainingId,
                QuestionType  = questionType,
                QuestionText  = questionText,
                ChoiceA       = choiceA,
                ChoiceB       = choiceB,
                ChoiceC       = choiceC,
                ChoiceD       = choiceD,
                CorrectAnswer = finalAnswer,
                Points        = points
            });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = questionId > 0 ? "Question updated!" : "Question added!";
        return RedirectToAction("TestMaker", new { trainingId, tab = "list" });
    }

    // ── POST: /Content/DeleteQuestion2 ──────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion2(int questionId, int trainingId)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");

        var q = await _db.Questions.FindAsync(questionId);
        if (q != null)
        {
            _db.Questions.Remove(q);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Question deleted!";
        }

        return RedirectToAction("TestMaker", new { trainingId, tab = "list" });
    }

    // ── POST: /Content/SaveTestSettings ─────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTestSettings(
        int trainingId, int minimumScore, int posttestTimer)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");

        var training = await _db.Trainings.FindAsync(trainingId);
        if (training != null)
        {
            training.MinimumScore  = minimumScore;
            training.PosttestTimer = posttestTimer;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Test settings saved!";
        }

        return RedirectToAction("TestMaker", new { trainingId, tab = "settings" });
    }

    // ════════════════════════════════════════════════════════════
    //  GRADES (built into TestMaker page as a tab, reused here)
    // ════════════════════════════════════════════════════════════

    // ── POST: /Content/ResetAttempts ────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAttempts(int userId, int trainingId)
    {
        if (!CanManageContent()) return RedirectToAction("Login", "Home");

        var attempts = await _db.ParticipantTestAttempts
            .Where(a => a.UserId     == userId
                     && a.TrainingId == trainingId
                     && a.TestType   == "PostTest")
            .ToListAsync();

        _db.ParticipantTestAttempts.RemoveRange(attempts);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Post-test attempts reset successfully.";
        return RedirectToAction("TestMaker", new { trainingId });
    }
}