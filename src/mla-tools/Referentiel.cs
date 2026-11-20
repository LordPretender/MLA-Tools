using System.Globalization;
using System.Text;

namespace MlaTools;

internal static class Referentiel
{
    public const string FactionOrdre = "Ordre";
    public const string FactionChaos = "Chaos";

    // Déclarée avant Factions, qui la reprend : les initialiseurs statiques s'exécutent dans l'ordre du texte.
    public static readonly string[] FactionsRegulieres = ["Élémental", "Technique", "Arts Martiaux", "Lumière", "Obscur"];

    public static readonly string[] Factions = [.. FactionsRegulieres, FactionOrdre, FactionChaos, "Astral"];

    public static readonly string[] Classes =
    [
        "Adc", "Tank", "Mage", "Combattant", "Support", "Sniper", "Ranger", "Garde d'acier",
        "Stratégiste", "Sorcier", "Arcaniste", "Chevalier Dragon", "Inquisiteur", "Esprit", "Oracle",
    ];

    public const string EquipementMoyen = "moyen";
    public const string EquipementLourd = "lourd";

    public static readonly string[] Equipements = ["léger", EquipementMoyen, EquipementLourd];

    // Espaces multiples et retours à la ligne de l'OCR ramenés à un seul espace.
    public static string Compacter(string texte) =>
        string.Join(' ', texte.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // Sans accents ni casse, ponctuation écartée : l'OCR et le fichier de données ne s'écrivent pas pareil.
    public static string Normaliser(string texte)
    {
        var decompose = texte.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decompose.Length);
        foreach (var c in decompose)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
            else if (char.IsWhiteSpace(c))
            {
                sb.Append(' ');
            }
        }
        return sb.ToString().Trim();
    }

    // Deux fiches forment un même héros en combat quand l'une porte le nom de l'autre précédé d'un qualificatif (« Singularity Lunox » et « Lunox »).
    // La famille est l'identifiant du nom le plus court du groupe ; un nom vide ne rejoint aucun groupe.
    public static Dictionary<int, int> Familles(IEnumerable<(int Id, string Nom)> heros)
    {
        var normes = heros.Select(h => (h.Id, Norme: Normaliser(h.Nom))).ToList();
        return normes.ToDictionary(
            h => h.Id,
            h => h.Norme.Length == 0
                ? h.Id
                : normes
                    .Where(b => b.Norme.Length > 0 && (b.Norme == h.Norme || h.Norme.EndsWith(' ' + b.Norme, StringComparison.Ordinal)))
                    .OrderBy(b => b.Norme.Length).ThenBy(b => b.Id)
                    .First().Id);
    }

    // Retourne null si rien n'est assez proche ou si deux valeurs sont à égalité : une valeur illisible n'est jamais devinée.
    public static string? Rapprocher(string lu, IReadOnlyList<string> canoniques)
    {
        var norme = Normaliser(lu);
        if (norme.Length == 0)
        {
            return null;
        }

        var fenetres = SuitesDeMots(norme);
        string? meilleure = null;
        var meilleureDistance = int.MaxValue;
        var egalite = false;
        foreach (var candidate in canoniques)
        {
            var normeCandidate = Normaliser(candidate);
            var distance = fenetres.Min(f => Levenshtein(f, normeCandidate));
            if (distance > Math.Max(1, normeCandidate.Length / 4))
            {
                continue;
            }
            if (distance < meilleureDistance)
            {
                meilleure = candidate;
                meilleureDistance = distance;
                egalite = false;
            }
            else if (distance == meilleureDistance)
            {
                egalite = true;
            }
        }
        return egalite ? null : meilleure;
    }

    // L'OCR lit parfois l'icône voisine (« (9 Obscur ») ou une ligne en trop (« Arts Martial IY ») avec le texte utile :
    // chaque suite contiguë de mots est une lecture possible.
    private static List<string> SuitesDeMots(string norme)
    {
        var mots = norme.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var suites = new List<string>();
        for (var debut = 0; debut < mots.Length; debut++)
        {
            for (var fin = debut; fin < mots.Length; fin++)
            {
                suites.Add(string.Join(' ', mots[debut..(fin + 1)]));
            }
        }
        return suites;
    }

    private static int Levenshtein(string a, string b)
    {
        var precedente = new int[b.Length + 1];
        var courante = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            precedente[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            courante[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cout = a[i - 1] == b[j - 1] ? 0 : 1;
                courante[j] = Math.Min(Math.Min(courante[j - 1] + 1, precedente[j] + 1), precedente[j - 1] + cout);
            }
            (precedente, courante) = (courante, precedente);
        }
        return precedente[b.Length];
    }
}
