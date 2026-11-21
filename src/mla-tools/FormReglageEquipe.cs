using static MlaTools.Constantes;

namespace MlaTools;

/// <summary>Dialogue des réglages d'une équipe du Générateur, un onglet par critère ; l'étoile minimum n'existe que dans le mode « meilleure équipe d'abord ».</summary>
internal sealed class FormReglageEquipe : FormDialogue
{
    private const string LibelleMinimum = "Étoiles minimum";
    private const string MessageAucunReglage = "Aucun réglage disponible dans ce mode.";
    private const string MessageBientot = "Pas encore disponible.";
    private const string OngletHeros = "Héros";
    private const string OngletRoles = "Rôles";
    private const string OngletFactions = "Factions";
    private const string OngletEquipement = "Équipement";
    private const string OngletEtoiles = "Étoiles";
    private const string LibelleReinitialiser = "Réinitialiser";
    private const int LargeurChamp = 70;

    private readonly int _equipe;
    private readonly IReadOnlyList<HeroStocke> _heros;
    private readonly NumericUpDown? _minimum;
    private readonly ListBox _imposes = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ListBox _bannis = new() { Dock = DockStyle.Fill, IntegralHeight = false };

    /// <param name="equipe">Index de l'équipe, à partir de 0.</param>
    /// <param name="etoilesMinimum">Minimum courant ; nul quand le réglage n'existe pas dans le mode courant.</param>
    /// <param name="reglages">Réglages de toutes les équipes ; le dialogue travaille sur une copie, rendue par <see cref="Reglages"/>.</param>
    /// <param name="heros">Héros de la base, pour nommer et choisir les héros imposés ou bannis.</param>
    public FormReglageEquipe(int equipe, int? etoilesMinimum, ReglagesEquipes reglages, IReadOnlyList<HeroStocke> heros)
        : base($"Réglages — Équipe {equipe + 1}", new Size(460, 380))
    {
        _equipe = equipe;
        _heros = heros;
        Reglages = reglages.Copier();

        var onglets = new TabControl { Dock = DockStyle.Fill };
        onglets.TabPages.Add(ConstruireOngletHeros());
        onglets.TabPages.Add(ConstruireOngletVide(OngletRoles));
        onglets.TabPages.Add(ConstruireOngletVide(OngletFactions));
        onglets.TabPages.Add(ConstruireOngletVide(OngletEquipement));
        if (etoilesMinimum is { } valeur)
        {
            _minimum = new NumericUpDown { Minimum = 0, Maximum = FormOptionsGeneration.EtoilesMinimumMax, Value = valeur, Width = LargeurChamp };
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            OutilsDialogue.AjouterLigne(table, LibelleMinimum, _minimum);
            onglets.TabPages.Add(ConstruireOnglet(OngletEtoiles, table));
        }
        else
        {
            onglets.TabPages.Add(ConstruireOnglet(OngletEtoiles, new Label { Text = MessageAucunReglage, Dock = DockStyle.Top, AutoSize = true, ForeColor = Color.DimGray }));
        }

        var boutons = ConstruireBoutons(() => DialogResult = DialogResult.OK);
        var reinitialiser = new Button { Text = LibelleReinitialiser, AutoSize = true };
        reinitialiser.Click += (_, _) => Reinitialiser();
        boutons.Controls.Add(reinitialiser);
        Controls.Add(onglets);
        Controls.Add(boutons);
        RemplirListes();
    }

    /// <summary>Réglages de toutes les équipes tels que saisis.</summary>
    public ReglagesEquipes Reglages { get; }

    /// <summary>Minimum saisi ; nul quand le réglage n'était pas proposé.</summary>
    public int? EtoilesMinimum => _minimum is null ? null : (int)_minimum.Value;

    private static TabPage ConstruireOnglet(string titre, Control contenu)
    {
        var page = new TabPage(titre) { Padding = new Padding(10) };
        page.Controls.Add(contenu);
        return page;
    }

    private static TabPage ConstruireOngletVide(string titre) =>
        ConstruireOnglet(titre, new Label { Text = MessageBientot, Dock = DockStyle.Top, AutoSize = true, ForeColor = Color.DimGray });

    private TabPage ConstruireOngletHeros()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        table.Controls.Add(new Label { Text = "Héros imposés", AutoSize = true }, 0, 0);
        table.Controls.Add(new Label { Text = "Héros bannis", AutoSize = true }, 1, 0);
        table.Controls.Add(_imposes, 0, 1);
        table.Controls.Add(_bannis, 1, 1);
        table.Controls.Add(BarreListe(AjouterImpose, () => Retirer(_imposes, Reglages.RetirerImpose)), 0, 2);
        table.Controls.Add(BarreListe(AjouterBanni, () => Retirer(_bannis, Reglages.RetirerBanni)), 1, 2);
        return ConstruireOnglet(OngletHeros, table);
    }

    private static FlowLayoutPanel BarreListe(Action ajouter, Action retirer)
    {
        var barre = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var boutonAjouter = new Button { Text = "Ajouter", AutoSize = true };
        var boutonRetirer = new Button { Text = "Retirer", AutoSize = true };
        boutonAjouter.Click += (_, _) => ajouter();
        boutonRetirer.Click += (_, _) => retirer();
        barre.Controls.Add(boutonAjouter);
        barre.Controls.Add(boutonRetirer);
        return barre;
    }

    private void AjouterImpose() => Ajouter("Imposer un héros", Reglages.Imposes(_equipe), Reglages.Imposer, "Ce héros est déjà imposé à une autre équipe ou banni ici.");

    private void AjouterBanni() => Ajouter("Bannir un héros", Reglages.Bannis(_equipe), Reglages.Bannir, "Ce héros est imposé dans cette équipe.");

    private void Ajouter(string titre, IReadOnlyCollection<int> dejaChoisis, Func<int, int, bool> enregistrer, string messageRefus)
    {
        var candidats = _heros.Where(h => !dejaChoisis.Contains(h.Id)).OrderBy(h => h.Hero.Nom, StringComparer.CurrentCultureIgnoreCase).ToList();
        using var choix = new FormChoixHero(titre, candidats);
        if (choix.ShowDialog(this) != DialogResult.OK || choix.Resultat is not { } id)
        {
            return;
        }
        if (!enregistrer(_equipe, id))
        {
            MessageBox.Show(this, messageRefus, TitreApplication, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        RemplirListes();
    }

    private void Retirer(ListBox liste, Action<int, int> retirer)
    {
        if (liste.SelectedItem is not Entree entree)
        {
            return;
        }
        retirer(_equipe, entree.Id);
        RemplirListes();
    }

    private void Reinitialiser()
    {
        Reglages.Reinitialiser(_equipe);
        if (_minimum is not null)
        {
            _minimum.Value = 0;
        }
        RemplirListes();
    }

    private void RemplirListes()
    {
        Remplir(_imposes, Reglages.Imposes(_equipe));
        Remplir(_bannis, Reglages.Bannis(_equipe));
    }

    private void Remplir(ListBox liste, IEnumerable<int> ids)
    {
        liste.Items.Clear();
        liste.Items.AddRange([.. ids
            .Select(id => new Entree(id, _heros.FirstOrDefault(h => h.Id == id)?.Hero.Nom ?? $"#{id}"))
            .OrderBy(e => e.Nom, StringComparer.CurrentCultureIgnoreCase)]);
    }

    private sealed record Entree(int Id, string Nom)
    {
        public override string ToString() => Nom;
    }
}
