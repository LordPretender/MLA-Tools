using Xunit;

namespace MlaTools.Tests;

public class LecteurEtoilesPaliersTests
{
    private static readonly (int Valeur, string Libelle)[] PaliersAttendus =
    [
        (3, "3 étoiles bleues"),
        (4, "4 étoiles violettes"),
        (5, "5 étoiles jaunes"),
        (6, "1 étoile rouge"),
        (7, "2 étoiles rouges"),
        (8, "3 étoiles rouges"),
        (9, "4 étoiles rouges"),
        (10, "5 étoiles rouges"),
        (11, "Éveillé 0"),
        (12, "Éveillé 1"),
        (13, "Éveillé 2"),
        (14, "Éveillé 3"),
        (15, "Éveillé 4"),
        (16, "Éveillé 5"),
        (17, "Éveillé 6"),
        (18, "Éveillé 7"),
        (19, "Éveillé 8"),
    ];

    private static int[] Valeurs() => LecteurEtoiles.Paliers.Select(palier => palier.Valeur).ToArray();

    [Fact]
    public void Paliers_Liste_RendLesValeursEtLibellesAttendusDansLOrdre() =>
        Assert.Equal(
            PaliersAttendus,
            LecteurEtoiles.Paliers.Select(palier => (palier.Valeur, palier.Libelle)).ToArray());

    [Fact]
    public void Paliers_Valeurs_SontStrictementCroissantes()
    {
        var valeurs = Valeurs();

        Assert.Equal(valeurs.Order().ToArray(), valeurs);
        Assert.Equal(valeurs.Distinct().Count(), valeurs.Length);
    }
}
