using System.Globalization;

namespace MlaTools;

/// <summary>Dessine les cartes d'équipe et la carte de réserve ; partagé par le Générateur et l'onglet des compositions enregistrées.</summary>
/// <remarks>Les polices appartiennent à la fenêtre appelante, qui les libère.</remarks>
internal sealed class CartesEquipes(Font police, Font gras) : IDisposable
{
    public const int LargeurCarte = 340;
    public const int MargeCarte = 8;

    public const string SeparateurInfos = " · ";

    /// <summary>Entrée du menu contextuel d'une équipe, commune au Générateur et à l'onglet des compositions.</summary>
    public const string LibelleRetrait = "Envoyer en réserve";
    private const string AlerteRoles = " — hors cible";
    private const string LibelleInactive = "Équipe non générée : ";
    private const string LibelleAjout = "Ajouter un héros…";
    private const string LibelleReglage = "⚙";
    private const string InfobulleReglage = "Réglages de l'équipe";
    private const int LargeurBoutonReglage = 30;
    private const int HauteurEnTete = 34;
    private const int HauteurPastille = 30;
    private const int MargeCadreReserve = 12;

    private static readonly Dictionary<int, string> LibellesEtoiles = LecteurEtoiles.Paliers.ToDictionary(p => p.Valeur, p => p.Libelle);

    private static readonly Color FondVert = Color.FromArgb(230, 244, 234);
    private static readonly Color TexteVert = Color.FromArgb(24, 128, 56);
    private static readonly Color FondRouge = Color.FromArgb(252, 232, 230);
    private static readonly Color TexteRouge = Color.FromArgb(197, 34, 31);
    private static readonly Color FondNeutre = Color.FromArgb(238, 238, 238);

    // Une seule infobulle pour toutes les lignes : une par ligne serait recréée à chaque affichage des cartes.
    private readonly ToolTip _infobulle = new();

    public void Dispose() => _infobulle.Dispose();

    public MoteurEquipes Moteur { get; set; } = new([], new Dictionary<string, string>());

    public IReadOnlyDictionary<int, HeroStocke> Heros { get; set; } = new Dictionary<int, HeroStocke>();

    public int? Selection { get; set; }

    /// <summary>Menu contextuel des lignes d'une équipe (pas de la réserve).</summary>
    public ContextMenuStrip? MenuEquipe { get; set; }

    /// <summary>Clic gauche sur une ligne de héros ; nul en consultation.</summary>
    public Action<int>? SurClic { get; set; }

    /// <summary>Clic sur « Ajouter un héros » d'une équipe incomplète (index de l'équipe) ; nul quand l'ajout n'est pas proposé.</summary>
    public Action<int>? SurAjout { get; set; }

    /// <summary>Clic sur ⚙ d'une carte d'équipe (index de l'équipe) ; nul quand le réglage n'est pas proposé (onglet des compositions).</summary>
    public Action<int>? SurReglage { get; set; }

    /// <summary>Indique qu'une équipe est sous son minimum d'étoiles ; nul quand aucun minimum ne s'applique.</summary>
    public Func<EvaluationEquipe, int, bool>? EstSousMinimum { get; set; }

    /// <summary>Indique qu'une équipe (ses héros, son rang) ne tient pas ses héros imposés ou bannis ; nul quand aucun critère ne s'applique.</summary>
    public Func<IReadOnlyList<int>, int, bool>? CriteresNonTenus { get; set; }

    /// <summary>Motif pour lequel une équipe (son rang) n'a pas été générée ; nul quand elle l'est ou qu'aucun critère ne s'applique.</summary>
    public Func<int, string?>? RaisonInactive { get; set; }

    public static string LibelleEtoiles(int valeur) => LibellesEtoiles.GetValueOrDefault(valeur, valeur.ToString(CultureInfo.InvariantCulture));

    public static Color CouleurRole(string role) => role switch
    {
        MoteurEquipes.RoleTank => Color.RoyalBlue,
        MoteurEquipes.RoleSoutien => Color.DarkGreen,
        _ => Color.Firebrick,
    };

