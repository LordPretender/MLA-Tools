using System.Globalization;

namespace MlaTools;

/// <summary>Critères du Générateur réglés par équipe (rang = index) : héros et factions imposés ou bannis, étoiles minimum par héros, avec les règles de cohérence qui les lient.</summary>
internal sealed class ReglagesEquipes
{
    private const char SeparateurEquipes = ';';
    private const char SeparateurRangEtIds = ':';
    private const char SeparateurIds = ',';

    private readonly Dictionary<int, HashSet<int>> _imposes = [];
    private readonly Dictionary<int, HashSet<int>> _bannis = [];
    private readonly Dictionary<int, int> _etoilesMiniHeros = [];
    private readonly Dictionary<int, HashSet<string>> _factionsImposees = [];
    private readonly Dictionary<int, HashSet<string>> _factionsBannies = [];

    private const int EtoilesMiniCompositionParDefaut = 0;
    private const bool LienAmeActifParDefaut = true;

    /// <summary>Niveau composition : seuil d'étoiles par héros valable pour toutes les équipes ; 0 = aucun. Hors <see cref="Reinitialiser"/> : il n'appartient à aucune équipe, seul <see cref="ReinitialiserComposition"/> le remet à zéro.</summary>
    public int EtoilesMiniHerosComposition { get; set; } = EtoilesMiniCompositionParDefaut;

    /// <summary>Niveau composition : faux dans un mode de jeu sans lien d'âme. Hors <see cref="Reinitialiser"/>, comme le seuil.</summary>
    public bool LienAmeActif { get; set; } = LienAmeActifParDefaut;

    /// <summary>Vrai quand le niveau composition s'écarte de ses valeurs par défaut.</summary>
    public bool ADesReglagesComposition => EtoilesMiniHerosComposition != EtoilesMiniCompositionParDefaut || LienAmeActif != LienAmeActifParDefaut;

    /// <summary>Remet le niveau composition à ses valeurs par défaut ; les réglages des équipes ne bougent pas.</summary>
    public void ReinitialiserComposition()
    {
        EtoilesMiniHerosComposition = EtoilesMiniCompositionParDefaut;
        LienAmeActif = LienAmeActifParDefaut;
    }

    public IReadOnlyCollection<int> Imposes(int equipe) => Lire(_imposes, equipe);

    /// <summary>Seuil propre à l'équipe, sans le seuil de la composition.</summary>
    public int EtoilesMiniHeros(int equipe) => _etoilesMiniHeros.GetValueOrDefault(equipe);

    // Le plus élevé des deux : une équipe peut durcir le seuil de la composition, jamais l'assouplir.
    private int EtoilesMiniHerosEffectif(int equipe) => Math.Max(EtoilesMiniHerosComposition, EtoilesMiniHeros(equipe));

    /// <summary>0 retire le seuil.</summary>
    public void FixerEtoilesMiniHeros(int equipe, int etoiles)
    {
        if (etoiles > 0)
        {
            _etoilesMiniHeros[equipe] = etoiles;
        }
        else
        {
            _etoilesMiniHeros.Remove(equipe);
        }
    }

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

    public IReadOnlyCollection<string> FactionsImposees(int equipe) => Lire(_factionsImposees, equipe);

    public IReadOnlyCollection<string> FactionsBannies(int equipe) => Lire(_factionsBannies, equipe);

    /// <summary>Refusé quand la faction est bannie dans cette même équipe. Une faction peut être imposée à plusieurs équipes.</summary>
    public bool ImposerFaction(int equipe, string faction)
    {
        if (Lire(_factionsBannies, equipe).Contains(faction))
        {
            return false;
        }
        Ensemble(_factionsImposees, equipe).Add(faction);
        return true;
    }

    /// <summary>Refusé quand la faction est imposée dans cette même équipe.</summary>
    public bool BannirFaction(int equipe, string faction)
    {
        if (Lire(_factionsImposees, equipe).Contains(faction))
        {
            return false;
        }
        Ensemble(_factionsBannies, equipe).Add(faction);
        return true;
    }

    public void RetirerFactionImposee(int equipe, string faction) => Lire(_factionsImposees, equipe).Remove(faction);

    public void RetirerFactionBannie(int equipe, string faction) => Lire(_factionsBannies, equipe).Remove(faction);

    public void RetirerImpose(int equipe, int heroId) => Lire(_imposes, equipe).Remove(heroId);

