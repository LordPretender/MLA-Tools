namespace MlaTools;

/// <summary>Dialogue des réglages globaux de la génération, un onglet par thème : poids du score, valeur des héros Chaos. Les réglages propres à une composition sont dans <see cref="FormReglageComposition"/>.</summary>
internal sealed class FormOptionsGeneration : FormDialogue
{
    public const int NombreEquipesMax = 20;
    public const int PoidsMax = 1000;
    public const int EtoilesMinimumMax = 500;

    // Dans l'ordre de ModeGeneration : l'index d'un libellé est la valeur du mode.
    public static readonly string[] LibellesModes = ["Meilleure équipe d'abord", "Équipes équilibrées (somme maximale)"];

    private const string OngletScore = "Score";
    private const string OngletChaos = "Héros Chaos";
    private const string LibelleValeursParDefaut = "Valeurs par défaut";
    private const int LargeurChamp = 80;
    public const bool ChaosAuNiveauMoyenParDefaut = true;

    private readonly NumericUpDown _poidsRoles = ChampPoids();

    private readonly NumericUpDown _poidsResonance = ChampPoids();

    private readonly NumericUpDown _poidsEtoiles = ChampPoids();

    private readonly NumericUpDown _poidsChaos1 = ChampPoids();

    private readonly NumericUpDown _poidsChaos2 = ChampPoids();

    private readonly NumericUpDown _poidsChaos3 = ChampPoids();

    private readonly CheckBox _chaosAuNiveauMoyen = new() { AutoSize = true };

    public FormOptionsGeneration(PoidsEquipes poids, bool chaosAuNiveauMoyen)
        : base("Options de génération", new Size(440, 360))
    {
        Afficher(poids, chaosAuNiveauMoyen);

        var onglets = new TabControl { Dock = DockStyle.Fill };
        onglets.TabPages.Add(ConstruireOngletScore());
        onglets.TabPages.Add(ConstruireOngletChaos());

        var boutons = ConstruireBoutons(() => DialogResult = DialogResult.OK);
        var parDefaut = new Button { Text = LibelleValeursParDefaut, AutoSize = true };
        parDefaut.Click += (_, _) => Afficher(PoidsEquipes.ParDefaut, ChaosAuNiveauMoyenParDefaut);
        boutons.Controls.Add(parDefaut);
        Controls.Add(onglets);
        Controls.Add(boutons);
    }

    public PoidsEquipes Poids => new(
        (double)_poidsRoles.Value, (double)_poidsResonance.Value, (double)_poidsEtoiles.Value,
        (double)_poidsChaos1.Value, (double)_poidsChaos2.Value, (double)_poidsChaos3.Value);

    /// <summary>Vrai : un héros Chaos sous la moyenne d'étoiles du reste de son équipe perd son bonus.</summary>
    public bool ChaosAuNiveauMoyen => _chaosAuNiveauMoyen.Checked;

    private static NumericUpDown ChampPoids() => new() { Minimum = 0, Maximum = PoidsMax, Width = LargeurChamp };

    private static decimal Borner(double poids) => Math.Clamp((decimal)poids, 0, PoidsMax);

    private void Afficher(PoidsEquipes poids, bool chaosAuNiveauMoyen)
    {
        _chaosAuNiveauMoyen.Checked = chaosAuNiveauMoyen;
        _poidsRoles.Value = Borner(poids.Roles);
        _poidsResonance.Value = Borner(poids.Resonance);
        _poidsEtoiles.Value = Borner(poids.Etoiles);
        _poidsChaos1.Value = Borner(poids.Chaos1);
        _poidsChaos2.Value = Borner(poids.Chaos2);
        _poidsChaos3.Value = Borner(poids.Chaos3);
    }

    private TabPage ConstruireOngletScore() => OutilsDialogue.PageChamps(OngletScore, table =>
    {
        OutilsDialogue.AjouterChampAvecAide(table, "Poids des rôles", _poidsRoles, "Coût d'une composition de rôles qui s'écarte de l'idéal (DPS, tanks, soutiens) : plus il est haut, plus la répartition des rôles prime.");
        OutilsDialogue.AjouterChampAvecAide(table, "Poids de la résonance", _poidsResonance, "Valeur des points de résonance (factions) de l'équipe.");
        OutilsDialogue.AjouterChampAvecAide(table, "Poids des étoiles", _poidsEtoiles, "Valeur de chaque étoile des héros de l'équipe.");
    });

    private TabPage ConstruireOngletChaos() => OutilsDialogue.PageChamps(OngletChaos, table =>
    {
        OutilsDialogue.AjouterChampAvecAide(table, "Valeur du 1er héros Chaos", _poidsChaos1, "Bonus d'une équipe qui compte un héros Chaos, dans l'unité des points de résonance.");
        OutilsDialogue.AjouterChampAvecAide(table, "Valeur du 2e héros Chaos", _poidsChaos2, "Bonus ajouté par un deuxième héros Chaos dans la même équipe.");
        OutilsDialogue.AjouterChampAvecAide(table, "Valeur du 3e héros Chaos", _poidsChaos3, "Bonus ajouté par un troisième héros Chaos ; au-delà, plus de bonus.");
        OutilsDialogue.AjouterChampAvecAide(table, "Chaos au moins au niveau moyen de l'équipe", _chaosAuNiveauMoyen, "Un héros Chaos dont les étoiles sont sous la moyenne des autres héros de l'équipe ne donne plus de bonus. Décocher pour que tout héros Chaos compte.");
    });
}
