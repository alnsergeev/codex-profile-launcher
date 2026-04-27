namespace CodexProfileLauncher;

public sealed class MainForm : Form
{
    private readonly CodexConfigService _configService;
    private readonly CodexLauncherService _launcherService;
    private readonly Label _configPathLabel;
    private readonly Label _currentProfileLabel;
    private readonly Label _hintLabel;
    private readonly Label _warningLabel;
    private readonly FlowLayoutPanel _buttonPanel;
    private readonly Button _addProfileButton;
    private readonly Button _reloadButton;
    private readonly Button _openConfigButton;
    private readonly Button _openConfigFolderButton;
    private readonly Button _copyConfigPathButton;

    public MainForm()
    {
        _configService = new CodexConfigService(CodexConfigService.ResolveDefaultConfigPath());
        _launcherService = new CodexLauncherService();

        Text = "Codex Profile Launcher";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1160, 560);
        MinimumSize = new Size(1120, 520);

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, Font.Size + 3, FontStyle.Bold),
            Text = "Choose a Codex profile",
            Margin = new Padding(0, 0, 0, 8),
        };

        _hintLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Text = "Click a profile to save it as the active default and launch Codex with that profile, or add a new profile first.",
            Margin = new Padding(0, 0, 0, 14),
        };

        _configPathLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Margin = new Padding(0, 0, 0, 6),
        };

        _currentProfileLabel = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8),
        };

        _warningLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.DarkGoldenrod,
            MaximumSize = new Size(1100, 0),
            Margin = new Padding(0, 0, 0, 12),
        };

        _addProfileButton = CreateActionButton("Add new profile");
        _addProfileButton.Click += (_, _) => AddProfile();

        _reloadButton = CreateActionButton("Refresh profiles");
        _reloadButton.Click += (_, _) => ReloadProfiles();

        _openConfigButton = CreateActionButton("Open config.toml file");
        _openConfigButton.Click += (_, _) => OpenConfigFile();

        _openConfigFolderButton = CreateActionButton("Open folder with config.toml");
        _openConfigFolderButton.Click += (_, _) => OpenConfigFolder();

        _copyConfigPathButton = CreateActionButton("Copy config path");
        _copyConfigPathButton.Click += (_, _) => CopyConfigPath();

        var actionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        actionPanel.Controls.Add(_addProfileButton);
        actionPanel.Controls.Add(_reloadButton);
        actionPanel.Controls.Add(_openConfigButton);
        actionPanel.Controls.Add(_openConfigFolderButton);
        actionPanel.Controls.Add(_copyConfigPathButton);

        _buttonPanel = new FlowLayoutPanel
        {
            AutoScroll = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(20),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(_hintLabel, 0, 1);
        root.Controls.Add(_configPathLabel, 0, 2);
        root.Controls.Add(_currentProfileLabel, 0, 3);
        root.Controls.Add(_warningLabel, 0, 4);
        root.Controls.Add(actionPanel, 0, 5);
        root.Controls.Add(_buttonPanel, 0, 6);

        Controls.Add(root);

        Load += (_, _) => ReloadProfiles();
        Resize += (_, _) => ResizeProfileButtons();
    }

    private static Button CreateActionButton(string text)
    {
        return new Button
        {
            AutoSize = false,
            Width = 210,
            Height = 36,
            Text = text,
            Margin = new Padding(0, 0, 10, 12),
        };
    }

    private void ReloadProfiles()
    {
        _buttonPanel.SuspendLayout();
        _buttonPanel.Controls.Clear();

        var snapshot = _configService.Load();

        _configPathLabel.Text = $"Config: {snapshot.ConfigPath}";
        _currentProfileLabel.Text = $"Current profile: {snapshot.ActiveProfile ?? "(not set)"}";
        _warningLabel.ForeColor = Color.DarkGoldenrod;
        _warningLabel.Text = FormatWarnings(snapshot.Warnings);
        _openConfigButton.Enabled = snapshot.Exists;

        if (!snapshot.Exists)
        {
            ShowEmptyState("config.toml was not found. Use Open folder with config.toml to inspect the expected location.");
            _buttonPanel.ResumeLayout();
            return;
        }

        if (snapshot.Profiles.Count == 0)
        {
            ShowEmptyState("No profiles were found. Use Add new profile or add [profiles.<name>] sections to config.toml, then refresh.");
            _buttonPanel.ResumeLayout();
            return;
        }

        foreach (var profile in snapshot.Profiles)
        {
            var isActive = profile.Key.Equals(snapshot.ActiveProfile, StringComparison.OrdinalIgnoreCase);
            var button = new Button
            {
                AutoSize = false,
                Width = GetProfileButtonWidth(),
                Height = 48,
                Text = profile.ButtonText(isActive),
                Tag = profile,
                Margin = new Padding(0, 0, 0, 12),
                Font = isActive ? new Font(Font, FontStyle.Bold) : Font,
                BackColor = isActive ? Color.Honeydew : SystemColors.Control,
                FlatStyle = FlatStyle.Standard,
            };
            button.Click += StartProfileButton_Click;
            _buttonPanel.Controls.Add(button);
        }

        _buttonPanel.ResumeLayout();
    }

    private void ShowEmptyState(string message)
    {
        _buttonPanel.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(GetProfileButtonWidth(), 0),
            Text = message,
            Margin = new Padding(0, 8, 0, 0),
        });
    }

    private static string FormatWarnings(IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return string.Empty;
        }

        return warnings.Count == 1
            ? $"Warning: {warnings[0]}"
            : $"Warnings: {warnings[0]} (+{warnings.Count - 1} more)";
    }

    private void StartProfileButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.Tag is not CodexProfile profile)
        {
            return;
        }

        var codexIsRunning = _launcherService.IsCodexRunning();
        var restartCodex = !codexIsRunning;

        if (codexIsRunning)
        {
            var result = MessageBox.Show(
                "Codex is already running.\n\nYes: restart Codex with the selected profile.\nNo: only save the default profile.\nCancel: do nothing.",
                "Codex is already running",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
            {
                return;
            }

            restartCodex = result == DialogResult.Yes;
        }

        try
        {
            _configService.SetActiveProfile(profile.Key);

            if (restartCodex)
            {
                _launcherService.RestartCodex();
                ReloadProfiles();
                ShowStatus($"Saved '{profile.DisplayName}' and launched Codex.");
            }
            else
            {
                ReloadProfiles();
                ShowStatus($"Saved '{profile.DisplayName}'. Restart Codex later to apply it.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Launcher error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ShowStatus(string message)
    {
        _warningLabel.ForeColor = Color.ForestGreen;
        _warningLabel.Text = message;
    }

    private void OpenConfigFile()
    {
        if (!File.Exists(_configService.ConfigPath))
        {
            MessageBox.Show(
                $"Could not find config.toml at:\n{_configService.ConfigPath}",
                "Config not found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        StartShellPath(_configService.ConfigPath, "Could not open config.toml.");
    }

    private void OpenConfigFolder()
    {
        var directory = Path.GetDirectoryName(_configService.ConfigPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            MessageBox.Show("Could not determine the config folder.", "Config folder not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Directory.CreateDirectory(directory);
        StartShellPath(directory, "Could not open the config folder.");
    }

    private void CopyConfigPath()
    {
        Clipboard.SetText(_configService.ConfigPath);
        ShowStatus("Copied config path to clipboard.");
    }

    private void AddProfile()
    {
        using var dialog = new AddProfileForm(_configService);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ProfileRequest is null)
        {
            return;
        }

        try
        {
            _configService.AddProfile(dialog.ProfileRequest);
            ReloadProfiles();

            var message = dialog.ProfileRequest.SetAsActive
                ? $"Added '{dialog.ProfileRequest.DisplayNameOrKey}' and set it as the current profile."
                : $"Added '{dialog.ProfileRequest.DisplayNameOrKey}'.";

            ShowStatus(message);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Add profile failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void StartShellPath(string path, string errorMessage)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{errorMessage}\n\n{ex.Message}", "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private int GetProfileButtonWidth()
    {
        var availableWidth = _buttonPanel.ClientSize.Width;

        if (availableWidth <= 0)
        {
            return 680;
        }

        return Math.Max(560, availableWidth - 24);
    }

    private void ResizeProfileButtons()
    {
        var buttonWidth = GetProfileButtonWidth();
        foreach (var button in _buttonPanel.Controls.OfType<Button>())
        {
            button.Width = buttonWidth;
        }
    }
}
