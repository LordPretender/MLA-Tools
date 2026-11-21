using Xunit;
using static MlaTools.Tests.OutilsMoteurEquipes;

namespace MlaTools.Tests;

public class MoteurEquipesEvaluationTests
{
    private const int PointsPour25 = 25;
    private const string BonusPour25 = "PV+10% Atq+15%";
    private const string BonusPour33 = "PV+15% Atq+18%";
    private const string BonusPour40 = "PV+20% Atq+20%";
    private const string BonusPour50 = "PV+25% Atq+25%";
    private const string BonusChaosDefense = "Défense+30%";
    private const string BonusChaosTrois = "Défense+30%, Régénération d'énergie+25%, Taux critique+15%";
    private const string DescriptionSansResonance = "Aucune résonance";
    private const int FactionsMaximum = 64;

    private static HeroMoteur[] Heros(int nombre, string classe, string faction, int premierId = 1) =>
        [.. Enumerable.Range(premierId, nombre).Select(id => Hero(id, classe, [faction]))];

    [Theory]
    [InlineData(2, 1, 0)]
    [InlineData(2, 2, 0)]
    [InlineData(3, 0, 25)]
    [InlineData(3, 1, 25)]
    [InlineData(3, 2, 33)]
    [InlineData(4, 0, 40)]
    [InlineData(4, 1, 40)]
    [InlineData(5, 0, 50)]
    public void Points_de_resonance_selon_la_repartition_des_factions(int nombreX, int nombreY, int pointsAttendus)
    {
        HeroMoteur[] equipe = [.. Heros(nombreX, ClasseDps, FactionX), .. Heros(nombreY, ClasseDps, FactionY, premierId: 100)];

        Assert.Equal(pointsAttendus, EvaluerEquipe(equipe).Points);
    }

    [Fact]
    public void Deux_heros_de_meme_faction_ne_font_pas_resonner()
    {
        var evaluation = EvaluerEquipe(Hero(1, ClasseDps, [FactionX]), Hero(2, ClasseDps, [FactionX]));

        Assert.Equal(0, evaluation.Points);
        Assert.Equal(DescriptionSansResonance, evaluation.Description);
    }

    [Fact]
    public void Description_sans_resonance()
    {
        Assert.Equal(DescriptionSansResonance, EvaluerEquipe(Hero(1, ClasseDps, [FactionX])).Description);
    }

    [Fact]
    public void Description_de_trois_heros_de_meme_faction()
    {
        var evaluation = EvaluerEquipe([.. Heros(3, ClasseDps, FactionX)]);

        Assert.Equal($"3× {FactionX} — {BonusPour25}", evaluation.Description);
    }

    [Fact]
    public void Description_de_trois_et_deux_heros_de_deux_factions()
    {
        HeroMoteur[] equipe = [.. Heros(3, ClasseDps, FactionX), .. Heros(2, ClasseDps, FactionY, premierId: 100)];

        Assert.Equal($"3× {FactionX} + 2× {FactionY} — {BonusPour33}", EvaluerEquipe(equipe).Description);
    }

    [Fact]
    public void Description_de_quatre_et_cinq_heros_de_meme_faction()
    {
        Assert.Equal($"4× {FactionX} — {BonusPour40}", EvaluerEquipe([.. Heros(4, ClasseDps, FactionX)]).Description);
        Assert.Equal($"5× {FactionX} — {BonusPour50}", EvaluerEquipe([.. Heros(5, ClasseDps, FactionX)]).Description);
    }

    [Fact]
    public void Heros_chaos_purs_ajoutent_un_suffixe_a_la_description()
    {
        HeroMoteur[] equipe = [.. Heros(3, ClasseDps, FactionX), HeroChaosPur(100)];

        Assert.Equal($"3× {FactionX} — {BonusPour25} (+1 Chaos : {BonusChaosDefense})", EvaluerEquipe(equipe).Description);
    }

    [Fact]
    public void Heros_chaos_purs_seuls_ne_resonnent_pas()
    {
        var evaluation = EvaluerEquipe([.. Heros(3, ClasseDps, Referentiel.FactionChaos)]);

        Assert.Equal(0, evaluation.Points);
        Assert.Equal($"{DescriptionSansResonance} (+3 Chaos : {BonusChaosTrois})", evaluation.Description);
    }

    [Fact]
    public void Heros_chaos_sont_comptes_dans_l_evaluation()
    {
        HeroMoteur[] equipe = [.. Heros(3, ClasseDps, FactionX), HeroChaosPur(100), HeroChaosPur(101)];

        Assert.Equal(2, EvaluerEquipe(equipe).Chaos);
    }

