namespace PiGUI;

internal sealed class NewProjectForm : Form
{
    private readonly TextBox nameBox = new();
    private readonly Label locationLabel = new();
    private readonly Label previewLabel = new();
    private string parentDirectory = ProjectWorkspace.DefaultRoot;
    public string? ProjectPath { get; private set; }

    public NewProjectForm()
    {
        Text = "New project"; Size = new Size(570, 355); MinimumSize = Size; MaximumSize = Size;
        StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; ShowInTaskbar = false;
        BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Ui;
        Controls.Add(new Label { Text = "Start a new project", Font = new Font("Segoe UI Semibold", 18), AutoSize = true, Location = new Point(30, 25) });
        Controls.Add(new Label { Text = "Pi GUI will create a dedicated workspace folder for you.", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(33, 65) });
        Controls.Add(new Label { Text = "Project name", AutoSize = true, Location = new Point(32, 108) });

        nameBox.Location = new Point(32, 132); nameBox.Size = new Size(498, 32); nameBox.Font = new Font("Segoe UI", 11); nameBox.BackColor = Theme.Surface; nameBox.ForeColor = Theme.Text; nameBox.BorderStyle = BorderStyle.FixedSingle; nameBox.Text = "my-project";
        nameBox.TextChanged += (_, _) => UpdatePreview(); Controls.Add(nameBox);
        Controls.Add(new Label { Text = "Create in", AutoSize = true, Location = new Point(32, 180) });
        locationLabel.Location = new Point(32, 204); locationLabel.Size = new Size(390, 34); locationLabel.ForeColor = Theme.Muted; locationLabel.AutoEllipsis = true; locationLabel.TextAlign = ContentAlignment.MiddleLeft; Controls.Add(locationLabel);
        var browse = Button("Browse…", new Point(436, 203), new Size(94, 34), false); browse.Click += (_, _) => ChooseParent(); Controls.Add(browse);
        previewLabel.Location = new Point(33, 246); previewLabel.Size = new Size(496, 24); previewLabel.ForeColor = Theme.Muted; previewLabel.AutoEllipsis = true; Controls.Add(previewLabel);
        var cancel = Button("Cancel", new Point(328, 280), new Size(94, 38), false); cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); }; Controls.Add(cancel);
        var create = Button("Create project", new Point(430, 280), new Size(100, 38), true); create.Click += (_, _) => CreateProject(); Controls.Add(create);
        AcceptButton = create; CancelButton = cancel; UpdatePreview();
    }

    private void ChooseParent()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose where new Pi projects should be created", SelectedPath = parentDirectory, UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) { parentDirectory = dialog.SelectedPath; UpdatePreview(); }
    }

    private void UpdatePreview()
    {
        locationLabel.Text = parentDirectory;
        previewLabel.Text = Path.Combine(parentDirectory, string.IsNullOrWhiteSpace(nameBox.Text) ? "my-project" : nameBox.Text.Trim());
    }

    private void CreateProject()
    {
        try { ProjectPath = ProjectWorkspace.Create(nameBox.Text, parentDirectory); DialogResult = DialogResult.OK; Close(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not create project", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private static ModernButton Button(string text, Point location, Size size, bool accent)
        => new() { Text = text, Location = location, Size = size, Radius = 9, NormalColor = accent ? Theme.Accent : Theme.Surface, HoverColor = accent ? Theme.AccentHover : Theme.SurfaceHover, BorderColor = accent ? Theme.Accent : Theme.Border, ForeColor = accent ? Color.White : Theme.Text };
}
