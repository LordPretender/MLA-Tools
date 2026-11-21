namespace MlaTools;

/// <summary>Dialogue des réglages globaux de la génération, un onglet par thème : poids du score, valeur des héros Chaos, contraintes valables pour toutes les équipes.</summary>
internal sealed class FormOptionsGeneration : FormDialogue
{
    public const int NombreEquipesMax = 20;
    public const int PoidsMax = 1000;
    public const int EtoilesMinimumMax = 500;

    // Dans l'ordre de ModeGeneration : l'index d'un libellé est la valeur du mode.
    public static readonly string[] LibellesModes = ["Meilleure équipe d'abord", "Équipes équilibrées (somme maximale)"];

    private const string OngletScore = "Score";
    private const string OngletChaos = "Héros Chaos";
    private const string OngletContraintes = "Contraintes";
    private const string LibelleValeursParDefaut = "Valeurs par défaut";
    private const int LargeurChamp = 80;
    private const int LargeurAide = 380;

    private readonly NumericUpDown _poidsRoles = ChampPoids();

    private readonly NumericUpDown _poidsResonance = ChampPoids();

    private readonly NumericUpDown _poidsEtoiles = ChampPoids();

    private readonly NumericUpDown _poidsChaos1 = ChampPoids();

    private readonly NumericUpDown _poidsChaos2 = ChampPoids();

    private readonly NumericUpDown _poidsChaos3 = ChampPoids();

    private readonly NumericUpDown _etoilesMiniHeros = new() { Minimum = 0, Maximum = EtoilesMinimumMax, Width = LargeurChamp };

    public FormOptionsGeneration(PoidsEquipes poids, int etoilesMiniHeros)
        : base("Options de génération", new Size(440, 360))
    {
        Afficher(poids, etoilesMiniHeros);

        var onglets = new TabControl { Dock = DockStyle.Fill };
        onglets.TabPages.Add(ConstruireOngletScore());
        onglets.TabPages.Add(ConstruireOngletChaos());
        onglets.TabPages.Add(ConstruireOngletContraintes());

        var boutons = ConstruireBoutons(() => DialogResult = DialogResult.OK);
        var parDefaut = new Button { Text = LibelleValeursParDefaut, AutoSize = true };
        parDefaut.Click += (_, _) => Afficher(PoidsEquipes.ParDefaut, 0);
        boutons.Controls.Add(parDefaut);
        Controls.Add(onglets);
        Controls.Add(boutons);
    }

    public PoidsEquipes Poids => new(
        (double)_poidsRoles.Value, (double)_poidsResonance.Value, (double)_poidsEtoiles.Value,
        (double)_poidsChaos1.Value, (double)_poidsChaos2.Value, (double)_poidsChaos3.Value);

    /// <summary>Seuil d'étoiles par héros pour toutes les équipes ; 0 = aucun.</summary>
    public int EtoilesMiniHeros => (int)_etoilesMiniHeros.Value;

    private static NumericUpDown ChampPoids() => new() { Minimum = 0, Maximum = PoidsMax, Width = LargeurChamp };

    private static decimal Borner(double poids) => Math.Clamp((decimal)poids, 0, PoidsMax);

    private static TabPage ConstruireOnglet(string titre, Action<TableLayoutPanel> remplir)
    {
        var table = OutilsDialogue.TableChamps(10);
        remplir(table);
        var page = new TabPage(titre);
        page.Controls.Add(table);
        return page;
    }

    // L'aide reste visible sous son champ : un poids sans explication ne se règle qu'à tâtons.
    private static void AjouterChamp(TableLayoutPanel table, string titre, NumericUpDown champ, string aide)
    {
        OutilsDialogue.AjouterLigne(table, titre, champ);
        var texte = new Label { Text = aide, AutoSize = true, MaximumSize = new Size(LargeurAide, 0), ForeColor = Color.DimGray, Margin = new Padding(3, 0, 3, 12) };
        table.Controls.Add(texte);
        table.SetColumnSpan(texte, 2);
    }

    private void Afficher(PoidsEquipes poids, int etoilesMiniHeros)
    {
        _poidsRoles.Value = Borner(poids.Roles);
        _poidsResonance.Value = Borner(poids.Resonance);
        _poidsEtoiles.Value = Borner(poids.Etoiles);
        _poidsChaos1.Value = Borner(poids.Chaos1);
        _poidsChaos2.Value = Borner(poids.Chaos2);
        _poidsChaos3.Value = Borner(poids.Chaos3);
        _etoilesMiniHeros.Value = Math.Clamp(etoilesMiniHeros, 0, EtoilesMinimumMax);
    }

    private TabPage ConstruireOngletScore() => ConstruireOnglet(OngletScore, table =>
    {
        AjouterChamp(table, "Poids des rôles", _poidsRoles, "Coût d'une composition de rôles qui s'écarte de l'idéal (DPS, tanks, soutiens) : plus il est haut, plus la répartition des rôles prime.");
        AjouterChamp(table, "Poids de la résonance", _poidsResonance, "Valeur des points de résonance (factions) de l'équipe.");
        AjouterChamp(table, "Poids des étoiles", _poidsEtoiles, "Valeur de chaque étoile des héros de l'équipe.");
    });

    private TabPage ConstruireOngletChaos() => ConstruireOnglet(OngletChaos, table =>
    {
        AjouterChamp(table, "Valeur du 1er héros Chaos", _poidsChaos1, "Bonus d'une équipe qui compte un héros Chaos, dans l'unité des points de résonance.");
        AjouterChamp(table, "Valeur du 2e héros Chaos", _poidsChaos2, "Bonus ajouté par un deuxième héros Chaos dans la même équipe.");
        AjouterChamp(table, "Valeur du 3e héros Chaos", _poidsChaos3, "Bonus ajouté par un troisième héros Chaos ; au-delà, plus de bonus.");
    });

    private TabPage ConstruireOngletContraintes() => ConstruireOnglet(OngletContraintes, table =>
        AjouterChamp(table, "Étoiles mini pour les héros", _etoilesMiniHeros, "Aucun héros d'une équipe n'a moins d'étoiles que ce seuil (0 = aucun seuil). Une équipe peut le relever dans ses propres réglages, jamais le baisser."));
}
