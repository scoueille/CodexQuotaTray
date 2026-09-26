namespace CodexQuotaTray;

/// <summary>A compact dialog for the single daily allocation preference.</summary>
internal sealed class SettingsDialog : Form
{
    private readonly NumericUpDown _workDaysInput;

    /// <summary>Renvoie le nombre de jours sélectionné dans la boîte de dialogue.</summary>
    public int WorkDaysPerWeek => decimal.ToInt32(_workDaysInput.Value);

    /// <summary>Construit la boîte de dialogue de réglage du nombre de jours de travail.</summary>
    public SettingsDialog(int workDaysPerWeek)
    {
        Text = AppText.Get("Dialog.SettingsTitle");
        ClientSize = new Size(590, 145);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;

        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Location = new Point(20, 30),
            WrapContents = false
        };
        row.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0),
            Text = AppText.Get("Settings.WorkDaysPrefix")
        });

        _workDaysInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 7,
            Increment = 1,
            Value = Math.Clamp(workDaysPerWeek, 1, 7),
            TextAlign = HorizontalAlignment.Center,
            Width = 58,
            Margin = new Padding(0, 1, 8, 0)
        };
        row.Controls.Add(_workDaysInput);
        row.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
            Text = AppText.Get("Settings.WorkDaysSuffix")
        });
        Controls.Add(row);

        var buttons = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(0, 0, 12, 0)
        };
        var saveButton = new Button
        {
            Size = new Size(100, 32),
            DialogResult = DialogResult.OK,
            Text = AppText.Get("Dialog.Save")
        };
        var cancelButton = new Button
        {
            Size = new Size(100, 32),
            DialogResult = DialogResult.Cancel,
            Text = AppText.Get("Dialog.Cancel")
        };
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        buttons.Resize += (_, _) => PositionButtons();
        Controls.Add(buttons);
        PositionButtons();
        AcceptButton = saveButton;
        CancelButton = cancelButton;

        // Aligne les boutons en bas à droite de la boîte de dialogue.
        void PositionButtons()
        {
            saveButton.Location = new Point(buttons.ClientSize.Width - buttons.Padding.Right - saveButton.Width, 6);
            cancelButton.Location = new Point(saveButton.Left - cancelButton.Width - 8, 6);
        }
    }
}
