namespace MacharPos.Web.Tests.TestInfrastructure;

public sealed class SkipTestException : Exception
{
    public SkipTestException(string message) : base(message) { }
}