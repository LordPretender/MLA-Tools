using Xunit;

namespace MlaTools.Tests;

public class ReglagesEquipesTests
{
    private const int PremiereEquipe = 0;
    private const int SecondeEquipe = 1;
    private const int Heros = 7;
    private const int NombreEquipes = 2;

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
}
