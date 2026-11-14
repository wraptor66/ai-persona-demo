// Stand-ins for the ASP.NET Core routing attributes, so the fixture needs no packages.
namespace Fixture.Shared;

[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
public sealed class RouteAttribute(string template) : System.Attribute { public string Template { get; } = template; }

[System.AttributeUsage(System.AttributeTargets.Method)]
public sealed class HttpGetAttribute(string template) : System.Attribute { public string Template { get; } = template; }
