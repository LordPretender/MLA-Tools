using Xunit;

namespace MlaTools.Tests;

public class ReglagesEquipesTests
{
    private const int PremiereEquipe = 0;
    private const int SecondeEquipe = 1;
    private const int Heros = 7;
    private const int NombreEquipes = 2;
    private const int SeuilHeros = 4;
    private const int SeuilGlobal = 6;
    private const int SeuilEquipeHaut = 9;
    private static readonly string FactionA = Referentiel.FactionsRegulieres[0];
    private static readonly string FactionB = Referentiel.FactionsRegulieres[1];

    [Fact]
    public void Un_heros_ne_peut_pas_etre_impose_a_deux_equipes()
    {
        var reglages = new ReglagesEquipes();

        Assert.True(reglages.Imposer(PremiereEquipe, Heros));
        Assert.False(reglages.Imposer(SecondeEquipe, Heros));
        Assert.Empty(reglages.Imposes(SecondeEquipe));
    }

    [Fact]
    public void Un_heros_impose_ne_peut_pas_etre_banni_dans_la_meme_equipe_et_inversement()
    {
        var reglages = new ReglagesEquipes();
        reglages.Imposer(PremiereEquipe, Heros);

        Assert.False(reglages.Bannir(PremiereEquipe, Heros));
        Assert.True(reglages.Bannir(SecondeEquipe, Heros));
        Assert.False(reglages.Imposer(SecondeEquipe, Heros));
    }

    [Fact]
    public void Retirer_un_heros_impose_le_rend_disponible_pour_une_autre_equipe()
    {
        var reglages = new ReglagesEquipes();
        reglages.Imposer(PremiereEquipe, Heros);

        reglages.RetirerImpose(PremiereEquipe, Heros);

        Assert.True(reglages.Imposer(SecondeEquipe, Heros));
    }

    [Fact]
    public void Tout_reinitialiser_vide_chaque_equipe_mais_garde_le_seuil_global()
    {
        var reglages = new ReglagesEquipes { EtoilesMiniHerosComposition = SeuilGlobal };
        reglages.Imposer(PremiereEquipe, Heros);
        reglages.Bannir(SecondeEquipe, Heros);
        reglages.FixerEtoilesMiniHeros(PremiereEquipe, SeuilHeros);
        reglages.ImposerFaction(PremiereEquipe, FactionA);
        reglages.BannirFaction(SecondeEquipe, FactionB);

        reglages.ReinitialiserToutes();

        for (var equipe = PremiereEquipe; equipe < NombreEquipes; equipe++)
        {
            Assert.Empty(reglages.Imposes(equipe));
            Assert.Empty(reglages.Bannis(equipe));
            Assert.Equal(0, reglages.EtoilesMiniHeros(equipe));
            Assert.Empty(reglages.FactionsImposees(equipe));
            Assert.Empty(reglages.FactionsBannies(equipe));
        }
        Assert.Equal(SeuilGlobal, reglages.EtoilesMiniHerosComposition);
    }

    [Fact]
    public void Resume_decrit_les_criteres_actifs_d_une_equipe_et_est_vide_sans_critere()
    {
        var reglages = new ReglagesEquipes { EtoilesMiniHerosComposition = SeuilGlobal };
        Assert.False(reglages.ADesReglages);
        Assert.Empty(reglages.Resume(PremiereEquipe));

        reglages.Imposer(PremiereEquipe, Heros);
        reglages.Imposer(PremiereEquipe, Heros + 1);
        reglages.Bannir(PremiereEquipe, Heros + 2);
        reglages.BannirFaction(PremiereEquipe, FactionB);
        reglages.FixerEtoilesMiniHeros(PremiereEquipe, SeuilHeros);

        Assert.True(reglages.ADesReglages);
        Assert.Equal(["2 héros imposés", "1 héros banni", "1 faction bannie", $"héros ≥ {SeuilHeros} étoiles"], reglages.Resume(PremiereEquipe));
        Assert.Empty(reglages.Resume(SecondeEquipe));

        reglages.ReinitialiserToutes();

        Assert.False(reglages.ADesReglages);
    }

