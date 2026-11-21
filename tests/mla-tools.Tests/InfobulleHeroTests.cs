using Xunit;

namespace MlaTools.Tests;

public class InfobulleHeroTests
{
    private const string Nom = "Héros d'essai";
    private const int Etoiles = 3;
    private const string Classe = "Tank";
    private const string Equipement = "Lourd";
    private const string FactionCourante = "Astral";

    private static HeroStocke Stocke(bool lienAme, params string[] factions) =>
        new(1, new Hero(Nom, Etoiles, factions.Length > 0 ? factions : [FactionCourante], Classe, Equipement), lienAme, null);

    [Fact]
    public void Sans_lien_d_ame_seules_les_factions_du_heros_sont_listees()
    {
        var factions = InfobulleHero.Factions(Stocke(lienAme: false));

        Assert.Equal([new InfobulleHero.FactionAffichee(FactionCourante, false)], factions);
    }

    [Fact]
    public void Le_lien_d_ame_marque_la_faction_ordre_qu_il_ajoute()
    {
        var factions = InfobulleHero.Factions(Stocke(lienAme: true));

        Assert.Equal(
            [new InfobulleHero.FactionAffichee(FactionCourante, false), new InfobulleHero.FactionAffichee(Referentiel.FactionOrdre, true)],
            factions);
    }

    [Fact]
    public void Un_heros_deja_ordre_n_est_pas_marque_par_le_lien_d_ame()
    {
        var factions = InfobulleHero.Factions(Stocke(lienAme: true, Referentiel.FactionOrdre));

        Assert.Equal([new InfobulleHero.FactionAffichee(Referentiel.FactionOrdre, false)], factions);
    }
}
