using Microsoft.AspNetCore.Mvc;
using LDHub.Data;

namespace LDHub.Controllers;

public class ResourcePersonController : Controller
{
    // Database connection
    private readonly AppDbContext _db;

    public ResourcePersonController(AppDbContext db)
    {
        _db = db;
    }

    // ── Auth check helper ─────────────────────────────────────────
    private bool IsResourcePerson() =>
        HttpContext.Session.GetString("UserRole") == "ResourcePerson";

    // ── ViewBag helper ────────────────────────────────────────────
    private void SetViewBag()
    {
        ViewBag.ActiveRole = "ResourcePerson";
        ViewBag.UserName  = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto = HttpContext.Session.GetString("UserPhoto");
    }

    // ── GET: /ResourcePerson/Index ────────────────────────────────
    // External SME lands here after login
    public IActionResult Index()
    {
        if (!IsResourcePerson()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/ResourcePerson/Index.cshtml");
    }

    // ── GET: /ResourcePerson/Dashboard ───────────────────────────
    public IActionResult Dashboard()
    {
        if (!IsResourcePerson()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/ResourcePerson/Dashboard.cshtml");
    }

    // ── GET: /ResourcePerson/Grades ───────────────────────────────
    public IActionResult Grades()
    {
        if (!IsResourcePerson()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/ResourcePerson/Grades.cshtml");
    }

    // ── GET: /ResourcePerson/TrainingHistory ──────────────────────
    public IActionResult TrainingHistory()
    {
        if (!IsResourcePerson()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/ResourcePerson/TrainingHistory.cshtml");
    }

    // ── GET: /ResourcePerson/TestMaker ────────────────────────────
    public IActionResult TestMaker()
    {
        if (!IsResourcePerson()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/ResourcePerson/TestMaker.cshtml");
    }

    // ── GET: /ResourcePerson/TestResult ───────────────────────────
    public IActionResult TestResult()
    {
        if (!IsResourcePerson()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/ResourcePerson/TestResult.cshtml");
    }
}