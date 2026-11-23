using System.Globalization;
using FonctionCout = System.Func<System.Collections.Generic.List<MlaTools.EvaluationEquipe>, System.Collections.Generic.List<System.Collections.Generic.List<int>>, double>;

namespace MlaTools;

/// <summary><paramref name="Factions"/> sont les factions réelles du héros, que lisent les critères ; <paramref name="LienAme"/> ne joue que sur la résonance.</summary>
internal sealed record HeroMoteur(int Id, int Etoiles, string Classe, IReadOnlyList<string> Factions, string Equipement, int? Famille = null, bool LienAme = false);

/// <summary>Chaos1, Chaos2 et Chaos3 : valeur du 1er, du 2e et du 3e héros Chaos d'une équipe, dans l'unité des points de résonance.</summary>
internal sealed record PoidsEquipes(double Roles, double Resonance, double Etoiles, double Chaos1 = 15, double Chaos2 = 5, double Chaos3 = 3)
{
    public static readonly PoidsEquipes ParDefaut = new(100, 10, 1);

    // Les paliers se cumulent : le 2e héros s'ajoute au 1er, le 3e au 2e ; au-delà de trois, aucun palier de plus.
    public double ValeurChaos(int chaos) => new[] { Chaos1, Chaos2, Chaos3 }.Take(chaos).Sum();
}

/// <summary>Façon de répartir les héros : la première équipe la plus forte possible, ou la somme des scores maximale.</summary>
internal enum ModeGeneration
{
    MeilleureEquipeDAbord = 0,
    SommeMaximale = 1,
}

internal sealed record EvaluationEquipe(int Dps, int Tanks, int TanksSecondaires, int Soutiens, int Etoiles, int PenaliteRoles, int Points, string Description, int Chaos = 0);

/// <summary>Héros et factions imposés ou bannis d'une équipe, et étoiles minimum de chaque héros (0 = aucun seuil) ; contrainte dure, tenue avant tout score.</summary>
internal sealed record CriteresEquipe(IReadOnlySet<int> Imposes, IReadOnlySet<int> Bannis, int EtoilesMiniHeros = 0)
{
    public static readonly CriteresEquipe Aucun = new(new HashSet<int>(), new HashSet<int>());

    /// <summary>Chaque faction imposée doit être portée par au moins un héros de l'équipe.</summary>
    public IReadOnlySet<string> FactionsImposees { get; init; } = new HashSet<string>();

    /// <summary>Un héros dont toutes les factions sont bannies n'entre pas dans l'équipe ; un héros qui garde une faction autorisée reste admis.</summary>
    public IReadOnlySet<string> FactionsBannies { get; init; } = new HashSet<string>();

    public bool EstVide => Imposes.Count == 0 && Bannis.Count == 0 && EtoilesMiniHeros == 0 && FactionsImposees.Count == 0 && FactionsBannies.Count == 0;

    public int FactionsManquantes(IReadOnlyCollection<int> equipe, Func<int, IReadOnlyList<string>> factionsDe) =>
        FactionsImposees.Count(faction => !equipe.Any(id => factionsDe(id).Contains(faction)));

    // Un héros sans faction n'est jamais exclu : « toutes bannies » serait vrai à vide.
    private bool FactionsToutesBannies(IReadOnlyList<string> factions) =>
        factions.Count > 0 && factions.All(FactionsBannies.Contains);

    public int HerosDeFactionBannie(IReadOnlyCollection<int> equipe, Func<int, IReadOnlyList<string>> factionsDe) =>
        equipe.Count(id => FactionsToutesBannies(factionsDe(id)));

    /// <summary>Un héros interdit n'entre jamais dans l'équipe : banni, de factions toutes bannies, ou sous le seuil d'étoiles par héros.</summary>
    public bool Interdit(int id, Func<int, int> etoilesDe, Func<int, IReadOnlyList<string>> factionsDe) =>
        Bannis.Contains(id) || FactionsToutesBannies(factionsDe(id)) || (EtoilesMiniHeros > 0 && etoilesDe(id) < EtoilesMiniHeros);

    public IEnumerable<int> ImposesManquants(IReadOnlyCollection<int> equipe) => Imposes.Where(id => !equipe.Contains(id));

    public IEnumerable<int> BannisPresents(IReadOnlyCollection<int> equipe) => Bannis.Where(equipe.Contains);

    // Comme pour le minimum de l'équipe, le manque se compte en étoiles : le recuit y trouve une pente à descendre.
    public int EtoilesManquantes(IReadOnlyCollection<int> equipe, Func<int, int> etoilesDe) =>
        equipe.Sum(id => Math.Max(0, EtoilesMiniHeros - etoilesDe(id)));

