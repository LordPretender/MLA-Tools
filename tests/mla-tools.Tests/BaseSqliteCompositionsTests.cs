using Xunit;
using static MlaTools.Tests.HerosDeTest;

namespace MlaTools.Tests;

public class BaseSqliteCompositionsTests : IDisposable
{
    private const string HeroA = "A";
    private const string HeroB = "B";
    private const string Hero1 = "H1";
    private const string Hero2 = "H2";
    private const string Hero3 = "H3";
    private const string Hero4 = "H4";

    private const string NomAlpha = "Alpha";
    private const string NomBeta = "Beta";
    private const string NomZebre = "Zèbre";
    private const string NomAccentue = "Équipe Rouge";
    private const string NomSansAccent = "equipe rouge";
    private const string NomCopie = "Copie";

    private const string ClasseTest = "Berserker";
    private const string RoleDps = "Dps";
    private const string RoleTank = "Tank";

    private const string CleTest = "cle_test";
    private const string CleTest2 = "cle_test_2";
    private const string ValeurUne = "une";
    private const string ValeurDeux = "deux";
    private const string PorteeTest = "portee_test";
    private const string ZoneNomA = "ZoneA";
    private const string ZoneNomB = "ZoneB";

    private static readonly Dictionary<string, string> ReglagesTest = new() { [CleTest] = ValeurUne };

    private readonly BaseTemporaire _base = new();

    public void Dispose() => _base.Dispose();

    private int AjouterHeroAnodin(string nom) =>
        BaseSqlite.AjouterHero(UnHero(nom), _base.Chemin);

    [Fact]
    public void CreerComposition_PuisLireComposition_RestitueLesAffectations()
    {
        var a = AjouterHeroAnodin(HeroA);
        var b = AjouterHeroAnodin(HeroB);
        List<Affectation> contenu = [new(a, 0, 0), new(b, 0, 1)];

        var id = BaseSqlite.CreerComposition(NomAlpha, contenu, 1, _base.Chemin);

        Assert.Equal(contenu, BaseSqlite.LireComposition(id, _base.Chemin));
    }

    [Fact]
    public void ListerCompositions_TrieParNomNormalise()
    {
        BaseSqlite.CreerComposition(NomZebre, [], 0, _base.Chemin);
        BaseSqlite.CreerComposition(NomBeta, [], 0, _base.Chemin);
        BaseSqlite.CreerComposition(NomAlpha, [], 0, _base.Chemin);

        Assert.Equal([NomAlpha, NomBeta, NomZebre], BaseSqlite.ListerCompositions(_base.Chemin).Select(c => c.Nom));
    }

    [Fact]
    public void NomCompositionPris_IgnoreCasseEtAccents()
    {
        BaseSqlite.CreerComposition(NomAccentue, [], 0, _base.Chemin);

        Assert.True(BaseSqlite.NomCompositionPris(NomSansAccent, null, _base.Chemin));
        Assert.False(BaseSqlite.NomCompositionPris(NomAlpha, null, _base.Chemin));
    }

    [Fact]
    public void NomCompositionPris_IdIgnore_UneCompositionNEstPasEnConflitAvecElleMeme()
    {
        var id = BaseSqlite.CreerComposition(NomAccentue, [], 0, _base.Chemin);

        Assert.False(BaseSqlite.NomCompositionPris(NomSansAccent, id, _base.Chemin));
    }

    [Fact]
    public void RenommerComposition_ChangeLeNom_EtLAncienNomEstLibre()
    {
        var id = BaseSqlite.CreerComposition(NomAlpha, [], 0, _base.Chemin);

        BaseSqlite.RenommerComposition(id, NomBeta, _base.Chemin);

        Assert.Equal(NomBeta, Assert.Single(BaseSqlite.ListerCompositions(_base.Chemin)).Nom);
        Assert.False(BaseSqlite.NomCompositionPris(NomAlpha, null, _base.Chemin));
        Assert.True(BaseSqlite.NomCompositionPris(NomBeta, null, _base.Chemin));
    }