    [Fact]
    public void Heros_chaos_pur_ne_compte_pas_dans_les_heros_qui_resonnent()
    {
        HeroMoteur[] equipe = [.. Heros(2, ClasseDps, FactionX), HeroChaosPur(100)];

        Assert.Equal(0, EvaluerEquipe(equipe).Points);
    }

    [Fact]
    public void Heros_chaos_et_faction_ne_resonne_pas_car_le_chaos_l_emporte()
    {
        HeroMoteur[] equipe = [.. Enumerable.Range(1, 3).Select(id => Hero(id, ClasseDps, [Referentiel.FactionChaos, FactionX]))];

        var evaluation = EvaluerEquipe(equipe);

        Assert.Equal(0, evaluation.Points);
        Assert.Equal($"{DescriptionSansResonance} (+3 Chaos : {BonusChaosTrois})", evaluation.Description);
    }

    [Fact]
    public void Heros_chaos_et_ordre_n_est_pas_un_joker()
    {
        HeroMoteur[] equipe = [.. Heros(2, ClasseDps, FactionX), Hero(100, ClasseDps, [Referentiel.FactionChaos, Referentiel.FactionOrdre])];

        Assert.Equal(0, EvaluerEquipe(equipe).Points);
    }

    public static IEnumerable<object[]> FactionsRegulieres => Referentiel.FactionsRegulieres.Select(faction => new object[] { faction });

    [Theory]
    [MemberData(nameof(FactionsRegulieres))]
    public void Heros_ordre_est_un_joker_pour_chaque_faction_reguliere(string faction)
    {
        HeroMoteur[] equipe = [.. Heros(2, ClasseDps, faction), Hero(100, ClasseDps, [Referentiel.FactionOrdre])];

        var evaluation = EvaluerEquipe(equipe);

        Assert.Equal(PointsPour25, evaluation.Points);
        Assert.Equal($"3× {faction} — {BonusPour25}", evaluation.Description);
    }

    [Fact]
    public void Equipe_de_jokers_seuls_se_decrit_comme_ordre_et_non_comme_une_faction_reguliere()
    {
        var evaluation = EvaluerEquipe([.. Enumerable.Range(1, 5).Select(id => Hero(id, ClasseDps, [Referentiel.FactionOrdre]))]);

        Assert.Equal($"5× {Referentiel.FactionOrdre} — {BonusPour50}", evaluation.Description);
    }

    [Fact]
    public void Lien_d_ame_ajoute_ordre_aux_factions_du_heros()
    {
        var hero = MoteurEquipes.CreerHero(1, 1, ClasseDps, [FactionX], lienAme: true, EquipementLeger);

        Assert.Equal(new[] { FactionX, Referentiel.FactionOrdre }, hero.Factions);
    }

    [Fact]
    public void Lien_d_ame_n_ajoute_pas_ordre_en_double()
    {
        var hero = MoteurEquipes.CreerHero(1, 1, ClasseDps, [Referentiel.FactionOrdre], lienAme: true, EquipementLeger);

        Assert.Equal(new[] { Referentiel.FactionOrdre }, hero.Factions);
    }

    [Fact]
    public void Sans_lien_d_ame_les_factions_sont_inchangees()
    {
        var hero = MoteurEquipes.CreerHero(1, 1, ClasseDps, [FactionX], lienAme: false, EquipementLeger);

        Assert.Equal(new[] { FactionX }, hero.Factions);
    }

    [Fact]
    public void Lien_d_ame_fait_resonner_le_heros_avec_la_faction_des_autres()
    {
        HeroMoteur[] equipe =
        [
            .. Heros(2, ClasseDps, FactionX),
            MoteurEquipes.CreerHero(100, 1, ClasseDps, [FactionY], lienAme: true, EquipementLeger),
        ];

        Assert.Equal(PointsPour25, EvaluerEquipe(equipe).Points);
    }

    [Fact]
    public void Classe_absente_des_roles_est_un_dps()
    {
        var moteur = Moteur(Hero(1, ClasseSansRole));

        Assert.Equal(MoteurEquipes.RoleDps, moteur.RoleDe(1));
    }

    [Fact]
    public void Roles_attribues_par_classe()
    {
        var moteur = Moteur(Hero(1, ClasseTank), Hero(2, ClasseDps), Hero(3, ClasseSoutien));

        Assert.Equal(MoteurEquipes.RoleTank, moteur.RoleDe(1));
        Assert.Equal(MoteurEquipes.RoleDps, moteur.RoleDe(2));
        Assert.Equal(MoteurEquipes.RoleSoutien, moteur.RoleDe(3));
    }

