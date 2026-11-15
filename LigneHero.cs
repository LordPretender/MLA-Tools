namespace MlaTools;

/// <summary>Ligne d'un héros dans une carte d'équipe : bande de couleur du rôle, nom, classe et rôle, étoiles à droite.</summary>
/// <remarks>Dessinée à la main : un nom trop long est coupé par « … » au lieu de passer à la ligne.</remarks>
internal sealed class LigneHero : Control
{
    public const int Hauteur = 44;

    private const int LargeurBande = 5;
    private const int Marge = 12;
    private const int HauteurNom = 18;
    private const int HauteurDetail = 16;
    private const int HautNom = 5;
    private const int HautDetail = 23;
    private const TextFormatFlags FormatGauche = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
    private const TextFormatFlags FormatDroite = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;

    private readonly string _nom;
    private readonly string _detail;
    private readonly string _etoiles;
    private readonly Color _couleurRole;
    private readonly Font _gras;

    public LigneHero(int heroId, string nom, string detail, string etoiles, Color couleurRole, bool selectionne, Font police, Font gras)
    {
        HeroId = heroId;
        _nom = nom;
        _detail = detail;
        _etoiles = etoiles;
        _couleurRole = couleurRole;
        _gras = gras;
        Font = police;
        Height = Hauteur;
        Margin = Padding.Empty;
        Cursor = Cursors.Hand;
        BackColor = selectionne ? Color.Khaki : Color.White;
        DoubleBuffered = true;
    }

    public int HeroId { get; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var bande = new SolidBrush(_couleurRole))
        {
            e.Graphics.FillRectangle(bande, 0, 0, LargeurBande, Height);
        }
        using (var separateur = new Pen(Color.Gainsboro))
        {
            e.Graphics.DrawLine(separateur, 0, Height - 1, Width, Height - 1);
        }

        var largeurEtoiles = TextRenderer.MeasureText(e.Graphics, _etoiles, Font).Width;
        var zoneEtoiles = new Rectangle(Width - largeurEtoiles - Marge, 0, largeurEtoiles, Height);
        TextRenderer.DrawText(e.Graphics, _etoiles, Font, zoneEtoiles, Color.DimGray, FormatDroite);

        var gauche = LargeurBande + Marge;
        var largeurTexte = Math.Max(0, zoneEtoiles.Left - Marge - gauche);
        TextRenderer.DrawText(e.Graphics, _nom, _gras, new Rectangle(gauche, HautNom, largeurTexte, HauteurNom), ForeColor, FormatGauche);
        TextRenderer.DrawText(e.Graphics, _detail, Font, new Rectangle(gauche, HautDetail, largeurTexte, HauteurDetail), Color.DimGray, FormatGauche);
    }
}
