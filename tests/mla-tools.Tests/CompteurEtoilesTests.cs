using System.Drawing;
using Xunit;

namespace MlaTools.Tests;

public class CompteurEtoilesTests
{
    private const int LargeurCapture = 200;
    private const int HauteurCapture = 40;
    private const int CoteTache = 6;
    private const int EspacementTaches = 20;
    private const int MargeTaches = 10;
    private const int MilieuCapture = LargeurCapture / 2;
    private const int XPremiereTacheMoitieDroite = 110;
    private const int XSecondeTacheMoitieDroite = 130;
    private const int XTacheHorsZone = 150;

    private static readonly Color Fond = Color.FromArgb(128, 128, 128);
    private static readonly Color Rouge = Color.FromArgb(200, 40, 40);
    private static readonly Color Jaune = Color.FromArgb(230, 200, 40);
    private static readonly Zone ZoneComplete = new(0, 0, LargeurCapture, HauteurCapture);

    private static Bitmap Capture()
    {
        var capture = new Bitmap(LargeurCapture, HauteurCapture);
        for (var y = 0; y < HauteurCapture; y++)
        {
            for (var x = 0; x < LargeurCapture; x++)
            {
                capture.SetPixel(x, y, Fond);
            }
        }
        return capture;
    }

    private static void Tache(Bitmap capture, int x, int y, int largeur, int hauteur, Color couleur)
    {
        for (var dy = 0; dy < hauteur; dy++)
        {
            for (var dx = 0; dx < largeur; dx++)
            {
                capture.SetPixel(x + dx, y + dy, couleur);
            }
        }
    }

    private static Bitmap CaptureAvecTaches(int nombre, Color couleur)
    {
        var capture = Capture();
        for (var i = 0; i < nombre; i++)
        {
            Tache(capture, MargeTaches + i * EspacementTaches, MargeTaches, CoteTache, CoteTache, couleur);
        }
        return capture;
    }

    private static ResultatEtoiles ValeurAttendue(int valeur) => new(valeur, null);

    private static ResultatEtoiles AlerteAttendue(int rouges) =>
        new(null, $"Palier à revoir ({rouges} étoile(s) rouge(s) détectée(s))");

    [Theory]
    [InlineData(1, 6)]
    [InlineData(2, 7)]
    [InlineData(3, 8)]
    [InlineData(4, 9)]
    [InlineData(5, 10)]
    public void Compter_UnACinqRouges_RendCinqPlusLeNombre(int rouges, int valeurAttendue)
    {
        using var capture = CaptureAvecTaches(rouges, Rouge);

        Assert.Equal(ValeurAttendue(valeurAttendue), CompteurEtoiles.Compter(capture, ZoneComplete));
    }

    [Fact]
    public void Compter_AucuneTache_RendUneAlerte()
    {
        using var capture = Capture();

        Assert.Equal(AlerteAttendue(0), CompteurEtoiles.Compter(capture, ZoneComplete));
    }

    [Fact]
    public void Compter_SixRouges_RendUneAlerte()
    {
        using var capture = CaptureAvecTaches(6, Rouge);

        Assert.Equal(AlerteAttendue(6), CompteurEtoiles.Compter(capture, ZoneComplete));
    }

    [Fact]
    public void Compter_TroisJaunesSansRouge_RendLePalierJaune()
    {
        using var capture = CaptureAvecTaches(3, Jaune);

        Assert.Equal(ValeurAttendue(5), CompteurEtoiles.Compter(capture, ZoneComplete));
    }

    [Fact]
    public void Compter_DeuxJaunesSansRouge_RendUneAlerte()
    {
        using var capture = CaptureAvecTaches(2, Jaune);

        Assert.Equal(AlerteAttendue(0), CompteurEtoiles.Compter(capture, ZoneComplete));
    }

