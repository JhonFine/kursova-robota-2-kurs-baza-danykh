namespace CarRental.Shared.ReferenceData;

public static class ChangeSources
{
    public const string Employee = "EMPLOYEE";
    public const string Client = "CLIENT";
    public const string System = "SYSTEM";

    public static IReadOnlyList<string> All { get; } = [Employee, Client, System];
}
