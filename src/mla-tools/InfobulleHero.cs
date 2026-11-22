using System.Drawing.Drawing2D;

namespace MlaTools;

/// <summary>Infobulle d'un héros : fiche dessinée en trois zones (identité, rôle/classe/équipement, factions), identique dans le Générateur et dans les compositions enregistrées.</summary>
/// <remarks>Les polices reçues appartiennent à la fenêtre appelante ; celles créées ici sont libérées avec l'infobulle.</remarks>
internal sealed class InfobulleHero : IDisposable
{
    private const string LibelleFactions = "FACTIONS";
    private const string PuceFaction = "● ";
    private const string SuffixeLienAme = " (+ lien d'âme)";
    private const string LibelleLienAme = "Lien d'âme";
    private const string LibelleMiseAJour = "Mis à jour : ";

    // Dimensions de référence pour une police de 15 px de haut ; E() les adapte à la police réelle (DPI).
    private const int HauteurPoliceReference = 15;
    private const int LargeurReference = 240;
    private const int LargeurBande = 5;
    private const int Marge = 12;
    private const int EspaceBloc = 8;
    private const int EspaceLignes = 4;
    private const int EspacePuces = 6;
    private const int PaddingHorizontal = 8;
    private const int PaddingVertical = 3;
    private const int RayonPastille = 8;

    private const TextFormatFlags FormatTexte = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
    private const TextFormatFlags FormatPuce = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
    private const TextFormatFlags FormatMesure = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

    private static readonly Color CouleurEtoiles = Color.DarkGoldenrod;

    private readonly Font _police;
    private readonly Font _titre;
    private readonly Font _petite;
    private readonly Font _italique;

    // Une seule infobulle pour toutes les lignes : une par ligne serait recréée à chaque affichage des cartes.
    private readonly ToolTip _infobulle = new() { OwnerDraw = true };

    private const int DelaiAffichageMs = 500;
    private const int DecalageCurseur = 20;
    private const int EcartAuDessusCurseur = 2;

    // Affichage manuel : le ToolTip natif calcule sa position avant que Popup n'en change la taille, et la fiche déborde alors de l'écran.
    private readonly System.Windows.Forms.Timer _delai = new() { Interval = DelaiAffichageMs };
    private Control? _ligneSurvolee;

    public InfobulleHero(Font police, Font gras)
    {
        _delai.Tick += SurDelai;
        _police = police;
        _titre = new Font(gras.FontFamily, gras.Size + 1, gras.Style, gras.Unit);
        _petite = new Font(police.FontFamily, police.Size - 1, police.Style, police.Unit);
        _italique = new Font(police, police.Style | FontStyle.Italic);
        _infobulle.Popup += SurPopup;
        _infobulle.Draw += SurDessin;
    }

    internal readonly record struct FactionAffichee(string Nom, bool ViaLienAme);

    private sealed record Fiche(HeroStocke Stocke, string Role);

    private readonly record struct Puce(string Texte, Font Police, Color CouleurTexte, Color? Fond);

    public void Dispose()
    {
        _delai.Dispose();
        _infobulle.Dispose();
        _titre.Dispose();
        _petite.Dispose();
        _italique.Dispose();
    }

    /// <summary>Associe l'infobulle d'un héros à sa ligne ; le texte du ToolTip n'est qu'un repli, le dessin lit la fiche portée par la ligne.</summary>
    public void Associer(Control ligne, HeroStocke stocke, string role)
    {
        ligne.Tag = new Fiche(stocke, role);
        ligne.MouseEnter += (_, _) =>
        {
            _ligneSurvolee = ligne;
            _delai.Start();
        };
        ligne.MouseLeave += (_, _) => Masquer(ligne);
        ligne.MouseDown += (_, _) => Masquer(ligne);
        ligne.Disposed += (_, _) => Masquer(ligne);
    }

    private void Masquer(Control ligne)
    {
        if (_ligneSurvolee != ligne)
        {
            return;
        }
        _delai.Stop();
        _ligneSurvolee = null;
        _infobulle.Hide(ligne);
    }

    private void SurDelai(object? sender, EventArgs e)
    {
        _delai.Stop();
        if (_ligneSurvolee is not { IsDisposed: false } ligne || ligne.Tag is not Fiche fiche)
        {
            return;
        }
        var curseur = Control.MousePosition;
        var position = PositionDansEcran(curseur, Taille(fiche), Screen.FromPoint(curseur).WorkingArea);
        var local = ligne.PointToClient(position);
        _infobulle.Show(fiche.Stocke.Hero.Nom, ligne, local.X, local.Y);
    }

