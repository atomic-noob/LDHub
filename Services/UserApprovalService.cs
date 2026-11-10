using LDHub.Data;
using LDHub.Helpers;
using LDHub.Models;
using LDHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Services;

public class UserApprovalService : IUserApprovalService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;

    public UserApprovalService(AppDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task<string?> ApproveAsync(PendingParticipant pending, bool removePending)
    {
        if (await _db.Users.AnyAsync(u => u.Email == pending.Email))
            return "Email already exists in users!";

        var now = DateTime.UtcNow;

        var user = new User
        {
            FirstName     = pending.FirstName,
            MiddleName    = pending.MiddleName,
            LastName      = pending.LastName,
            Email         = pending.Email,
            Password      = pending.Password,
            ContactNumber = pending.ContactNumber,
            Gender        = pending.Gender,
            Age           = pending.Age,
            Position      = pending.Position,
            Designation   = pending.Designation,
            SalaryGrade   = pending.SalaryGrade,
            RegionId      = pending.RegionId,
            OfficeId      = pending.OfficeId,
            DivisionId    = pending.DivisionId,
            Status        = RecordStatus.Active,
            CreatedAt     = now,
            UpdatedAt     = now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.UserRoles.Add(new UserRole
        {
            UserId     = user.UserId,
            Role       = AppRoles.Employee,
            AssignedAt = now
        });

        if (removePending) _db.PendingParticipants.Remove(pending);
        else               pending.Status = RecordStatus.Active;

        await _db.SaveChangesAsync();

        await _email.SendRegistrationApprovedAsync(pending.Email, $"{pending.FirstName} {pending.LastName}");
        return null;
    }
}
