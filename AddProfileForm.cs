namespace CodexProfileLauncher;

public sealed class AddProfileForm : Form
{
    private readonly CodexConfigService _configService;
    private readonly TextBox _keyTextBox;
    private readonly TextBox _displayNameTextBox;
    private readonly TextBox _modelTextBox;
    private readonly TextBox _modelProviderTextBox;
    private readonly TextBox _reasoningEffortTextBox;
    private readonly Button _addModelProviderButton;
    private readonly CheckBox _setAsActiveCheckBox;

    public AddProfileForm(CodexConfigService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));

        Text = "Add Codex profile";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(780, 430);

        var introLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(740, 0),
            Text = "Create a basic [profiles.<key>] section. You can also create a basic model provider here and keep advanced settings in config.toml.",
            Margin = new Padding(0, 0, 0, 12),
        };

        _keyTextBox = CreateTextBox("openai or ollama");
        _displayNameTextBox = CreateTextBox("Optional friendly label shown in the launcher");
        _modelTextBox = CreateTextBox("gpt-5.4 or qwen3:latest");
        _modelProviderTextBox = CreateTextBox("Optional provider key, for example remote_ollama");
        _reasoningEffortTextBox = CreateTextBox("Optional value, for example medium or high");
        _addModelProviderButton = new Button
        {
            AutoSize = true,
            Text = "New provider...",
            Margin = new Padding(0, 3, 0, 3),
        };
        _addModelProviderButton.Click += AddModelProviderButton_Click;

        var modelProviderEditor = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
            Margin = new Padding(0),
            Padding = new Padding(0),
            RowCount = 1,
        };
        modelProviderEditor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        modelProviderEditor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        modelProviderEditor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _modelProviderTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _modelProviderTextBox.Margin = new Padding(0, 4, 8, 4);
        modelProviderEditor.Controls.Add(_modelProviderTextBox, 0, 0);
        modelProviderEditor.Controls.Add(_addModelProviderButton, 1, 0);

        _setAsActiveCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "Set as active profile after saving",
            Checked = true,
            Margin = new Padding(0, 4, 0, 12),
        };

        var saveButton = new Button
        {
            AutoSize = true,
            Text = "Add profile",
            Margin = new Padding(8, 0, 0, 0),
        };
        saveButton.Click += SaveButton_Click;

        var cancelButton = new Button
        {
            AutoSize = true,
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
        };

        var buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 8, 0, 0),
        };
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(16),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(introLabel, 0, 0);
        root.SetColumnSpan(introLabel, 2);
        AddField(root, 1, "Profile key", _keyTextBox);
        AddField(root, 2, "Display name", _displayNameTextBox);
        AddField(root, 3, "Model", _modelTextBox);
        AddField(root, 4, "Model provider", modelProviderEditor);
        AddField(root, 5, "Reasoning effort", _reasoningEffortTextBox);
        root.Controls.Add(_setAsActiveCheckBox, 1, 6);
        root.Controls.Add(buttonPanel, 0, 7);
        root.SetColumnSpan(buttonPanel, 2);

        Controls.Add(root);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    public NewCodexProfile? ProfileRequest { get; private set; }

    private void AddModelProviderButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new AddModelProviderForm();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ModelProviderRequest is null)
        {
            return;
        }

        try
        {
            _configService.AddModelProvider(dialog.ModelProviderRequest);
            _modelProviderTextBox.Text = dialog.ModelProviderRequest.Key;

            MessageBox.Show(
                $"Added model provider '{dialog.ModelProviderRequest.DisplayNameOrKey}' and selected it for this profile.",
                "Model provider added",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Add model provider failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        var key = _keyTextBox.Text.Trim();
        if (!CodexConfigService.IsValidProfileKey(key))
        {
            MessageBox.Show(
                "Profile key must use only letters, numbers, '.', '_' or '-'.",
                "Invalid profile key",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _keyTextBox.Focus();
            return;
        }

        var model = _modelTextBox.Text.Trim();
        if (model.Length == 0)
        {
            MessageBox.Show(
                "Model is required.",
                "Missing model",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _modelTextBox.Focus();
            return;
        }

        ProfileRequest = new NewCodexProfile(
            key,
            NormalizeOptional(_displayNameTextBox.Text),
            model,
            NormalizeOptional(_modelProviderTextBox.Text),
            NormalizeOptional(_reasoningEffortTextBox.Text),
            _setAsActiveCheckBox.Checked);

        DialogResult = DialogResult.OK;
        Close();
    }

    private static void AddField(TableLayoutPanel layout, int rowIndex, string labelText, Control control)
    {
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = labelText,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 8),
        }, 0, rowIndex);

        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(0, 4, 0, 4);
        layout.Controls.Add(control, 1, rowIndex);
    }

    private static TextBox CreateTextBox(string placeholder)
    {
        return new TextBox
        {
            PlaceholderText = placeholder,
        };
    }

    private static string? NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}