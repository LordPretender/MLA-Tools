using Xunit;

namespace MlaTools.Tests;

public class ComposantesTests
{
    private const char PixelVrai = '#';

    public static TheoryData<bool> ModesVoisinage { get; } = new() { false, true };

    private sealed record Grille(bool[] Masque, int Largeur, int Hauteur);

    // Grille de texte plutôt que tableau de booléens : le cas testé se lit directement.
    private static Grille Masque(params string[] lignes) =>
        new(lignes.SelectMany(ligne => ligne.Select(c => c == PixelVrai)).ToArray(), lignes[0].Length, lignes.Length);

    // Index triés dans chaque composante, composantes triées par premier index : l'ordre de parcours interne n'est pas un contrat.
    private static int[][] ListerTriees(Grille grille, bool huitVoisins) =>
        Composantes.Lister(grille.Masque, grille.Largeur, grille.Hauteur, huitVoisins)
            .Select(composante => composante.Order().ToArray())
            .OrderBy(composante => composante[0])
            .ToArray();

    [Theory]
    [MemberData(nameof(ModesVoisinage))]
    public void Lister_MasqueVide_NeRendAucuneComposante(bool huitVoisins) =>
        Assert.Empty(ListerTriees(Masque("...", "..."), huitVoisins));

    [Theory]
    [MemberData(nameof(ModesVoisinage))]
    public void Lister_MasquePlein_RendUneSeuleComposanteAvecTousLesPixels(bool huitVoisins) =>
        Assert.Equal([[0, 1, 2, 3, 4, 5]], ListerTriees(Masque("###", "###"), huitVoisins));

    [Theory]
    [MemberData(nameof(ModesVoisinage))]
    public void Lister_DeuxTachesSeparees_RendDeuxComposantesAvecLesBonsIndex(bool huitVoisins) =>
        Assert.Equal([[0, 1, 5, 6], [4, 9]], ListerTriees(Masque("##..#", "##..#"), huitVoisins));

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public void Lister_PixelsEnDiagonale_NeSeRejoignentQuEnHuitVoisins(bool huitVoisins, int composantesAttendues) =>
        Assert.Equal(composantesAttendues, ListerTriees(Masque("#.", ".#"), huitVoisins).Length);

    // Index 2 et 3 sont consécutifs mais sur deux lignes : la fin d'une ligne ne touche pas le début de la suivante.
    [Theory]
    [MemberData(nameof(ModesVoisinage))]
    public void Lister_FinDeLigneEtDebutDeLaSuivante_NeFusionnentPas(bool huitVoisins) =>
        Assert.Equal([[2], [3]], ListerTriees(Masque("..#", "#.."), huitVoisins));

    [Theory]
    [MemberData(nameof(ModesVoisinage))]
    public void Lister_PixelsIsolesDansLesCoins_RendUneComposanteParPixel(bool huitVoisins) =>
        Assert.Equal([[0], [8]], ListerTriees(Masque("#..", "...", "..#"), huitVoisins));

    [Theory]
    [MemberData(nameof(ModesVoisinage))]
    public void Lister_ChaquePixelVrai_ApparaitDansExactementUneComposante(bool huitVoisins)
    {
        var grille = Masque("##.#", "#..#", ".##.");
        var indexVrais = Enumerable.Range(0, grille.Masque.Length).Where(i => grille.Masque[i]);

        var indexListes = ListerTriees(grille, huitVoisins).SelectMany(composante => composante);

        Assert.Equal(indexVrais, indexListes.Order());
    }
}
