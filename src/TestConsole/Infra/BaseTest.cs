using System.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace TestConsole.Infra;

public abstract class BaseTest
{
    protected IConfigurationRoot Configuration { get; private set; }
    protected Stopwatch Stopwatch { get; private set; }
    protected abstract string TestName { get; }

    public BaseTest(IConfigurationRoot configuration)
    {
        Configuration = configuration;
        Stopwatch = new Stopwatch();
    }

    /// <summary>
    /// Main entry point for running the test with built-in logging and error handling.
    /// </summary>
    public async Task RunAsync()
    {
        Stopwatch.Restart();
        LogTestStart();

        try
        {
            await TestDefinitionAsync();
            LogTestComplete();
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }

    /// <summary>
    /// Implement this method in derived classes with the specific test logic.
    /// </summary>
    protected abstract Task TestDefinitionAsync();

    protected void LogTestStart()
    {
        Console.WriteLine($"\n{'═'} {TestName} {'═'}");
        Console.WriteLine($"START: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }

    protected void LogTestComplete()
    {
        Stopwatch.Stop();
        Console.WriteLine($"{TestName} COMPLETED in {Stopwatch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"{'═'} END {DateTime.Now:yyyy-MM-dd HH:mm:ss} {'═'}\n");
    }

    protected void LogError(Exception ex)
    {
        Stopwatch.Stop();
        Console.WriteLine($"\n{TestName} FAILED after {Stopwatch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"   Error: {ex.Message}");
        Console.WriteLine($"   Type: {ex.GetType().Name}");
        if (ex.InnerException != null)
            Console.WriteLine($"   Inner: {ex.InnerException.Message}");
        Console.WriteLine($"   Stack: {ex.StackTrace}");
        Console.WriteLine($"{'═'} END {DateTime.Now:yyyy-MM-dd HH:mm:ss} {'═'}\n");
    }

    protected void LogInfo(string message) => Console.WriteLine(message);
}
