namespace MlaTools;

/// <summary>Onglet « Équipes » : compositions enregistrées, affichées en cartes comme dans le Générateur.</summary>
internal sealed class PanneauCompositions : UserControl
{
    private const string SuffixeCopie = " (copie)";
    private const string MessageNomPris = "Une composition porte déjà ce nom.";
    private const string MessageVide = "Aucune composition à afficher. Nouvelle composition… ou Générateur > Enregistrer la composition en crée une.";
    private const int LargeurCombo = 260;

    private readonly string _chemin;
    private readonly Func<Action, bool> _proteger;
    private readonly CartesEquipes _cartes;

    private readonly ComboBox _liste = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = LargeurCombo, DisplayMember = nameof(CompositionStockee.Nom) };
    private readonly Panel _zoneCartes = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly Panel _zoneReserve = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly ContextMenuStrip _menuEquipe = new();
    private readonly Button _nouvelle = new() { Text = "Nouvelle…", AutoSize = true };
    private readonly Button _renommer = new() { Text = "Renommer…", AutoSize = true };
    private readonly Button _dupliquer = new() { Text = "Dupliquer…", AutoSize = true };
    private readonly Button _supprimer = new() { Text = "Supprimer", AutoSize = true };

    private IReadOnlyDictionary<int, HeroStocke> _heros = new Dictionary<int, HeroStocke>();
    private List<List<int>> _equipes = [];
    private List<int> _reserve = [];
    private int _colonnes = 1;
    private int? _selection;
    private int _nombreEquipesDefaut;
    private bool _enMiseAJour;

    /// <param name="proteger">Exécute un accès à la base en signalant l'erreur à l'utilisateur ; retourne false en cas d'échec.</param>
    public PanneauCompositions(string chemin, Font police, Font gras, Func<Action, bool> proteger)
    {
        _chemin = chemin;
        _proteger = proteger;
        _cartes = new CartesEquipes(police, gras) { MenuEquipe = _menuEquipe, SurClic = Cliquer, SurAjout = Ajouter };
        Dock = DockStyle.Fill;

        _menuEquipe.Items.Add(new ToolStripMenuItem(CartesEquipes.LibelleRetrait, null, (_, _) =>
        {
            if (_menuEquipe.SourceControl is LigneHero ligne)
            {
                Retirer(ligne.HeroId);
            }
        }));
        _liste.SelectedIndexChanged += (_, _) => ChangerSelection();
        _nouvelle.Click += (_, _) => Creer();
        _renommer.Click += (_, _) => Renommer();
        _dupliquer.Click += (_, _) => Dupliquer();
        _supprimer.Click += (_, _) => Supprimer();
        _zoneCartes.Resize += (_, _) =>
        {
            if (CartesEquipes.Colonnes(_zoneCartes) != _colonnes)
            {
                Afficher();
            }
        };

        _liste.Margin = new Padding(3, 4, 12, 3);
        var barre = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        barre.Controls.AddRange([_liste, _nouvelle, _renommer, _dupliquer, _supprimer]);

        var contenu = new Panel { Dock = DockStyle.Fill };
        contenu.Controls.Add(_zoneCartes);
        contenu.Controls.Add(CartesEquipes.CreerCadreReserve(_zoneReserve));

        Controls.Add(contenu);
        Controls.Add(barre);
    }

    /// <summary>Relit la liste en base ; garde la composition choisie si elle existe encore.</summary>
    /// <param name="nombreEquipesDefaut">Nombre d'équipes proposé à la création d'une composition.</param>
    public void Recharger(MoteurEquipes moteur, IReadOnlyDictionary<int, HeroStocke> heros, int nombreEquipesDefaut)
    {
        _cartes.Moteur = moteur;
        _cartes.Heros = heros;
        _heros = heros;
        _nombreEquipesDefaut = nombreEquipesDefaut;

        var idChoisi = (_liste.SelectedItem as CompositionStockee)?.Id;
        IReadOnlyList<CompositionStockee> compositions = [];
        if (!_proteger(() => compositions = BaseSqlite.ListerCompositions(_chemin)))
        {
            return;
        }

        _enMiseAJour = true;
        try
        {
            _liste.Items.Clear();
            _liste.Items.AddRange([.. compositions]);
            _liste.SelectedItem = compositions.FirstOrDefault(c => c.Id == idChoisi) ?? compositions.FirstOrDefault();
        }
        finally
        {
            _enMiseAJour = false;
        }
        ChangerSelection();
    }

    private CompositionStockee? Choisie => _liste.SelectedItem as CompositionStockee;

    private void ChangerSelection()
    {
        if (_enMiseAJour)
        {
            return;
        }

        var choisie = Choisie;
        _renommer.Enabled = _dupliquer.Enabled = _supprimer.Enabled = choisie is not null;
        _equipes = [];
        _reserve = [];
        _selection = null;
        if (choisie is not null)
        {
            IReadOnlyList<Affectation> affectations = [];
            if (_proteger(() => affectations = BaseSqlite.LireComposition(choisie.Id, _chemin)))
            {
                Reconstruire(affectations, choisie.NombreEquipes);
            }
        }
        Afficher();
    }

    // Un héros supprimé depuis l'enregistrement est ignoré : la composition s'affiche sans lui.
    private void Reconstruire(IReadOnlyList<Affectation> affectations, int nombreEquipes)
    {
        (_equipes, _reserve) = Affectation.Repartir(affectations, _heros.ContainsKey, nombreEquipes);
        // Repartir ne crée aucune équipe quand rien n'est placé ; une composition neuve garde pourtant les siennes, vides.
        while (_equipes.Count < nombreEquipes)
        {
            _equipes.Add([]);
        }
    }

    private void Afficher()
    {
        _cartes.Selection = _selection;
        _colonnes = _cartes.Afficher(_zoneCartes, _zoneReserve, _equipes, _reserve, MessageVide);
    }

    private void Relire() => Recharger(_cartes.Moteur, _heros, _nombreEquipesDefaut);

    private void Creer()
    {
        using var formulaire = new FormSaisieNom("Nouvelle composition", string.Empty, MessageNomPris,
            nom => BaseSqlite.NomCompositionPris(nom, null, _chemin), _nombreEquipesDefaut);
        if (formulaire.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        // Tous les héros de la base partent en réserve, les mieux notés en tête.
        var reserve = _heros.Values.OrderByDescending(h => h.Hero.Etoiles).ThenBy(h => h.Hero.Nom, StringComparer.CurrentCultureIgnoreCase)
            .Select((h, position) => new Affectation(h.Id, null, position)).ToList();
        var idNouvelle = 0;
        if (_proteger(() => idNouvelle = BaseSqlite.CreerComposition(formulaire.Resultat!, reserve, formulaire.NombreEquipes, _chemin)))
        {
            Relire();
            _liste.SelectedItem = _liste.Items.Cast<CompositionStockee>().FirstOrDefault(c => c.Id == idNouvelle);
        }
    }

    private void Cliquer(int id)
    {
        if (Affectation.BasculerSelection(ref _selection, _equipes, _reserve, id))
        {
            Enregistrer();
        }
        // L'affichage détruit la ligne cliquée : on le reporte après la fin de son gestionnaire.
        BeginInvoke(new Action(Afficher));
    }

    private void Retirer(int id)
    {
        if (!Affectation.EnvoyerEnReserve(_equipes, _reserve, id))
        {
            return;
        }

        _selection = null;
        Enregistrer();
        BeginInvoke(new Action(Afficher));
    }

    private void Ajouter(int index)
    {
        // Un héros déjà dans une équipe n'est pas proposé : il se déplace par l'échange, pour n'apparaître qu'une fois.
        var candidats = _heros.Values.Where(h => !_equipes.Any(e => e.Contains(h.Id)))
            .OrderBy(h => h.Hero.Nom, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (candidats.Count == 0)
        {
            MessageBox.Show(this, "Aucun héros disponible : tous sont déjà dans une équipe.", Constantes.TitreApplication);
            return;
        }

        using var formulaire = new FormChoixHero($"Ajouter à l'équipe {index + 1}", candidats);
        if (formulaire.ShowDialog(this) != DialogResult.OK || formulaire.Resultat is not { } id)
        {
            return;
        }

        _reserve.Remove(id);
        _equipes[index].Add(id);
        _selection = null;
        Enregistrer();
        BeginInvoke(new Action(Afficher));
    }

    // En cas d'échec, la composition est relue en base : l'écran ne doit pas montrer un état qui n'est pas enregistré.
    private void Enregistrer()
    {
        if (Choisie is { } composition
            && !_proteger(() => BaseSqlite.RemplacerComposition(composition.Id, Affectation.Aplatir(_equipes, _reserve), _chemin)))
        {
            ChangerSelection();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _menuEquipe.Dispose();
        }
        base.Dispose(disposing);
    }

    private void Renommer()
    {
        if (Choisie is not { } composition)
        {
            return;
        }

        using var formulaire = new FormSaisieNom("Renommer la composition", composition.Nom, MessageNomPris,
            nom => BaseSqlite.NomCompositionPris(nom, composition.Id, _chemin));
        if (formulaire.ShowDialog(this) == DialogResult.OK && _proteger(() => BaseSqlite.RenommerComposition(composition.Id, formulaire.Resultat!, _chemin)))
        {
            Relire();
        }
    }

    private void Dupliquer()
    {
        if (Choisie is not { } composition)
        {
            return;
        }

        using var formulaire = new FormSaisieNom("Dupliquer la composition", composition.Nom + SuffixeCopie, MessageNomPris,
            nom => BaseSqlite.NomCompositionPris(nom, null, _chemin));
        if (formulaire.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var idCopie = 0;
        if (_proteger(() => idCopie = BaseSqlite.DupliquerComposition(composition.Id, formulaire.Resultat!, _chemin)))
        {
            Relire();
            _liste.SelectedItem = _liste.Items.Cast<CompositionStockee>().FirstOrDefault(c => c.Id == idCopie);
        }
    }

    private void Supprimer()
    {
        if (Choisie is not { } composition)
        {
            return;
        }

        var reponse = MessageBox.Show(this, $"Supprimer la composition « {composition.Nom} » ? Cette action est définitive.",
            Constantes.TitreApplication, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (reponse == DialogResult.Yes && _proteger(() => BaseSqlite.SupprimerComposition(composition.Id, _chemin)))
        {
            Relire();
        }
    }
}
