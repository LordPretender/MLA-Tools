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

    private static readonly Rectangle Ecran = new(0, 0, 1000, 800);
    private static readonly Size Fiche = new(240, 300);

    [Fact]
    public void La_fiche_s_affiche_sous_le_curseur_quand_la_place_suffit()
    {
        var position = InfobulleHero.PositionDansEcran(new Point(100, 100), Fiche, Ecran);

        Assert.Equal(new Point(100, 120), position);
    }

    [Fact]
    public void La_fiche_passe_au_dessus_du_curseur_quand_elle_depasserait_en_bas()
    {
        var position = InfobulleHero.PositionDansEcran(new Point(100, 700), Fiche, Ecran);

        Assert.Equal(new Point(100, 398), position);
    }

    [Fact]
    public void La_fiche_reste_dans_l_ecran_a_droite()
    {
        var position = InfobulleHero.PositionDansEcran(new Point(950, 100), Fiche, Ecran);

        Assert.Equal(760, position.X);
    }

    [Fact]
    public void La_fiche_plus_haute_que_l_ecran_est_calee_en_haut()
    {
        var position = InfobulleHero.PositionDansEcran(new Point(100, 100), new Size(240, 900), Ecran);

        Assert.Equal(0, position.Y);
    }

    [Fact]
    public void Un_heros_deja_ordre_n_est_pas_marque_par_le_lien_d_ame()
    {
        var factions = InfobulleHero.Factions(Stocke(lienAme: true, Referentiel.FactionOrdre));

        Assert.Equal([new InfobulleHero.FactionAffichee(Referentiel.FactionOrdre, false)], factions);
    }
}
