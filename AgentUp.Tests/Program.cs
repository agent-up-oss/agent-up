using NUnitLite;

namespace AgentUp.Tests;

public static class E2ETestRunner
{
    public static int Main(string[] args)
    {
        return new AutoRun(typeof(E2ETestRunner).Assembly)
            .Execute(args.Length > 0 ? args : ["--workers=0"]);
    }
}
