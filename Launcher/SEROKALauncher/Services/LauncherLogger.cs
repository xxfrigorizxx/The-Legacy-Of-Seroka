namespace SEROKALauncher.Services;

public sealed class LauncherLogger
{
    private readonly string _logPath;
    private readonly object _sync = new();

    public LauncherLogger(string logPath)
    {
        _logPath = logPath;
        string? dir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        string line = $"[{DateTime.UtcNow:O}] {level} {message}";
        lock (_sync)
        {
            File.AppendAllLines(_logPath, new[] { line });
        }
        Console.WriteLine(line);
    }
}