    /// <param name="etoilesDe">Étoiles d'un héros ; inutile (nul) tant qu'aucun seuil par héros n'est réglé.</param>
    /// <param name="factionsDe">Factions d'un héros ; inutile (nul) tant qu'aucune faction n'est imposée ou bannie.</param>
    public int Ecarts(IReadOnlyCollection<int> equipe, Func<int, int>? etoilesDe = null, Func<int, IReadOnlyList<string>>? factionsDe = null) =>
        ImposesManquants(equipe).Count() + BannisPresents(equipe).Count()
        + (EtoilesMiniHeros > 0 && etoilesDe is not null ? EtoilesManquantes(equipe, etoilesDe) : 0)
        + (factionsDe is null ? 0 : FactionsManquantes(equipe, factionsDe) + HerosDeFactionBannie(equipe, factionsDe));
}

internal sealed record RepartitionEquipes(IReadOnlyList<IReadOnlyList<int>> Equipes, IReadOnlyList<int> Reserve);

/// <summary>
/// Composition d'équipes sans dépendance à l'interface ni à la base. Le résultat est reproductible :
/// la recherche des meilleures équipes est exhaustive et le recuit simulé part d'une graine fixe.
/// </summary>
internal sealed class MoteurEquipes
{
    public const int TailleEquipe = 5;
    public const string RoleDps = "DPS";
    public const string RoleTank = "Tank";
    public const string RoleSoutien = "Soutien";
    public const int NombreEquipesParDefaut = 2;

    /// <summary>Avec une seule équipe, il n'y a rien à répartir : le mode choisi n'a plus d'effet et seul « Meilleure équipe d'abord » a un sens.</summary>
    public static ModeGeneration ModeEffectif(ModeGeneration mode, int nombreEquipes) =>
        nombreEquipes == 1 ? ModeGeneration.MeilleureEquipeDAbord : mode;

    public static readonly string[] Roles = [RoleDps, RoleTank, RoleSoutien];

    private const string DescriptionSansResonance = "Aucune résonance";
    private const string RaisonHerosImposeIntrouvable = "un héros imposé est introuvable";
    private const string RaisonHerosImposeInterdit = "un héros imposé est interdit dans cette équipe (banni, factions toutes bannies ou étoiles insuffisantes)";
    private const string RaisonAucunHeroAutorise = "aucun héros n'est autorisé par les critères de cette équipe";
    private const string RaisonFactionImposeeIntrouvable = "aucun héros autorisé ne porte la faction imposée {0}";
    private const string FactionOrdre = Referentiel.FactionOrdre;
    private const string FactionChaos = Referentiel.FactionChaos;
    private const int IndexDps = 0;
    private const int IndexTank = 1;
    private const int IndexSoutien = 2;
    private const int NombreExecutions = 6;
    private const int NombreIterations = 20000;
    private const double TemperatureInitiale = 50;
    private const double TauxRefroidissement = 0.002;
    // Départage seulement : à somme égale, on préfère des équipes de force proche.
    private const double PoidsEcart = 0.01;
    private const int MinimumResonance = 3;
    private const int DpsCible = 2;
    private const int PointsBonusDouble = 33;
    private const int ReservoirMaximum = 45;
    private const int GraineParDefaut = 7;
    private const int FactionsMaximum = 64;
    private const int PenaliteSansTank = 2;
    private const int PenaliteFrontSeul = 1;
    // Assez grand pour qu'un point d'étoiles manquant coûte toujours plus qu'une perte de score, quels que soient les poids réglés.
    private const double PoidsDeficit = 1_000_000;
    // On lance plus de fois que demandé puis on garde les meilleures : les relances qui retombent sur une répartition déjà vue sont perdues.
    private const int FacteurRelances = 3;
    private const int EchangesPerturbation = 6;

    private static readonly string[] FactionsRegulieres = Referentiel.FactionsRegulieres;

    // Dans l'ordre des paliers : le n-ième bonus s'ajoute à partir du n-ième héros Chaos.
    private static readonly string[] BonusChaos = ["Défense+30%", "Régénération d'énergie+25%", "Taux critique+15%"];

    private static readonly Dictionary<int, string> Bonus = new()
    {
        [50] = "PV+25% Atq+25%",
        [40] = "PV+20% Atq+20%",
        [33] = "PV+15% Atq+18%",
        [25] = "PV+10% Atq+15%",
    };

    // Un tank secondaire est un héros lourd qui n'a pas le rôle Tank ; un héros moyen de rôle Tank est déjà compté comme tank.
    private sealed record InfoHero(
        int Id, int Etoiles, string Role, int IndexRole, int[] Options, ulong Masque, bool ChaosPur, bool TankSecondaire, bool Moyen, string Equipement, int Famille, IReadOnlyList<string> Factions, bool Joker);

    private readonly record struct Resonance(int Points, int Premier, int PremiereFaction, int SecondeFaction);

    private readonly record struct Mesure(int Dps, int Tanks, int TanksSecondaires, int Soutiens, int Etoiles, int Penalite, int Chaos, Resonance Resonance, bool TousJokers);

    private readonly Dictionary<int, InfoHero> _infos;
    private readonly List<string> _factions = [];
    private readonly int[] _comptes;
    private readonly Dictionary<string, EvaluationEquipe> _cache = [];
    private Random _hasard = new(GraineParDefaut);
    private int[][] _optionsEquipe = new int[TailleEquipe][];
    // Sans héros de même famille, aucun échange n'a à être contrôlé : la recherche garde alors exactement son parcours d'origine.
    private readonly bool _famillesPartagees;

