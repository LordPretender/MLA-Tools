namespace MlaTools;

internal static class CompteurEtoiles
{
    // Seuils initiaux de la teinte rouge-rose des étoiles : à ajuster sur de vraies captures.
    private const float TeinteMaxRouge = 15f;
    private const float TeinteMinRose = 330f;
    private const float SaturationMin = 0.5f;
    private const float LuminositeMin = 0.35f;
    private const float LuminositeMax = 0.8f;
    private const int SurfaceMinEtoile = 20;

    // Jaune doré des étoiles du palier 5, mesuré à l'œil sur les captures : à ajuster au besoin.
    private const int PalierJaune = 5;
    private const int MinJaunes = 3;
    private const float TeinteMinJaune = 35f;
    private const float TeinteMaxJaune = 65f;
    private const float LuminositeMinJaune = 0.45f;

    internal static ResultatEtoiles Compter(Bitmap capture, Zone zone)
    {
        var nombre = CompterComposantes(capture, zone, EstRouge);
        if (nombre is >= 1 and <= LecteurEtoiles.MaxRouges)
        {
            return new ResultatEtoiles(LecteurEtoiles.PremierRouge - 1 + nombre, null);
        }

        // Le palier jaune est toujours affiché avec ses 5 étoiles : plusieurs taches jaunes suffisent à le reconnaître.
        if (nombre == 0 && CompterComposantes(capture, zone, EstJaune) >= MinJaunes)
        {
            return new ResultatEtoiles(PalierJaune, null);
        }
        return new ResultatEtoiles(null, $"Palier à revoir ({nombre} étoile(s) rouge(s) détectée(s))");
    }

    private static bool EstJaune(Color c)
    {
        var teinte = c.GetHue();
        return teinte is >= TeinteMinJaune and <= TeinteMaxJaune
            && c.GetSaturation() >= SaturationMin
            && c.GetBrightness() >= LuminositeMinJaune;
    }

    private static bool EstRouge(Color c)
    {
        var teinte = c.GetHue();
        return (teinte <= TeinteMaxRouge || teinte >= TeinteMinRose)
            && c.GetSaturation() >= SaturationMin
            && c.GetBrightness() is >= LuminositeMin and <= LuminositeMax;
    }

    // Une étoile = une composante connexe de pixels rouges ; les petites taches (bruit, reflets) sont ignorées.
    private static int CompterComposantes(Bitmap capture, Zone zone, Func<Color, bool> estEtoile)
    {
        var masque = new bool[zone.Largeur * zone.Hauteur];
        for (var y = 0; y < zone.Hauteur; y++)
        {
            for (var x = 0; x < zone.Largeur; x++)
            {
                masque[y * zone.Largeur + x] = estEtoile(capture.GetPixel(zone.X + x, zone.Y + y));
            }
        }

        return Composantes.Lister(masque, zone.Largeur, zone.Hauteur, huitVoisins: false)
            .Count(composante => composante.Count >= SurfaceMinEtoile);
    }
}
