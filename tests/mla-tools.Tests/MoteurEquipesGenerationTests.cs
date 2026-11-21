using Xunit;
using static MlaTools.Tests.OutilsMoteurEquipes;

namespace MlaTools.Tests;

public class MoteurEquipesGenerationTests
{
    private const int NombreVariantes = 4;
    private const int ReferenceSansVariante = 1;
    private const int MinimumEtoilesEquipe = 30;
    private const int EtoilesHeroFort = 10;
    private const int FamilleDesDeuxFormes = 3;
    private const int IdChaos = 6;
    private const int IdAutreType = 5;
    // Assez grand pour que chaque famille ainsi numérotée diffère de tous les identifiants de héros du jeu de test.
    private const int DecalageFamilleDistincte = 100;
    // Dans JeuVarie : un héros faible (1 étoile) que le score n'enverrait pas en première équipe, et deux héros forts qu'il y mettrait.
    private const int HeroFaible = 6;
    private const int HeroFortA = 1;
    private const int HeroFortB = 5;
    private const int SeuilHeros = 4;
    private const int SeuilIntenable = 5;
    private const int MinimumEquipeCumule = 22;

    // Le mode passe par son nom : ModeGeneration est internal, un paramètre de test public ne peut pas en porter le type.
    public static IEnumerable<object[]> Modes => Enum.GetNames<ModeGeneration>().Select(nom => new object[] { nom });

    private static readonly PoidsEquipes Poids = PoidsEquipes.ParDefaut;
    private static readonly IReadOnlyList<int> SansMinimum = [];
    private static readonly string FactionZ = Referentiel.FactionsRegulieres[2];