    /// <param name="lienAmeActif">Faux dans un mode de jeu sans lien d'âme : aucun héros n'y devient joker Ordre pour la résonance.</param>
    public MoteurEquipes(IEnumerable<HeroMoteur> heros, IReadOnlyDictionary<string, string> roles, bool lienAmeActif = true)
    {
        foreach (var faction in FactionsRegulieres)
        {
            IndexerFaction(faction);
        }
        _infos = heros.Select(h => Construire(h, roles, lienAmeActif)).ToDictionary(h => h.Id);
        _famillesPartagees = _infos.Values.Select(h => h.Famille).Distinct().Count() < _infos.Count;
        _comptes = new int[_factions.Count];
    }

    public string RoleDe(int heroId) => _infos[heroId].Role;

    public int FamilleDe(int heroId) => _infos[heroId].Famille;

    public int EtoilesDe(int heroId) => _infos[heroId].Etoiles;

    /// <summary>Factions réelles du héros, telles que les critères les lisent : le lien d'âme n'y ajoute rien.</summary>
    public IReadOnlyList<string> FactionsDe(int heroId) => _infos[heroId].Factions;

    /// <summary>Motif pour lequel une équipe n'est pas générée (un imposé qu'aucune composition valide ne peut tenir, ou aucun héros autorisé par les critères) ; nul quand elle l'est.</summary>
    public string? RaisonInactive(CriteresEquipe criteres)
    {
        foreach (var id in criteres.Imposes)
        {
            if (!_infos.ContainsKey(id))
            {
                return RaisonHerosImposeIntrouvable;
            }
            if (criteres.Interdit(id, EtoilesDe, FactionsDe))
            {
                return RaisonHerosImposeInterdit;
            }
        }
        foreach (var faction in criteres.FactionsImposees)
        {
            if (!_infos.Keys.Any(id => !criteres.Interdit(id, EtoilesDe, FactionsDe) && FactionsDe(id).Contains(faction)))
            {
                return string.Format(CultureInfo.CurrentCulture, RaisonFactionImposeeIntrouvable, faction);
            }
        }
        // Sans aucun héros autorisé, l'équipe ne se remplira jamais : la laisser en jeu ferait déposer là des héros que l'optimiseur croit utiles, puis perdus.
        if (_infos.Count > 0 && _infos.Keys.All(id => criteres.Interdit(id, EtoilesDe, FactionsDe)))
        {
            return RaisonAucunHeroAutorise;
        }
        return null;
    }

    public EvaluationEquipe Evaluer(IReadOnlyList<int> ids)
    {
        var cle = string.Join(',', ids.Order());
        if (_cache.TryGetValue(cle, out var enCache))
        {
            return enCache;
        }

        var mesure = Mesurer(ids.Select(id => _infos[id]).ToArray());
        var evaluation = new EvaluationEquipe(
            mesure.Dps, mesure.Tanks, mesure.TanksSecondaires, mesure.Soutiens, mesure.Etoiles, mesure.Penalite, mesure.Resonance.Points, Decrire(mesure), mesure.Chaos);
        _cache[cle] = evaluation;
        return evaluation;
    }

    // Un minimum d'étoiles par équipe (rang = index) ne vaut que pour « meilleure équipe d'abord » ; 0 ou une équipe hors liste = aucun minimum.
    // Les critères (héros imposés ou bannis, étoiles minimum par héros, même rang) valent pour les deux modes ; une équipe hors liste n'a aucun critère.
    // La première variante est la répartition sans brassage ; les suivantes repartent d'un départ brassé, car la recherche gloutonne est déterministe
    // et ne donnerait sinon qu'une seule répartition. Le hasard est réinitialisé pour que deux appels identiques donnent la même liste.
    public IReadOnlyList<RepartitionEquipes> GenererVariantes(
        int nombreEquipes, PoidsEquipes poids, ModeGeneration mode, IReadOnlyList<int> etoilesMinimum, int nombreVariantes, IReadOnlyList<CriteresEquipe>? criteres = null)
    {
        criteres ??= [];
        _hasard = new Random(GraineParDefaut);
        _cache.Clear();
        // Une équipe dont un imposé est intenable n'est pas générée : elle reste vide à son rang, les autres se composent sans elle.
        var inactives = Enumerable.Range(0, Math.Min(nombreEquipes, criteres.Count)).Where(i => RaisonInactive(criteres[i]) is not null).ToHashSet();
        var depart = MeilleureEquipeDAbord(nombreEquipes - inactives.Count, poids);
        foreach (var rang in inactives.Order())
        {
            depart.Equipes.Insert(rang, []);
        }
        var cout = CoutDuMode(poids, mode, etoilesMinimum, criteres, inactives);
        var candidates = new Dictionary<string, (double Cout, RepartitionEquipes Repartition)>();

        for (var lancer = 0; lancer < nombreVariantes * FacteurRelances; lancer++)
        {
            var equipes = Copier(depart.Equipes);
            var reserve = depart.Reserve.ToList();
            if (lancer > 0)
            {
                Brasser(equipes, reserve);
            }
            (equipes, reserve) = Affiner(equipes, reserve, cout, mode, etoilesMinimum, criteres, optimiserToujours: lancer > 0);
            Respecter(equipes, reserve, criteres, inactives, poids);
            candidates.TryAdd(CleDeRepartition(equipes), (cout(equipes.Select(e => Evaluer(e)).ToList(), equipes), EnRepartition(equipes, reserve)));
        }
        return [.. candidates.Values.OrderBy(c => c.Cout).Take(nombreVariantes).Select(c => c.Repartition)];
    }

