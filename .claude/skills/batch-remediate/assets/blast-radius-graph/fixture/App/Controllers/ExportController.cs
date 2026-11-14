using Fixture.Shared;

namespace Fixture.Controllers;

[Route("api/[controller]")]
public class ExportController
{
    // Calls the OVERLOAD and the OTHER type's Format: neither may appear in the blast radius of
    // Fixture.Shared.Formatter.Format(string).
    [HttpGet("csv")]
    public string Csv() => Formatter.Format("x", 10) + Fixture.Other.Formatter.Format("y");
}