    /// <summary>Panneau de réserve avec son cadre pointillé, à poser à droite de la zone des cartes.</summary>
    /// <remarks>Le cadre se dessine sur un panneau fixe : sur la zone défilante, il partirait avec le défilement.</remarks>
    public static Panel CreerCadreReserve(Panel zoneReserve)
    {
        var cadre = new Panel
        {
            Dock = DockStyle.Right,
            Width = LargeurCarte + 2 * MargeCarte + SystemInformation.VerticalScrollBarWidth + MargeCadreReserve,
            Padding = new Padding(6),
        };
        cadre.Paint += (_, e) =>
        {
            using var pointille = new Pen(Color.Gray) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            e.Graphics.DrawRectangle(pointille, 2, 2, cadre.Width - 5, cadre.Height - 5);
        };
        cadre.Controls.Add(zoneReserve);
        return cadre;
    }

    public static int Colonnes(Control zoneCartes) =>
        Math.Max(1, (zoneCartes.ClientSize.Width - SystemInformation.VerticalScrollBarWidth) / (LargeurCarte + 2 * MargeCarte));

    /// <summary>Remplace le contenu des deux zones ; retourne le nombre de colonnes utilisé.</summary>
    public int Afficher(Panel zoneCartes, Panel zoneReserve, IReadOnlyList<List<int>> equipes, IReadOnlyList<int> reserve, string messageVide)
    {
        var colonnes = Colonnes(zoneCartes);
        zoneCartes.SuspendLayout();
        zoneReserve.SuspendLayout();
        Vider(zoneCartes);
        Vider(zoneReserve);

        if (equipes.Count == 0)
        {
            zoneCartes.Controls.Add(new Label
            {
                Text = messageVide,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(16),
                ForeColor = Color.DimGray,
            });
        }
        else
        {
            zoneCartes.Controls.Add(CreerTableCartes(equipes, colonnes));
            zoneReserve.Controls.Add(CreerCarteReserve(reserve));
        }

        zoneReserve.ResumeLayout();
        zoneCartes.ResumeLayout();
        return colonnes;
    }

    // Les contrôles remplacés sont libérés : à chaque affichage, des dizaines de lignes seraient sinon abandonnées.
    public static void Vider(Control parent)
    {
        foreach (var ancien in parent.Controls.Cast<Control>().ToList())
        {
            parent.Controls.Remove(ancien);
            ancien.Dispose();
        }
    }