    public static HeroMoteur CreerHero(int id, int etoiles, string classe, IReadOnlyList<string> factions, bool lienAme, string equipement, int? famille = null) =>
        new(id, etoiles, classe, factions, equipement, famille, lienAme);

    // Le lien d'âme fait compter le héros dans la faction Ordre pour la résonance seulement ; les critères de faction lisent les factions réelles du héros.
    public static IReadOnlyList<string> FactionsEffectives(IReadOnlyList<string> factions, bool lienAme) =>
        lienAme && !factions.Contains(FactionOrdre) ? [.. factions, FactionOrdre] : factions;

    private InfoHero Construire(HeroMoteur hero, IReadOnlyDictionary<string, string> roles, bool lienAmeActif)
    {
        var role = roles.TryGetValue(hero.Classe, out var attribue) ? attribue : RoleDps;
        var indexRole = role switch
        {
            RoleDps => IndexDps,
            RoleTank => IndexTank,
            _ => IndexSoutien,
        };

        var famille = hero.Famille ?? hero.Id;
        var equipement = Referentiel.Normaliser(hero.Equipement);
        var estTank = indexRole == IndexTank;
        var tankSecondaire = !estTank && equipement == Referentiel.EquipementLourd;
        var moyen = !estTank && equipement == Referentiel.EquipementMoyen;

        // Le Chaos l'emporte sur toute autre faction : le héros ne résonne pas mais donne un bonus aux alliés.
        if (hero.Factions.Contains(FactionChaos))
        {
            return new InfoHero(hero.Id, hero.Etoiles, role, indexRole, [], 0, true, tankSecondaire, moyen, equipement, famille, hero.Factions, Joker: false);
        }
        var factions = FactionsEffectives(hero.Factions, hero.LienAme && lienAmeActif).ToList();
        var joker = factions.Contains(FactionOrdre);
        if (joker)
        {
            factions = [.. factions.Where(f => f != FactionOrdre).Concat(FactionsRegulieres).Distinct()];
        }

        var options = factions.Select(IndexerFaction).ToArray();
        var masque = options.Aggregate(0UL, (acc, index) => acc | (1UL << index));
        return new InfoHero(hero.Id, hero.Etoiles, role, indexRole, options, masque, false, tankSecondaire, moyen, equipement, famille, hero.Factions, joker);
    }

    private int IndexerFaction(string faction)
    {
        var index = _factions.IndexOf(faction);
        if (index >= 0)
        {
            return index;
        }
        if (_factions.Count >= FactionsMaximum)
        {
            throw new InvalidOperationException($"Plus de {FactionsMaximum} factions distinctes.");
        }
        _factions.Add(faction);
        return _factions.Count - 1;
    }

    private Mesure Mesurer(ReadOnlySpan<InfoHero> equipe)
    {
        if (_optionsEquipe.Length < equipe.Length)
        {
            Array.Resize(ref _optionsEquipe, equipe.Length);
        }

        int dps = 0, tanks = 0, soutiens = 0, etoiles = 0, chaos = 0, avecFaction = 0, secondaires = 0, moyens = 0, jokers = 0;
        foreach (var hero in equipe)
        {
            if (hero.TankSecondaire)
            {
                secondaires++;
            }
            if (hero.Moyen)
            {
                moyens++;
            }
            switch (hero.IndexRole)
            {
                case IndexDps: dps++; break;
                case IndexTank: tanks++; break;
                default: soutiens++; break;
            }
            etoiles += hero.Etoiles;
            if (hero.ChaosPur)
            {
                chaos++;
            }
            else
            {
                if (hero.Joker)
                {
                    jokers++;
                }
                _optionsEquipe[avecFaction++] = hero.Options;
            }
        }

        var penalite = Math.Abs(dps - DpsCible) + PenaliteFront(tanks, secondaires, moyens) + Math.Max(0, 1 - soutiens);
        var resonance = new Resonance(0, 0, -1, -1);
        if (avecFaction >= MinimumResonance)
        {
            // Recherche exhaustive : chaque héros choisit une de ses factions, on garde la combinaison la mieux payée.
            Explorer(0, avecFaction, ref resonance);
        }
        return new Mesure(dps, tanks, secondaires, soutiens, etoiles, penalite, chaos, resonance, avecFaction > 0 && jokers == avecFaction);
    }

