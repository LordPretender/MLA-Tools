namespace MlaTools;

internal sealed record ResultatEtoiles(int? Valeur, string? Alerte);

internal sealed record Palier(int Valeur, string Libelle);

internal static class LecteurEtoiles
{
    internal const int PremierRouge = 6;
    internal const int MaxRouges = 5;
    private const int PremierEveil = 11;
    private const int MaxEveil = 8;
    private const string MotEveille = "Éveillé";

    public static readonly IReadOnlyList<Palier> Paliers = ConstruirePaliers();

    public static async Task<ResultatEtoiles> LireAsync(Bitmap capture, Zone etoiles, Zone chiffreEveil)
    {
        var texte = await Ocr.LireAsync(capture, etoiles);
        var estEveille = Referentiel.Compacter(texte).Split(' ')
            .Any(mot => Referentiel.Rapprocher(mot, [MotEveille]) is not null);

        return estEveille ? await LireEveilAsync(capture, chiffreEveil) : CompteurEtoiles.Compter(capture, etoiles);
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
