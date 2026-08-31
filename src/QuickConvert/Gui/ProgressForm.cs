using System.Drawing;
using System.Windows.Forms;
namespace QuickConvert.Gui;

internal sealed class ProgressForm : Form
{
    private readonly Label _label;
    private readonly ProgressBar _progress;

    public ProgressForm(int total, string target)
    {
        Text = "QuickConvert";
        ClientSize = new Size(360, 84);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        Font = SystemFonts.MessageBoxFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        ShowIcon = false;
        UseWaitCursor = true;

        _label = new Label
        {
            AutoSize = false,
            Text = $"Converting {total} file{(total == 1 ? "" : "s")} to {target.ToUpperInvariant()}...",
            Location = new Point(14, 14),
            Size = new Size(332, 20),
            AutoEllipsis = true,
        };
        _progress = new ProgressBar
        {
            Location = new Point(14, 43),
            Size = new Size(332, 18),
            Minimum = 0,
            Maximum = Math.Max(1, total),
            Style = ProgressBarStyle.Continuous,
        };

        Controls.Add(_label);
        Controls.Add(_progress);
    }

    public void SetProgress(int current, int total, string name)
    {
        _progress.Maximum = Math.Max(1, total);
        _progress.Value = Math.Clamp(current, 0, _progress.Maximum);
        _label.Text = $"Converting {name} ({current}/{total})...";
    }
}