    // Un tank obligatoire, puis un second front : autre tank, tank secondaire ou héros d'équipement moyen.
    private static int PenaliteFront(int tanks, int secondaires, int moyens)
    {
        if (tanks == 0)
        {
            return PenaliteSansTank;
        }
        return tanks > 1 || secondaires > 0 || moyens > 0 ? 0 : PenaliteFrontSeul;
    }

    private void Explorer(int index, int total, ref Resonance meilleure)
    {
        if (index == total)
        {
            Retenir(ref meilleure);
            return;
        }
        foreach (var faction in _optionsEquipe[index])
        {
            _comptes[faction]++;
            Explorer(index + 1, total, ref meilleure);
            _comptes[faction]--;
        }
    }

    private void Retenir(ref Resonance meilleure)
    {
        int premier = 0, premiereFaction = -1, second = 0, secondeFaction = -1;
        for (var i = 0; i < _comptes.Length; i++)
        {
            if (_comptes[i] > premier)
            {
                premier = _comptes[i];
                premiereFaction = i;
            }
        }
        for (var i = 0; i < _comptes.Length; i++)
        {
            if (i != premiereFaction && _comptes[i] > second)
            {
                second = _comptes[i];
                secondeFaction = i;
            }
        }

        var points = PointsDe(premier, second);
        if (points > meilleure.Points)
        {
            meilleure = new Resonance(points, premier, premiereFaction, secondeFaction);
        }
    }

    private string Decrire(Mesure mesure)
    {
        var resonance = mesure.Resonance;
        var description = DescriptionSansResonance;
        if (resonance.Points > 0)
        {
            // Des héros tous Ordre sont des jokers : nommer l'une des factions régulières (la première à égalité) serait faux.
            var premiere = mesure.TousJokers ? FactionOrdre : _factions[resonance.PremiereFaction];
            var secondaire = resonance.Points == PointsBonusDouble ? $" + 2× {_factions[resonance.SecondeFaction]}" : string.Empty;
            description = $"{resonance.Premier}× {premiere}{secondaire} — {Bonus[resonance.Points]}";
        }
        return mesure.Chaos > 0 ? description + $" (+{mesure.Chaos} Chaos : {string.Join(", ", BonusChaos.Take(mesure.Chaos))})" : description;
    }

    private static int PointsDe(int premier, int second) => premier switch
    {
        >= 5 => 50,
        4 => 40,
        3 when second >= 2 => PointsBonusDouble,
        3 => 25,
        _ => 0,
    };

    // Plus le score est haut, meilleure est l'équipe ; la résonance est comptée en dixièmes de points de bonus.
    private static double Score(int penalite, int points, int etoiles, int chaos, PoidsEquipes poids) =>
        -poids.Roles * penalite + poids.Resonance * points / 10 + poids.Etoiles * etoiles + poids.ValeurChaos(chaos);

    private static double Score(EvaluationEquipe evaluation, PoidsEquipes poids) =>
        Score(evaluation.PenaliteRoles, evaluation.Points, evaluation.Etoiles, evaluation.Chaos, poids);

    // ---------- Mode « meilleure équipe d'abord » ----------

    private (List<List<int>> Equipes, List<int> Reserve) MeilleureEquipeDAbord(int nombreEquipes, PoidsEquipes poids)
    {
        var restants = _infos.Values.OrderByDescending(h => h.Etoiles).ThenBy(h => h.Id).ToList();
        var equipes = new List<List<int>>();
        // Une famille déjà engagée dans une équipe n'est plus disponible pour les suivantes : ses autres formes restent en réserve.
        var famillesEngagees = new HashSet<int>();
        while (equipes.Count < nombreEquipes)
        {
            var candidats = restants.Where(h => !famillesEngagees.Contains(h.Famille)).ToList();
            var retenus = candidats.Count == 0 ? Array.Empty<InfoHero>() : MeilleureEquipe(candidats, poids);
            famillesEngagees.UnionWith(retenus.Select(h => h.Famille));
            var ids = retenus.Select(h => h.Id).ToHashSet();
            equipes.Add([.. retenus.Select(h => h.Id)]);
            restants.RemoveAll(h => ids.Contains(h.Id));
        }
        return (equipes, [.. restants.Select(h => h.Id)]);
    }

    private InfoHero[] MeilleureEquipe(List<InfoHero> restants, PoidsEquipes poids)
    {
        var reservoir = Reduire(restants);
        var taille = Math.Min(TailleEquipe, reservoir.Select(h => h.Famille).Distinct().Count());
        var courante = new InfoHero[taille];
        var meilleure = new InfoHero[taille];
        var meilleurScore = double.NegativeInfinity;

        void Choisir(int position, int debut)
        {
            if (position == taille)
            {
                var mesure = Mesurer(courante);
                var score = Score(mesure.Penalite, mesure.Resonance.Points, mesure.Etoiles, mesure.Chaos, poids);
                if (score > meilleurScore)
                {
                    meilleurScore = score;
                    Array.Copy(courante, meilleure, taille);
                }
                return;
            }
            for (var i = debut; i <= reservoir.Count - (taille - position); i++)
            {
                if (courante.Take(position).Any(h => h.Famille == reservoir[i].Famille))
                {
                    continue;
                }
                courante[position] = reservoir[i];
                Choisir(position + 1, i + 1);
            }
        }

        Choisir(0, 0);
        return meilleure;
    }

