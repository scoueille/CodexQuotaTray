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
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
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

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(0, 6, 12, 0),
            ColumnCount = 3,
            RowCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var saveButton = new Button
        {
            Size = new Size(100, 32),
            Anchor = AnchorStyles.None,
            DialogResult = DialogResult.OK,
            Text = AppText.Get("Dialog.Save")
        };
        var cancelButton = new Button
        {
            Size = new Size(100, 32),
            Anchor = AnchorStyles.None,
            DialogResult = DialogResult.Cancel,
            Text = AppText.Get("Dialog.Cancel")
        };
        // Chaque bouton dispose d'une cellule propre; l'alignement ne repose
        // pas sur une position calculée ni sur l'ordre de flux des contrôles.
        buttons.Controls.Add(cancelButton, 1, 0);
        buttons.Controls.Add(saveButton, 2, 0);

        Controls.Add(buttons);
        // La rangée supérieure est ajoutée avant le pied de page; impose le
        // z-order pour qu'elle ne recouvre jamais les boutons.
        buttons.BringToFront();
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }
}
