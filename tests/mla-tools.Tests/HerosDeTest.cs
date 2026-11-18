namespace MlaTools.Tests;

/// <summary>Valeurs neutres pour les tests où seul le nom d'un héros compte.</summary>
internal static class HerosDeTest
{
    public const string ClasseParDefaut = "Mage";
    public const string EquipementParDefaut = "Standard";
    public const int EtoilesParDefaut = 5;

    public static Hero UnHero(string nom, params string[] factions) =>
        new(nom, EtoilesParDefaut, factions, ClasseParDefaut, EquipementParDefaut);
}