    [Fact]
    public void La_reinitialisation_ne_touche_que_son_equipe()
    {
        var reglages = new ReglagesEquipes();
        reglages.Imposer(PremiereEquipe, Heros);
        reglages.Bannir(PremiereEquipe, Heros + 1);
        reglages.Bannir(SecondeEquipe, Heros + 1);

        reglages.Reinitialiser(PremiereEquipe);

        Assert.Empty(reglages.Imposes(PremiereEquipe));
        Assert.Empty(reglages.Bannis(PremiereEquipe));
        Assert.Single(reglages.Bannis(SecondeEquipe));
    }

    [Fact]
    public void La_copie_est_independante_de_l_original()
    {
        var reglages = new ReglagesEquipes();
        var copie = reglages.Copier();

        copie.Imposer(PremiereEquipe, Heros);

        Assert.Empty(reglages.Imposes(PremiereEquipe));
    }

    [Fact]
    public void La_serialisation_se_relit_a_l_identique()
    {
        var reglages = new ReglagesEquipes();
        reglages.Imposer(PremiereEquipe, Heros);
        reglages.Imposer(SecondeEquipe, Heros + 1);
        reglages.Bannir(PremiereEquipe, Heros + 2);

        var relu = ReglagesEquipes.Charger(reglages.SerialiserImposes(), reglages.SerialiserBannis());

        Assert.Equal(reglages.SerialiserImposes(), relu.SerialiserImposes());
        Assert.Equal(reglages.SerialiserBannis(), relu.SerialiserBannis());
    }

    [Fact]
    public void Le_seuil_d_etoiles_par_heros_se_serialise_se_copie_et_se_reinitialise_par_equipe()
    {
        var reglages = new ReglagesEquipes();
        reglages.FixerEtoilesMiniHeros(PremiereEquipe, SeuilHeros);
        reglages.FixerEtoilesMiniHeros(SecondeEquipe, SeuilHeros + 1);

        var relu = ReglagesEquipes.Charger(null, null, reglages.SerialiserEtoilesMiniHeros());
        var copie = reglages.Copier();
        reglages.Reinitialiser(PremiereEquipe);

        Assert.Equal(SeuilHeros, relu.EtoilesMiniHeros(PremiereEquipe));
        Assert.Equal(SeuilHeros + 1, relu.EtoilesMiniHeros(SecondeEquipe));
        Assert.Equal(SeuilHeros, copie.EtoilesMiniHeros(PremiereEquipe));
        Assert.Equal(0, reglages.EtoilesMiniHeros(PremiereEquipe));
        Assert.Equal(SeuilHeros + 1, reglages.Criteres(SecondeEquipe, _ => true).EtoilesMiniHeros);
    }

    [Fact]
    public void Un_seuil_nul_retire_le_critere_de_l_equipe()
    {
        var reglages = new ReglagesEquipes();
        reglages.FixerEtoilesMiniHeros(PremiereEquipe, SeuilHeros);

        reglages.FixerEtoilesMiniHeros(PremiereEquipe, 0);

        Assert.True(reglages.Criteres(PremiereEquipe, _ => true).EstVide);
        Assert.Equal(string.Empty, reglages.SerialiserEtoilesMiniHeros());
    }

    [Fact]
    public void Un_seuil_illisible_ou_hors_borne_est_ignore_ou_ramene_au_maximum()
    {
        var reglages = ReglagesEquipes.Charger(null, null, "x:3;0:abc;1:9999");

        Assert.Equal(0, reglages.EtoilesMiniHeros(PremiereEquipe));
        Assert.Equal(FormOptionsGeneration.EtoilesMinimumMax, reglages.EtoilesMiniHeros(SecondeEquipe));
    }

    [Fact]
    public void Le_seuil_global_s_applique_a_toutes_les_equipes_et_le_seuil_d_equipe_le_releve()
    {
        var reglages = new ReglagesEquipes { EtoilesMiniHerosComposition = SeuilGlobal };
        reglages.FixerEtoilesMiniHeros(SecondeEquipe, SeuilEquipeHaut);

        Assert.Equal(SeuilGlobal, reglages.Criteres(PremiereEquipe, _ => true).EtoilesMiniHeros);
        Assert.Equal(SeuilEquipeHaut, reglages.Criteres(SecondeEquipe, _ => true).EtoilesMiniHeros);
    }

