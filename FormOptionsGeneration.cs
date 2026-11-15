namespace MlaTools;

/// <summary>Dialogue des réglages rarement touchés de la génération : les poids des trois critères du moteur.</summary>
internal sealed class FormOptionsGeneration : FormDialogue
{
    public const int NombreEquipesMax = 20;
    public const int PoidsMax = 1000;
    public const int EtoilesMinimumMax = 500;

    // Dans l'ordre de ModeGeneration : l'index d'un libellé est la valeur du mode.
    public static readonly string[] LibellesModes = ["Meilleure équipe d'abord", "Équipes équilibrées (somme maximale)"];

    private readonly NumericUpDown _poidsRoles = ChampPoids();

    private readonly NumericUpDown _poidsResonance = ChampPoids();

    private readonly NumericUpDown _poidsEtoiles = ChampPoids();

    public FormOptionsGeneration(PoidsEquipes poids)
        : base("Options de génération", new Size(400, 170))
    {
        _poidsRoles.Value = Borner(poids.Roles);
        _poidsResonance.Value = Borner(poids.Resonance);
        _poidsEtoiles.Value = Borner(poids.Etoiles);

        Controls.Add(ConstruireChamps());
        Controls.Add(ConstruireBoutons(() => DialogResult = DialogResult.OK));
    }

    public PoidsEquipes Poids => new((double)_poidsRoles.Value, (double)_poidsResonance.Value, (double)_poidsEtoiles.Value);

    private static NumericUpDown ChampPoids() => new() { Minimum = 0, Maximum = PoidsMax, Width = 80 };

    private static decimal Borner(double poids) => Math.Clamp((decimal)poids, 0, PoidsMax);

    private TableLayoutPanel ConstruireChamps()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(14) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        OutilsDialogue.AjouterLigne(table, "Poids des rôles", _poidsRoles);
        OutilsDialogue.AjouterLigne(table, "Poids de la résonance", _poidsResonance);
        OutilsDialogue.AjouterLigne(table, "Poids des étoiles", _poidsEtoiles);
        return table;
    }
}
