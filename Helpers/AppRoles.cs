namespace LDHub.Helpers;

/// <summary>Role names as stored in the session ("UserRole") and the user_roles table.</summary>
public static class AppRoles
{
    public const string Admin          = "Admin";
    public const string CourseManager  = "CourseManager";
    public const string Raam           = "Raam";
    public const string ResourcePerson = "ResourcePerson";
    public const string Employee       = "Employee";
}

/// <summary>Test types stored on test attempts and test sessions.</summary>
public static class TestTypes
{
    public const string PreTest  = "PreTest";
    public const string PostTest = "PostTest";
    public const string Quiz     = "Quiz";
}