    // Retire sans perte les héros « dominés » : s'il en existe au moins TailleEquipe autres de même rôle
    // et de même équipement (le front en dépend), de même nature Chaos (son bonus n'est pas une faction), aux étoiles au moins égales et aux factions au moins aussi nombreuses, l'un d'eux est forcément hors de l'équipe
    // et la remplace sans rien perdre. Les dominateurs se comptent par famille : quatre coéquipiers bloquent au plus quatre familles, donc cinq familles
    // dominantes laissent toujours un remplaçant sans doublon. Au-delà de ReservoirMaximum, on ne garde que les mieux dotés en étoiles.
    private static List<InfoHero> Reduire(List<InfoHero> restants)
    {
        var gardes = new List<InfoHero>();
        for (var i = 0; i < restants.Count; i++)
        {
            var hero = restants[i];
            var dominateurs = new HashSet<int>();
            for (var j = 0; j < i && dominateurs.Count < TailleEquipe; j++)
            {
                if (restants[j].IndexRole == hero.IndexRole && restants[j].Equipement == hero.Equipement && restants[j].ChaosPur == hero.ChaosPur && (restants[j].Masque & hero.Masque) == hero.Masque)
                {
                    dominateurs.Add(restants[j].Famille);
                }
            }
            if (dominateurs.Count < TailleEquipe)
            {
                gardes.Add(hero);
            }
        }
        return gardes.Count > ReservoirMaximum ? gardes[..ReservoirMaximum] : gardes;
    }

    // ---------- Optimisation par échanges (somme maximale, étoiles minimum) ----------

    // Un écart de critère (imposé manquant, banni présent, faction imposée absente, héros de faction bannie, étoiles sous le seuil) coûte plus que n'importe quelle perte de score :
    // le coût guide la recherche vers zéro écart, et Respecter garantit ensuite les interdits.
    // Les équipes inactives restent vides : leur score constant fausserait l'écart à la moyenne de la somme maximale, elles n'y comptent donc pas.
    private FonctionCout CoutDuMode(PoidsEquipes poids, ModeGeneration mode, IReadOnlyList<int> etoilesMinimum, IReadOnlyList<CriteresEquipe> criteres, IReadOnlySet<int> inactives)
    {
        Func<List<EvaluationEquipe>, double> coutDuScore = mode == ModeGeneration.SommeMaximale
            ? evaluations => Cout([.. evaluations.Where((_, rang) => !inactives.Contains(rang))], poids)
            : evaluations => CoutAvecMinimum(evaluations, poids, etoilesMinimum);
        return (evaluations, equipes) => coutDuScore(evaluations) + PoidsDeficit * EcartsDeCriteres(equipes, evaluations.Count, criteres);
    }

    // Le coût écarte les interdits quand c'est possible ; ce passage garantit la règle quand ça ne l'est pas : l'interdit part en réserve et l'équipe reste incomplète
    // si aucun héros autorisé ne peut la compléter.
    private void Respecter(List<List<int>> equipes, List<int> reserve, IReadOnlyList<CriteresEquipe> criteres, IReadOnlySet<int> inactives, PoidsEquipes poids)
    {
        var allegees = new List<int>();
        for (var rang = 0; rang < Math.Min(equipes.Count, criteres.Count); rang++)
        {
            var interdits = equipes[rang].Where(id => criteres[rang].Interdit(id, EtoilesDe, FactionsDe)).ToList();
            if (interdits.Count > 0)
            {
                equipes[rang].RemoveAll(interdits.Contains);
                reserve.AddRange(interdits);
                allegees.Add(rang);
            }
        }
        foreach (var rang in allegees.Where(r => !inactives.Contains(r)))
        {
            Completer(equipes, reserve, rang, criteres, poids);
        }
    }

    private void Completer(List<List<int>> equipes, List<int> reserve, int rang, IReadOnlyList<CriteresEquipe> criteres, PoidsEquipes poids)
    {
        var equipe = equipes[rang];
        while (equipe.Count < TailleEquipe)
        {
            var famillesPrises = equipes.SelectMany(e => e).Select(id => _infos[id].Famille).ToHashSet();
            var candidats = reserve
                .Where(id => !criteres[rang].Interdit(id, EtoilesDe, FactionsDe)
                    && !criteres.Where((_, autre) => autre != rang).Any(c => c.Imposes.Contains(id))
                    && !(_famillesPartagees && famillesPrises.Contains(_infos[id].Famille)))
                .Select(id => (Id: id, Score: Score(Evaluer([.. equipe, id]), poids)))
                .OrderByDescending(c => c.Score).ThenBy(c => c.Id)
                .ToList();
            if (candidats.Count == 0)
            {
                return;
            }
            equipe.Add(candidats[0].Id);
            reserve.Remove(candidats[0].Id);
        }
    }