    [Fact]
    public void Un_seuil_d_equipe_sous_le_seuil_global_ne_l_assouplit_pas()
    {
        var reglages = new ReglagesEquipes { EtoilesMiniHerosComposition = SeuilGlobal };
        reglages.FixerEtoilesMiniHeros(PremiereEquipe, SeuilHeros);

        Assert.Equal(SeuilGlobal, reglages.Criteres(PremiereEquipe, _ => true).EtoilesMiniHeros);
    }

    [Fact]
    public void Le_seuil_global_survit_a_la_copie_et_a_la_reinitialisation_d_une_equipe()
    {
        var reglages = new ReglagesEquipes { EtoilesMiniHerosComposition = SeuilGlobal };

        var copie = reglages.Copier();
        reglages.Reinitialiser(PremiereEquipe);

        Assert.Equal(SeuilGlobal, copie.EtoilesMiniHerosComposition);
        Assert.Equal(SeuilGlobal, reglages.EtoilesMiniHerosComposition);
    }

    [Fact]
    public void Le_seuil_global_charge_est_borne_au_maximum_et_au_minimum()
    {
        Assert.Equal(FormOptionsGeneration.EtoilesMinimumMax, ReglagesEquipes.Charger(null, null, null, 9999).EtoilesMiniHerosComposition);
        Assert.Equal(0, ReglagesEquipes.Charger(null, null, null, -3).EtoilesMiniHerosComposition);
        Assert.Equal(SeuilGlobal, ReglagesEquipes.Charger(null, null, null, SeuilGlobal).EtoilesMiniHerosComposition);
    }

    [Fact]
    public void Le_lien_d_ame_est_actif_par_defaut_et_au_chargement_sans_valeur()
    {
        Assert.True(new ReglagesEquipes().LienAmeActif);
        Assert.True(ReglagesEquipes.Charger(null, null).LienAmeActif);
    }

    [Fact]
    public void Le_lien_d_ame_inactif_est_charge_copie_et_survit_aux_reinitialisations()
    {
        var reglages = ReglagesEquipes.Charger(null, null, lienAmeActif: false);

        reglages.ReinitialiserToutes();
        reglages.Reinitialiser(PremiereEquipe);

        Assert.False(reglages.LienAmeActif);
        Assert.False(reglages.Copier().LienAmeActif);
    }

    [Fact]
    public void Une_faction_ne_peut_pas_etre_imposee_et_bannie_dans_la_meme_equipe()
    {
        var reglages = new ReglagesEquipes();
        reglages.ImposerFaction(PremiereEquipe, FactionA);

        Assert.False(reglages.BannirFaction(PremiereEquipe, FactionA));
        Assert.True(reglages.BannirFaction(SecondeEquipe, FactionA));
        Assert.False(reglages.ImposerFaction(SecondeEquipe, FactionA));
    }

    [Fact]
    public void Une_faction_peut_etre_imposee_a_plusieurs_equipes()
    {
        var reglages = new ReglagesEquipes();

        Assert.True(reglages.ImposerFaction(PremiereEquipe, FactionA));
        Assert.True(reglages.ImposerFaction(SecondeEquipe, FactionA));
    }

    [Fact]
    public void Retirer_une_faction_la_rend_disponible_pour_l_autre_critere()
    {
        var reglages = new ReglagesEquipes();
        reglages.ImposerFaction(PremiereEquipe, FactionA);

        reglages.RetirerFactionImposee(PremiereEquipe, FactionA);

        Assert.True(reglages.BannirFaction(PremiereEquipe, FactionA));
    }