    [Fact]
    public void RemplacerComposition_RemplaceLeContenuSansToucherLeNombreDEquipes()
    {
        var a = AjouterHeroAnodin(HeroA);
        var b = AjouterHeroAnodin(HeroB);
        var id = BaseSqlite.CreerComposition(NomAlpha, [new(a, 0, 0), new(b, 1, 0)], 2, _base.Chemin);
        List<Affectation> nouveau = [new(b, 0, 0)];

        BaseSqlite.RemplacerComposition(id, nouveau, _base.Chemin);

        Assert.Equal(nouveau, BaseSqlite.LireComposition(id, _base.Chemin));
        Assert.Equal(2, Assert.Single(BaseSqlite.ListerCompositions(_base.Chemin)).NombreEquipes);
    }

    [Fact]
    public void CreerComposition_EquipeVide_ConserveSonNombreDEquipes()
    {
        var a = AjouterHeroAnodin(HeroA);

        BaseSqlite.CreerComposition(NomAlpha, [new(a, 0, 0)], 3, _base.Chemin);

        Assert.Equal(3, Assert.Single(BaseSqlite.ListerCompositions(_base.Chemin)).NombreEquipes);
    }

    [Fact]
    public void DupliquerComposition_CopieLeContenuEtLeNombreDEquipes()
    {
        var a = AjouterHeroAnodin(HeroA);
        List<Affectation> contenu = [new(a, 1, 0)];
        var original = BaseSqlite.CreerComposition(NomAlpha, contenu, 2, _base.Chemin);

        var copie = BaseSqlite.DupliquerComposition(original, NomCopie, _base.Chemin);

        Assert.NotEqual(original, copie);
        Assert.Equal(contenu, BaseSqlite.LireComposition(copie, _base.Chemin));
        Assert.Equal(2, BaseSqlite.ListerCompositions(_base.Chemin).Single(c => c.Id == copie).NombreEquipes);
    }

    [Fact]
    public void SupprimerComposition_RetireSesLignesEtLaisseLesAutres()
    {
        var a = AjouterHeroAnodin(HeroA);
        var supprimee = BaseSqlite.CreerComposition(NomAlpha, [new(a, 0, 0)], 1, _base.Chemin);
        var gardee = BaseSqlite.CreerComposition(NomBeta, [new(a, 0, 0)], 1, _base.Chemin);

        BaseSqlite.SupprimerComposition(supprimee, _base.Chemin);

        Assert.Equal(gardee, Assert.Single(BaseSqlite.ListerCompositions(_base.Chemin)).Id);
        Assert.Empty(BaseSqlite.LireComposition(supprimee, _base.Chemin));
        Assert.Single(BaseSqlite.LireComposition(gardee, _base.Chemin));
    }

    [Fact]
    public void CreerComposition_AvecReglages_LesRestitue()
    {
        var reglages = new Dictionary<string, string> { [CleTest] = ValeurUne, [CleTest2] = ValeurDeux };

        var id = BaseSqlite.CreerComposition(NomAlpha, [], 0, _base.Chemin, reglages);

        Assert.Equal(reglages, BaseSqlite.LireReglagesComposition(id, _base.Chemin));
    }

    [Fact]
    public void CreerComposition_SansReglages_NEnRestitueAucun()
    {
        var id = BaseSqlite.CreerComposition(NomAlpha, [], 0, _base.Chemin);

        Assert.Empty(BaseSqlite.LireReglagesComposition(id, _base.Chemin));
    }

    [Fact]
    public void LireReglagesComposition_NeMelangePasLesCompositions()
    {
        var alpha = BaseSqlite.CreerComposition(NomAlpha, [], 0, _base.Chemin, ReglagesTest);
        var beta = BaseSqlite.CreerComposition(NomBeta, [], 0, _base.Chemin, new Dictionary<string, string> { [CleTest] = ValeurDeux });

        Assert.Equal(ValeurUne, BaseSqlite.LireReglagesComposition(alpha, _base.Chemin)[CleTest]);
        Assert.Equal(ValeurDeux, BaseSqlite.LireReglagesComposition(beta, _base.Chemin)[CleTest]);
    }

    [Fact]
    public void SupprimerComposition_RetireSesReglages()
    {
        var id = BaseSqlite.CreerComposition(NomAlpha, [], 0, _base.Chemin, ReglagesTest);

        BaseSqlite.SupprimerComposition(id, _base.Chemin);

        Assert.Empty(BaseSqlite.LireReglagesComposition(id, _base.Chemin));
    }