    /// <summary>Coin haut-gauche de la fiche : sous le curseur, ou au-dessus s'il n'y a pas la place en bas, et toujours dans la zone de travail de l'écran.</summary>
    internal static Point PositionDansEcran(Point curseur, Size taille, Rectangle zone)
    {
        var y = curseur.Y + DecalageCurseur;
        if (y + taille.Height > zone.Bottom)
        {
            y = curseur.Y - taille.Height - EcartAuDessusCurseur;
        }
        var x = Math.Min(curseur.X, zone.Right - taille.Width);
        return new Point(Math.Max(x, zone.Left), Math.Max(y, zone.Top));
    }

    private Size Taille(Fiche fiche)
    {
        var largeur = E(LargeurReference);
        return new Size(largeur, Rendre(null, fiche, new Rectangle(0, 0, largeur, 0)));
    }

    /// <summary>Factions affichées : celles du héros, puis celles que seul le lien d'âme ajoute.</summary>
    public static IReadOnlyList<FactionAffichee> Factions(HeroStocke stocke)
    {
        var hero = stocke.Hero;
        var effectives = MoteurEquipes.FactionsEffectives(hero.Factions, stocke.LienAme);
        // Le lien d'âme ajoute Ordre à la résonance seulement, pas aux critères de faction : on le dit pour que l'infobulle explique les écarts.
        return [.. hero.Factions.Select(f => new FactionAffichee(f, false)), .. effectives.Except(hero.Factions).Select(f => new FactionAffichee(f, true))];
    }

    private void SurPopup(object? sender, PopupEventArgs e)
    {
        if (e.AssociatedControl?.Tag is Fiche fiche)
        {
            e.ToolTipSize = Taille(fiche);
        }
    }

    private void SurDessin(object? sender, DrawToolTipEventArgs e)
    {
        if (e.AssociatedControl?.Tag is Fiche fiche)
        {
            Rendre(e.Graphics, fiche, e.Bounds);
        }
    }

    private int Gauche(Rectangle zone) => zone.Left + E(LargeurBande) + E(Marge);

    private int E(int pixels) => (int)Math.Round(pixels * (double)_police.Height / HauteurPoliceReference);

    /// <summary>Mesure la fiche (graphique nul) ou la dessine ; une seule mise en page pour les deux, afin que la taille réservée colle au dessin. Retourne la hauteur.</summary>
    private int Rendre(Graphics? g, Fiche fiche, Rectangle zone)
    {
        var hero = fiche.Stocke.Hero;
        var couleurRole = CartesEquipes.CouleurRole(fiche.Role);
        var gauche = Gauche(zone);
        var droite = zone.Right - E(Marge);
        var largeur = droite - gauche;

        if (g is not null)
        {
            using var fond = new SolidBrush(Color.White);
            using var bande = new SolidBrush(couleurRole);
            using var bord = new Pen(Color.Gainsboro);
            g.FillRectangle(fond, zone);
            g.FillRectangle(bande, zone.Left, zone.Top, E(LargeurBande), zone.Height);
            g.DrawRectangle(bord, zone.Left, zone.Top, zone.Width - 1, zone.Height - 1);
        }

        var y = zone.Top + E(EspaceBloc);
        y = Ligne(g, hero.Nom, _titre, SystemColors.WindowText, gauche, y, largeur) + E(EspaceLignes);
        y = Etoiles(g, fiche, gauche, droite, y);
        y = Separateur(g, zone, y);

        var pastilles = new List<Puce> { new(fiche.Role, _police, Color.White, couleurRole) };
        pastilles.AddRange(new[] { hero.Classe, hero.Equipement }.Where(s => !string.IsNullOrEmpty(s)).Select(s => new Puce(s, _police, Color.DimGray, CartesEquipes.FondNeutre)));
        y = Flux(g, pastilles, gauche, droite, y);

        var factions = Factions(fiche.Stocke);
        if (factions.Count > 0)
        {
            y = Separateur(g, zone, y);
            y = Ligne(g, LibelleFactions, _petite, Color.Gray, gauche, y, largeur) + E(EspaceLignes);
            y = Flux(g, [.. factions.Select(PuceDeFaction)], gauche, droite, y);
        }

        y = Separateur(g, zone, y);
        y = Ligne(g, LibelleMiseAJour + OutilsDialogue.LibelleDate(fiche.Stocke.DateMaj), _petite, Color.Gray, gauche, y, largeur);
        return y + E(EspaceBloc) - zone.Top;
    }

