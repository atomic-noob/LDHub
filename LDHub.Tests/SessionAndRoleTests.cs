using LDHub.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace LDHub.Tests;

public class SessionAndRoleTests
{
    // ── GetUserId ────────────────────────────────────────────────
    [Fact]
    public void GetUserId_reads_the_id_the_way_login_stores_it()
    {
        var ctx = TestHttp.Context(userId: 42);

        Assert.Equal(42, ctx.Session.GetUserId());
    }

    [Fact]
    public void GetUserId_is_null_when_nobody_is_logged_in()
    {
        var ctx = TestHttp.Context();

        Assert.Null(ctx.Session.GetUserId());
    }

    [Fact]
    public void GetUserId_is_null_for_a_value_that_is_not_a_number()
    {
        var ctx = TestHttp.Context();
        ctx.Session.SetString("UserId", "abc");

        Assert.Null(ctx.Session.GetUserId());
    }

    // ── RequireRole ──────────────────────────────────────────────
    private static ActionExecutingContext ActionContext(HttpContext http) =>
        new(new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

    [Fact]
    public void RequireRole_lets_a_matching_role_through()
    {
        var context = ActionContext(TestHttp.Context(role: AppRoles.Admin));

        new RequireRoleAttribute(AppRoles.Admin).OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void RequireRole_accepts_any_of_several_roles()
    {
        var context = ActionContext(TestHttp.Context(role: AppRoles.Raam));

        new RequireRoleAttribute(AppRoles.CourseManager, AppRoles.Raam).OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void RequireRole_sends_the_wrong_role_to_the_login_page()
    {
        var context = ActionContext(TestHttp.Context(role: AppRoles.Employee));

        new RequireRoleAttribute(AppRoles.Admin).OnActionExecuting(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public void RequireRole_sends_a_logged_out_visitor_to_the_login_page()
    {
        var context = ActionContext(TestHttp.Context());

        new RequireRoleAttribute(AppRoles.Employee).OnActionExecuting(context);

        Assert.IsType<RedirectToActionResult>(context.Result);
    }

    [Fact]
    public void RequireRole_can_redirect_somewhere_else()
    {
        var context = ActionContext(TestHttp.Context(role: AppRoles.Employee));

        new RequireRoleAttribute(AppRoles.Admin) { RedirectAction = "Index", RedirectController = "Admin" }
            .OnActionExecuting(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Admin", redirect.ControllerName);
    }
}
