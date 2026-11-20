using Xunit;

namespace MlaTools.Tests;

public class AffectationTests
{
    private static readonly Predicate<int> TousConnus = _ => true;

    private static List<List<int>> Listes(params int[][] listes) => listes.Select(liste => liste.ToList()).ToList();

    // Listes neuves à chaque appel : les méthodes testées les modifient en place.
    private static List<List<int>> EquipesInitiales() => Listes([1, 2], [3, 4]);

    private static List<int> ReserveInitiale() => [5];

    private static (List<List<int>> Equipes, List<int> Reserve) Situation() => (EquipesInitiales(), ReserveInitiale());

    [Fact]
    public void Repartir_MoinsDEquipesQueLeMinimum_CompleteAvecDesEquipesVides()
    {
        var (equipes, reserve) = Affectation.Repartir([new(1, 0, 0)], TousConnus, 3);

        Assert.Equal(Listes([1], [], []), equipes);
        Assert.Empty(reserve);
    }

    [Fact]
    public void Repartir_EquipeDIndiceSuperieurAuMinimum_AgrandiLaListe()
    {
        var (equipes, _) = Affectation.Repartir([new(1, 0, 0), new(2, 4, 0)], TousConnus, 2);

        Assert.Equal(Listes([1], [], [], [], [2]), equipes);
    }

    [Fact]
    public void Repartir_HerosInconnu_EstEcarte()
    {
        var (equipes, reserve) = Affectation.Repartir([new(1, 0, 0), new(2, 0, 1), new(3, null, 0)], id => id != 2, 1);

        Assert.Equal(Listes([1]), equipes);
        Assert.Equal([3], reserve);
    }

    [Fact]
    public void Repartir_EquipeNulle_VaALaReserve()
    {
        var (equipes, reserve) = Affectation.Repartir([new(1, null, 0), new(2, 0, 0)], TousConnus, 1);

        Assert.Equal(Listes([2]), equipes);
        Assert.Equal([1], reserve);
    }

    [Fact]
    public void Repartir_AffectationsDansLeDesordre_TrieParPosition()
    {
        var (equipes, reserve) = Affectation.Repartir(
            [new(1, 0, 2), new(2, 0, 0), new(3, 0, 1), new(4, null, 1), new(5, null, 0)], TousConnus, 1);

        Assert.Equal(Listes([2, 3, 1]), equipes);
        Assert.Equal([5, 4], reserve);
    }

    [Fact]
    public void Repartir_AucunHerosPlace_NeRendAucuneEquipeMemeAvecUnMinimum()
    {
        var (equipes, reserve) = Affectation.Repartir([new(1, 0, 0), new(2, null, 0)], _ => false, 3);

        Assert.Empty(equipes);
        Assert.Empty(reserve);
    }

    [Fact]
    public void Echanger_EntreDeuxEquipes_PermuteLesDeuxHeros()
    {
        var (equipes, reserve) = Situation();

        Affectation.Echanger(equipes, reserve, 2, 3);

        Assert.Equal(Listes([1, 3], [2, 4]), equipes);
        Assert.Equal(ReserveInitiale(), reserve);
    }

    [Fact]
    public void Echanger_EntreUneEquipeEtLaReserve_PermuteLesDeuxHeros()
    {
        var (equipes, reserve) = Situation();

        Affectation.Echanger(equipes, reserve, 1, 5);

        Assert.Equal(Listes([5, 2], [3, 4]), equipes);
        Assert.Equal([1], reserve);
    }

    [Fact]
    public void Echanger_DeuxHerosDeLaMemeEquipe_InverseLeurOrdre()
    {
        var (equipes, reserve) = Situation();

        Affectation.Echanger(equipes, reserve, 1, 2);

        Assert.Equal(Listes([2, 1], [3, 4]), equipes);
        Assert.Equal(ReserveInitiale(), reserve);
    }