    private Puce PuceDeFaction(FactionAffichee faction) => faction.ViaLienAme
        ? new Puce(PuceFaction + faction.Nom + SuffixeLienAme, _italique, Color.DimGray, null)
        : new Puce(PuceFaction + faction.Nom, _police, SystemColors.WindowText, null);

    // Les étoiles à gauche, le badge « lien d'âme » à droite s'il est actif.
    private int Etoiles(Graphics? g, Fiche fiche, int gauche, int droite, int y)
    {
        var hauteur = _police.Height + 2 * E(PaddingVertical);
        if (g is not null)
        {
            var libelle = CartesEquipes.LibelleEtoiles(fiche.Stocke.Hero.Etoiles);
            TextRenderer.DrawText(g, libelle, _police, new Rectangle(gauche, y + E(PaddingVertical), droite - gauche, _police.Height), CouleurEtoiles, FormatTexte);
            if (fiche.Stocke.LienAme)
            {
                var largeurBadge = TextRenderer.MeasureText(LibelleLienAme, _petite, Size.Empty, FormatMesure).Width + 2 * E(PaddingHorizontal);
                DessinerPuce(g, new Puce(LibelleLienAme, _petite, CartesEquipes.TexteVert, CartesEquipes.FondVert), new Rectangle(droite - largeurBadge, y, largeurBadge, hauteur));
            }
        }
        return y + hauteur;
    }

    private int Flux(Graphics? g, IReadOnlyList<Puce> puces, int gauche, int droite, int y)
    {
        var x = gauche;
        var hauteurLigne = 0;
        foreach (var puce in puces)
        {
            var marge = puce.Fond is null ? 0 : E(PaddingHorizontal);
            var largeur = TextRenderer.MeasureText(puce.Texte, puce.Police, Size.Empty, FormatMesure).Width + 2 * marge;
            var hauteur = puce.Police.Height + 2 * E(PaddingVertical);
            if (x > gauche && x + largeur > droite)
            {
                x = gauche;
                y += hauteurLigne + E(EspaceLignes);
                hauteurLigne = 0;
            }
            if (g is not null)
            {
                DessinerPuce(g, puce, new Rectangle(x, y, largeur, hauteur));
            }
            x += largeur + E(EspacePuces);
            hauteurLigne = Math.Max(hauteurLigne, hauteur);
        }
        return y + hauteurLigne;
    }

    private void DessinerPuce(Graphics g, Puce puce, Rectangle zone)
    {
        if (puce.Fond is Color fond)
        {
            var lissage = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pinceau = new SolidBrush(fond);
            using var forme = Arrondi(zone, E(RayonPastille));
            g.FillPath(pinceau, forme);
            g.SmoothingMode = lissage;
        }
        TextRenderer.DrawText(g, puce.Texte, puce.Police, zone, puce.CouleurTexte, FormatPuce);
    }

    private static GraphicsPath Arrondi(Rectangle zone, int rayon)
    {
        var diametre = Math.Min(rayon * 2, Math.Min(zone.Width, zone.Height));
        var forme = new GraphicsPath();
        forme.AddArc(zone.Left, zone.Top, diametre, diametre, 180, 90);
        forme.AddArc(zone.Right - diametre, zone.Top, diametre, diametre, 270, 90);
        forme.AddArc(zone.Right - diametre, zone.Bottom - diametre, diametre, diametre, 0, 90);
        forme.AddArc(zone.Left, zone.Bottom - diametre, diametre, diametre, 90, 90);
        forme.CloseFigure();
        return forme;
    }

    private int Separateur(Graphics? g, Rectangle zone, int y)
    {
        y += E(EspaceBloc);
        if (g is not null)
        {
            using var trait = new Pen(Color.Gainsboro);
            g.DrawLine(trait, Gauche(zone), y, zone.Right - E(Marge), y);
        }
        return y + 1 + E(EspaceBloc);
    }

    private static int Ligne(Graphics? g, string texte, Font police, Color couleur, int x, int y, int largeur)
    {
        if (g is not null)
        {
            TextRenderer.DrawText(g, texte, police, new Rectangle(x, y, largeur, police.Height), couleur, FormatTexte);
        }
        return y + police.Height;
    }
}
