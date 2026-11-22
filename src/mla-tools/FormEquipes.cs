using System.ComponentModel;
using System.Globalization;
using static MlaTools.Constantes;

namespace MlaTools;

/// <summary>Fenêtre principale : héros et équipes de la base ; la capture et la lecture OCR s'ouvrent depuis le menu Fichier.</summary>
internal sealed class FormEquipes : FormBase
{
    private const int DureeAvisMs = 4000;
    private const string ColNom = "Nom";
    private const string ColEtoiles = "Etoiles";
    private const string ColClasse = "Classe";
    private const string ColRole = "Role";
    private const string ColFactions = "Factions";
    private const string ColLienAme = "LienAme";
    private const string ColEquipement = "Equipement";
    private const string ColDateMaj = "DateMaj";
    private const string FlecheCroissant = " ▲";
    private const string FlecheDecroissant = " ▼";
    private const string TitreLienAme = "Lien d'âme";
    private const string TousLiensAme = "Tous";
    private const string AvecLienAme = "Avec lien d'âme";
    private const string SansLienAme = "Sans lien d'âme";
    private const string SeparateurFactions = ", ";
    private const string CleNombreEquipes = "nombre_equipes";
    private const string CleRoles = "poids_roles";
    private const string CleResonance = "poids_resonance";
    private const string CleEtoiles = "poids_etoiles";
    private const string CleChaos1 = "poids_chaos1";
    private const string CleChaos2 = "poids_chaos2";
    private const string CleChaos3 = "poids_chaos3";
    private const string CleMode = "mode_generation";
    private const string CleEtoilesMinimum = "etoiles_min";
    private const char SeparateurMinimums = ',';
    private const string CleHerosImposes = "heros_imposes";
    private const string CleHerosBannis = "heros_bannis";
    private const string CleFactionsImposees = "factions_imposees";
    private const string CleFactionsBannies = "factions_bannies";
    private const string CleEtoilesMiniHeros = "etoiles_mini_heros";
    private const string CleEtoilesMiniHerosGlobal = "etoiles_mini_heros_global";
    private const string CleVariantes = "variantes";
    private const int VariantesParDefaut = 5;
    private const int VariantesMax = 20;
    private const string CleListeVariantes = "variantes_liste";
    private const string CleVarianteChoisie = "variante_choisie";
    private const char SeparateurVariantes = '|';
    private const char SeparateurEquipes = ';';
    private const char SeparateurIds = ',';
    // Identifiant qu'aucun héros ne porte : marque une valeur illisible dans la liste enregistrée.
    private const int IdIllisible = -1;
    private const string SeparateurPoints = " / ";
    private const string SigneAlerte = " ⚠";
    private const string ToutesValeurs = "Toutes";


    private readonly string _chemin = BaseSqlite.CheminParDefaut;

    private readonly Font _police = new(OutilsDialogue.NomPolice, OutilsDialogue.TaillePolice);

    private readonly Font _gras;

    private readonly CartesEquipes _cartes;

    private readonly PanneauCompositions _panneauCompositions;

