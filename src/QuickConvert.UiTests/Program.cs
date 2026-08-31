using System.Windows.Forms;
using QuickConvert.Gui;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var form = new MainForm(Array.Empty<string>());

        if (form.FormBorderStyle != FormBorderStyle.FixedDialog || form.MaximizeBox || form.MinimizeBox)
            throw new InvalidOperationException("QuickConvert should use a native fixed-dialog property-sheet style window.");

        if (!form.ShowIcon || form.Icon is null)
            throw new InvalidOperationException("QuickConvert should show its application icon in the utility window title bar.");

        var tabs = form.Controls.OfType<TabControl>().SingleOrDefault();
        if (tabs is null || tabs.TabPages.Count < 2)
            throw new InvalidOperationException("Native UI should expose Convert and Options tabs.");

        if (form.ClientSize.Width > 600 || form.ClientSize.Height > 460)
            throw new InvalidOperationException("QuickConvert should open as a compact utility window.");

        var convertButton = form.Controls.OfType<Button>().FirstOrDefault(button => button.Text == "Convert");
        if (convertButton is null || convertButton.Width <= 0 || convertButton.Height <= 0)
            throw new InvalidOperationException("Convert button must be visible at the default size.");

        Console.WriteLine("[PASS] Native Win32-themed WinForms UI contract");
    }
}
