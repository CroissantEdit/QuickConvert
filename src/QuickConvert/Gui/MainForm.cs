using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using QuickConvert.Configuration;
using QuickConvert.Conversion;
using QuickConvert.Shell;

namespace QuickConvert.Gui;

internal sealed record QualityOption(Quality Quality, string Label)
{
    public override string ToString() => Label;
}

internal sealed class FileItem
{
    public FileItem(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        Ext = FormatCatalog.NormalizeExt(System.IO.Path.GetExtension(path)).ToUpperInvariant();
        SizeText = FormatSize(new FileInfo(path).Length);
    }

    public string Path { get; }
    public string Name { get; }
    public string Ext { get; }
    public string SizeText { get; }
    public string Status { get; set; } = "Ready";
    public string? Output { get; set; }
    public string? Error { get; set; }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:0.##} MB",
        _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:0.##} GB",
    };
}

public sealed class MainForm : Form
{
    private readonly List<FileItem> _items = new();
    private readonly TabControl _tabs = new();
    private readonly TabPage _convertPage = new("Convert");
    private readonly TabPage _optionsPage = new("Options");
    private readonly ListView _fileList = new();
    private readonly Button _addButton = new();
    private readonly Button _removeButton = new();
    private readonly Button _clearButton = new();
    private readonly ComboBox _formatBox = new();
    private readonly ComboBox _qualityBox = new();
    private readonly CheckBox _sameFolderCheck = new();
    private readonly TextBox _outputFolderText = new();
    private readonly Button _browseButton = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _status = new();
    private readonly Button _convertButton = new();
    private readonly Button _closeButton = new();
    private readonly Button _installButton = new();
    private readonly CheckBox _notifyCheck = new();
    private bool _converting;

    public MainForm(IEnumerable<string> initialFiles)
    {
        Text = "QuickConvert";
        ClientSize = new Size(548, 397);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = SystemFonts.MessageBoxFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        AllowDrop = true;
        KeyPreview = true;

        ShowIcon = false;

        BuildInterface();

        _qualityBox.Items.AddRange(new object[]
        {
            new QualityOption(Quality.Best, "Best quality"),
            new QualityOption(Quality.Balanced, "Balanced (default)"),
            new QualityOption(Quality.Small, "Smaller file"),
        });
        _qualityBox.SelectedIndex = 1;
        _sameFolderCheck.Checked = true;
        _notifyCheck.Checked = SettingsStore.Load().ShowSuccessNotifications;

        HookEvents();
        UpdateOutputControls();
        UpdateInstallButton();
        AddFiles(initialFiles);
    }

    private void BuildInterface()
    {
        _tabs.SetBounds(7, 7, 534, 347);
        _tabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _tabs.TabPages.AddRange(new[] { _convertPage, _optionsPage });
        Controls.Add(_tabs);

        BuildConvertPage();
        BuildOptionsPage();

        _convertButton.Text = "Convert";
        _convertButton.SetBounds(371, 362, 82, 26);
        _convertButton.Enabled = false;
        _convertButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        Controls.Add(_convertButton);

        _closeButton.Text = "Close";
        _closeButton.SetBounds(459, 362, 82, 26);
        _closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _closeButton.DialogResult = DialogResult.Cancel;
        Controls.Add(_closeButton);

        AcceptButton = _convertButton;
        CancelButton = _closeButton;
    }