    [Fact]
    public void Compter_UnRougeEtTroisJaunes_LeRougePrime()
    {
        using var capture = CaptureAvecTaches(3, Jaune);
        Tache(capture, MargeTaches + 3 * EspacementTaches, MargeTaches, CoteTache, CoteTache, Rouge);

        Assert.Equal(ValeurAttendue(6), CompteurEtoiles.Compter(capture, ZoneComplete));
    }

    [Fact]
    public void Compter_TacheDeVingtPixels_EstComptee()
    {
        using var capture = Capture();
        Tache(capture, MargeTaches, MargeTaches, 4, 5, Rouge);

        Assert.Equal(ValeurAttendue(6), CompteurEtoiles.Compter(capture, ZoneComplete));
    }

    [Fact]
    public void Compter_TacheDeDixNeufPixels_EstIgnoree()
    {
        using var capture = Capture();
        Tache(capture, MargeTaches, MargeTaches, 4, 5, Rouge);
        capture.SetPixel(MargeTaches, MargeTaches, Fond);

        Assert.Equal(AlerteAttendue(0), CompteurEtoiles.Compter(capture, ZoneComplete));
    }

    [Fact]
    public void Compter_TacheHorsDeLaZone_EstIgnoree()
    {
        using var capture = CaptureAvecTaches(1, Rouge);
        Tache(capture, XTacheHorsZone, MargeTaches, CoteTache, CoteTache, Rouge);

        Assert.Equal(ValeurAttendue(6), CompteurEtoiles.Compter(capture, new Zone(0, 0, MilieuCapture, HauteurCapture)));
    }

    [Fact]
    public void Compter_ZoneDecalee_CompteSeulementSesTaches()
    {
        using var capture = CaptureAvecTaches(1, Rouge);
        Tache(capture, XPremiereTacheMoitieDroite, MargeTaches, CoteTache, CoteTache, Rouge);
        Tache(capture, XSecondeTacheMoitieDroite, MargeTaches, CoteTache, CoteTache, Rouge);

        Assert.Equal(ValeurAttendue(7), CompteurEtoiles.Compter(capture, new Zone(MilieuCapture, 0, MilieuCapture, HauteurCapture)));
    }

    [Fact]
    public void Compter_DeuxTachesTouchantParUnCoin_SontDeuxEtoiles()
    {
        using var capture = Capture();
        Tache(capture, MargeTaches, MargeTaches, CoteTache, CoteTache, Rouge);
        Tache(capture, MargeTaches + CoteTache, MargeTaches + CoteTache, CoteTache, CoteTache, Rouge);

        Assert.Equal(ValeurAttendue(7), CompteurEtoiles.Compter(capture, ZoneComplete));
    }

    [Theory]
    [InlineData(200, 79, 40, true)]
    [InlineData(200, 82, 40, false)]
    [InlineData(200, 40, 119, true)]
    [InlineData(200, 40, 121, false)]
    [InlineData(190, 60, 60, true)]
    [InlineData(190, 65, 65, false)]
    [InlineData(185, 0, 0, true)]
    [InlineData(170, 0, 0, false)]
    [InlineData(255, 150, 150, true)]
    [InlineData(255, 170, 170, false)]
    public void Compter_CouleurEnLimiteDeSeuil_EstRougeOuNon(int r, int g, int b, bool estRouge)
    {
        using var capture = CaptureAvecTaches(1, Color.FromArgb(r, g, b));

        var attendu = estRouge ? ValeurAttendue(6) : AlerteAttendue(0);
        Assert.Equal(attendu, CompteurEtoiles.Compter(capture, ZoneComplete));
    }

    [Theory]
    [InlineData(230, 152, 40, true)]
    [InlineData(230, 147, 40, false)]
    public void Compter_TeinteJauneEnLimite_EstJauneOuNon(int r, int g, int b, bool estJaune)
    {
        using var capture = CaptureAvecTaches(3, Color.FromArgb(r, g, b));

        var attendu = estJaune ? ValeurAttendue(5) : AlerteAttendue(0);
        Assert.Equal(attendu, CompteurEtoiles.Compter(capture, ZoneComplete));
    }
}