    // equipes peut porter la réserve en dernier : seules les nombreEquipes premières listes sont des équipes.
    private int EcartsDeCriteres(List<List<int>> equipes, int nombreEquipes, IReadOnlyList<CriteresEquipe> criteres)
    {
        var ecarts = 0;
        for (var i = 0; i < nombreEquipes && i < criteres.Count; i++)
        {
            ecarts += criteres[i].Ecarts(equipes[i], EtoilesDe, FactionsDe);
        }
        return ecarts;
    }

    // Sans minimum, « meilleure équipe d'abord » n'optimise pas : optimiserToujours force le recuit pour les variantes brassées.
    private (List<List<int>> Equipes, List<int> Reserve) Affiner(
        List<List<int>> equipes, List<int> reserve, FonctionCout cout, ModeGeneration mode, IReadOnlyList<int> etoilesMinimum, IReadOnlyList<CriteresEquipe> criteres, bool optimiserToujours)
    {
        if (mode != ModeGeneration.SommeMaximale && !optimiserToujours && !etoilesMinimum.Any(m => m > 0) && criteres.All(c => c.EstVide))
        {
            return (equipes, reserve);
        }
        return Optimiser(equipes, reserve, cout);
    }

    private void Brasser(List<List<int>> equipes, List<int> reserve)
    {
        var listes = new List<List<int>>(equipes) { reserve };
        for (var essai = 0; essai < EchangesPerturbation; essai++)
        {
            if (TirerEchange(listes) is var (a, i, b, j))
            {
                EchangerSiValide(listes, equipes.Count, (a, i), (b, j));
            }
        }
    }

    // Deux formes du même héros ne peuvent pas combattre ensemble : un échange qui les réunirait dans les équipes (la réserve, dernière liste, ne combat pas) est annulé.
    private bool EchangerSiValide(List<List<int>> listes, int nombreEquipes, (int Liste, int Position) premier, (int Liste, int Position) second)
    {
        (listes[premier.Liste][premier.Position], listes[second.Liste][second.Position]) = (listes[second.Liste][second.Position], listes[premier.Liste][premier.Position]);
        if (!_famillesPartagees || SansDoublonDeFamille(listes, nombreEquipes))
        {
            return true;
        }
        (listes[premier.Liste][premier.Position], listes[second.Liste][second.Position]) = (listes[second.Liste][second.Position], listes[premier.Liste][premier.Position]);
        return false;
    }

    private bool SansDoublonDeFamille(List<List<int>> listes, int nombreEquipes)
    {
        var vues = new HashSet<int>();
        return listes.Take(nombreEquipes).SelectMany(e => e).All(id => vues.Add(_infos[id].Famille));
    }

    // Deux héros de deux listes distinctes non vides ; null quand le tirage n'en donne pas.
    private (int A, int I, int B, int J)? TirerEchange(List<List<int>> listes)
    {
        var a = _hasard.Next(listes.Count);
        var b = _hasard.Next(listes.Count);
        if (a == b || listes[a].Count == 0 || listes[b].Count == 0)
        {
            return null;
        }
        return (a, _hasard.Next(listes[a].Count), b, _hasard.Next(listes[b].Count));
    }

    // Même répartition = mêmes héros dans les mêmes équipes, quels que soient l'ordre des équipes et celui des héros ; la réserve en découle.
    private static string CleDeRepartition(List<List<int>> equipes) =>
        string.Join('|', equipes.Select(e => string.Join(',', e.Order())).Order(StringComparer.Ordinal));

    private static RepartitionEquipes EnRepartition(List<List<int>> equipes, List<int> reserve) =>
        new([.. equipes.Select(e => (IReadOnlyList<int>)e)], reserve);

    private (List<List<int>> Equipes, List<int> Reserve) Optimiser(List<List<int>> equipes, List<int> reserve, FonctionCout cout)
    {
        // Le départ est le résultat du mode précédent : le coût ne peut que baisser.
        var meilleure = (Cout: cout(equipes.Select(e => Evaluer(e)).ToList(), equipes), Equipes: equipes, Reserve: reserve);
        for (var execution = 0; execution < NombreExecutions; execution++)
        {
            var resultat = Recuire(Copier(meilleure.Equipes), meilleure.Reserve.ToList(), cout);
            if (resultat.Cout < meilleure.Cout - 1e-9)
            {
                meilleure = resultat;
            }
        }
        Polir(meilleure.Equipes, meilleure.Reserve, cout);
        return (meilleure.Equipes, meilleure.Reserve);
    }

    // Le déficit d'étoiles domine ; ensuite chaque équipe compte moins que la précédente, pour que les premières perdent le moins possible.
    private static double CoutAvecMinimum(List<EvaluationEquipe> evaluations, PoidsEquipes poids, IReadOnlyList<int> etoilesMinimum)
    {
        var deficit = 0;
        var scorePondere = 0.0;
        for (var i = 0; i < evaluations.Count; i++)
        {
            var minimum = i < etoilesMinimum.Count ? etoilesMinimum[i] : 0;
            deficit += Math.Max(0, minimum - evaluations[i].Etoiles);
            scorePondere += Score(evaluations[i], poids) / (i + 1);
        }
        return PoidsDeficit * deficit - scorePondere;
    }

