namespace MlaTools;

/// <summary>Dialogue des réglages d'une équipe du Générateur ; ne propose que les réglages disponibles dans le mode courant.</summary>
internal sealed class FormReglageEquipe : FormDialogue
{
    private const string LibelleMinimum = "Étoiles minimum";
    private const string MessageAucunReglage = "Aucun réglage disponible dans ce mode.";
    private const int LargeurChamp = 70;

    private readonly NumericUpDown? _minimum;

    /// <param name="equipe">Index de l'équipe, à partir de 0.</param>
    /// <param name="etoilesMinimum">Minimum courant ; nul quand le réglage n'existe pas dans le mode courant.</param>
    public FormReglageEquipe(int equipe, int? etoilesMinimum)
        : base($"Réglages — Équipe {equipe + 1}", new Size(320, 130))
    {
        var zone = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        if (etoilesMinimum is { } valeur)
        {
            _minimum = new NumericUpDown { Minimum = 0, Maximum = FormOptionsGeneration.EtoilesMinimumMax, Value = valeur, Width = LargeurChamp };
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            OutilsDialogue.AjouterLigne(table, LibelleMinimum, _minimum);
            zone.Controls.Add(table);
        }
        else
        {
            zone.Controls.Add(new Label { Text = MessageAucunReglage, Dock = DockStyle.Top, AutoSize = true, ForeColor = Color.DimGray });
        }
        Controls.Add(zone);
        Controls.Add(ConstruireBoutons(() => DialogResult = DialogResult.OK));
    }

    /// <summary>Minimum saisi ; nul quand le réglage n'était pas proposé.</summary>
    public int? EtoilesMinimum => _minimum is null ? null : (int)_minimum.Value;
}
