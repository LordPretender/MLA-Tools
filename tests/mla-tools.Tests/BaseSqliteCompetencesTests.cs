using Microsoft.Data.Sqlite;
using Xunit;
using static MlaTools.Tests.HerosDeTest;

namespace MlaTools.Tests;

public class BaseSqliteCompetencesTests : IDisposable
{
    private const string Alpha = "Alpha";
    private const string Beta = "Beta";
    private const string TypeCompetence = "Compétence";
    private const string TypeUlti = "Ulti";
    private const string TagSoigner = "Soigner";
    private const string TagControler = "Contrôler";
    private const string TagZone = "AoE";
    private const string TagZoneNormalise = "aoe";
    private const string TagSoignerNormalise = "soigner";
    private const string TagControlerNormalise = "controler";
    private const string TagControlerMajuscules = "CONTROLER";
    private const string TagControlerAccentMajuscules = "CONTRÔLER";
    private const int NumeroInvalide = 5;
    private const int PremiereCompetence = 1;
    private const int DeuxiemeCompetence = 2;
    private const int VersionAvantCompetences = 6;
    private const int VersionAvecCompetences = 7;
    private const string SqlRetourVersionAvantCompetences = """
        DROP INDEX ix_competences_tags_tag;
        DROP TABLE competences_tags;
        DROP TABLE competences_recensees;
        PRAGMA user_version = 6;
        """;

    private readonly BaseTemporaire _base = new();

    public void Dispose() => _base.Dispose();

    private int AjouterHero(string nom) => BaseSqlite.AjouterHero(UnHero(nom), _base.Chemin);

    [Fact]
    public void EnregistrerCompetence_PuisLire_RestitueTypeEtTagsDansLOrdre()
    {
        var hero = AjouterHero(Alpha);

        BaseSqlite.EnregistrerCompetence(hero, PremiereCompetence, TypeCompetence, [TagControler, TagZone], _base.Chemin);

        var competence = Assert.Single(BaseSqlite.LireCompetences(_base.Chemin));
        Assert.Equal(hero, competence.HeroId);
        Assert.Equal(PremiereCompetence, competence.Numero);
        Assert.Equal(TypeCompetence, competence.Type);
        Assert.Equal([TagControler, TagZone], competence.Tags);
        Assert.NotNull(competence.DateLecture);
    }

    [Fact]
    public void EnregistrerCompetence_SansTag_RestitueUneListeVide()
    {
        var hero = AjouterHero(Alpha);

        BaseSqlite.EnregistrerCompetence(hero, PremiereCompetence, TypeUlti, [], _base.Chemin);

        Assert.Empty(Assert.Single(BaseSqlite.LireCompetences(_base.Chemin)).Tags);
    }

    [Fact]
    public void EnregistrerCompetence_MemeCompetenceRelue_RemplaceTypeEtTags()
    {
        var hero = AjouterHero(Alpha);
        BaseSqlite.EnregistrerCompetence(hero, PremiereCompetence, TypeCompetence, [TagSoigner, TagZone], _base.Chemin);

        BaseSqlite.EnregistrerCompetence(hero, PremiereCompetence, TypeUlti, [TagControler], _base.Chemin);

        var competence = Assert.Single(BaseSqlite.LireCompetences(_base.Chemin));
        Assert.Equal(TypeUlti, competence.Type);
        Assert.Equal([TagControler], competence.Tags);
    }

    [Fact]
    public void EnregistrerCompetence_TagEnDoublon_NeLeStockeQuUneFois()
    {
        var hero = AjouterHero(Alpha);

        BaseSqlite.EnregistrerCompetence(hero, PremiereCompetence, TypeCompetence, [TagZone, TagZone], _base.Chemin);

        Assert.Equal([TagZone], Assert.Single(BaseSqlite.LireCompetences(_base.Chemin)).Tags);
    }

    [Fact]
    public void EnregistrerCompetence_NumeroHorsDeUnAQuatre_EstRefuse()
    {
        var hero = AjouterHero(Alpha);

        Assert.Throws<SqliteException>(() => BaseSqlite.EnregistrerCompetence(hero, NumeroInvalide, TypeCompetence, [], _base.Chemin));
        Assert.Empty(BaseSqlite.LireCompetences(_base.Chemin));
    }

    [Fact]
    public void LireCompetences_PlusieursHeros_SontTriesParHerosPuisNumero()
    {
        var alpha = AjouterHero(Alpha);
        var beta = AjouterHero(Beta);
        BaseSqlite.EnregistrerCompetence(beta, PremiereCompetence, TypeCompetence, [TagSoigner], _base.Chemin);
        BaseSqlite.EnregistrerCompetence(alpha, DeuxiemeCompetence, TypeUlti, [TagZone], _base.Chemin);
        BaseSqlite.EnregistrerCompetence(alpha, PremiereCompetence, TypeCompetence, [], _base.Chemin);

        var lues = BaseSqlite.LireCompetences(_base.Chemin).Select(c => (c.HeroId, c.Numero)).ToList();

        Assert.Equal([(alpha, PremiereCompetence), (alpha, DeuxiemeCompetence), (beta, PremiereCompetence)], lues);
    }

