namespace CodexProfileLauncher;

public sealed class AddModelProviderForm : Form
{
    private readonly TextBox _keyTextBox;
    private readonly TextBox _displayNameTextBox;
    private readonly TextBox _baseUrlTextBox;
    private readonly TextBox _wireApiTextBox;

    public AddModelProviderForm()
    {
        Text = "Add model provider";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(720, 320);

        var introLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            Text = "Create a basic [model_providers.<key>] section. Add auth, headers, or other advanced settings later in config.toml.",
            Margin = new Padding(0, 0, 0, 12),
        };

        _keyTextBox = CreateTextBox("remote_ollama");
        _displayNameTextBox = CreateTextBox("Optional friendly name, for example Remote Ollama");
        _baseUrlTextBox = CreateTextBox("http://your-ollama-host:11434/v1");
        _wireApiTextBox = CreateTextBox("responses");
        _wireApiTextBox.Text = "responses";

        var saveButton = new Button
        {
            AutoSize = true,
            Text = "Add provider",
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
            RowCount = 6,
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

        root.Controls.Add(introLabel, 0, 0);
        root.SetColumnSpan(introLabel, 2);
        AddField(root, 1, "Provider key", _keyTextBox);
        AddField(root, 2, "Display name", _displayNameTextBox);
        AddField(root, 3, "Base URL", _baseUrlTextBox);
        AddField(root, 4, "Wire API", _wireApiTextBox);
        root.Controls.Add(buttonPanel, 0, 5);
        root.SetColumnSpan(buttonPanel, 2);

        Controls.Add(root);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    public NewCodexModelProvider? ModelProviderRequest { get; private set; }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        var key = _keyTextBox.Text.Trim();
        if (!CodexConfigService.IsValidModelProviderKey(key))
        {
            MessageBox.Show(
                "Model provider key must use only letters, numbers, '.', '_' or '-'.",
                "Invalid model provider key",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _keyTextBox.Focus();
            return;
        }

        var baseUrl = _baseUrlTextBox.Text.Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(
                "Base URL must be a valid absolute http or https URL.",
                "Invalid base URL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _baseUrlTextBox.Focus();
            return;
        }

        var wireApi = _wireApiTextBox.Text.Trim();
        if (wireApi.Length == 0)
        {
            MessageBox.Show(
                "Wire API is required.",
                "Missing wire API",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _wireApiTextBox.Focus();
            return;
        }

        ModelProviderRequest = new NewCodexModelProvider(
            key,
            NormalizeOptional(_displayNameTextBox.Text),
            baseUrl,
            wireApi);

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

        control.Dock = DockStyle.Fill;
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