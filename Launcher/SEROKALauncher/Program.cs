using SEROKALauncher.Services;

var runtime = new LauncherRuntime();
int exitCode = await runtime.RunAsync(args);
Environment.ExitCode = exitCode;