    [Fact]
    public void SupprimerHero_RetireSesCompetencesEtSesTags()
    {
        var alpha = AjouterHero(Alpha);
        var beta = AjouterHero(Beta);
        BaseSqlite.EnregistrerCompetence(alpha, PremiereCompetence, TypeCompetence, [TagZone], _base.Chemin);
        BaseSqlite.EnregistrerCompetence(beta, PremiereCompetence, TypeCompetence, [TagSoigner], _base.Chemin);

        BaseSqlite.SupprimerHero(alpha, _base.Chemin);

        Assert.Equal(beta, Assert.Single(BaseSqlite.LireCompetences(_base.Chemin)).HeroId);
        Assert.Equal([TagSoigner], BaseSqlite.ListerTagsDistincts(_base.Chemin).Select(t => t.Libelle).ToList());
    }

    [Fact]
    public void ListerTagsDistincts_CompteLesHerosDistinctsParTag()
    {
        var alpha = AjouterHero(Alpha);
        var beta = AjouterHero(Beta);
        BaseSqlite.EnregistrerCompetence(alpha, PremiereCompetence, TypeCompetence, [TagZone], _base.Chemin);
        BaseSqlite.EnregistrerCompetence(alpha, DeuxiemeCompetence, TypeUlti, [TagZone, TagSoigner], _base.Chemin);
        BaseSqlite.EnregistrerCompetence(beta, PremiereCompetence, TypeCompetence, [TagZone], _base.Chemin);

        var tags = BaseSqlite.ListerTagsDistincts(_base.Chemin);

        Assert.Equal([new TagRecense(TagZoneNormalise, TagZone, 2), new TagRecense(TagSoignerNormalise, TagSoigner, 1)], tags);
    }

    [Fact]
    public void EnregistrerCompetence_TagsDeCasseOuAccentsDifferents_SontUnSeulTagNormalise()
    {
        var alpha = AjouterHero(Alpha);
        var beta = AjouterHero(Beta);
        BaseSqlite.EnregistrerCompetence(alpha, PremiereCompetence, TypeCompetence, [TagControler, TagControlerMajuscules], _base.Chemin);
        BaseSqlite.EnregistrerCompetence(beta, PremiereCompetence, TypeCompetence, [TagControlerNormalise], _base.Chemin);

        var tags = BaseSqlite.ListerTagsDistincts(_base.Chemin);

        // Le doublon « CONTROLER » d'Alpha est écarté ; entre « Contrôler » et « controler », la majuscule précède.
        Assert.Equal(new TagRecense(TagControlerNormalise, TagControler, 2), Assert.Single(tags));
        Assert.Equal([TagControler], Assert.Single(BaseSqlite.LireCompetences(_base.Chemin), c => c.HeroId == alpha).Tags);
    }

    [Fact]
    public void HerosAyantTag_AccepteUneFormeDifferenteDuTag()
    {
        var alpha = AjouterHero(Alpha);
        BaseSqlite.EnregistrerCompetence(alpha, PremiereCompetence, TypeCompetence, [TagControler], _base.Chemin);

        Assert.Equal([alpha], BaseSqlite.HerosAyantTag(TagControlerNormalise, _base.Chemin));
        Assert.Equal([alpha], BaseSqlite.HerosAyantTag(TagControlerAccentMajuscules, _base.Chemin));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("...")]
    public void EnregistrerCompetence_TagSansLettreNiChiffre_EstIgnore(string bruit)
    {
        var hero = AjouterHero(Alpha);

        BaseSqlite.EnregistrerCompetence(hero, PremiereCompetence, TypeCompetence, [bruit, TagZone], _base.Chemin);

        Assert.Equal([TagZone], Assert.Single(BaseSqlite.LireCompetences(_base.Chemin)).Tags);
    }

    [Fact]
    public void HerosAyantTag_RetourneLesHerosDistinctsPorteursDuTag()
    {
        var alpha = AjouterHero(Alpha);
        var beta = AjouterHero(Beta);
        BaseSqlite.EnregistrerCompetence(alpha, PremiereCompetence, TypeCompetence, [TagZone], _base.Chemin);
        BaseSqlite.EnregistrerCompetence(alpha, DeuxiemeCompetence, TypeUlti, [TagZone], _base.Chemin);
        BaseSqlite.EnregistrerCompetence(beta, PremiereCompetence, TypeCompetence, [TagSoigner], _base.Chemin);

        Assert.Equal([alpha], BaseSqlite.HerosAyantTag(TagZone, _base.Chemin));
        Assert.Equal([beta], BaseSqlite.HerosAyantTag(TagSoigner, _base.Chemin));
        Assert.Empty(BaseSqlite.HerosAyantTag(TagControler, _base.Chemin));
    }

    [Fact]
    public void BaseVersionSix_MonteEnVersionSeptEtGardeSesDonnees()
    {
        var hero = AjouterHero(Alpha);
        var composition = BaseSqlite.CreerComposition(Beta, [new Affectation(hero, 0, 0)], 1, _base.Chemin);
        _base.ExecuterSql(SqlRetourVersionAvantCompetences);
        Assert.Equal(VersionAvantCompetences, _base.LireVersion());

        var heros = BaseSqlite.ListerHeros(_base.Chemin);

        Assert.Equal(Alpha, Assert.Single(heros).Hero.Nom);
        Assert.Single(BaseSqlite.LireComposition(composition, _base.Chemin));
        Assert.Empty(BaseSqlite.LireCompetences(_base.Chemin));
        Assert.Equal(VersionAvecCompetences, _base.LireVersion());
    }
}
