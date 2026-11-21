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

    private readonly NumericUpDown _poidsChaos1 = ChampPoids();

    private readonly NumericUpDown _poidsChaos2 = ChampPoids();

    private readonly NumericUpDown _poidsChaos3 = ChampPoids();

    public FormOptionsGeneration(PoidsEquipes poids)
        : base("Options de génération", new Size(400, 270))
    {
        _poidsRoles.Value = Borner(poids.Roles);
        _poidsResonance.Value = Borner(poids.Resonance);
        _poidsEtoiles.Value = Borner(poids.Etoiles);
        _poidsChaos1.Value = Borner(poids.Chaos1);
        _poidsChaos2.Value = Borner(poids.Chaos2);
        _poidsChaos3.Value = Borner(poids.Chaos3);

        Controls.Add(ConstruireChamps());
        Controls.Add(ConstruireBoutons(() => DialogResult = DialogResult.OK));
    }

    public PoidsEquipes Poids => new(
        (double)_poidsRoles.Value, (double)_poidsResonance.Value, (double)_poidsEtoiles.Value,
        (double)_poidsChaos1.Value, (double)_poidsChaos2.Value, (double)_poidsChaos3.Value);

    private static NumericUpDown ChampPoids() => new() { Minimum = 0, Maximum = PoidsMax, Width = 80 };

    private static decimal Borner(double poids) => Math.Clamp((decimal)poids, 0, PoidsMax);

    private TableLayoutPanel ConstruireChamps()
    {
        var table = OutilsDialogue.TableChamps(14);


        OutilsDialogue.AjouterLigne(table, "Poids des rôles", _poidsRoles);
        OutilsDialogue.AjouterLigne(table, "Poids de la résonance", _poidsResonance);
        OutilsDialogue.AjouterLigne(table, "Poids des étoiles", _poidsEtoiles);
        OutilsDialogue.AjouterLigne(table, "Valeur du 1er héros Chaos", _poidsChaos1);
        OutilsDialogue.AjouterLigne(table, "Valeur du 2e héros Chaos", _poidsChaos2);
        OutilsDialogue.AjouterLigne(table, "Valeur du 3e héros Chaos", _poidsChaos3);
        return table;
    }
}
