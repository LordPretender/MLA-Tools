using System.Globalization;

namespace MlaTools;

/// <summary>Critères du Générateur réglés par équipe (rang = index) : héros imposés et bannis, avec les règles de cohérence qui les lient.</summary>
internal sealed class ReglagesEquipes
{
    private const char SeparateurEquipes = ';';
    private const char SeparateurRangEtIds = ':';
    private const char SeparateurIds = ',';

    private readonly Dictionary<int, HashSet<int>> _imposes = [];
    private readonly Dictionary<int, HashSet<int>> _bannis = [];

    public IReadOnlyCollection<int> Imposes(int equipe) => Lire(_imposes, equipe);

    public IReadOnlyCollection<int> Bannis(int equipe) => Lire(_bannis, equipe);

    /// <summary>Refusé quand le héros est déjà imposé à une autre équipe (jamais dans deux équipes) ou banni dans celle-ci.</summary>
    public bool Imposer(int equipe, int heroId)
    {
        if (_imposes.Any(e => e.Key != equipe && e.Value.Contains(heroId)) || Lire(_bannis, equipe).Contains(heroId))
        {
            return false;
        }
        Ensemble(_imposes, equipe).Add(heroId);
        return true;
    }

    /// <summary>Refusé quand le héros est imposé dans cette même équipe.</summary>
    public bool Bannir(int equipe, int heroId)
    {
        if (Lire(_imposes, equipe).Contains(heroId))
        {
            return false;
        }
        Ensemble(_bannis, equipe).Add(heroId);
        return true;
    }

    public void RetirerImpose(int equipe, int heroId) => Lire(_imposes, equipe).Remove(heroId);

    public void RetirerBanni(int equipe, int heroId) => Lire(_bannis, equipe).Remove(heroId);

    public void Reinitialiser(int equipe)
    {
        _imposes.Remove(equipe);
        _bannis.Remove(equipe);
    }

    /// <summary>Critères d'une équipe ; un héros qui n'existe plus (existe faux) est ignoré.</summary>
    public CriteresEquipe Criteres(int equipe, Func<int, bool> existe) =>
        new(Lire(_imposes, equipe).Where(existe).ToHashSet(), Lire(_bannis, equipe).Where(existe).ToHashSet());

    public IReadOnlyList<CriteresEquipe> Pour(int nombreEquipes, Func<int, bool> existe) =>
        [.. Enumerable.Range(0, nombreEquipes).Select(i => Criteres(i, existe))];

    public ReglagesEquipes Copier()
    {
        var copie = new ReglagesEquipes();
        foreach (var (equipe, ids) in _imposes)
        {
            copie._imposes[equipe] = [.. ids];
        }
        foreach (var (equipe, ids) in _bannis)
        {
            copie._bannis[equipe] = [.. ids];
        }
        return copie;
    }

    public string SerialiserImposes() => Serialiser(_imposes);

    public string SerialiserBannis() => Serialiser(_bannis);

    /// <summary>Valeurs issues de la base : une partie illisible ou incohérente est ignorée, le reste est gardé.</summary>
    public static ReglagesEquipes Charger(string? imposes, string? bannis)
    {
        var reglages = new ReglagesEquipes();
        foreach (var (equipe, id) in Decoder(imposes))
        {
            reglages.Imposer(equipe, id);
        }
        foreach (var (equipe, id) in Decoder(bannis))
        {
            reglages.Bannir(equipe, id);
        }
        return reglages;
    }

    // Un ensemble absent se lit comme vide, sans être créé.
    private static HashSet<int> Lire(Dictionary<int, HashSet<int>> source, int equipe) =>
        source.TryGetValue(equipe, out var ids) ? ids : [];

    private static HashSet<int> Ensemble(Dictionary<int, HashSet<int>> source, int equipe)
    {
        if (!source.TryGetValue(equipe, out var ids))
        {
            ids = [];
            source[equipe] = ids;
        }
        return ids;
    }

    private static string Serialiser(Dictionary<int, HashSet<int>> source) =>
        string.Join(SeparateurEquipes, source
            .Where(e => e.Value.Count > 0)
            .OrderBy(e => e.Key)
            .Select(e => $"{e.Key.ToString(CultureInfo.InvariantCulture)}{SeparateurRangEtIds}{string.Join(SeparateurIds, e.Value.Order().Select(i => i.ToString(CultureInfo.InvariantCulture)))}"));

    private static IEnumerable<(int Equipe, int Id)> Decoder(string? texte)
    {
        foreach (var groupe in (texte ?? string.Empty).Split(SeparateurEquipes, StringSplitOptions.RemoveEmptyEntries))
        {
            var parties = groupe.Split(SeparateurRangEtIds);
            if (parties.Length != 2 || !int.TryParse(parties[0], NumberStyles.None, CultureInfo.InvariantCulture, out var equipe))
            {
                continue;
            }
            foreach (var id in parties[1].Split(SeparateurIds))
            {
                if (int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var heroId))
                {
                    yield return (equipe, heroId);
                }
            }
        }
    }
}