    // Somme des scores (à maximiser, donc signe négatif) plus un léger écart à la moyenne qui départage les égalités.
    private static double Cout(List<EvaluationEquipe> evaluations, PoidsEquipes poids)
    {
        var scores = new double[evaluations.Count];
        for (var i = 0; i < scores.Length; i++)
        {
            scores[i] = Score(evaluations[i], poids);
        }
        var somme = scores.Sum();
        var moyenne = scores.Length == 0 ? 0 : somme / scores.Length;
        return -somme + PoidsEcart * scores.Sum(s => Math.Abs(s - moyenne));
    }

    private (double Cout, List<List<int>> Equipes, List<int> Reserve) Recuire(List<List<int>> equipes, List<int> reserve, FonctionCout cout)
    {
        var listes = new List<List<int>>(equipes) { reserve };
        var nombreEquipes = equipes.Count;
        var evaluations = equipes.Select(e => Evaluer(e)).ToList();
        var courant = cout(evaluations, equipes);
        var meilleur = courant;
        var meilleuresEquipes = Copier(equipes);
        var meilleureReserve = reserve.ToList();

        for (var it = 0; it < NombreIterations; it++)
        {
            var temperature = TemperatureInitiale * Math.Pow(TauxRefroidissement, (double)it / NombreIterations);
            if (TirerEchange(listes) is not var (a, i, b, j))
            {
                continue;
            }
            var ancienneA = a < nombreEquipes ? evaluations[a] : null;
            var ancienneB = b < nombreEquipes ? evaluations[b] : null;

            if (!EchangerSiValide(listes, nombreEquipes, (a, i), (b, j)))
            {
                continue;
            }
            if (a < nombreEquipes) evaluations[a] = Evaluer(listes[a]);
            if (b < nombreEquipes) evaluations[b] = Evaluer(listes[b]);

            var nouveau = cout(evaluations, equipes);
            var ecart = nouveau - courant;
            if (ecart <= 0 || _hasard.NextDouble() < Math.Exp(-ecart / temperature))
            {
                courant = nouveau;
                if (courant < meilleur - 1e-9)
                {
                    meilleur = courant;
                    meilleuresEquipes = Copier(equipes);
                    meilleureReserve = reserve.ToList();
                }
            }
            else
            {
                (listes[a][i], listes[b][j]) = (listes[b][j], listes[a][i]);
                if (ancienneA is not null) evaluations[a] = ancienneA;
                if (ancienneB is not null) evaluations[b] = ancienneB;
            }
        }
        return (meilleur, meilleuresEquipes, meilleureReserve);
    }

    // Descente finale : tout échange qui améliore le coût est gardé, jusqu'à ce qu'il n'en reste plus.
    private void Polir(List<List<int>> equipes, List<int> reserve, FonctionCout cout)
    {
        var listes = new List<List<int>>(equipes) { reserve };
        var evaluations = equipes.Select(e => Evaluer(e)).ToList();
        var courant = cout(evaluations, equipes);

        var ameliore = true;
        while (ameliore)
        {
            ameliore = false;
            for (var a = 0; a < equipes.Count; a++)
            {
                for (var b = a + 1; b < listes.Count; b++)
                {
                    ameliore |= AmeliorerPaire(listes, evaluations, (a, b), cout, ref courant);
                }
            }
        }
    }

    // Essaie tous les échanges entre deux listes (la réserve est la dernière, sans évaluation) ; renvoie vrai si l'un a été gardé.
    private bool AmeliorerPaire(List<List<int>> listes, List<EvaluationEquipe> evaluations, (int A, int B) paire, FonctionCout cout, ref double courant)
    {
        var ameliore = false;
        for (var i = 0; i < listes[paire.A].Count; i++)
        {
            for (var j = 0; j < listes[paire.B].Count; j++)
            {
                ameliore |= EssayerEchange(listes, evaluations, paire, (i, j), cout, ref courant);
            }
        }
        return ameliore;
    }

    private bool EssayerEchange(List<List<int>> listes, List<EvaluationEquipe> evaluations, (int A, int B) paire, (int I, int J) positions, FonctionCout cout, ref double courant)
    {
        var (a, b) = paire;
        var (i, j) = positions;
        var ancienneA = evaluations[a];
        var ancienneB = b < evaluations.Count ? evaluations[b] : null;

        if (!EchangerSiValide(listes, evaluations.Count, (a, i), (b, j)))
        {
            return false;
        }
        evaluations[a] = Evaluer(listes[a]);
        if (ancienneB is not null) evaluations[b] = Evaluer(listes[b]);

        var nouveau = cout(evaluations, listes);
        if (nouveau < courant - 1e-9)
        {
            courant = nouveau;
            return true;
        }

        (listes[a][i], listes[b][j]) = (listes[b][j], listes[a][i]);
        evaluations[a] = ancienneA;
        if (ancienneB is not null) evaluations[b] = ancienneB;
        return false;
    }

    private static List<List<int>> Copier(List<List<int>> listes) => [.. listes.Select(l => l.ToList())];
}