    public void RetirerBanni(int equipe, int heroId) => Lire(_bannis, equipe).Remove(heroId);

    public void Reinitialiser(int equipe)
    {
        _imposes.Remove(equipe);
        _bannis.Remove(equipe);
        _etoilesMiniHeros.Remove(equipe);
        _factionsImposees.Remove(equipe);
        _factionsBannies.Remove(equipe);
    }

    /// <summary>Vide les réglages de toutes les équipes ; le niveau composition, qui n'appartient à aucune équipe, reste.</summary>
    public void ReinitialiserToutes()
    {
        _imposes.Clear();
        _bannis.Clear();
        _etoilesMiniHeros.Clear();
        _factionsImposees.Clear();
        _factionsBannies.Clear();
    }

    /// <summary>Vrai dès qu'une équipe a au moins un critère ; le niveau composition n'en est pas un.</summary>
    public bool ADesReglages =>
        _imposes.Values.Any(e => e.Count > 0) || _bannis.Values.Any(e => e.Count > 0) || _etoilesMiniHeros.Count > 0
        || _factionsImposees.Values.Any(e => e.Count > 0) || _factionsBannies.Values.Any(e => e.Count > 0);

    /// <summary>Libellés des critères actifs d'une équipe, vide quand elle n'en a aucun.</summary>
    public IReadOnlyList<string> Resume(int equipe)
    {
        var resume = new List<string>();
        AjouterCompte(resume, Lire(_imposes, equipe).Count, "héros imposé", "héros imposés");
        AjouterCompte(resume, Lire(_bannis, equipe).Count, "héros banni", "héros bannis");
        AjouterCompte(resume, Lire(_factionsImposees, equipe).Count, "faction imposée", "factions imposées");
        AjouterCompte(resume, Lire(_factionsBannies, equipe).Count, "faction bannie", "factions bannies");
        if (EtoilesMiniHeros(equipe) > 0)
        {
            resume.Add($"héros ≥ {EtoilesMiniHeros(equipe)} étoiles");
        }
        return resume;
    }

    private static void AjouterCompte(List<string> resume, int nombre, string singulier, string pluriel)
    {
        if (nombre > 0)
        {
            resume.Add($"{nombre} {(nombre > 1 ? pluriel : singulier)}");
        }
    }

    /// <summary>Critères d'une équipe ; un héros qui n'existe plus (existe faux) est ignoré.</summary>
    public CriteresEquipe Criteres(int equipe, Func<int, bool> existe) =>
        new(Lire(_imposes, equipe).Where(existe).ToHashSet(), Lire(_bannis, equipe).Where(existe).ToHashSet(), EtoilesMiniHerosEffectif(equipe))
        {
            FactionsImposees = Lire(_factionsImposees, equipe).ToHashSet(),
            FactionsBannies = Lire(_factionsBannies, equipe).ToHashSet(),
        };

    public IReadOnlyList<CriteresEquipe> Pour(int nombreEquipes, Func<int, bool> existe) =>
        [.. Enumerable.Range(0, nombreEquipes).Select(i => Criteres(i, existe))];

    public ReglagesEquipes Copier()
    {
        var copie = new ReglagesEquipes { EtoilesMiniHerosComposition = EtoilesMiniHerosComposition, LienAmeActif = LienAmeActif };
        foreach (var (equipe, ids) in _imposes)
        {
            copie._imposes[equipe] = [.. ids];
        }
        foreach (var (equipe, ids) in _bannis)
        {
            copie._bannis[equipe] = [.. ids];
        }
        foreach (var (equipe, etoiles) in _etoilesMiniHeros)
        {
            copie._etoilesMiniHeros[equipe] = etoiles;
        }
        foreach (var (equipe, factions) in _factionsImposees)
        {
            copie._factionsImposees[equipe] = [.. factions];
        }
        foreach (var (equipe, factions) in _factionsBannies)
        {
            copie._factionsBannies[equipe] = [.. factions];
        }
        return copie;
    }

    public string SerialiserImposes() => Serialiser(_imposes);

    public string SerialiserBannis() => Serialiser(_bannis);

    public string SerialiserFactionsImposees() => Serialiser(_factionsImposees);

    public string SerialiserFactionsBannies() => Serialiser(_factionsBannies);