    private void BuildConvertPage()
    {
        var filesGroup = new GroupBox { Text = "Files" };
        filesGroup.SetBounds(8, 8, 510, 194);
        _convertPage.Controls.Add(filesGroup);

        _addButton.Text = "Add...";
        _addButton.SetBounds(10, 20, 72, 25);
        filesGroup.Controls.Add(_addButton);

        _removeButton.Text = "Remove";
        _removeButton.SetBounds(88, 20, 72, 25);
        _removeButton.Enabled = false;
        filesGroup.Controls.Add(_removeButton);

        _clearButton.Text = "Clear";
        _clearButton.SetBounds(166, 20, 72, 25);
        filesGroup.Controls.Add(_clearButton);

        filesGroup.Controls.Add(new Label
        {
            Text = "Drag and drop files here",
            AutoSize = true,
            Location = new Point(248, 25),
        });

        _fileList.SetBounds(10, 52, 490, 132);
        _fileList.View = View.Details;
        _fileList.FullRowSelect = true;
        _fileList.MultiSelect = true;
        _fileList.HideSelection = false;
        _fileList.ShowItemToolTips = true;
        _fileList.Columns.Add("File", 238);
        _fileList.Columns.Add("Type", 55, HorizontalAlignment.Center);
        _fileList.Columns.Add("Size", 72, HorizontalAlignment.Right);
        _fileList.Columns.Add("Status", 116);
        filesGroup.Controls.Add(_fileList);

        var conversionGroup = new GroupBox { Text = "Conversion" };
        conversionGroup.SetBounds(8, 208, 248, 91);
        _convertPage.Controls.Add(conversionGroup);

        conversionGroup.Controls.Add(new Label { Text = "Format:", AutoSize = true, Location = new Point(10, 24) });
        _formatBox.SetBounds(64, 20, 174, 24);
        _formatBox.DropDownStyle = ComboBoxStyle.DropDownList;
        conversionGroup.Controls.Add(_formatBox);

        conversionGroup.Controls.Add(new Label { Text = "Quality:", AutoSize = true, Location = new Point(10, 56) });
        _qualityBox.SetBounds(64, 52, 174, 24);
        _qualityBox.DropDownStyle = ComboBoxStyle.DropDownList;
        conversionGroup.Controls.Add(_qualityBox);

        var outputGroup = new GroupBox { Text = "Output" };
        outputGroup.SetBounds(264, 208, 254, 91);
        _convertPage.Controls.Add(outputGroup);

        _sameFolderCheck.Text = "Save next to original";
        _sameFolderCheck.AutoSize = true;
        _sameFolderCheck.Location = new Point(10, 21);
        outputGroup.Controls.Add(_sameFolderCheck);

        _outputFolderText.SetBounds(10, 50, 172, 23);
        outputGroup.Controls.Add(_outputFolderText);

        _browseButton.Text = "Browse...";
        _browseButton.SetBounds(188, 49, 56, 25);
        outputGroup.Controls.Add(_browseButton);

        _progress.SetBounds(8, 307, 510, 12);
        _progress.Minimum = 0;
        _progress.Maximum = 1;
        _convertPage.Controls.Add(_progress);

        _status.Text = "Add files to begin.";
        _status.AutoEllipsis = true;
        _status.SetBounds(8, 323, 510, 18);
        _convertPage.Controls.Add(_status);
    }

    private void BuildOptionsPage()
    {
        var explorerGroup = new GroupBox { Text = "File Explorer" };
        explorerGroup.SetBounds(8, 8, 510, 95);
        _optionsPage.Controls.Add(explorerGroup);

        explorerGroup.Controls.Add(new Label
        {
            Text = "Show QuickConvert in the Windows 11 right-click menu for supported media files.",
            AutoSize = false,
            Location = new Point(10, 22),
            Size = new Size(485, 30),
        });

        _installButton.SetBounds(10, 56, 164, 26);
        explorerGroup.Controls.Add(_installButton);

        var behaviorGroup = new GroupBox { Text = "Quick conversions" };
        behaviorGroup.SetBounds(8, 111, 510, 130);
        _optionsPage.Controls.Add(behaviorGroup);

        _notifyCheck.Text = "Show a notification after successful right-click conversions";
        _notifyCheck.AutoSize = true;
        _notifyCheck.Location = new Point(10, 23);
        behaviorGroup.Controls.Add(_notifyCheck);

        behaviorGroup.Controls.Add(new Label
        {
            Text = "Quick conversions are silent by default. Errors are always shown.",
            AutoSize = false,
            Location = new Point(10, 55),
            Size = new Size(458, 35),
        });
    }

