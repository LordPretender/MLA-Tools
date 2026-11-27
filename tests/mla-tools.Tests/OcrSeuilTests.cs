using Xunit;

namespace MlaTools.Tests;

public class OcrSeuilTests
{
    private const int NiveauxGris = 256;
    private const int FondSombre = 40;
    private const int TexteGrisClair = 170;
    private const int TexteBlanc = 250;
    private const int PixelsFond = 900;
    private const int PixelsTexte = 100;

    private static int[] Histogramme(params (int Niveau, int Nombre)[] classes)
    {
        var histogramme = new int[NiveauxGris];
        foreach (var (niveau, nombre) in classes)
        {
            histogramme[niveau] += nombre;
        }
        return histogramme;
    }

    [Fact]
    public void CalculerSeuilOtsu_TexteGrisSurFondSombre_SeuilEntreLesDeuxClasses()
    {
        var seuil = Ocr.CalculerSeuilOtsu(Histogramme((FondSombre, PixelsFond), (TexteGrisClair, PixelsTexte)));

        Assert.InRange(seuil, FondSombre + 1, TexteGrisClair);
    }

    [Fact]
    public void CalculerSeuilOtsu_TexteBlancSurFondSombre_SeuilEntreLesDeuxClasses()
    {
        var seuil = Ocr.CalculerSeuilOtsu(Histogramme((FondSombre, PixelsFond), (TexteBlanc, PixelsTexte)));

        Assert.InRange(seuil, FondSombre + 1, TexteBlanc);
    }

    [Fact]
    public void CalculerSeuilOtsu_UneSeuleClasse_Zero()
    {
        Assert.Equal(0, Ocr.CalculerSeuilOtsu(Histogramme((FondSombre, PixelsFond))));
    }
}