    public string SerialiserEtoilesMiniHeros() =>
        string.Join(SeparateurEquipes, _etoilesMiniHeros
            .OrderBy(e => e.Key)
            .Select(e => $"{e.Key.ToString(CultureInfo.InvariantCulture)}{SeparateurRangEtIds}{e.Value.ToString(CultureInfo.InvariantCulture)}"));

    /// <summary>Valeurs issues de la base : une partie illisible ou incohérente est ignorée, le reste est gardé.</summary>
    public static ReglagesEquipes Charger(
        string? imposes, string? bannis, string? etoilesMiniHeros = null, int etoilesMiniHerosComposition = 0, string? factionsImposees = null, string? factionsBannies = null, bool lienAmeActif = true)
    {
        var reglages = new ReglagesEquipes { EtoilesMiniHerosComposition = Math.Clamp(etoilesMiniHerosComposition, 0, FormOptionsGeneration.EtoilesMinimumMax), LienAmeActif = lienAmeActif };
        foreach (var (equipe, id) in Decoder(imposes))
        {
            reglages.Imposer(equipe, id);
        }
        foreach (var (equipe, id) in Decoder(bannis))
        {
            reglages.Bannir(equipe, id);
        }
        // Une faction inconnue (base éditée à la main ou liste qui a changé) est ignorée.
        foreach (var (equipe, faction) in DecoderJetons(factionsImposees).Where(j => Referentiel.Factions.Contains(j.Jeton)))
        {
            reglages.ImposerFaction(equipe, faction);
        }
        foreach (var (equipe, faction) in DecoderJetons(factionsBannies).Where(j => Referentiel.Factions.Contains(j.Jeton)))
        {
            reglages.BannirFaction(equipe, faction);
        }
        // Même forme « rang:valeur » que les listes d'identifiants, avec une seule valeur par rang.
        foreach (var (equipe, etoiles) in Decoder(etoilesMiniHeros))
        {
            reglages.FixerEtoilesMiniHeros(equipe, Math.Min(etoiles, FormOptionsGeneration.EtoilesMinimumMax));
        }
        return reglages;
    }

    // Un ensemble absent se lit comme vide, sans être créé.
    private static HashSet<T> Lire<T>(Dictionary<int, HashSet<T>> source, int equipe) =>
        source.TryGetValue(equipe, out var valeurs) ? valeurs : [];

    private static HashSet<T> Ensemble<T>(Dictionary<int, HashSet<T>> source, int equipe)
    {
        if (!source.TryGetValue(equipe, out var valeurs))
        {
            valeurs = [];
            source[equipe] = valeurs;
        }
        return valeurs;
    }

    private static string Serialiser(Dictionary<int, HashSet<int>> source) =>
        Serialiser(source, id => id.ToString(CultureInfo.InvariantCulture));

    private static string Serialiser(Dictionary<int, HashSet<string>> source) =>
        Serialiser(source, faction => faction);

    private static string Serialiser<T>(Dictionary<int, HashSet<T>> source, Func<T, string> texte) where T : IComparable<T> =>
        string.Join(SeparateurEquipes, source
            .Where(e => e.Value.Count > 0)
            .OrderBy(e => e.Key)
            .Select(e => $"{e.Key.ToString(CultureInfo.InvariantCulture)}{SeparateurRangEtIds}{string.Join(SeparateurIds, e.Value.Order().Select(texte))}"));

    private static IEnumerable<(int Equipe, int Id)> Decoder(string? texte)
    {
        foreach (var (equipe, jeton) in DecoderJetons(texte))
        {
            if (int.TryParse(jeton, NumberStyles.None, CultureInfo.InvariantCulture, out var heroId))
            {
                yield return (equipe, heroId);
            }
        }
    }

    // Forme « rang:valeur,valeur;rang:valeur » ; une valeur reste du texte, c'est à l'appelant de la valider.
    private static IEnumerable<(int Equipe, string Jeton)> DecoderJetons(string? texte)
    {
        foreach (var groupe in (texte ?? string.Empty).Split(SeparateurEquipes, StringSplitOptions.RemoveEmptyEntries))
        {
            var parties = groupe.Split(SeparateurRangEtIds);
            if (parties.Length != 2 || !int.TryParse(parties[0], NumberStyles.None, CultureInfo.InvariantCulture, out var equipe))
            {
                continue;
            }
            foreach (var jeton in parties[1].Split(SeparateurIds))
            {
                yield return (equipe, jeton);
            }
        }
    }
}