    private void HookEvents()
    {
        _addButton.Click += (_, _) => ChooseFiles();
        _removeButton.Click += (_, _) => RemoveSelected();
        _clearButton.Click += (_, _) => ClearFiles();
        _fileList.SelectedIndexChanged += (_, _) => _removeButton.Enabled = !_converting && _fileList.SelectedItems.Count > 0;
        _fileList.DoubleClick += (_, _) => RevealSelectedOutput();
        _sameFolderCheck.CheckedChanged += (_, _) => UpdateOutputControls();
        _browseButton.Click += (_, _) => BrowseOutputFolder();
        _convertButton.Click += async (_, _) => await ConvertAsync();
        _closeButton.Click += (_, _) => Close();
        _installButton.Click += (_, _) => ToggleShellIntegration();
        _notifyCheck.CheckedChanged += (_, _) => SettingsStore.Save(new UserSettings { ShowSuccessNotifications = _notifyCheck.Checked });
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
    }

    private void ChooseFiles()
    {
        var patterns = string.Join(';', FormatCatalog.SupportedInputExtensions.Select(ext => $"*.{ext}"));
        using var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = $"Supported media|{patterns}|All files|*.*",
            Title = "Add files",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddFiles(dialog.FileNames);
    }

    private void AddFiles(IEnumerable<string> files)
    {
        var rejected = 0;
        foreach (var file in files)
        {
            if (!File.Exists(file)) continue;
            if (!FormatCatalog.IsSupportedInput(Path.GetExtension(file)))
            {
                rejected++;
                continue;
            }
            if (_items.Any(item => string.Equals(item.Path, file, StringComparison.OrdinalIgnoreCase))) continue;

            var item = new FileItem(file);
            _items.Add(item);
            var row = new ListViewItem(new[] { item.Name, item.Ext, item.SizeText, item.Status }) { Tag = item };
            _fileList.Items.Add(row);
        }

        RefreshFormatChoices();
        if (_items.Count == 0)
            _status.Text = rejected > 0 ? "That file type is not supported yet." : "Add files to begin.";
        else if (rejected > 0)
            _status.Text = $"{_items.Count} supported file{(_items.Count == 1 ? "" : "s")} ready; skipped {rejected}.";
        else
            _status.Text = $"{_items.Count} file{(_items.Count == 1 ? "" : "s")} ready.";
    }

    private void RemoveSelected()
    {
        if (_converting) return;
        foreach (ListViewItem row in _fileList.SelectedItems.Cast<ListViewItem>().ToArray())
        {
            if (row.Tag is FileItem item) _items.Remove(item);
            _fileList.Items.Remove(row);
        }
        RefreshFormatChoices();
        _status.Text = _items.Count == 0 ? "Add files to begin." : $"{_items.Count} file{(_items.Count == 1 ? "" : "s")} ready.";
    }

    private void ClearFiles()
    {
        if (_converting) return;
        _items.Clear();
        _fileList.Items.Clear();
        RefreshFormatChoices();
        _status.Text = "Add files to begin.";
    }

    private void RefreshFormatChoices()
    {
        var previous = (_formatBox.SelectedItem as FormatOption)?.Ext;
        var compatible = FormatCatalog.GetCompatibleTargets(_items.Select(item => item.Ext));

        _formatBox.BeginUpdate();
        _formatBox.Items.Clear();
        foreach (var option in compatible) _formatBox.Items.Add(option);
        _formatBox.DisplayMember = nameof(FormatOption.DisplayName);
        _formatBox.EndUpdate();

        FormatOption? selection = null;
        if (previous is not null)
            selection = compatible.FirstOrDefault(option => option.Ext == previous);

        if (selection is null && _items.Count > 0)
        {
            var suggested = FormatCatalog.GetMenuTargets(_items[0].Ext)
                .FirstOrDefault(target => compatible.Any(option => option.Ext == target));
            selection = compatible.FirstOrDefault(option => option.Ext == suggested) ?? compatible.FirstOrDefault();
        }

        _formatBox.SelectedItem = selection;
        _convertButton.Enabled = !_converting && _items.Count > 0 && selection is not null;
        if (_items.Count > 0 && compatible.Count == 0)
            _status.Text = "These files do not share a compatible output format.";
    }

    private void UpdateOutputControls()
    {
        var customFolder = !_sameFolderCheck.Checked;
        _outputFolderText.Enabled = customFolder;
        _browseButton.Enabled = customFolder;
    }