    [Fact]
    public void Les_factions_se_serialisent_se_copient_et_se_reinitialisent_par_equipe()
    {
        var reglages = new ReglagesEquipes();
        reglages.ImposerFaction(PremiereEquipe, FactionA);
        reglages.BannirFaction(PremiereEquipe, FactionB);
        reglages.BannirFaction(SecondeEquipe, FactionB);

        var relu = ReglagesEquipes.Charger(null, null, factionsImposees: reglages.SerialiserFactionsImposees(), factionsBannies: reglages.SerialiserFactionsBannies());
        var copie = reglages.Copier();
        reglages.Reinitialiser(PremiereEquipe);

        Assert.Equal(copie.SerialiserFactionsBannies(), relu.SerialiserFactionsBannies());
        Assert.Equal([FactionA], relu.FactionsImposees(PremiereEquipe));
        Assert.Equal([FactionB], relu.FactionsBannies(PremiereEquipe));
        Assert.Equal([FactionA], copie.FactionsImposees(PremiereEquipe));
        Assert.Empty(reglages.FactionsImposees(PremiereEquipe));
        Assert.Empty(reglages.FactionsBannies(PremiereEquipe));
        Assert.Equal([FactionB], reglages.FactionsBannies(SecondeEquipe));
    }

    [Fact]
    public void Une_faction_inconnue_est_ignoree_au_chargement()
    {
        var reglages = ReglagesEquipes.Charger(null, null, factionsImposees: $"{PremiereEquipe}:Inconnue,{FactionA};x:{FactionB}");

        Assert.Equal([FactionA], reglages.FactionsImposees(PremiereEquipe));
    }

    [Fact]
    public void Les_factions_figurent_dans_les_criteres_de_l_equipe()
    {
        var reglages = new ReglagesEquipes();
        reglages.ImposerFaction(PremiereEquipe, FactionA);
        reglages.BannirFaction(PremiereEquipe, FactionB);

        var criteres = reglages.Criteres(PremiereEquipe, _ => true);

        Assert.False(criteres.EstVide);
        Assert.Equal([FactionA], criteres.FactionsImposees);
        Assert.Equal([FactionB], criteres.FactionsBannies);
        Assert.True(reglages.Criteres(SecondeEquipe, _ => true).EstVide);
    }

    [Fact]
    public void Une_valeur_illisible_est_ignoree_au_chargement()
    {
        var reglages = ReglagesEquipes.Charger("x:1;0:abc,5;2", null);

        Assert.Equal([5], reglages.Imposes(PremiereEquipe));
    }

    [Fact]
    public void Un_heros_inexistant_est_ignore_dans_les_criteres()
    {
        var reglages = new ReglagesEquipes();
        reglages.Imposer(PremiereEquipe, Heros);
        reglages.Imposer(PremiereEquipe, Heros + 1);

        var criteres = reglages.Pour(NombreEquipes, id => id == Heros);

        Assert.Equal([Heros], criteres[PremiereEquipe].Imposes);
        Assert.True(criteres[SecondeEquipe].EstVide);
    }

    [Fact]
    public void Les_valeurs_par_defaut_du_niveau_composition_ne_comptent_pas_comme_des_reglages()
    {
        var reglages = new ReglagesEquipes();

        Assert.False(reglages.ADesReglagesComposition);
        Assert.Equal(0, reglages.Criteres(PremiereEquipe, _ => true).EtoilesMiniHeros);
    }

    [Fact]
    public void ReinitialiserComposition_remet_le_seuil_et_le_lien_d_ame_par_defaut_sans_toucher_aux_equipes()
    {
        var reglages = new ReglagesEquipes { EtoilesMiniHerosComposition = SeuilGlobal, LienAmeActif = false };
        reglages.Imposer(PremiereEquipe, Heros);
        reglages.FixerEtoilesMiniHeros(SecondeEquipe, SeuilHeros);

        Assert.True(reglages.ADesReglagesComposition);
        reglages.ReinitialiserComposition();

        Assert.False(reglages.ADesReglagesComposition);
        Assert.Equal(0, reglages.EtoilesMiniHerosComposition);
        Assert.True(reglages.LienAmeActif);
        Assert.Equal([Heros], reglages.Imposes(PremiereEquipe));
        Assert.Equal(SeuilHeros, reglages.EtoilesMiniHeros(SecondeEquipe));
    }

    [Fact]
    public void ReinitialiserToutes_ne_touche_pas_au_niveau_composition()
    {
        var reglages = new ReglagesEquipes { EtoilesMiniHerosComposition = SeuilGlobal, LienAmeActif = false };

        reglages.ReinitialiserToutes();

        Assert.Equal(SeuilGlobal, reglages.EtoilesMiniHerosComposition);
        Assert.False(reglages.LienAmeActif);
    }
}
