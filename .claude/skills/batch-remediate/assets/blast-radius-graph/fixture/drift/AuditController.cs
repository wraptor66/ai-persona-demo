using Fixture.Shared;

namespace Fixture.Controllers;

// Hardening check 5: copy this into App/Controllers after M2 to add a caller the agreed set lacks.
[Route("api/[controller]")]
public class AuditController
{
    [HttpGet("entry")]
    public string Entry() => Formatter.Format("audit");
}
