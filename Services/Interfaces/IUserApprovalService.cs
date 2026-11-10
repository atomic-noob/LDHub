using LDHub.Models;

namespace LDHub.Services.Interfaces;

public interface IUserApprovalService
{
    /// <summary>
    /// Turns a pending registration into a user with the Employee role and emails them.
    /// removePending: delete the pending row (admin) instead of marking it Active (RAAM).
    /// Returns an error message, or null when it worked.
    /// </summary>
    Task<string?> ApproveAsync(PendingParticipant pending, bool removePending);
}