    [Fact]
    public void DupliquerComposition_CopieLesReglages()
    {
        var original = BaseSqlite.CreerComposition(NomAlpha, [], 0, _base.Chemin, ReglagesTest);

        var copie = BaseSqlite.DupliquerComposition(original, NomCopie, _base.Chemin);

        Assert.Equal(ReglagesTest, BaseSqlite.LireReglagesComposition(copie, _base.Chemin));
        Assert.Equal(ReglagesTest, BaseSqlite.LireReglagesComposition(original, _base.Chemin));
    }

    [Fact]
    public void EcrireAffectations_RemplaceToutL_Etat()
    {
        var a = AjouterHeroAnodin(HeroA);
        var b = AjouterHeroAnodin(HeroB);
        BaseSqlite.EcrireAffectations([new(a, 0, 0)], _base.Chemin);

        BaseSqlite.EcrireAffectations([new(b, 1, 0)], _base.Chemin);

        Assert.Equal([new Affectation(b, 1, 0)], BaseSqlite.LireAffectations(_base.Chemin));
    }

    [Fact]
    public void LireAffectations_TrieParEquipePuisPosition_ReserveEnTete()
    {
        var h1 = AjouterHeroAnodin(Hero1);
        var h2 = AjouterHeroAnodin(Hero2);
        var h3 = AjouterHeroAnodin(Hero3);
        var h4 = AjouterHeroAnodin(Hero4);

        BaseSqlite.EcrireAffectations([new(h1, 1, 0), new(h2, null, 0), new(h3, 0, 1), new(h4, 0, 0)], _base.Chemin);

        // Le tri SQLite place les équipes nulles (la réserve) avant les équipes numérotées.
        Assert.Equal(
            [new Affectation(h2, null, 0), new Affectation(h4, 0, 0), new Affectation(h3, 0, 1), new Affectation(h1, 1, 0)],
            BaseSqlite.LireAffectations(_base.Chemin));
    }

    [Fact]
    public void EcrireRole_InserePuisMetAJourLeMemeRole()
    {
        BaseSqlite.EcrireRole(ClasseTest, RoleDps, _base.Chemin);
        Assert.Equal(RoleDps, BaseSqlite.LireRoles(_base.Chemin)[ClasseTest]);

        BaseSqlite.EcrireRole(ClasseTest, RoleTank, _base.Chemin);

        Assert.Equal(RoleTank, BaseSqlite.LireRoles(_base.Chemin)[ClasseTest]);
    }

    [Fact]
    public void EcrireParametre_SecondeEcritureDeLaMemeCle_RemplaceLaValeur()
    {
        BaseSqlite.EcrireParametre(CleTest, ValeurUne, _base.Chemin);

        BaseSqlite.EcrireParametre(CleTest, ValeurDeux, _base.Chemin);

        Assert.Equal(ValeurDeux, BaseSqlite.LireParametres(_base.Chemin)[CleTest]);
    }

    [Fact]
    public void EcrireCalibrage_EcritParametresEtZones_UnSecondAppelRemplaceToutesLesZones()
    {
        var zoneA = new ZoneStockee(PorteeTest, ZoneNomA, new Zone(1, 2, 3, 4));
        var zoneB = new ZoneStockee(PorteeTest, ZoneNomB, new Zone(5, 6, 7, 8));
        BaseSqlite.EcrireCalibrage(new Dictionary<string, string> { [CleTest] = ValeurUne }, [zoneA, zoneB], _base.Chemin);

        Assert.Equal(ValeurUne, BaseSqlite.LireParametres(_base.Chemin)[CleTest]);
        Assert.Equal([zoneA, zoneB], BaseSqlite.LireZones(_base.Chemin).OrderBy(z => z.Nom));

        BaseSqlite.EcrireCalibrage(new Dictionary<string, string> { [CleTest] = ValeurDeux }, [zoneB], _base.Chemin);

        Assert.Equal(ValeurDeux, BaseSqlite.LireParametres(_base.Chemin)[CleTest]);
        Assert.Equal([zoneB], BaseSqlite.LireZones(_base.Chemin));
    }
}
