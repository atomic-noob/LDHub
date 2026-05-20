using Microsoft.AspNetCore.Mvc;
using LDHub.Data;

namespace LDHub.Controllers;

public class RaamController : Controller
{
    // Database connection
    private readonly AppDbContext _db;

    public RaamController(AppDbContext db)
    {
        _db = db;
    }

    // ── Auth check helper ─────────────────────────────────────────
    private bool IsRaam() =>
        HttpContext.Session.GetString("UserRole") == "Raam";

    // ── ViewBag helper ────────────────────────────────────────────
    private void SetViewBag()
    {
        ViewBag.ActiveRole = "Raam";
        ViewBag.UserName  = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto = HttpContext.Session.GetString("UserPhoto");
    }

    // ── GET: /Raam/Dashboard ──────────────────────────────────────
    public IActionResult Dashboard()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Raam/Dashboard.cshtml");
    }

    // ── GET: /Raam/Training ───────────────────────────────────────
    public IActionResult Training()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Raam/Training.cshtml");
    }

    // ── GET: /Raam/PendingParticipants ────────────────────────────
    public IActionResult PendingParticipants()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Raam/PendingParticipants.cshtml");
    }

    // ── GET: /Raam/ParticipantsList ───────────────────────────────
    public IActionResult ParticipantsList()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Raam/ParticipantsList.cshtml");
    }

    // ── GET: /Raam/Offices ────────────────────────────────────────
    public IActionResult Offices()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Raam/Offices.cshtml");
    }

    // ── GET: /Raam/Divisions ──────────────────────────────────────
    public IActionResult Divisions()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Raam/Divisions.cshtml");
    }

    // ── GET: /Raam/Modules ────────────────────────────────────────
    public IActionResult Modules()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Raam/Modules.cshtml");
    }

    // ── GET: /Raam/TestMaker ──────────────────────────────────────
    public IActionResult TestMaker()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Raam/TestMaker.cshtml");
    }

    // ── GET: /Raam/TestResult ─────────────────────────────────────
    public IActionResult TestResult()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Raam/TestResult.cshtml");
    }

    // ── GET: /Raam/TrainingReport ─────────────────────────────────
    public IActionResult TrainingReport()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Raam/TrainingReport.cshtml");
    }

    // ── GET: /Raam/AutoPdf ────────────────────────────────────────
    public IActionResult AutoPdf()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Raam/AutoPdf.cshtml");
    }
}