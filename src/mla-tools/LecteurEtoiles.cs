namespace MlaTools;

internal sealed record ResultatEtoiles(int? Valeur, string? Alerte);

internal sealed record Palier(int Valeur, string Libelle);

internal static class LecteurEtoiles
{
    private const int PremierRouge = 6;
    private const int MaxRouges = 5;
    private const int PremierEveil = 11;
    private const int MaxEveil = 8;
    private const string MotEveille = "Éveillé";

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

    public static readonly IReadOnlyList<Palier> Paliers = ConstruirePaliers();

    public static async Task<ResultatEtoiles> LireAsync(Bitmap capture, Zone etoiles, Zone chiffreEveil)
    {
        var texte = await Ocr.LireAsync(capture, etoiles);
        var estEveille = Referentiel.Compacter(texte).Split(' ')
            .Any(mot => Referentiel.Rapprocher(mot, [MotEveille]) is not null);

        return estEveille ? await LireEveilAsync(capture, chiffreEveil) : CompterRouges(capture, etoiles);
    }

    private static async Task<ResultatEtoiles> LireEveilAsync(Bitmap capture, Zone chiffreEveil)
    {
        var texte = await Ocr.LireChiffreAsync(capture, chiffreEveil);
        // Le chiffre est répété à la lecture : le plus fréquent écarte une traînée prise pour un caractère.
        var chiffre = texte.Where(char.IsDigit).GroupBy(c => c).OrderByDescending(g => g.Count())
            .Select(g => g.Key).FirstOrDefault();
        if (chiffre == default)
        {
            return new ResultatEtoiles(PremierEveil, $"Éveillé 0 supposé (pas de chiffre lu, OCR : « {texte} ») : à confirmer");
        }

        var niveau = chiffre - '0';
        return niveau > MaxEveil
            ? new ResultatEtoiles(null, $"Chiffre d'éveil illisible ({chiffre})")
            : new ResultatEtoiles(PremierEveil + niveau, null);
    }

    private static ResultatEtoiles CompterRouges(Bitmap capture, Zone zone)
    {
        var nombre = CompterComposantes(capture, zone, EstRouge);
        if (nombre is >= 1 and <= MaxRouges)
        {
            return new ResultatEtoiles(PremierRouge - 1 + nombre, null);
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

    private static List<Palier> ConstruirePaliers()
    {
        var liste = new List<Palier>
        {
            new(3, "3 étoiles bleues"),
            new(4, "4 étoiles violettes"),
            new(5, "5 étoiles jaunes"),
        };
        for (var n = 1; n <= MaxRouges; n++)
        {
            liste.Add(new Palier(PremierRouge - 1 + n, n == 1 ? "1 étoile rouge" : $"{n} étoiles rouges"));
        }
        for (var n = 0; n <= MaxEveil; n++)
        {
            liste.Add(new Palier(PremierEveil + n, $"{MotEveille} {n}"));
        }
        return liste;
    }
}