    private void BrowseOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose output folder",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _outputFolderText.Text = dialog.SelectedPath;
    }

    private async Task ConvertAsync()
    {
        if (_converting || _items.Count == 0 || _formatBox.SelectedItem is not FormatOption format) return;
        if (_qualityBox.SelectedItem is not QualityOption qualityOption) return;

        var outputDir = _sameFolderCheck.Checked ? null : _outputFolderText.Text;
        if (!string.IsNullOrWhiteSpace(outputDir) && !Directory.Exists(outputDir))
        {
            _status.Text = "The selected output folder doesn't exist.";
            return;
        }

        _converting = true;
        SetEditingEnabled(false);
        _progress.Minimum = 0;
        _progress.Maximum = Math.Max(1, _items.Count);
        _progress.Value = 0;
        foreach (ListViewItem row in _fileList.Items)
        {
            if (row.Tag is not FileItem item) continue;
            item.Status = "Converting...";
            item.Output = null;
            item.Error = null;
            row.SubItems[3].Text = item.Status;
        }

        var paths = _items.Select(item => item.Path).ToArray();
        var results = await Task.Run(() =>
            Converter.ConvertBatch(paths, format.Ext, qualityOption.Quality, outputDir,
                (index, total, name) =>
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke(new Action(() =>
                    {
                        _status.Text = $"Converting {name} ({index + 1}/{total})...";
                        _progress.Maximum = Math.Max(1, total);
                        _progress.Value = Math.Clamp(index + 1, 0, _progress.Maximum);
                    }));
                }));

        for (var i = 0; i < results.Count && i < _items.Count; i++)
        {
            var result = results[i];
            var item = _items[i];
            if (result.Success)
            {
                item.Status = $"Done ({format.DisplayName})";
                item.Output = result.Output;
                item.Error = null;
            }
            else
            {
                item.Status = "Failed";
                item.Output = null;
                item.Error = result.Error ?? "Conversion failed.";
            }
            _fileList.Items[i].SubItems[3].Text = item.Status;
            _fileList.Items[i].ToolTipText = result.Success
                ? $"Created: {result.Output}"
                : item.Error;
        }

        var succeeded = results.Count(result => result.Success);
        _status.Text = succeeded == results.Count
            ? $"Converted {succeeded} file{(succeeded == 1 ? "" : "s")} to {format.DisplayName}."
            : $"Converted {succeeded}; {results.Count - succeeded} failed.";

        _converting = false;
        SetEditingEnabled(true);
        RefreshFormatChoices();
    }

    private void SetEditingEnabled(bool enabled)
    {
        _addButton.Enabled = enabled;
        _clearButton.Enabled = enabled;
        _removeButton.Enabled = enabled && _fileList.SelectedItems.Count > 0;
        _formatBox.Enabled = enabled;
        _qualityBox.Enabled = enabled;
        _sameFolderCheck.Enabled = enabled;
        _browseButton.Enabled = enabled && !_sameFolderCheck.Checked;
        _outputFolderText.Enabled = enabled && !_sameFolderCheck.Checked;
        _convertButton.Enabled = enabled && _items.Count > 0 && _formatBox.SelectedItem is FormatOption;
    }

    private void RevealSelectedOutput()
    {
        if (_fileList.SelectedItems.Count != 1 || _fileList.SelectedItems[0].Tag is not FileItem item) return;
        if (!string.IsNullOrWhiteSpace(item.Output))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Output}\"") { UseShellExecute = true });
        else if (!string.IsNullOrWhiteSpace(item.Error))
            MessageBox.Show(item.Error, "QuickConvert", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void UpdateInstallButton()
    {
        try
        {
            _installButton.Text = ShellRegistry.IsInstalled() ? "Remove right-click menu" : "Install right-click menu";
        }
        catch
        {
            _installButton.Text = "Install right-click menu";
        }
    }

    private void ToggleShellIntegration()
    {
        try
        {
            if (ShellRegistry.IsInstalled())
            {
                ShellRegistry.Uninstall();
                _status.Text = "Removed from the right-click menu.";
            }
            else
            {
                ShellRegistry.Install();
                _status.Text = "Added to the Windows 11 right-click menu.";
            }
            UpdateInstallButton();
        }
        catch (Exception exception)
        {
            _status.Text = $"Could not update the right-click menu: {exception.Message}";
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (_converting) return;
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (_converting) return;
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
            AddFiles(files);
    }
}
