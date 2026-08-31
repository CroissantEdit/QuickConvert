using System.Diagnostics;
using System.IO;

namespace QuickConvert.Shell;

public static class ShellRegistry
{
    public static bool IsInstalled() => RunPowerShell(
        "-Command",
        "if (Get-AppxPackage -Name QuickConvert.Desktop) { exit 0 } else { exit 1 }") == 0;

    public static void Install() => RunScript("install-shell.ps1");

    public static void Uninstall() => RunScript("uninstall-shell.ps1");

    private static void RunScript(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        if (!File.Exists(path)) throw new FileNotFoundException("QuickConvert shell installer is missing.", path);
        if (RunPowerShell("-ExecutionPolicy", "Bypass", "-File", path) != 0)
            throw new InvalidOperationException($"QuickConvert shell installer failed: {name}");
    }

    private static int RunPowerShell(params string[] arguments)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell could not start.");
        process.WaitForExit();
        return process.ExitCode;
    }
}