    [Fact]
    public void EnvoyerEnReserve_HerosDEquipe_LePlaceEnFinDeReserve()
    {
        var (equipes, reserve) = Situation();

        var envoye = Affectation.EnvoyerEnReserve(equipes, reserve, 2);

        Assert.True(envoye);
        Assert.Equal(Listes([1], [3, 4]), equipes);
        Assert.Equal([5, 2], reserve);
    }

    // 5 est déjà en réserve, 99 n'est nulle part : dans les deux cas il n'y a rien à envoyer.
    [Theory]
    [InlineData(5)]
    [InlineData(99)]
    public void EnvoyerEnReserve_HerosAbsentDesEquipes_NeChangeRien(int id)
    {
        var (equipes, reserve) = Situation();

        var envoye = Affectation.EnvoyerEnReserve(equipes, reserve, id);

        Assert.False(envoye);
        Assert.Equal(EquipesInitiales(), equipes);
        Assert.Equal(ReserveInitiale(), reserve);
    }

    [Fact]
    public void BasculerSelection_PremierClic_ChoisitLeHerosSansEchanger()
    {
        var (equipes, reserve) = Situation();
        int? selection = null;

        var echange = Affectation.BasculerSelection(ref selection, equipes, reserve, 1);

        Assert.False(echange);
        Assert.Equal(1, selection);
        Assert.Equal(EquipesInitiales(), equipes);
    }

    [Fact]
    public void BasculerSelection_SecondClicSurLeMemeHeros_Deselectionne()
    {
        var (equipes, reserve) = Situation();
        int? selection = 1;

        var echange = Affectation.BasculerSelection(ref selection, equipes, reserve, 1);

        Assert.False(echange);
        Assert.Null(selection);
        Assert.Equal(EquipesInitiales(), equipes);
    }

    [Fact]
    public void BasculerSelection_SecondClicSurUnAutreHeros_EchangeEtVideLaSelection()
    {
        var (equipes, reserve) = Situation();
        int? selection = 1;

        var echange = Affectation.BasculerSelection(ref selection, equipes, reserve, 3);

        Assert.True(echange);
        Assert.Null(selection);
        Assert.Equal(Listes([3, 2], [1, 4]), equipes);
    }

    // Les héros 1 et 5 sont deux formes du même héros ; les autres sont chacun leur famille.
    private static int FamilleUnEtCinq(int id) => id == 5 ? 1 : id;

    [Theory]
    [InlineData(5, 3, false)]
    [InlineData(5, 1, true)]
    [InlineData(5, 2, false)]
    [InlineData(2, 5, false)]
    public void EchangeAutorise_EquipeEtReserve_RefuseSeulementSiLaFormeEstDejaEnEquipe(int a, int b, bool attendu)
    {
        var (equipes, reserve) = Situation();

        Assert.Equal(attendu, Affectation.EchangeAutorise(equipes, reserve, a, b, FamilleUnEtCinq));
    }

    [Fact]
    public void EchangeAutorise_EntreDeuxEquipes_EstToujoursPermis()
    {
        var (equipes, reserve) = (Listes([1, 2], [5, 4]), new List<int>());

        Assert.True(Affectation.EchangeAutorise(equipes, reserve, 2, 5, FamilleUnEtCinq));
    }

    [Fact]
    public void Aplatir_UtiliseLOrdreDesListesCommePosition()
    {
        var affectations = Affectation.Aplatir(Listes([1, 2], [], [3]), [4, 5]);

        Assert.Equal(
            [new(1, 0, 0), new(2, 0, 1), new(3, 2, 0), new(4, null, 0), new(5, null, 1)],
            affectations);
    }

    [Fact]
    public void AplatirPuisRepartir_RedonneLaRepartitionDOrigine()
    {
        var equipes = Listes([1, 2], [], [3]);
        List<int> reserve = [4, 5];

        var (equipesRelues, reserveRelue) = Affectation.Repartir(Affectation.Aplatir(equipes, reserve), TousConnus, 0);

        Assert.Equal(equipes, equipesRelues);
        Assert.Equal(reserve, reserveRelue);
    }
}
