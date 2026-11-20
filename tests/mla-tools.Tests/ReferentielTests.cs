using Xunit;

namespace MlaTools.Tests;

public class ReferentielTests
{
    private const string ArtsMartiaux = "Arts Martiaux";
    private const string Obscur = "Obscur";
    private const string Astral = "Astral";
    private const string ArtsMartiauxNormalise = "arts martiaux";

    // Liste dédiée : l'issue d'un rapprochement ne doit pas bouger quand le référentiel réel s'enrichit.
    private static readonly string[] Candidats = [ArtsMartiaux, Obscur, "Chaos"];

    [Theory]
    [InlineData("Élémental", "elemental")]
    [InlineData("Garde d'acier", "garde dacier")]
    [InlineData("ARTS Martiaux", ArtsMartiauxNormalise)]
    [InlineData("arts\nmartiaux", ArtsMartiauxNormalise)]
    [InlineData("  obscur  ", "obscur")]
    [InlineData("", "")]
    [InlineData("(.,;'!)", "")]
    public void Normaliser_RetireAccentsCasseEtPonctuation(string texte, string attendu) =>
        Assert.Equal(attendu, Referentiel.Normaliser(texte));

    [Theory]
    [InlineData("arts   martiaux", ArtsMartiauxNormalise)]
    [InlineData("arts\r\nmartiaux", ArtsMartiauxNormalise)]
    [InlineData("  arts \n\n martiaux  ", ArtsMartiauxNormalise)]
    [InlineData("", "")]
    public void Compacter_RameneLesBlancsAUnSeulEspace(string texte, string attendu) =>
        Assert.Equal(attendu, Referentiel.Compacter(texte));

    [Theory]
    [InlineData("Arts Martial IY", ArtsMartiaux)]
    [InlineData("(9 Obscur", Obscur)]
    public void Rapprocher_LectureBruitee_RenvoieLaValeurCanonique(string lu, string attendu) =>
        Assert.Equal(attendu, Referentiel.Rapprocher(lu, Candidats));

    [Fact]
    public void Rapprocher_ValeurExacte_RenvoieLaGraphieCanonique() =>
        Assert.Equal("Élémental", Referentiel.Rapprocher("elemental", ["Élémental", Obscur]));

    [Fact]
    public void Rapprocher_EgaliteEntreDeuxCandidats_RenvoieNull() =>
        Assert.Null(Referentiel.Rapprocher("abcx", ["abcd", "abce"]));

    [Theory]
    [InlineData("obscxr", Obscur)]
    [InlineData("obsxxr", null)]
    public void Rapprocher_SeuilDeDistance(string lu, string? attendu) =>
        Assert.Equal(attendu, Referentiel.Rapprocher(lu, [Obscur]));

    [Theory]
    [InlineData("")]
    [InlineData("(.,;)")]
    public void Rapprocher_EntreeSansContenu_RenvoieNull(string lu) =>
        Assert.Null(Referentiel.Rapprocher(lu, Candidats));

    [Fact]
    public void Familles_QualificatifDevantUnNom_RegroupeLesDeuxFiches()
    {
        var familles = Referentiel.Familles([(1, "Singularity Lunox"), (2, "Lunox"), (3, "Layla")]);

        Assert.Equal(familles[2], familles[1]);
        Assert.NotEqual(familles[2], familles[3]);
    }

    [Fact]
    public void Familles_NomSeulementContenuDansUnAutre_NeRegroupePas()
    {
        var familles = Referentiel.Familles([(1, "Lunoxa"), (2, "Lunox")]);

        Assert.NotEqual(familles[2], familles[1]);
    }

    [Fact]
    public void Familles_NomVide_RestePropreFamille()
    {
        var familles = Referentiel.Familles([(1, ""), (2, "Lunox")]);

        Assert.Equal(1, familles[1]);
        Assert.Equal(2, familles[2]);
    }

    [Fact]
    public void Factions_ContientLesFactionsRegulieresEtLesFactionsSpeciales()
    {
        Assert.All(Referentiel.FactionsRegulieres, faction => Assert.Contains(faction, Referentiel.Factions));
        Assert.Contains(Referentiel.FactionOrdre, Referentiel.Factions);
        Assert.Contains(Referentiel.FactionChaos, Referentiel.Factions);
        Assert.Contains(Astral, Referentiel.Factions);
        Assert.Contains(ArtsMartiaux, Referentiel.FactionsRegulieres);
        Assert.Contains(Obscur, Referentiel.FactionsRegulieres);
    }

    [Fact]
    public void Classes_SansDoublonApresNormalisation() => AssertSansDoublon(Referentiel.Classes);

    [Fact]
    public void Equipements_SansDoublonApresNormalisation() => AssertSansDoublon(Referentiel.Equipements);

    private static void AssertSansDoublon(IReadOnlyList<string> valeurs) =>
        Assert.Equal(valeurs.Count, valeurs.Select(Referentiel.Normaliser).Distinct().Count());
}
