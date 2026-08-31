using System.Drawing;
using System.Windows.Forms;
using System.IO;
using QuickConvert.Configuration;
using QuickConvert.Conversion;
using QuickConvert.Gui;
using QuickConvert.Shell;

namespace QuickConvert;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Length == 0)
        {
            Application.Run(new MainForm(Array.Empty<string>()));
            return 0;
        }

        return args[0] switch
        {
            "--install" => RunShellAction(ShellRegistry.Install, "Added to the right-click menu.", "Could not install the right-click menu."),
            "--uninstall" => RunShellAction(ShellRegistry.Uninstall, "Removed from the right-click menu.", "Could not remove the right-click menu."),
            "--convert" => RunHeadlessConversion(args.Skip(1).ToArray()),
            "--open" => RunGui(args.Skip(1).ToArray()),
            _ => RunGui(args),
        };
    }

    private static int RunGui(IEnumerable<string> files)
    {
        Application.Run(new MainForm(files));
        return 0;
    }

    private static int RunShellAction(Action action, string successMessage, string failureMessage)
    {
        try
        {
            action();
            MessageBox.Show(successMessage, "QuickConvert", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        catch (Exception exception)
        {
            MessageBox.Show($"{failureMessage}\r\n\r\n{exception.Message}", "QuickConvert", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static int RunHeadlessConversion(string[] raw)
    {
        if (raw.Length < 2)
        {
            MessageBox.Show("Nothing to convert — no files were passed.", "QuickConvert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 1;
        }

        var target = FormatCatalog.NormalizeExt(raw[0]);
        var files = raw.Skip(1).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (files.Length == 0)
        {
            MessageBox.Show("Nothing to convert — the selected files no longer exist.", "QuickConvert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 1;
        }

        ProgressForm? progress = null;
        var conversionTask = Task.Run(() =>
            Converter.ConvertBatch(files, target, Quality.Balanced, null,
                (index, total, name) =>
                {
                    var current = progress;
                    if (current is null || current.IsDisposed || !current.IsHandleCreated) return;
                    try
                    {
                        current.BeginInvoke(new Action(() => current.SetProgress(index + 1, total, name)));
                    }
                    catch (InvalidOperationException) { }
                }));

        // Keep tiny image conversions invisible. If the job is slower, show a small native dialog.
        if (!conversionTask.Wait(500))
        {
            progress = new ProgressForm(files.Length, target);
            var shown = progress;
            shown.Shown += (_, _) =>
            {
                conversionTask.ContinueWith(_ =>
                {
                    if (shown.IsDisposed) return;
                    try { shown.BeginInvoke(new Action(shown.Close)); } catch (InvalidOperationException) { }
                }, TaskScheduler.Default);
            };
            Application.Run(shown);
        }

        var results = conversionTask.GetAwaiter().GetResult();
        var errors = results.Where(result => !result.Success)
            .Select(result => $"{Path.GetFileName(result.Source)} — {result.Error}")
            .ToArray();

        if (errors.Length > 0)
        {
            var succeeded = results.Count(result => result.Success);
            var heading = succeeded == 0
                ? "Conversion failed."
                : $"Converted {succeeded} of {results.Count} files.";
            MessageBox.Show($"{heading}\r\n\r\n{string.Join("\r\n", errors.Take(6))}", "QuickConvert", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        if (SettingsStore.Load().ShowSuccessNotifications)
        {
            MessageBox.Show(
                $"Converted {results.Count} file{(results.Count == 1 ? "" : "s")} to {target.ToUpperInvariant()}.",
                "QuickConvert", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        return 0;
    }
}
