using System.Net;
using LDHub.Data;
using LDHub.Models;
using LDHub.Services;
using LDHub.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LDHub.Tests;

/// <summary>A web session that lives in memory, so code that reads Session can be tested.</summary>
public sealed class FakeSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public bool IsAvailable => true;
    public string Id { get; } = Guid.NewGuid().ToString();
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;

    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value)
    {
        var found = _store.TryGetValue(key, out var v);
        value = v;
        return found;
    }
}

public sealed class TestSessionFeature : ISessionFeature
{
    public ISession Session { get; set; } = null!;
}

public static class TestHttp
{
    /// <summary>A request with a session, optionally "logged in" the way the real login does it.</summary>
    public static DefaultHttpContext Context(string? role = null, int? userId = null, string? ip = "10.0.0.1")
    {
        var ctx = new DefaultHttpContext();
        var session = new FakeSession();
        ctx.Features.Set<ISessionFeature>(new TestSessionFeature { Session = session });

        if (role != null) session.SetString("UserRole", role);
        if (userId != null) session.SetString("UserId", userId.Value.ToString());   // stored as text, like login
        if (ip != null) ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);

        return ctx;
    }
}

public static class TestDb
{
    /// <summary>A fresh, empty in-memory database for one test.</summary>
    public static AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public static User NewUser(int id, string? email = null, int? regionId = null) => new()
    {
        UserId   = id,
        FirstName = "First" + id,
        LastName  = "Last" + id,
        Email     = email ?? $"user{id}@example.com",
        Password  = "hashed",
        Gender    = "F",
        Position  = "Analyst",
        Status    = "Active",
        RegionId  = regionId
    };

    public static Training NewTraining(int id, int? regionId = null, int minimumScore = 2) => new()
    {
        TrainingId    = id,
        TrainingCode  = "TRN-2026-" + id.ToString("000"),
        TrainingTitle = "Training " + id,
        RegionId      = regionId,
        MinimumScore  = minimumScore,
        PosttestTimer = 30
    };

    public static Question NewQuestion(int id, int trainingId, string type, string correct) => new()
    {
        Id            = id,
        TrainingId    = trainingId,
        QuestionType  = type,
        QuestionText  = "Question " + id,
        CorrectAnswer = correct,
        Points        = 1
    };

    public static TrainingModule NewModule(int id, int trainingId, string? filePath) => new()
    {
        ModuleId          = id,
        TrainingId        = trainingId,
        ModuleName        = "Module " + id,
        ModuleDescription = "Description",
        ModuleType        = "Lecture",
        FilePath          = filePath
    };
}

/// <summary>An email service that records what it was asked to send.</summary>
public sealed class FakeEmailService : IEmailService
{
    public List<string> ApprovedEmails { get; } = new();

    public Task<int> SendManyAsync(IEnumerable<EmailMessage> messages) => Task.FromResult(0);
    public Task SendTrainingAssignedAsync(IEnumerable<User> participants, Training training) => Task.CompletedTask;
    public Task SendInstructorAssignedAsync(IEnumerable<User> instructors, Training training) => Task.CompletedTask;
    public Task SendAnnouncementAsync(IEnumerable<User> recipients, Training training, string title, string body) => Task.CompletedTask;
    public Task SendRegistrationRejectedAsync(string email, string fullName, string? reason) => Task.CompletedTask;

    public Task SendRegistrationApprovedAsync(string email, string fullName)
    {
        ApprovedEmails.Add(email);
        return Task.CompletedTask;
    }
}