    [Fact]
    public void Compteurs_de_roles_et_somme_des_etoiles()
    {
        var evaluation = EvaluerEquipe(
            Hero(1, ClasseTank, etoiles: 3), Hero(2, ClasseDps, etoiles: 2), Hero(3, ClasseDps, etoiles: 2), Hero(4, ClasseSoutien, etoiles: 1));

        Assert.Equal(2, evaluation.Dps);
        Assert.Equal(1, evaluation.Tanks);
        Assert.Equal(0, evaluation.TanksSecondaires);
        Assert.Equal(1, evaluation.Soutiens);
        Assert.Equal(8, evaluation.Etoiles);
    }

    [Fact]
    public void Sans_tank_la_penalite_est_de_deux()
    {
        Assert.Equal(2, EvaluerEquipe(Hero(1, ClasseDps), Hero(2, ClasseDps), Hero(3, ClasseSoutien)).PenaliteRoles);
    }

    [Fact]
    public void Un_tank_seul_coute_un_point()
    {
        Assert.Equal(1, EvaluerEquipe(Hero(1, ClasseTank), Hero(2, ClasseDps), Hero(3, ClasseDps), Hero(4, ClasseSoutien)).PenaliteRoles);
    }

    [Fact]
    public void Deux_tanks_suppriment_la_penalite_de_front()
    {
        Assert.Equal(0, EvaluerEquipe(EquipeEquilibree()).PenaliteRoles);
    }

    [Fact]
    public void Un_heros_lourd_sans_role_tank_est_un_tank_secondaire()
    {
        var evaluation = EvaluerEquipe(
            Hero(1, ClasseTank), Hero(2, ClasseDps, equipement: Referentiel.EquipementLourd), Hero(3, ClasseDps), Hero(4, ClasseSoutien));

        Assert.Equal(1, evaluation.TanksSecondaires);
        Assert.Equal(0, evaluation.PenaliteRoles);
    }

    [Fact]
    public void Un_tank_d_equipement_lourd_n_est_pas_un_tank_secondaire()
    {
        var evaluation = EvaluerEquipe(
            Hero(1, ClasseTank, equipement: Referentiel.EquipementLourd), Hero(2, ClasseDps), Hero(3, ClasseDps), Hero(4, ClasseSoutien));

        Assert.Equal(0, evaluation.TanksSecondaires);
        Assert.Equal(1, evaluation.PenaliteRoles);
    }

    [Fact]
    public void Un_heros_d_equipement_moyen_forme_le_second_front()
    {
        var evaluation = EvaluerEquipe(
            Hero(1, ClasseTank), Hero(2, ClasseDps, equipement: Referentiel.EquipementMoyen), Hero(3, ClasseDps), Hero(4, ClasseSoutien));

        Assert.Equal(0, evaluation.PenaliteRoles);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    public void Ecart_du_nombre_de_dps_a_deux_coute_son_ecart(int nombreDps, int penaliteAttendue)
    {
        HeroMoteur[] equipe = [Hero(1, ClasseTank), Hero(2, ClasseTank), Hero(3, ClasseSoutien), .. Enumerable.Range(10, nombreDps).Select(id => Hero(id, ClasseDps))];

        Assert.Equal(penaliteAttendue, EvaluerEquipe(equipe).PenaliteRoles);
    }

    [Fact]
    public void Sans_soutien_la_penalite_est_d_un_point()
    {
        Assert.Equal(1, EvaluerEquipe(Hero(1, ClasseTank), Hero(2, ClasseTank), Hero(3, ClasseDps), Hero(4, ClasseDps)).PenaliteRoles);
    }

    [Fact]
    public void Evaluation_ne_depend_pas_de_l_ordre_des_identifiants()
    {
        HeroMoteur[] equipe = [.. Heros(3, ClasseDps, FactionX), Hero(100, ClasseTank, [FactionY]), Hero(101, ClasseSoutien, [FactionY])];

        var dansLOrdre = Moteur(equipe).Evaluer([1, 2, 3, 100, 101]);
        var inverse = Moteur(equipe).Evaluer([101, 100, 3, 2, 1]);

        Assert.Equal(dansLOrdre, inverse);
    }

    [Fact]
    public void Soixante_quatre_factions_distinctes_sont_acceptees()
    {
        var autresFactions = FactionsMaximum - Referentiel.FactionsRegulieres.Length;

        var exception = Record.Exception(() => Moteur(Hero(1, ClasseDps, FactionsInventees(autresFactions))));

        Assert.Null(exception);
    }

    [Fact]
    public void Plus_de_soixante_quatre_factions_distinctes_levent_une_exception()
    {
        var autresFactions = FactionsMaximum - Referentiel.FactionsRegulieres.Length + 1;

        Assert.Throws<InvalidOperationException>(() => Moteur(Hero(1, ClasseDps, FactionsInventees(autresFactions))));
    }

    private static string[] FactionsInventees(int nombre) =>
        [.. Enumerable.Range(0, nombre).Select(i => $"Faction inventée {i}")];
}