    private TableLayoutPanel CreerTableCartes(IReadOnlyList<List<int>> equipes, int colonnes)
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = colonnes };
        for (var i = 0; i < colonnes; i++)
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }
        for (var i = 0; i < equipes.Count; i++)
        {
            table.Controls.Add(CreerCarteEquipe(i, equipes[i]));
        }
        return table;
    }

    private TableLayoutPanel CreerCarteEquipe(int index, List<int> ids)
    {
        var evaluation = Moteur.Evaluer(ids);
        var carte = NouvelleCarte();
        var sousMinimum = (EstSousMinimum?.Invoke(evaluation, index) ?? false) || (CriteresNonTenus?.Invoke(ids, index) ?? false);
        var reglage = SurReglage is null ? null : BoutonReglage(() => SurReglage(index));
        AjouterLigne(carte, EnTete($"Équipe {index + 1}", $"{evaluation.Etoiles} points d'étoiles", sousMinimum ? TexteRouge : null, reglage), HauteurEnTete);
        if (RaisonInactive?.Invoke(index) is { } raison)
        {
            AjouterLigne(carte, Pastille(LibelleInactive + raison, FondRouge, TexteRouge), 2 * HauteurPastille);
        }

        var resonance = evaluation.Points > 0
            ? Pastille(evaluation.Description, FondVert, TexteVert)
            : Pastille(evaluation.Description, FondNeutre, Color.DimGray);
        AjouterLigne(carte, resonance, HauteurPastille);

        var secondaires = evaluation.TanksSecondaires > 0 ? $" (+{evaluation.TanksSecondaires} sec.)" : string.Empty;
        var roles = string.Join(SeparateurInfos, $"{evaluation.Dps} DPS", $"{evaluation.Tanks} Tank{secondaires}", $"{evaluation.Soutiens} Soutien");
        var pastilleRoles = evaluation.PenaliteRoles > 0
            ? Pastille(roles + AlerteRoles, FondRouge, TexteRouge)
            : Pastille(roles, FondVert, TexteVert);
        AjouterLigne(carte, pastilleRoles, HauteurPastille);

        AjouterHeros(carte, ids, MenuEquipe);
        if (SurAjout is not null && ids.Count < MoteurEquipes.TailleEquipe)
        {
            var ajouter = new Button { Text = LibelleAjout };
            OutilsDialogue.StyliserBouton(ajouter);
            ajouter.Click += (_, _) => SurAjout(index);
            AjouterLigne(carte, ajouter, LigneHero.Hauteur);
        }
        return carte;
    }

    private TableLayoutPanel CreerCarteReserve(IReadOnlyList<int> reserve)
    {
        var carte = NouvelleCarte();
        AjouterLigne(carte, EnTete("Réserve", $"{reserve.Count} héros"), HauteurEnTete);
        if (reserve.Count == 0)
        {
            AjouterLigne(carte, new Label { Text = "Aucun héros en réserve", ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleCenter }, LigneHero.Hauteur);
        }
        AjouterHeros(carte, reserve, null);
        return carte;
    }

    private void AjouterHeros(TableLayoutPanel carte, IEnumerable<int> ids, ContextMenuStrip? menu)
    {
        foreach (var id in ids)
        {
            var hero = Heros[id].Hero;
            var role = Moteur.RoleDe(id);
            var detail = string.Join(SeparateurInfos, new[] { hero.Classe, role, hero.Equipement }.Where(s => !string.IsNullOrEmpty(s)));
            var ligne = new LigneHero(id, hero.Nom, detail, LibelleEtoiles(hero.Etoiles),
                CouleurRole(role), id == Selection, police, gras)
            {
                ContextMenuStrip = menu,
            };
            _infobulle.SetToolTip(ligne, InfobulleHero.Texte(Heros[id], role));
            if (SurClic is not null)
            {
                ligne.MouseClick += (_, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        SurClic(id);
                    }
                };
            }
            AjouterLigne(carte, ligne, LigneHero.Hauteur);
        }
    }

    // Hauteurs de ligne absolues : la carte garde des lignes de même taille quel que soit le texte.
    private static void AjouterLigne(TableLayoutPanel carte, Control contenu, int hauteur)
    {
        carte.RowStyles.Add(new RowStyle(SizeType.Absolute, hauteur));
        contenu.Dock = DockStyle.Fill;
        carte.Controls.Add(contenu);
    }

    private static TableLayoutPanel NouvelleCarte()
    {
        var carte = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(MargeCarte),
            MinimumSize = new Size(LargeurCarte, 0),
            MaximumSize = new Size(LargeurCarte, 0),
        };
        carte.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return carte;
    }

    private static Button BoutonReglage(Action surClic)
    {
        var bouton = new Button { Text = LibelleReglage, Width = LargeurBoutonReglage, Dock = DockStyle.Fill, Margin = new Padding(4, 2, 0, 2) };
        OutilsDialogue.StyliserBouton(bouton);
        var infobulle = new ToolTip();
        infobulle.SetToolTip(bouton, InfobulleReglage);
        bouton.Disposed += (_, _) => infobulle.Dispose();
        bouton.Click += (_, _) => surClic();
        return bouton;
    }

    private TableLayoutPanel EnTete(string titre, string droite, Color? couleurDroite = null, Button? reglage = null)
    {
        var entete = new TableLayoutPanel { ColumnCount = reglage is null ? 2 : 3, RowCount = 1, Padding = new Padding(12, 0, 12, 0) };
        entete.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        entete.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        entete.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        entete.Controls.Add(new Label { Text = titre, Font = gras, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false });
        entete.Controls.Add(new Label { Text = droite, ForeColor = couleurDroite ?? Color.DimGray, Dock = DockStyle.Fill, AutoSize = true, TextAlign = ContentAlignment.MiddleRight, UseMnemonic = false });
        if (reglage is not null)
        {
            entete.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            entete.Controls.Add(reglage);
        }
        return entete;
    }

    private static Label Pastille(string texte, Color fond, Color couleurTexte) => new()
    {
        Text = texte,
        BackColor = fond,
        ForeColor = couleurTexte,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoEllipsis = true,
        UseMnemonic = false,
        Padding = new Padding(8, 0, 8, 0),
        Margin = new Padding(8, 3, 8, 3),
    };
}
