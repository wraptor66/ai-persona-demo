namespace Fixture.Shared;

// The changed symbol is Format(string). Format(string, int) is an overload that must NOT pull in
// its own callers, and Fixture.Other.Formatter.Format(string) is a same-named method on another type.
public static class Formatter
{
    public static string Format(string value) => value.Trim();

    public static string Format(string value, int width) => value.PadLeft(width);
}
