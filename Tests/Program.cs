namespace ResolutionAssistant.Regression;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var failures = 0;
        var total = 0;
        foreach (var (name, run) in AutomationRegression.Cases.Concat(ConfigMigrationRegression.Cases))
        {
            total++;
            try
            {
                run();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {exception}");
            }
        }
        Console.WriteLine($"{total - failures}/{total} regressions passed.");
        return failures == 0 ? 0 : 1;
    }
}

internal static class RegressionAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message} Expected: {expected}; actual: {actual}.");
    }

    public static void Throws<T>(Action action, string message) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
