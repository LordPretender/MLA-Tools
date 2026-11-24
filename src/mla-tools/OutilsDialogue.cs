using System.Globalization;

namespace MlaTools;

/// <summary>Éléments de mise en page communs aux dialogues de saisie.</summary>
internal static class OutilsDialogue
{
    private const string FormatDateAffichee = "dd/MM/yyyy HH:mm";
    private const string LibelleDateInconnue = "inconnue";

    public const string NomPolice = "Segoe UI";
    public const float TaillePolice = 9.5f;

    public static readonly Color FondFenetre = Color.FromArgb(243, 243, 243);

    private static readonly Color AccentBouton = Color.FromArgb(0, 103, 192);
    private static readonly Color FondBoutonDesactive = Color.FromArgb(204, 204, 204);
    private static readonly Color TexteBoutonDesactive = Color.FromArgb(120, 120, 120);
    private const int MargeHorizontaleBouton = 20;
    private const int MargeVerticaleBouton = 7;
    private const int LargeurAide = 380;

    /// <summary>Donne à tous les boutons d'une fenêtre le même style plat bleu.</summary>
    public static void StyliserBoutons(Control racine)
    {
        foreach (Control enfant in racine.Controls)
        {
            if (enfant is Button bouton)
            {
                StyliserBouton(bouton);
            }
            StyliserBoutons(enfant);
        }
    }

    /// <summary>À appeler directement pour un bouton créé après l'ouverture de la fenêtre.</summary>
    public static void StyliserBouton(Button bouton)
    {
        bouton.FlatStyle = FlatStyle.Flat;
        bouton.UseVisualStyleBackColor = false;
        bouton.FlatAppearance.BorderSize = 0;
        // AutoSize ajoute sa propre marge : un bouton resterait plus haut qu'un champ (29 px contre 24 avec Segoe UI 9,5), quel que soit le Padding.
        bouton.AutoSize = false;
        // Retrait avant ajout : styler deux fois le même bouton n'empile pas deux gestionnaires.
        // La police n'est héritée qu'une fois le bouton posé dans son parent, d'où FontChanged.
        bouton.TextChanged -= SurChangementDimensionsBouton;
        bouton.TextChanged += SurChangementDimensionsBouton;
        bouton.FontChanged -= SurChangementDimensionsBouton;
        bouton.FontChanged += SurChangementDimensionsBouton;
        // Un bouton plat désactivé garde son fond : sans cette bascule, il resterait bleu avec un texte gris illisible.
        bouton.EnabledChanged -= SurChangementEtatBouton;
        bouton.EnabledChanged += SurChangementEtatBouton;
        DimensionnerBouton(bouton);
        ColorerBouton(bouton);
    }

    private static void SurChangementDimensionsBouton(object? expediteur, EventArgs e)
    {
        if (expediteur is Button bouton)
        {
            DimensionnerBouton(bouton);
        }
    }

    // Même hauteur que les champs de saisie (police + marge), largeur d'après le texte.
    private static void DimensionnerBouton(Button bouton)
    {
        var largeurTexte = TextRenderer.MeasureText(bouton.Text, bouton.Font).Width;
        bouton.Size = new Size(largeurTexte + MargeHorizontaleBouton, bouton.Font.Height + MargeVerticaleBouton);
    }

    private static void SurChangementEtatBouton(object? expediteur, EventArgs e)
    {
        if (expediteur is Button bouton)
        {
            ColorerBouton(bouton);
        }
    }

    private static void ColorerBouton(Button bouton)
    {
        bouton.BackColor = bouton.Enabled ? AccentBouton : FondBoutonDesactive;
        bouton.ForeColor = bouton.Enabled ? Color.White : TexteBoutonDesactive;
    }

    /// <summary>Cadre titré qui s'étend avec son contenu : regroupe les boutons d'une même tâche.</summary>
    public static GroupBox Groupe(string titre, Control contenu)
    {
        var groupe = new GroupBox
        {
            Text = titre,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 6, 10, 10),
            Margin = new Padding(0, 0, 0, 10),
        };
        contenu.Dock = DockStyle.Top;
        groupe.Controls.Add(contenu);
        return groupe;
    }

    /// <summary>Pile verticale de contrôles, chacun à sa taille.</summary>
    public static FlowLayoutPanel Pile(params Control[] contenus)
    {
        var pile = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        pile.Controls.AddRange(contenus);
        return pile;
    }

    /// <summary>Date de mise à jour telle qu'affichée à l'utilisateur ; null (héros antérieur à la colonne) donne « inconnue ».</summary>
    public static string LibelleDate(DateTime? date) =>
        date?.ToString(FormatDateAffichee, CultureInfo.InvariantCulture) ?? LibelleDateInconnue;

    /// <summary>Vrai, après avoir prévenu l'utilisateur, quand l'échange des deux héros réunirait deux formes du même personnage dans les équipes : il ne doit alors pas avoir lieu.</summary>
    public static bool RefuserDoublonHeros(
        IWin32Window parent, IReadOnlyList<List<int>> equipes, IReadOnlyList<int> reserve, int premier, int second, Func<int, int> famille)
    {
        if (Affectation.EchangeAutorise(equipes, reserve, premier, second, famille))
        {
            return false;
        }

        MessageBox.Show(parent, Constantes.MessageDoublonHeros, Constantes.TitreApplication, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return true;
    }

    /// <summary>Tableau libellé / champ : la colonne des libellés suit son contenu, celle des champs absorbe la largeur.</summary>
    public static TableLayoutPanel TableChamps(int marge)
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(marge) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    public static void AjouterLigne(TableLayoutPanel table, string titre, Control champ)
    {
        table.Controls.Add(new Label { Text = titre, AutoSize = true, Margin = new Padding(3, 6, 16, 3) });
        table.Controls.Add(champ);
    }

    public static TabPage PageChamps(string titre, Action<TableLayoutPanel> remplir)
    {
        var table = TableChamps(10);
        remplir(table);
        var page = new TabPage(titre);
        page.Controls.Add(table);
        return page;
    }

    // L'aide reste visible sous son champ : un réglage sans explication ne se règle qu'à tâtons.
    public static void AjouterChampAvecAide(TableLayoutPanel table, string titre, Control champ, string aide)
    {
        // Ancrés à gauche seulement, libellé et champ se centrent verticalement dans la ligne : une marge fixe ne compense pas la hauteur propre d'une case à cocher.
        table.Controls.Add(new Label { Text = titre, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 3, 16, 3) });
        champ.Anchor = AnchorStyles.Left;
        champ.Margin = new Padding(3);
        table.Controls.Add(champ);
        var texte = new Label { Text = aide, AutoSize = true, MaximumSize = new Size(LargeurAide, 0), ForeColor = Color.DimGray, Margin = new Padding(3, 0, 3, 12) };
        table.Controls.Add(texte);
        table.SetColumnSpan(texte, 2);
    }

    /// <summary>Barre de boutons alignée à droite ; le premier bouton passé occupe l'extrémité droite.</summary>
    public static FlowLayoutPanel BarreBoutons(params Button[] boutons)
    {
        var barre = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6), FlowDirection = FlowDirection.RightToLeft };
        barre.Controls.AddRange(boutons);
        return barre;
    }
}
