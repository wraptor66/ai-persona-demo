using Fixture.Shared;

namespace Fixture.Controllers;

[Route("api/[controller]")]
public class ReportController
{
    // Reaches the changed symbol through a service, so the walk crosses two hops.
    [HttpGet("summary")]
    public string Summary() => ReportService.Title("q3");
}

public static class ReportService
{
    public static string Title(string name) => Formatter.Format(name);
}