    private readonly DataGridView _grilleHeros = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        // Seule la colonne du lien d'âme est modifiable : les colonnes de texte se déclarent en lecture seule.
        ReadOnly = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None,
        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(247, 248, 250) },
        RowTemplate = { Height = 28 },
    };

    private readonly TextBox _recherche = new() { Width = 280, PlaceholderText = "Rechercher un héros…" };

    private readonly ComboBox _filtreClasse = NouveauFiltre();

    private readonly ComboBox _filtreRole = NouveauFiltre();

    private readonly ComboBox _filtreFaction = NouveauFiltre();

    private readonly ComboBox _filtreLienAme = NouveauFiltre();

    private readonly Label _libelleMode = new() { Text = "Mode", AutoSize = true, Margin = new Padding(16, 6, 6, 0) };
    private readonly ComboBox _choixMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };

    private readonly Label _avisReinitialisation = new() { Text = "Réglages des équipes réinitialisés", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(16, 8, 3, 0), Visible = false };
    private readonly System.Windows.Forms.Timer _delaiAvis = new() { Interval = DureeAvisMs };

    private readonly ToolStripMenuItem _menuModifier = new("Modifier…") { ShortcutKeys = Keys.F2 };

    private readonly ToolStripMenuItem _menuSupprimer = new("Supprimer") { ShortcutKeyDisplayString = "Suppr" };

    private readonly ContextMenuStrip _menuHeros = new();

    private readonly ContextMenuStrip _menuEquipe = new();

    private readonly NumericUpDown _nombreEquipes = new()
    {
        Minimum = 1,
        Maximum = FormOptionsGeneration.NombreEquipesMax,
        Value = MoteurEquipes.NombreEquipesParDefaut,
        Width = 60,
    };

    private readonly NumericUpDown _nombreVariantes = new()
    {
        Minimum = 1,
        Maximum = VariantesMax,
        Value = VariantesParDefaut,
        Width = 60,
    };

    private readonly ComboBox _choixVariante = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };

    // Visible seulement quand la dernière génération a produit plusieurs variantes.
    private readonly FlowLayoutPanel _barreVariantes = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 0, 8, 4), Visible = false };

    private readonly ToolStripStatusLabel _synthese = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    private readonly ToolStripStatusLabel _affichage = new() { BorderSides = ToolStripStatusLabelBorderSides.Right };

    private readonly Panel _zoneCartes = new() { Dock = DockStyle.Fill, AutoScroll = true };

    private readonly Panel _zoneReserve = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
    };

    private List<HeroStocke> _heros = [];
    private Dictionary<int, HeroStocke> _parId = [];
    private Dictionary<string, string> _roles = [];
    private MoteurEquipes _moteur = new([], new Dictionary<string, string>());
    private PoidsEquipes _poids = PoidsEquipes.ParDefaut;

    private ModeGeneration _mode = ModeGeneration.MeilleureEquipeDAbord;
    // Un minimum par équipe, rang = index ; peut dépasser le nombre d'équipes affichées pour garder les valeurs si on en retire.
    private List<int> _etoilesMinimum = [];
    private ReglagesEquipes _reglages = new();
    private List<List<int>> _equipes = [];
    private List<int> _reserve = [];
    // Variantes de la dernière génération, telles que produites : les échanges manuels ne portent que sur la variante affichée.
    private List<RepartitionEquipes> _variantes = [];
    private int _nombreEquipesPersiste = MoteurEquipes.NombreEquipesParDefaut;
    private int _colonnesAffichees = 1;
    private int? _selection;
    private string _colonneTri = ColNom;
    private ListSortDirection _sensTri = ListSortDirection.Ascending;

    // Évite de régénérer la grille à chaque entrée ajoutée pendant le remplissage des listes de filtres.
    private bool _filtresEnMiseAJour;

    // Rows.Add déclenche CellValueChanged : sans ce drapeau, le remplissage écrirait en base le lien d'âme de chaque ligne.
    private bool _grilleEnRemplissage;

    // Évite qu'un changement de mode venu des options soit réécrit en base par la liste de l'onglet Équipes.
    private bool _modeEnMiseAJour;

    // Poser le nombre d'équipes lu en base déclencherait sinon la réinitialisation des réglages, puis leur écriture avant même qu'ils soient chargés.
    private bool _parametresEnChargement;

    // Remplir la liste des variantes déclencherait sinon un changement de variante, donc un réaffichage et une écriture en base.
    private bool _variantesEnMiseAJour;

    // Lue avec les réglages, appliquée à l'affichage une fois le moteur recréé (le libellé d'une variante l'évalue).
    private int _varianteRestauree;

    public FormEquipes()
    {
        _gras = new Font(_police, FontStyle.Bold);
        _cartes = new CartesEquipes(_police, _gras)
        {
            MenuEquipe = _menuEquipe,
            SurClic = Cliquer,
            EstSousMinimum = EstSousMinimum,
            RaisonInactive = rang => _moteur.RaisonInactive(_reglages.Criteres(rang, _parId.ContainsKey)),
            CriteresNonTenus = (ids, rang) => _reglages.Criteres(rang, _parId.ContainsKey).Ecarts(ids, EtoilesDe, FactionsDe) > 0,
            SurReglage = ReglerEquipe,
            ResumeReglages = ResumeReglages,
        };
        _delaiAvis.Tick += (_, _) =>
        {
            _delaiAvis.Stop();
            _avisReinitialisation.Visible = false;
        };
        Font = _police;
        Text = TitreApplication;
        BackColor = OutilsDialogue.FondFenetre;
        ClientSize = new Size(1100, 600);
        MinimumSize = SizeFromClientSize(new Size(760, 480));

        var onglets = new TabControl { Dock = DockStyle.Fill };
        onglets.TabPages.Add(ConstruireOngletHeros());
        onglets.TabPages.Add(ConstruireOngletEquipes());
        _panneauCompositions = new PanneauCompositions(_chemin, _police, _gras, Proteger);
        var pageCompositions = NouvellePage("Équipes");
        pageCompositions.Controls.Add(_panneauCompositions);
        onglets.TabPages.Add(pageCompositions);
        // Une composition a pu être enregistrée depuis le Générateur depuis la dernière ouverture de l'onglet.
        onglets.SelectedIndexChanged += (_, _) =>
        {
            if (onglets.SelectedTab == pageCompositions)
            {
                _panneauCompositions.Recharger(_moteur, _parId, (int)_nombreEquipes.Value);
            }
        };
        var menu = ConstruireMenu();
        Controls.Add(onglets);
        Controls.Add(ConstruireBarreEtat());
        Controls.Add(menu);
        MainMenuStrip = menu;
        ConstruireMenusContextuels();

        Recharger();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _police.Dispose();
            _gras.Dispose();
            _cartes.Dispose();
            _menuHeros.Dispose();
            _menuEquipe.Dispose();
            _delaiAvis.Dispose();
        }
    }

    private static TabPage NouvellePage(string titre) =>
        new(titre) { UseVisualStyleBackColor = false, BackColor = OutilsDialogue.FondFenetre };

    private MenuStrip ConstruireMenu()
    {
        _menuModifier.Click += (_, _) => ModifierHero();
        _menuSupprimer.Click += (_, _) => SupprimerHero();

        var menu = new MenuStrip();
        menu.Items.Add(Menu("Fichier",
            Entree("Importer depuis le jeu…", OuvrirCapture),
            new ToolStripSeparator(),
            Entree("Quitter", Close)));
        menu.Items.Add(Menu("Héros",
            Entree("Ajouter…", AjouterHero, Keys.Control | Keys.N),
            _menuModifier,
            _menuSupprimer));
        menu.Items.Add(Menu("Générateur",
            Entree("Générer", GenererEquipes, Keys.F5),
            Entree("Options de génération…", OuvrirOptions)));
        menu.Items.Add(Menu("Outils", Entree("Rôle de chaque classe…", OuvrirRolesClasses)));
        menu.Items.Add(Menu("Aide", Entree("À propos", AfficherAPropos)));
        return menu;
    }

    private static ToolStripMenuItem Menu(string titre, params ToolStripItem[] entrees)
    {
        var menu = new ToolStripMenuItem(titre);
        menu.DropDownItems.AddRange(entrees);
        return menu;
    }

    private static ToolStripMenuItem Entree(string titre, Action action, Keys raccourci = Keys.None)
    {
        var entree = new ToolStripMenuItem(titre) { ShortcutKeys = raccourci };
        entree.Click += (_, _) => action();
        return entree;
    }

    private StatusStrip ConstruireBarreEtat()
    {
        var aide = new ToolStripStatusLabel("Clique un héros, puis un second pour les échanger");
        var barre = new StatusStrip();
        barre.Items.Add(_synthese);
        barre.Items.Add(_affichage);
        barre.Items.Add(aide);
        return barre;
    }

    private void ConstruireMenusContextuels()
    {
        _menuHeros.Items.Add(Entree("Modifier…", ModifierHero));
        _menuHeros.Items.Add(Entree("Supprimer", SupprimerHero));
        _menuEquipe.Items.Add(Entree(CartesEquipes.LibelleRetrait, () =>
        {
            if (_menuEquipe.SourceControl is LigneHero ligne)
            {
                EnvoyerEnReserve(ligne.HeroId);
            }
        }));
    }

    private TabPage ConstruireOngletHeros()
    {
        var page = NouvellePage("Héros");
        AjouterColonnesHeros();

        _recherche.TextChanged += (_, _) => RemplirGrilleHeros();
        _filtreLienAme.Items.AddRange([TousLiensAme, AvecLienAme, SansLienAme]);
        _filtreLienAme.SelectedIndex = 0;
        foreach (var filtre in new[] { _filtreClasse, _filtreRole, _filtreFaction, _filtreLienAme })
        {
            filtre.SelectedIndexChanged += (_, _) =>
            {
                if (!_filtresEnMiseAJour)
                {
                    RemplirGrilleHeros();
                }
            };
        }

        var barre = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        barre.Controls.Add(_recherche);
        AjouterFiltre(barre, "Classe", _filtreClasse);
        AjouterFiltre(barre, "Rôle", _filtreRole);
        AjouterFiltre(barre, "Faction", _filtreFaction);
        AjouterFiltre(barre, TitreLienAme, _filtreLienAme);

        AbonnerGrilleHeros();
        page.Controls.Add(_grilleHeros);
        page.Controls.Add(barre);
        return page;
    }

    private void AbonnerGrilleHeros()
    {
        _grilleHeros.ColumnHeaderMouseClick += (_, e) => TrierPar(_grilleHeros.Columns[e.ColumnIndex].Name);
        // Une case cochée reste « en cours d'édition » jusqu'à la perte du focus : on valide dès le clic.
        _grilleHeros.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grilleHeros.IsCurrentCellDirty && _grilleHeros.CurrentCell?.OwningColumn?.Name == ColLienAme)
            {
                _grilleHeros.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _grilleHeros.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && _grilleHeros.Columns[e.ColumnIndex].Name == ColLienAme)
            {
                ChangerLienAme(e.RowIndex);
            }
        };
        _grilleHeros.SelectionChanged += (_, _) => MettreAJourMenus();
        _grilleHeros.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                ModifierHero();
            }
        };
        // Le clic droit ne sélectionne pas la ligne par défaut : le menu contextuel agirait sur une autre.
        _grilleHeros.CellMouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                _grilleHeros.CurrentCell = _grilleHeros.Rows[e.RowIndex].Cells[ColNom];
            }
        };
        // Suppr est géré ici et non en raccourci de menu, qui volerait la touche au champ de recherche.
        _grilleHeros.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete)
            {
                SupprimerHero();
                e.Handled = true;
            }
        };
    }

    private TabPage ConstruireOngletEquipes()
    {
        var page = NouvellePage("Générateur");
        var generer = new Button { Text = "Générer", AutoSize = true, Margin = new Padding(16, 3, 3, 3) };
        generer.Click += (_, _) => GenererEquipes();
        var enregistrer = new Button { Text = "Enregistrer la composition", AutoSize = true, Margin = new Padding(8, 3, 3, 3) };
        enregistrer.Click += (_, _) => EnregistrerComposition();
        _nombreEquipes.ValueChanged += (_, _) =>
        {
            if (!_parametresEnChargement)
            {
                ReinitialiserEtSauvegarder();
            }
            AfficherChoixMode();
            AfficherCartes();
        };
        _choixMode.Items.AddRange(FormOptionsGeneration.LibellesModes);
        _choixMode.SelectedIndex = (int)_mode;
        _choixMode.SelectedIndexChanged += (_, _) => ChangerMode();

        var barre = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        barre.Controls.Add(new Label { Text = "Nombre d'équipes", AutoSize = true, Margin = new Padding(3, 6, 6, 0) });
        barre.Controls.Add(_nombreEquipes);
        barre.Controls.Add(new Label { Text = "Variantes", AutoSize = true, Margin = new Padding(16, 6, 6, 0) });
        barre.Controls.Add(_nombreVariantes);
        barre.Controls.Add(_libelleMode);
        barre.Controls.Add(_choixMode);
        AfficherChoixMode();
        barre.Controls.Add(generer);
        barre.Controls.Add(enregistrer);
        barre.Controls.Add(_avisReinitialisation);

        _zoneCartes.Resize += (_, _) =>
        {
            if (CartesEquipes.Colonnes(_zoneCartes) != _colonnesAffichees)
            {
                AfficherCartes();
            }
        };

        page.Controls.Add(_zoneCartes);
        page.Controls.Add(CartesEquipes.CreerCadreReserve(_zoneReserve));
        _choixVariante.SelectedIndexChanged += (_, _) => ChoisirVariante();
        _barreVariantes.Controls.Add(new Label { Text = "Variante", AutoSize = true, Margin = new Padding(3, 6, 6, 0) });
        _barreVariantes.Controls.Add(_choixVariante);
        page.Controls.Add(_barreVariantes);
        page.Controls.Add(barre);
        return page;
    }

    private void AjouterColonnesHeros()
    {
        // Le nom absorbe la largeur excédentaire quand la fenêtre est maximisée ; les autres colonnes suivent leur contenu.
        var nom = Texte(ColNom, "Nom");
        nom.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        nom.MinimumWidth = 160;
        _grilleHeros.Columns.Add(nom);
        _grilleHeros.Columns.Add(Texte(ColEtoiles, "Étoiles"));
        _grilleHeros.Columns.Add(Texte(ColClasse, "Classe"));
        _grilleHeros.Columns.Add(Texte(ColRole, "Rôle"));
        _grilleHeros.Columns.Add(Texte(ColFactions, "Factions"));
        _grilleHeros.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = ColLienAme,
            HeaderText = TitreLienAme,
            Tag = TitreLienAme,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
            SortMode = DataGridViewColumnSortMode.Programmatic,
        });
        _grilleHeros.Columns.Add(Texte(ColEquipement, "Équipement"));
        _grilleHeros.Columns.Add(Texte(ColDateMaj, "Mis à jour"));
    }

    // Tri programmatique : la grille ne trie plus seule, ce qui permet de conserver le tri au rechargement.
    // Le titre d'origine est gardé dans Tag : la flèche s'écrit dans le texte de l'en-tête, la flèche native restant invisible.
    private static DataGridViewTextBoxColumn Texte(string nom, string titre) => new()
    {
        Name = nom,
        HeaderText = titre,
        Tag = titre,
        ReadOnly = true,
        AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
        SortMode = DataGridViewColumnSortMode.Programmatic,
    };

    private static ComboBox NouveauFiltre() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };

    private static void AjouterFiltre(FlowLayoutPanel barre, string titre, Control filtre)
    {
        barre.Controls.Add(new Label { Text = titre, AutoSize = true, Margin = new Padding(12, 6, 4, 0) });
        barre.Controls.Add(filtre);
    }

    private void Recharger()
    {
        if (!Proteger(ChargerDepuisBase))
        {
            return;
        }
        _moteur = CreerMoteur();
        _selection = null;
        if (_variantes.Count > 0)
        {
            RemplirVariantes(_varianteRestauree);
        }
        else
        {
            _barreVariantes.Visible = false;
        }
        ActualiserFiltres();
        RemplirGrilleHeros();
        AfficherCartes();
    }

    private void ChargerDepuisBase()
    {
        _heros = [.. BaseSqlite.ListerHeros(_chemin)];
        _parId = _heros.ToDictionary(h => h.Id);
        _roles = BaseSqlite.LireRoles(_chemin);
        var parametres = BaseSqlite.LireParametres(_chemin);
        AppliquerParametres(parametres);
        ReconstruireRepartition(BaseSqlite.LireAffectations(_chemin));
        _variantes = LireVariantes(parametres);
        _varianteRestauree = (int)Math.Clamp(Lire(parametres, CleVarianteChoisie, 0), 0, Math.Max(0, _variantes.Count - 1));
    }

    // Une liste illisible ou périmée (héros supprimé ou ajouté depuis la génération) est abandonnée : mieux vaut une seule composition que des variantes fausses.
    private List<RepartitionEquipes> LireVariantes(IReadOnlyDictionary<string, string> parametres)
    {
        if (!parametres.TryGetValue(CleListeVariantes, out var texte) || texte.Length == 0)
        {
            return [];
        }

        var variantes = new List<RepartitionEquipes>();
        foreach (var bloc in texte.Split(SeparateurVariantes))
        {
            var groupes = bloc.Split(SeparateurEquipes)
                .Select(g => g.Split(SeparateurIds, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : IdIllisible)
                    .ToList())
                .ToList();
            var ids = groupes.SelectMany(g => g).ToList();
            if (ids.Count != _heros.Count || ids.Distinct().Count() != ids.Count || !ids.All(_parId.ContainsKey))
            {
                return [];
            }
            // La réserve est le dernier groupe, comme à l'écriture.
            variantes.Add(new RepartitionEquipes([.. groupes.Take(groupes.Count - 1)], groupes[^1]));
        }
        return variantes;
    }

    private static string SerialiserVariantes(IEnumerable<RepartitionEquipes> variantes) =>
        string.Join(SeparateurVariantes, variantes.Select(v =>
            string.Join(SeparateurEquipes, v.Equipes.Append(v.Reserve).Select(g => string.Join(SeparateurIds, g)))));

    // Les réglages viennent de la base : une valeur illisible ou hors bornes garde le réglage courant.
    private void AppliquerParametres(IReadOnlyDictionary<string, string> parametres)
    {
        _parametresEnChargement = true;
        _nombreEquipes.Value = Math.Clamp(Lire(parametres, CleNombreEquipes, _nombreEquipes.Value), _nombreEquipes.Minimum, _nombreEquipes.Maximum);
        _parametresEnChargement = false;
        _nombreEquipesPersiste = (int)_nombreEquipes.Value;
        _nombreVariantes.Value = Math.Clamp(Lire(parametres, CleVariantes, _nombreVariantes.Value), _nombreVariantes.Minimum, _nombreVariantes.Maximum);
        _poids = new PoidsEquipes(
            LirePoids(parametres, CleRoles, _poids.Roles),
            LirePoids(parametres, CleResonance, _poids.Resonance),
            LirePoids(parametres, CleEtoiles, _poids.Etoiles),
            LirePoids(parametres, CleChaos1, _poids.Chaos1),
            LirePoids(parametres, CleChaos2, _poids.Chaos2),
            LirePoids(parametres, CleChaos3, _poids.Chaos3));
        _mode = Lire(parametres, CleMode, (decimal)_mode) == (decimal)ModeGeneration.SommeMaximale
            ? ModeGeneration.SommeMaximale
            : ModeGeneration.MeilleureEquipeDAbord;
        _etoilesMinimum = LireMinimums(parametres);
        _reglages = ReglagesEquipes.Charger(parametres.GetValueOrDefault(CleHerosImposes), parametres.GetValueOrDefault(CleHerosBannis), parametres.GetValueOrDefault(CleEtoilesMiniHeros), (int)Math.Clamp(Lire(parametres, CleEtoilesMiniHerosGlobal, 0), 0, FormOptionsGeneration.EtoilesMinimumMax), parametres.GetValueOrDefault(CleFactionsImposees), parametres.GetValueOrDefault(CleFactionsBannies));
        AfficherMode();
        AfficherChoixMode();
    }

    // Valeur absente : on garde les minimums courants ; valeur illisible ou hors bornes : 0 pour l'équipe concernée.
    private List<int> LireMinimums(IReadOnlyDictionary<string, string> parametres) =>
        parametres.TryGetValue(CleEtoilesMinimum, out var texte)
            ? [.. texte.Split(SeparateurMinimums).Select(LireMinimum)]
            : _etoilesMinimum;

    private static int LireMinimum(string texte) =>
        int.TryParse(texte, NumberStyles.None, CultureInfo.InvariantCulture, out var valeur)
            ? Math.Min(valeur, FormOptionsGeneration.EtoilesMinimumMax)
            : 0;

    private static decimal Lire(IReadOnlyDictionary<string, string> parametres, string cle, decimal parDefaut) =>
        parametres.TryGetValue(cle, out var texte) && decimal.TryParse(texte, NumberStyles.Number, CultureInfo.InvariantCulture, out var valeur)
            ? valeur
            : parDefaut;

    private static double LirePoids(IReadOnlyDictionary<string, string> parametres, string cle, double parDefaut) =>
        (double)Math.Clamp(Lire(parametres, cle, (decimal)parDefaut), 0, FormOptionsGeneration.PoidsMax);

    // Un héros supprimé a déjà quitté la table (cascade) ; un héros ajouté depuis la dernière génération part en réserve.
    private void ReconstruireRepartition(IReadOnlyList<Affectation> affectations)
    {
        (_equipes, _reserve) = Affectation.Repartir(affectations, _parId.ContainsKey, _nombreEquipesPersiste);
        if (_equipes.Count == 0)
        {
            return;
        }

        var ids = _equipes.SelectMany(e => e).Concat(_reserve).ToHashSet();
        var nouveaux = _heros.Select(h => h.Id).Where(id => !ids.Contains(id)).ToList();
        if (nouveaux.Count > 0)
        {
            _reserve.AddRange(nouveaux);
            SauvegarderRepartition();
        }
    }

    private MoteurEquipes CreerMoteur()
    {
        var familles = Referentiel.Familles(_heros.Select(h => (h.Id, h.Hero.Nom)));
        return new(_heros.Select(h => MoteurEquipes.CreerHero(h.Id, h.Hero.Etoiles, h.Hero.Classe, h.Hero.Factions, h.LienAme, h.Hero.Equipement, familles[h.Id])), _roles);
    }

    private void RemplirGrilleHeros()
    {
        var idSelectionne = HeroSelectionne()?.Id;
        var texte = _recherche.Text.Trim();
        var filtre = texte.Length == 0 ? string.Empty : Referentiel.Normaliser(texte);

        var classe = Choix(_filtreClasse);
        var roleFiltre = Choix(_filtreRole);
        var faction = Choix(_filtreFaction);
        var lienAme = _filtreLienAme.SelectedIndex;

        _grilleEnRemplissage = true;
        _grilleHeros.Rows.Clear();
        var visibles = _heros
            .Select(h => (Stocke: h, Role: _moteur.RoleDe(h.Id)))
            .Where(x => (filtre.Length == 0 || Referentiel.Normaliser(x.Stocke.Hero.Nom).Contains(filtre))
                && (classe is null || x.Stocke.Hero.Classe == classe)
                && (roleFiltre is null || x.Role == roleFiltre)
                && (faction is null || x.Stocke.Hero.Factions.Contains(faction))
                && (lienAme == 0 || x.Stocke.LienAme == (lienAme == 1)))
            .ToList();
        _affichage.Text = $"{visibles.Count} affiché(s) sur {_heros.Count}";
        foreach (var (stocke, role) in Ordonner(visibles))
        {
            var hero = stocke.Hero;
            var index = _grilleHeros.Rows.Add(hero.Nom, CartesEquipes.LibelleEtoiles(hero.Etoiles), hero.Classe, role,
                string.Join(SeparateurFactions, hero.Factions), stocke.LienAme, hero.Equipement, OutilsDialogue.LibelleDate(stocke.DateMaj));
            var ligne = _grilleHeros.Rows[index];
            ligne.Tag = stocke.Id;
            ligne.ContextMenuStrip = _menuHeros;
            ligne.Cells[ColRole].Style.ForeColor = CartesEquipes.CouleurRole(role);
        }
        _grilleEnRemplissage = false;

        foreach (DataGridViewRow ligne in _grilleHeros.Rows)
        {
            if (ligne.Tag is int id && id == idSelectionne)
            {
                _grilleHeros.CurrentCell = ligne.Cells[ColNom];
            }
        }
        AfficherSensTri();
        MettreAJourMenus();
    }

    private static string? Choix(ComboBox filtre) => filtre.SelectedIndex > 0 ? (string)filtre.SelectedItem! : null;

    // Les libellés d'étoiles ne se trient pas comme du texte : la clé des étoiles est l'ordre des paliers.
    private IEnumerable<(HeroStocke Stocke, string Role)> Ordonner(List<(HeroStocke Stocke, string Role)> lignes)
    {
        IComparable Cle((HeroStocke Stocke, string Role) x) => _colonneTri switch
        {
            ColEtoiles => x.Stocke.Hero.Etoiles,
            ColClasse => Referentiel.Normaliser(x.Stocke.Hero.Classe),
            ColRole => x.Role,
            ColFactions => string.Join(SeparateurFactions, x.Stocke.Hero.Factions),
            ColLienAme => x.Stocke.LienAme,
            ColEquipement => Referentiel.Normaliser(x.Stocke.Hero.Equipement),
            // Une date inconnue se classe avant toutes les autres.
            ColDateMaj => x.Stocke.DateMaj ?? DateTime.MinValue,
            _ => Referentiel.Normaliser(x.Stocke.Hero.Nom),
        };
        string Nom((HeroStocke Stocke, string Role) x) => Referentiel.Normaliser(x.Stocke.Hero.Nom);

        return _sensTri == ListSortDirection.Ascending
            ? lignes.OrderBy(Cle).ThenBy(Nom)
            : lignes.OrderByDescending(Cle).ThenBy(Nom);
    }

    private void TrierPar(string colonne)
    {
        _sensTri = colonne == _colonneTri && _sensTri == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        _colonneTri = colonne;
        RemplirGrilleHeros();
    }

    private void AfficherSensTri()
    {
        foreach (DataGridViewColumn colonne in _grilleHeros.Columns)
        {
            var fleche = colonne.Name != _colonneTri
                ? string.Empty
                : _sensTri == ListSortDirection.Ascending ? FlecheCroissant : FlecheDecroissant;
            colonne.HeaderText = (string)colonne.Tag! + fleche;
        }
    }

    private void ChangerLienAme(int indexLigne)
    {
        if (_grilleEnRemplissage || _grilleHeros.Rows[indexLigne].Tag is not int id)
        {
            return;
        }

        var coche = _grilleHeros.Rows[indexLigne].Cells[ColLienAme].Value is true;
        Proteger(() => BaseSqlite.EcrireLienAme(id, coche, _chemin));
        // Le rechargement remplace les lignes : on le reporte après la fin de ce gestionnaire, et il annule l'affichage si l'écriture a échoué.
        BeginInvoke(new Action(Recharger));
    }

    private void ChangerMode()
    {
        if (_modeEnMiseAJour)
        {
            return;
        }

        var ancien = _mode;
        _mode = (ModeGeneration)_choixMode.SelectedIndex;
        if (!ReinitialiserEtSauvegarder())
        {
            _mode = ancien;
        }
        AfficherMode();
        AfficherCartes();
    }

    // Retourne vrai quand des réglages existaient : sans eux, le vidage est invisible et n'a pas à être annoncé.
    private bool ReinitialiserReglages()
    {
        var existaient = _reglages.ADesReglages || _etoilesMinimum.Any(minimum => minimum > 0);
        _reglages.ReinitialiserToutes();
        _etoilesMinimum.Clear();
        return existaient;
    }

    private void AnnoncerReinitialisation()
    {
        _avisReinitialisation.Visible = true;
        _delaiAvis.Stop();
        _delaiAvis.Start();
    }

    private string? ResumeReglages(int equipe)
    {
        var criteres = _reglages.Resume(equipe).ToList();
        if (ModeEffectif == ModeGeneration.MeilleureEquipeDAbord && Minimum(equipe) > 0)
        {
            criteres.Add($"équipe ≥ {Minimum(equipe)} étoiles");
        }
        return criteres.Count > 0 ? string.Join(", ", criteres) : null;
    }

    // Changer le nombre d'équipes ou le mode démarre une nouvelle composition : les réglages de chaque équipe repartent de zéro.
    private bool ReinitialiserEtSauvegarder()
    {
        var anciensReglages = _reglages.Copier();
        var anciensMinimums = _etoilesMinimum.ToList();
        var existaient = ReinitialiserReglages();
        if (Proteger(SauvegarderParametres))
        {
            if (existaient)
            {
                AnnoncerReinitialisation();
            }
            return true;
        }
        _reglages = anciensReglages;
        _etoilesMinimum = anciensMinimums;
        return false;
    }

    private void AfficherMode()
    {
        _modeEnMiseAJour = true;
        _choixMode.SelectedIndex = (int)_mode;
        _modeEnMiseAJour = false;
    }

    // Le _mode choisi reste intact et persisté : il revient quand on repasse à plusieurs équipes.
    private void AfficherChoixMode()
    {
        var visible = (int)_nombreEquipes.Value > 1;
        _libelleMode.Visible = visible;
        _choixMode.Visible = visible;
    }

    private ModeGeneration ModeEffectif => MoteurEquipes.ModeEffectif(_mode, (int)_nombreEquipes.Value);

    private int Minimum(int equipe) => equipe < _etoilesMinimum.Count ? _etoilesMinimum[equipe] : 0;

    // Minimums et critères vivent dans _etoilesMinimum et _reglages, indépendamment des cartes affichées (une carte vide garde les siens).
    private void ReglerEquipe(int equipe)
    {
        int? minimum = ModeEffectif == ModeGeneration.MeilleureEquipeDAbord ? Minimum(equipe) : null;
        using var dialogue = new FormReglageEquipe(equipe, minimum, _reglages, _heros);
        if (dialogue.ShowDialog(this) == DialogResult.OK)
        {
            ChangerReglages(equipe, dialogue.EtoilesMinimum, dialogue.Reglages);
        }
    }

    private void ChangerReglages(int equipe, int? minimum, ReglagesEquipes reglages)
    {
        var anciensReglages = _reglages;
        var ancienMinimum = _etoilesMinimum.ToList();
        _reglages = reglages;
        if (minimum is { } valeur)
        {
            while (_etoilesMinimum.Count <= equipe)
            {
                _etoilesMinimum.Add(0);
            }
            _etoilesMinimum[equipe] = valeur;
        }
        if (!Proteger(SauvegarderParametres))
        {
            _reglages = anciensReglages;
            _etoilesMinimum = ancienMinimum;
            return;
        }
        AfficherCartes();
    }

    private IReadOnlyList<CriteresEquipe> Criteres(int nombreEquipes) => _reglages.Pour(nombreEquipes, _parId.ContainsKey);

    private int EtoilesDe(int id) => _parId[id].Hero.Etoiles;

    private IReadOnlyList<string> FactionsDe(int id) => _parId[id].Hero.Factions;

    // Les listes se reconstruisent depuis la base : une classe ou une faction n'existe que si un héros la porte.
    private void ActualiserFiltres()
    {
        _filtresEnMiseAJour = true;
        try
        {
            ReremplirFiltre(_filtreClasse, _heros.Select(h => h.Hero.Classe));
            ReremplirFiltre(_filtreRole, MoteurEquipes.Roles);
            ReremplirFiltre(_filtreFaction, _heros.SelectMany(h => h.Hero.Factions));
        }
        finally
        {
            _filtresEnMiseAJour = false;
        }
    }

    private static void ReremplirFiltre(ComboBox filtre, IEnumerable<string> valeurs)
    {
        var precedent = Choix(filtre);
        filtre.Items.Clear();
        filtre.Items.Add(ToutesValeurs);
        filtre.Items.AddRange([.. valeurs.Distinct().Order(StringComparer.CurrentCultureIgnoreCase)]);
        var index = precedent is null ? 0 : filtre.Items.IndexOf(precedent);
        filtre.SelectedIndex = Math.Max(index, 0);
    }

    private HeroStocke? HeroSelectionne() =>
        _grilleHeros.CurrentRow?.Tag is int id && _parId.TryGetValue(id, out var stocke) ? stocke : null;

    private void MettreAJourMenus()
    {
        var selectionne = HeroSelectionne() is not null;
        _menuModifier.Enabled = selectionne;
        _menuSupprimer.Enabled = selectionne;
    }

    private bool NomPris(string nom, int? exclureId)
    {
        var norme = Referentiel.Normaliser(nom);
        return _heros.Any(h => h.Id != exclureId && Referentiel.Normaliser(h.Hero.Nom) == norme);
    }

    private Hero? Saisir(HeroStocke? existant)
    {
        using var formulaire = new FormSaisieHero(existant?.Hero, nom => NomPris(nom, existant?.Id), existant?.DateMaj);
        return formulaire.ShowDialog(this) == DialogResult.OK ? formulaire.Resultat : null;
    }

    private void AjouterHero()
    {
        if (Saisir(null) is { } hero && Proteger(() => BaseSqlite.AjouterHero(hero, _chemin)))
        {
            Recharger();
        }
    }

    private void ModifierHero()
    {
        if (HeroSelectionne() is { } stocke && Saisir(stocke) is { } hero && Proteger(() => BaseSqlite.ModifierHero(stocke.Id, hero, _chemin)))
        {
            Recharger();
        }
    }

    private void SupprimerHero()
    {
        if (HeroSelectionne() is not { } stocke)
        {
            return;
        }

        var reponse = MessageBox.Show(this, $"Supprimer {stocke.Hero.Nom} ? Il quittera aussi ses équipes.", TitreApplication,
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (reponse == DialogResult.Yes && Proteger(() => BaseSqlite.SupprimerHero(stocke.Id, _chemin)))
        {
            Recharger();
        }
    }

    // La fenêtre se masque le temps de la capture : elle ne doit pas gêner la lecture de l'écran du jeu.
    private void OuvrirCapture()
    {
        Hide();
        try
        {
            using var capture = new FormPrincipale();
            capture.ShowDialog();
        }
        finally
        {
            Show();
        }
        Recharger();
    }

    private void OuvrirOptions()
    {
        using var dialogue = new FormOptionsGeneration(_poids, _reglages.EtoilesMiniHerosGlobal);
        if (dialogue.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var ancien = _poids;
        var anciensReglages = _reglages.Copier();
        var anciensMinimums = _etoilesMinimum.ToList();
        var modifie = dialogue.Poids != _poids || dialogue.EtoilesMiniHeros != _reglages.EtoilesMiniHerosGlobal;
        _poids = dialogue.Poids;
        _reglages.EtoilesMiniHerosGlobal = dialogue.EtoilesMiniHeros;
        var existaient = false;
        if (modifie)
        {
            // Changer les options démarre une nouvelle composition : les réglages de chaque équipe repartent de zéro.
            existaient = ReinitialiserReglages();
        }
        if (!Proteger(SauvegarderParametres))
        {
            _poids = ancien;
            _reglages = anciensReglages;
            _etoilesMinimum = anciensMinimums;
            return;
        }
        if (existaient)
        {
            AnnoncerReinitialisation();
        }
        // Le seuil global et la réinitialisation changent ce que les cartes signalent comme non tenu.
        AfficherCartes();
    }

    private void OuvrirRolesClasses()
    {
        using var dialogue = new FormRolesClasses(_roles, EnregistrerRole);
        dialogue.ShowDialog(this);
        if (!dialogue.Modifie)
        {
            return;
        }

        _moteur = CreerMoteur();
        RemplirGrilleHeros();
        AfficherCartes();
    }

    private bool EnregistrerRole(string classe, string role)
    {
        if (!Proteger(() => BaseSqlite.EcrireRole(classe, role, _chemin)))
        {
            return false;
        }
        _roles[classe] = role;
        return true;
    }

    private void AfficherAPropos() =>
        MessageBox.Show(this, $"{TitreApplication}{Environment.NewLine}Héros et équipes de Mobile Legends Adventure.", "À propos", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void GenererEquipes()
    {
        if (_heros.Count == 0)
        {
            MessageBox.Show(this, "Aucun héros en base.", TitreApplication);
            return;
        }

        var nombre = (int)_nombreEquipes.Value;
        Cursor = Cursors.WaitCursor;
        try
        {
            _variantes = [.. _moteur.GenererVariantes(nombre, _poids, MoteurEquipes.ModeEffectif(_mode, nombre), _etoilesMinimum, (int)_nombreVariantes.Value, Criteres(nombre))];
            _nombreEquipesPersiste = nombre;
            RemplirVariantes();
            AppliquerVariante(0);
            Proteger(SauvegarderParametres);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ChoisirVariante()
    {
        if (!_variantesEnMiseAJour && _choixVariante.SelectedIndex >= 0)
        {
            AppliquerVariante(_choixVariante.SelectedIndex);
        }
    }

    private void AppliquerVariante(int index)
    {
        var repartition = _variantes[index];
        _equipes = [.. repartition.Equipes.Select(Trier)];
        _reserve = Trier(repartition.Reserve);
        _selection = null;
        Proteger(SauvegarderRepartition);
        Proteger(() => BaseSqlite.EcrireParametre(CleVarianteChoisie, index.ToString(CultureInfo.InvariantCulture), _chemin));
        AfficherCartes();
    }

    private void RemplirVariantes(int index = 0)
    {
        _variantesEnMiseAJour = true;
        try
        {
            _choixVariante.Items.Clear();
            for (var i = 0; i < _variantes.Count; i++)
            {
                _choixVariante.Items.Add(LibelleVariante(i));
            }
            _choixVariante.SelectedIndex = index;
        }
        finally
        {
            _variantesEnMiseAJour = false;
        }
        _barreVariantes.Visible = _variantes.Count > 1;
    }

    private string LibelleVariante(int index)
    {
        var evaluations = _variantes[index].Equipes.Select(e => _moteur.Evaluer(e)).ToList();
        var criteres = Criteres(evaluations.Count);
        var alerte = evaluations.Select((e, rang) => e.PenaliteRoles > 0 || EstSousMinimum(e, rang) || criteres[rang].Ecarts(_variantes[index].Equipes[rang], EtoilesDe, FactionsDe) > 0).Any(a => a);
        return $"{index + 1} · {string.Join(SeparateurPoints, evaluations.Select(e => e.Etoiles))}{(alerte ? SigneAlerte : string.Empty)}";
    }

    private bool EstSousMinimum(EvaluationEquipe evaluation, int equipe) =>
        ModeEffectif == ModeGeneration.MeilleureEquipeDAbord && evaluation.Etoiles < Minimum(equipe);

    private List<int> Trier(IEnumerable<int> ids) => [.. ids.OrderByDescending(id => _parId[id].Hero.Etoiles)];

    private void SauvegarderRepartition() => BaseSqlite.EcrireAffectations(ConstruireAffectations(), _chemin);

    private List<Affectation> ConstruireAffectations() => Affectation.Aplatir(_equipes, _reserve);

    private void EnregistrerComposition()
    {
        if (_equipes.Count == 0)
        {
            MessageBox.Show(this, "Aucune équipe à enregistrer.", TitreApplication);
            return;
        }

        using var formulaire = new FormSaisieNom("Enregistrer la composition", string.Empty, "Une composition porte déjà ce nom.",
            nom => BaseSqlite.NomCompositionPris(nom, null, _chemin));
        if (formulaire.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        // Copie figée : la composition enregistrée ne suit pas les générations ni les échanges suivants.
        var nomComposition = formulaire.Resultat!;
        if (Proteger(() => BaseSqlite.CreerComposition(nomComposition, ConstruireAffectations(), _equipes.Count, _chemin)))
        {
            MessageBox.Show(this, $"Composition « {nomComposition} » enregistrée.", TitreApplication);
        }
    }

    private void SauvegarderParametres()
    {
        var valeurs = new (string Cle, decimal Valeur)[]
        {
            (CleNombreEquipes, _nombreEquipesPersiste),
            (CleVariantes, _nombreVariantes.Value),
            (CleRoles, (decimal)_poids.Roles),
            (CleResonance, (decimal)_poids.Resonance),
            (CleEtoiles, (decimal)_poids.Etoiles),
            (CleChaos1, (decimal)_poids.Chaos1),
            (CleChaos2, (decimal)_poids.Chaos2),
            (CleChaos3, (decimal)_poids.Chaos3),
            (CleMode, (decimal)_mode),
        };
        foreach (var (cle, valeur) in valeurs)
        {
            BaseSqlite.EcrireParametre(cle, valeur.ToString(CultureInfo.InvariantCulture), _chemin);
        }
        BaseSqlite.EcrireParametre(CleEtoilesMinimum, string.Join(SeparateurMinimums, _etoilesMinimum), _chemin);
        BaseSqlite.EcrireParametre(CleHerosImposes, _reglages.SerialiserImposes(), _chemin);
        BaseSqlite.EcrireParametre(CleHerosBannis, _reglages.SerialiserBannis(), _chemin);
        BaseSqlite.EcrireParametre(CleFactionsImposees, _reglages.SerialiserFactionsImposees(), _chemin);
        BaseSqlite.EcrireParametre(CleFactionsBannies, _reglages.SerialiserFactionsBannies(), _chemin);
        BaseSqlite.EcrireParametre(CleEtoilesMiniHeros, _reglages.SerialiserEtoilesMiniHeros(), _chemin);
        BaseSqlite.EcrireParametre(CleEtoilesMiniHerosGlobal, _reglages.EtoilesMiniHerosGlobal.ToString(CultureInfo.InvariantCulture), _chemin);
        BaseSqlite.EcrireParametre(CleListeVariantes, SerialiserVariantes(_variantes), _chemin);
    }

    private void Cliquer(int id)
    {
        if (_selection is { } premier && premier != id && OutilsDialogue.RefuserDoublonHeros(this, _equipes, _reserve, premier, id, _moteur.FamilleDe))
        {
            _selection = null;
        }
        else if (Affectation.BasculerSelection(ref _selection, _equipes, _reserve, id))
        {
            Proteger(SauvegarderRepartition);
        }
        // L'affichage détruit la ligne cliquée : on le reporte après la fin de son gestionnaire.
        BeginInvoke(new Action(AfficherCartes));
    }

    private void EnvoyerEnReserve(int id)
    {
        if (!Affectation.EnvoyerEnReserve(_equipes, _reserve, id))
        {
            return;
        }

        _reserve = Trier(_reserve);
        _selection = null;
        Proteger(SauvegarderRepartition);
        BeginInvoke(new Action(AfficherCartes));
    }

    private void RafraichirSynthese()
    {
        var places = (int)_nombreEquipes.Value * MoteurEquipes.TailleEquipe;
        var ecart = _heros.Count - places;
        const string separateur = CartesEquipes.SeparateurInfos;
        var suite = ecart < 0 ? $"{separateur}{-ecart} place(s) vide(s)" : ecart > 0 ? $"{separateur}{ecart} en réserve" : string.Empty;
        _synthese.Text = $"{_heros.Count} héros{separateur}{places} places{suite}";
    }

    private void AfficherCartes()
    {
        RafraichirSynthese();
        _cartes.Moteur = _moteur;
        _cartes.Heros = _parId;
        _cartes.Selection = _selection;
        // Cartes vides purement visuelles : elles donnent accès aux réglages des équipes demandées mais pas encore composées.
        var affichees = _equipes.Count >= (int)_nombreEquipes.Value
            ? _equipes
            : [.. _equipes, .. Enumerable.Range(0, (int)_nombreEquipes.Value - _equipes.Count).Select(_ => new List<int>())];
        _colonnesAffichees = _cartes.Afficher(_zoneCartes, _zoneReserve, affichees, _reserve, string.Empty);
    }

    // Retourne false après avoir signalé l'erreur : l'état affiché reste celui d'avant l'échec.
    private bool Proteger(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (BaseSqlite.EstErreurAcces(ex))
        {
            // Un dialogue peut être ouvert : l'erreur s'affiche au-dessus de lui, pas derrière.
            MessageBox.Show(ActiveForm ?? this, ex.Message, TitreApplication, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }
}
