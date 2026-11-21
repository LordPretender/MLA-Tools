using Xunit;

namespace MlaTools.Tests;

public class InfobulleHeroTests
{
    private const string Nom = "Héros d'essai";
    private const int Etoiles = 3;
    private const string Classe = "Tank";
    private const string Role = "Tank";
    private const string Equipement = "Lourd";
    private const string FactionCourante = "Astral";
    private const int Annee = 2026;
    private const int Mois = 11;
    private const int Jour = 21;

    private static HeroStocke Stocke(bool lienAme, DateTime? date = null) =>
        new(1, new Hero(Nom, Etoiles, [FactionCourante], Classe, Equipement), lienAme, date);

    [Fact]
    public void Le_texte_reprend_toutes_les_caracteristiques_du_heros()
    {
        var date = new DateTime(Annee, Mois, Jour);

        var texte = InfobulleHero.Texte(Stocke(lienAme: false, date), Role);

        Assert.Contains(Nom, texte);
        Assert.Contains($"Étoiles : {CartesEquipes.LibelleEtoiles(Etoiles)}", texte);
        Assert.Contains($"Classe : {Classe}", texte);
        Assert.Contains($"Rôle : {Role}", texte);
        Assert.Contains($"Équipement : {Equipement}", texte);
        Assert.Contains($"Factions : {FactionCourante}", texte);
        Assert.Contains("Lien d'âme : non", texte);
        Assert.Contains($"Mis à jour : {OutilsDialogue.LibelleDate(date)}", texte);
    }

    [Fact]
    public void Le_lien_d_ame_signale_la_faction_ordre_qu_il_ajoute()
    {
        var texte = InfobulleHero.Texte(Stocke(lienAme: true), Role);

        Assert.Contains("Lien d'âme : oui", texte);
        Assert.Contains($"{Referentiel.FactionOrdre} (lien d'âme)", texte);
    }

    [Fact]
    public void Une_date_inconnue_s_affiche_comme_dans_la_grille()
    {
        var texte = InfobulleHero.Texte(Stocke(lienAme: false), Role);

        Assert.Contains($"Mis à jour : {OutilsDialogue.LibelleDate(null)}", texte);
    }
}