    // Score d'une équipe selon les poids par défaut : le moteur ne l'expose pas, on le recalcule depuis son évaluation.
    private static double Score(EvaluationEquipe evaluation) =>
        -Poids.Roles * evaluation.PenaliteRoles + Poids.Resonance * evaluation.Points / 10 + Poids.Etoiles * evaluation.Etoiles + Poids.ValeurChaos(evaluation.Chaos);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 15)]
    [InlineData(2, 20)]
    [InlineData(3, 23)]
    [InlineData(4, 23)]
    public void Valeur_chaos_cumule_les_paliers_sans_depasser_le_troisieme(int chaos, double attendu) =>
        Assert.Equal(attendu, Poids.ValeurChaos(chaos));

    private static double ScoreEquipe(MoteurEquipes moteur, IReadOnlyList<int> equipe) => Score(moteur.Evaluer(equipe));

    private static double SommeDesScores(MoteurEquipes moteur, RepartitionEquipes repartition) =>
        repartition.Equipes.Sum(e => ScoreEquipe(moteur, e));

    private static IReadOnlyList<RepartitionEquipes> Variantes(
        MoteurEquipes moteur, int nombreEquipes, ModeGeneration mode, int nombreVariantes = NombreVariantes, IReadOnlyList<int>? etoilesMinimum = null,
        IReadOnlyList<CriteresEquipe>? criteres = null) =>
        moteur.GenererVariantes(nombreEquipes, Poids, mode, etoilesMinimum ?? SansMinimum, nombreVariantes, criteres);

    private static CriteresEquipe Criteres(int[]? imposes = null, int[]? bannis = null, int etoilesMiniHeros = 0) =>
        new((imposes ?? []).ToHashSet(), (bannis ?? []).ToHashSet(), etoilesMiniHeros);

    // Douze héros variés : trois rôles, deux équipements, des factions qui se recoupent, des étoiles de 1 à 5.
    private static HeroMoteur[] JeuVarie() =>
    [
        Hero(1, ClasseTank, [FactionX], 5), Hero(2, ClasseTank, [FactionY], 3, Referentiel.EquipementLourd),
        Hero(3, ClasseDps, [FactionX], 4), Hero(4, ClasseDps, [FactionX, FactionY], 2), Hero(5, ClasseDps, [FactionY], 5),
        Hero(6, ClasseDps, [FactionZ], 1), Hero(7, ClasseSoutien, [FactionX], 3), Hero(8, ClasseSoutien, [FactionZ], 2),
        Hero(9, ClasseDps, [FactionY], 4), Hero(10, ClasseTank, [FactionZ], 1), Hero(11, ClasseDps, [FactionX], 3), Hero(12, ClasseSoutien, [FactionY], 5),
    ];

    private static HeroMoteur[] JeuAleatoire(int graine, int nombre)
    {
        var hasard = new Random(graine);
        string[] classes = [ClasseTank, ClasseDps, ClasseSoutien];
        string[] equipements = [EquipementLeger, Referentiel.EquipementLourd];
        string[] factions = [FactionX, FactionY, FactionZ];
        return [.. Enumerable.Range(1, nombre).Select(id => Hero(
            id,
            classes[hasard.Next(classes.Length)],
            [.. factions.OrderBy(_ => hasard.Next()).Take(1 + hasard.Next(2))],
            1 + hasard.Next(5),
            equipements[hasard.Next(equipements.Length)]))];
    }

    private static string Empreinte(IReadOnlyList<RepartitionEquipes> variantes) =>
        string.Join(';', variantes.Select(v => string.Join('|', v.Equipes.Select(e => string.Join(',', e))) + "/" + string.Join(',', v.Reserve)));

    private static string CleDeRepartition(RepartitionEquipes repartition) =>
        string.Join('|', repartition.Equipes.Select(e => string.Join(',', e.Order())).Order(StringComparer.Ordinal));

    private static IEnumerable<int[]> Combinaisons(int[] ids, int taille, int debut = 0)
    {
        if (taille == 0)
        {
            yield return [];
            yield break;
        }
        for (var i = debut; i <= ids.Length - taille; i++)
        {
            foreach (var suite in Combinaisons(ids, taille - 1, i + 1))
            {
                yield return [ids[i], .. suite];
            }
        }
    }

    private static double MeilleurScoreExhaustif(MoteurEquipes moteur, int[] ids) =>
        Combinaisons(ids, Math.Min(MoteurEquipes.TailleEquipe, ids.Length)).Max(c => ScoreEquipe(moteur, c));

    [Fact]
    public void Quatre_de_meme_type_et_un_chaos_battent_cinq_de_meme_type()
    {
        HeroMoteur[] heros = [.. Enumerable.Range(1, 5).Select(id => Hero(id, ClasseDps, [FactionX])), HeroChaosPur(IdChaos)];

        var equipe = Variantes(Moteur(heros), 1, ModeGeneration.MeilleureEquipeDAbord)[0].Equipes[0];

        Assert.Contains(IdChaos, equipe);
    }

    [Fact]
    public void Un_chaos_est_prefere_a_un_cinquieme_heros_d_un_autre_type()
    {
        HeroMoteur[] heros = [.. Enumerable.Range(1, 4).Select(id => Hero(id, ClasseDps, [FactionX])), Hero(IdAutreType, ClasseDps, [FactionY]), HeroChaosPur(IdChaos)];

        var equipe = Variantes(Moteur(heros), 1, ModeGeneration.MeilleureEquipeDAbord)[0].Equipes[0];

        Assert.Contains(IdChaos, equipe);
        Assert.DoesNotContain(IdAutreType, equipe);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Chaque_hero_apparait_exactement_une_fois(string nomMode)
    {
        var mode = Enum.Parse<ModeGeneration>(nomMode);
        var heros = JeuVarie();
        var moteur = Moteur(heros);

        foreach (var variante in Variantes(moteur, 2, mode))
        {
            var presents = variante.Equipes.SelectMany(e => e).Concat(variante.Reserve).Order();
            Assert.Equal(heros.Select(h => h.Id).Order(), presents);
        }
    }

    // Les héros 3 et 5 sont deux formes du même héros : les deux DPS les mieux dotés, que la génération voudrait réunir.
    [Theory]
    [MemberData(nameof(Modes))]
    public void Deux_formes_du_meme_heros_ne_sont_jamais_dans_les_equipes_ensemble(string nomMode)
    {
        var mode = Enum.Parse<ModeGeneration>(nomMode);
        var moteur = Moteur([.. JeuVarie().Select(h => h.Id is 3 or 5 ? h with { Famille = FamilleDesDeuxFormes } : h)]);

        foreach (var variante in Variantes(moteur, 2, mode))
        {
            var enEquipe = variante.Equipes.SelectMany(e => e).ToList();
            Assert.False(enEquipe.Contains(3) && enEquipe.Contains(5));
        }
    }

    [Fact]
    public void Deux_formes_du_meme_heros_dans_un_stock_de_cinq_laissent_l_une_en_reserve()
    {
        var moteur = Moteur(
            Hero(1, ClasseTank), Hero(2, ClasseDps), Hero(3, ClasseDps, famille: 2), Hero(4, ClasseSoutien), Hero(5, ClasseTank));

        var repartition = Assert.Single(Variantes(moteur, 1, ModeGeneration.MeilleureEquipeDAbord, ReferenceSansVariante));

        Assert.Equal(MoteurEquipes.TailleEquipe - 1, repartition.Equipes[0].Count);
        Assert.Single(repartition.Reserve);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Sans_famille_partagee_la_generation_est_celle_d_origine(string nomMode)
    {
        var mode = Enum.Parse<ModeGeneration>(nomMode);
        var heros = JeuVarie();

        Assert.Equal(
            Empreinte(Variantes(Moteur(heros), 2, mode)),
            Empreinte(Variantes(Moteur([.. heros.Select(h => h with { Famille = h.Id + DecalageFamilleDistincte })]), 2, mode)));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Une_equipe_compte_au_plus_cinq_heros(string nomMode)
    {
        var mode = Enum.Parse<ModeGeneration>(nomMode);
        var variantes = Variantes(Moteur(JeuVarie()), 2, mode);

        Assert.All(variantes.SelectMany(v => v.Equipes), equipe => Assert.True(equipe.Count <= MoteurEquipes.TailleEquipe));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Deux_appels_identiques_donnent_la_meme_liste(string nomMode)
    {
        var mode = Enum.Parse<ModeGeneration>(nomMode);
        var heros = JeuVarie();
        var moteur = Moteur(heros);

        var premier = Empreinte(Variantes(moteur, 2, mode));

        Assert.Equal(premier, Empreinte(Variantes(moteur, 2, mode)));
        Assert.Equal(premier, Empreinte(Variantes(Moteur(heros), 2, mode)));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Aucune_repartition_en_double_et_au_plus_le_nombre_demande(string nomMode)
    {
        var mode = Enum.Parse<ModeGeneration>(nomMode);
        var variantes = Variantes(Moteur(JeuVarie()), 2, mode);

        Assert.InRange(variantes.Count, 1, NombreVariantes);
        Assert.Equal(variantes.Count, variantes.Select(CleDeRepartition).Distinct().Count());
    }

    [Fact]
    public void La_meilleure_equipe_d_abord_n_est_depassee_par_aucune_equipe_du_mode_somme_maximale()
    {
        var moteur = Moteur(JeuVarie());
        var premiereEquipe = Variantes(moteur, 2, ModeGeneration.MeilleureEquipeDAbord, ReferenceSansVariante)[0].Equipes[0];
        var scoreReference = ScoreEquipe(moteur, premiereEquipe);

        var equipes = Variantes(moteur, 2, ModeGeneration.SommeMaximale).SelectMany(v => v.Equipes);

        Assert.All(equipes, equipe => Assert.True(ScoreEquipe(moteur, equipe) <= scoreReference));
    }

    [Fact]
    public void La_somme_maximale_n_a_pas_une_somme_inferieure_a_la_meilleure_equipe_d_abord()
    {
        var moteur = Moteur(JeuVarie());
        var reference = SommeDesScores(moteur, Variantes(moteur, 2, ModeGeneration.MeilleureEquipeDAbord, ReferenceSansVariante)[0]);

        var somme = SommeDesScores(moteur, Variantes(moteur, 2, ModeGeneration.SommeMaximale)[0]);

        Assert.True(somme >= reference, $"somme maximale {somme} < meilleure équipe d'abord {reference}");
    }

    // Les héros forts n'ont pas de tank ni de soutien : les prendre coûte des points de rôles, donc sans minimum la première équipe reste équilibrée et faible en étoiles.
    private static HeroMoteur[] JeuEtoilesContreRoles() =>
    [
        .. EquipeEquilibree(),
        .. Enumerable.Range(6, 5).Select(id => Hero(id, ClasseDps, etoiles: EtoilesHeroFort)),
    ];

    [Fact]
    public void Sans_minimum_la_premiere_equipe_privilegie_les_roles_sur_les_etoiles()
    {
        var moteur = Moteur(JeuEtoilesContreRoles());

        var premiereEquipe = Variantes(moteur, 2, ModeGeneration.MeilleureEquipeDAbord)[0].Equipes[0];

        Assert.True(moteur.Evaluer(premiereEquipe).Etoiles < MinimumEtoilesEquipe);
    }

    [Fact]
    public void Le_minimum_d_etoiles_atteignable_est_respecte_au_prix_du_score()
    {
        var moteur = Moteur(JeuEtoilesContreRoles());

        var premiereEquipe = Variantes(moteur, 2, ModeGeneration.MeilleureEquipeDAbord, etoilesMinimum: [MinimumEtoilesEquipe])[0].Equipes[0];

        Assert.True(moteur.Evaluer(premiereEquipe).Etoiles >= MinimumEtoilesEquipe);
    }

    [Fact]
    public void Moins_de_cinq_heros_donnent_une_seule_equipe_incomplete()
    {
        var moteur = Moteur(Hero(1, ClasseTank), Hero(2, ClasseDps), Hero(3, ClasseSoutien));

        var variantes = Variantes(moteur, 1, ModeGeneration.MeilleureEquipeDAbord, ReferenceSansVariante);

        var repartition = Assert.Single(variantes);
        Assert.Equal([3], repartition.Equipes.Select(e => e.Count));
        Assert.Empty(repartition.Reserve);
    }

    [Fact]
    public void Plus_d_equipes_que_le_stock_ne_permet_donne_des_equipes_vides_sans_exception()
    {
        var moteur = Moteur(JeuAleatoire(graine: 1, nombre: 7));

        var repartition = Assert.Single(Variantes(moteur, 3, ModeGeneration.MeilleureEquipeDAbord, ReferenceSansVariante));

        Assert.Equal([MoteurEquipes.TailleEquipe, 2, 0], repartition.Equipes.Select(e => e.Count));
        Assert.Empty(repartition.Reserve);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Sans_heros_les_equipes_et_la_reserve_sont_vides(string nomMode)
    {
        var mode = Enum.Parse<ModeGeneration>(nomMode);
        var variantes = Variantes(Moteur(), 2, mode);

        Assert.NotEmpty(variantes);
        Assert.All(variantes, v =>
        {
            Assert.Equal(2, v.Equipes.Count);
            Assert.All(v.Equipes, Assert.Empty);
            Assert.Empty(v.Reserve);
        });
    }

    [Theory]
    [InlineData(1, 6)]
    [InlineData(2, 7)]
    [InlineData(3, 8)]
    [InlineData(4, 9)]
    [InlineData(5, 9)]
    public void La_meilleure_equipe_egale_la_recherche_exhaustive(int graine, int nombreHeros)
    {
        var heros = JeuAleatoire(graine, nombreHeros);
        var moteur = Moteur(heros);

        var trouvee = Variantes(moteur, 1, ModeGeneration.MeilleureEquipeDAbord, ReferenceSansVariante)[0].Equipes[0];

        Assert.Equal(MeilleurScoreExhaustif(moteur, [.. heros.Select(h => h.Id)]), ScoreEquipe(moteur, trouvee));
    }

    // Huit héros interchangeables sauf les étoiles : Reduire écarte les trois derniers (cinq autres les dominent) ; l'équipe doit rester les cinq mieux dotés.
    [Fact]
    public void La_reduction_des_heros_domines_ne_fait_pas_perdre_la_meilleure_equipe()
    {
        var heros = Enumerable.Range(1, 8).Select(id => Hero(id, ClasseDps, [FactionX], etoiles: id)).ToArray();
        var moteur = Moteur(heros);

        var trouvee = Variantes(moteur, 1, ModeGeneration.MeilleureEquipeDAbord, ReferenceSansVariante)[0].Equipes[0];

        Assert.Equal(MeilleurScoreExhaustif(moteur, [.. heros.Select(h => h.Id)]), ScoreEquipe(moteur, trouvee));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Un_heros_impose_se_retrouve_dans_la_bonne_equipe(string nomMode)
    {
        var moteur = Moteur(JeuVarie());

        var variantes = Variantes(moteur, 2, Enum.Parse<ModeGeneration>(nomMode), criteres: [CriteresEquipe.Aucun, Criteres(imposes: [HeroFaible])]);

        Assert.All(variantes, v => Assert.Contains(HeroFaible, v.Equipes[1]));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Un_heros_banni_est_absent_de_son_equipe(string nomMode)
    {
        var moteur = Moteur(JeuVarie());

        var variantes = Variantes(moteur, 2, Enum.Parse<ModeGeneration>(nomMode), criteres: [Criteres(bannis: [HeroFortA, HeroFortB])]);

        Assert.All(variantes, v =>
        {
            Assert.DoesNotContain(HeroFortA, v.Equipes[0]);
            Assert.DoesNotContain(HeroFortB, v.Equipes[0]);
        });
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Sans_critere_la_generation_est_inchangee(string nomMode)
    {
        var mode = Enum.Parse<ModeGeneration>(nomMode);
        var moteur = Moteur(JeuVarie());

        Assert.Equal(
            Empreinte(Variantes(moteur, 2, mode)),
            Empreinte(Variantes(moteur, 2, mode, criteres: [CriteresEquipe.Aucun, CriteresEquipe.Aucun])));
    }

    [Fact]
    public void Un_heros_impose_inatteignable_ne_bloque_pas_la_generation()
    {
        var moteur = Moteur(JeuVarie());
        var criteres = new[] { Criteres(imposes: [HeroFortA]), Criteres(imposes: [HeroFortA]) };

        var variantes = Variantes(moteur, 2, ModeGeneration.SommeMaximale, criteres: criteres);

        Assert.NotEmpty(variantes);
        Assert.All(variantes, v => Assert.Equal(2, v.Equipes.Count));
    }

    [Fact]
    public void Les_ecarts_comptent_les_imposes_manquants_et_les_bannis_presents()
    {
        var criteres = Criteres(imposes: [1, 2], bannis: [3, 4]);

        Assert.Equal(2, criteres.Ecarts([1, 3, 9]));
        Assert.Equal(0, criteres.Ecarts([1, 2, 9]));
    }

    [Fact]
    public void Les_ecarts_comptent_les_etoiles_manquantes_sous_le_seuil_par_heros()
    {
        var criteres = Criteres(etoilesMiniHeros: SeuilHeros);

        // Étoiles = identifiant : 1 et 2 manquent de 3 et 2 étoiles, 5 est au-dessus du seuil.
        Assert.Equal(5, criteres.Ecarts([1, 2, 5], id => id));
        Assert.Equal(0, criteres.Ecarts([1, 2, 5]));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Aucun_heros_de_l_equipe_n_est_sous_le_seuil_d_etoiles(string nomMode)
    {
        var moteur = Moteur(JeuVarie());

        var variantes = Variantes(moteur, 2, Enum.Parse<ModeGeneration>(nomMode), criteres: [Criteres(etoilesMiniHeros: SeuilHeros), CriteresEquipe.Aucun]);

        Assert.All(variantes, v => Assert.All(v.Equipes[0], id => Assert.True(moteur.EtoilesDe(id) >= SeuilHeros)));
    }

    // Seuil par héros (4) et minimum d'équipe (22) ne sont tenables ensemble que par les cinq héros à 4 étoiles ou plus du jeu (total 23).
    [Fact]
    public void Le_seuil_par_heros_se_cumule_avec_le_minimum_de_l_equipe()
    {
        var moteur = Moteur(JeuVarie());

        var variantes = Variantes(
            moteur, 2, ModeGeneration.MeilleureEquipeDAbord, etoilesMinimum: [MinimumEquipeCumule], criteres: [Criteres(etoilesMiniHeros: SeuilHeros), CriteresEquipe.Aucun]);

        Assert.All(variantes, v =>
        {
            Assert.All(v.Equipes[0], id => Assert.True(moteur.EtoilesDe(id) >= SeuilHeros));
            Assert.True(moteur.Evaluer(v.Equipes[0]).Etoiles >= MinimumEquipeCumule);
        });
    }

    // Le jeu n'a que trois héros à 5 étoiles : deux équipes ne peuvent pas toutes deux n'avoir que des héros à 5 étoiles.
    [Theory]
    [MemberData(nameof(Modes))]
    public void Un_seuil_par_heros_intenable_remonte_un_deficit_sans_bloquer(string nomMode)
    {
        var moteur = Moteur(JeuVarie());
        var criteres = new[] { Criteres(etoilesMiniHeros: SeuilIntenable), Criteres(etoilesMiniHeros: SeuilIntenable) };

        var variantes = Variantes(moteur, 2, Enum.Parse<ModeGeneration>(nomMode), criteres: criteres);

        Assert.NotEmpty(variantes);
        Assert.All(variantes, v => Assert.Contains(Enumerable.Range(0, 2), rang => criteres[rang].Ecarts(v.Equipes[rang], moteur.EtoilesDe) > 0));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Un_seuil_par_heros_nul_laisse_la_generation_inchangee(string nomMode)
    {
        var mode = Enum.Parse<ModeGeneration>(nomMode);
        var moteur = Moteur(JeuVarie());

        Assert.Equal(
            Empreinte(Variantes(moteur, 2, mode)),
            Empreinte(Variantes(moteur, 2, mode, criteres: [Criteres(etoilesMiniHeros: 0), Criteres(etoilesMiniHeros: 0)])));
    }
}
