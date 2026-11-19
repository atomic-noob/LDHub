using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LDHub.Helpers;

/// <summary>
/// Lets an action (or a whole controller) run only for the listed roles, based on the
/// logged-in role in the session. Replaces the "if (!IsX()) return Redirect...;" line that
/// used to open every action. Anyone else is sent to the login page (or the page named below).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : ActionFilterAttribute
{
    private readonly string[] _roles;

    public string RedirectAction     { get; set; } = "Login";
    public string RedirectController { get; set; } = "Home";

    public RequireRoleAttribute(params string[] roles) => _roles = roles;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var role = context.HttpContext.Session.GetString("UserRole");

        if (role == null || !_roles.Contains(role))
            context.Result = new RedirectToActionResult(RedirectAction, RedirectController, null);
    }
}
