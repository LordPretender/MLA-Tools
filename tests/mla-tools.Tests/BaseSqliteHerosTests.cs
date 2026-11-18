using Microsoft.Data.Sqlite;
using Xunit;
using static MlaTools.Tests.HerosDeTest;

namespace MlaTools.Tests;

public class BaseSqliteHerosTests : IDisposable
{
    private const string Alpha = "Alpha";
    private const string Beta = "Beta";
    private const string Gamma = "Gamma";
    private const string FactionOrdre = "Ordre";
    private const string FactionChaos = "Chaos";
    private const string DateAncienne = "2000-01-01 00:00:00";

    private readonly BaseTemporaire _base = new();

    public void Dispose() => _base.Dispose();

    private void EcrireDateMaj(string valeur) =>
        _base.ExecuterSql("UPDATE heros SET date_maj = $date", ("$date", valeur));

    [Fact]
    public void Enregistrer_HerosNouveauModifieEtIdentique_SontClasses()
    {
        BaseSqlite.Enregistrer([UnHero(Alpha), UnHero(Beta)], _base.Chemin);

        var resultat = BaseSqlite.Enregistrer(
            [UnHero(Alpha), new Hero(Beta, 4, [], ClasseParDefaut, EquipementParDefaut), UnHero(Gamma)], _base.Chemin);

        Assert.Equal([Gamma], resultat.Ajoutes.Select(h => h.Nom));
        Assert.Equal([Beta], resultat.MisAJour.Select(h => h.Nom));
        Assert.Equal([Alpha], resultat.Inchanges.Select(h => h.Nom));
    }

    [Fact]
    public void Enregistrer_DoublonDansLeLot_LeDernierL_Emporte()
    {
        var resultat = BaseSqlite.Enregistrer(
            [UnHero("Héros"), new Hero("HEROS", 3, [], ClasseParDefaut, EquipementParDefaut)], _base.Chemin);

        var stockes = BaseSqlite.ListerHeros(_base.Chemin);
        Assert.Single(resultat.Ajoutes);
        Assert.Equal("HEROS", Assert.Single(stockes).Hero.Nom);
        Assert.Equal(3, stockes[0].Hero.Etoiles);
    }

    [Fact]
    public void Enregistrer_HerosIdentique_GardeLaDateMaj()
    {
        BaseSqlite.Enregistrer([UnHero(Alpha)], _base.Chemin);
        EcrireDateMaj(DateAncienne);

        BaseSqlite.Enregistrer([UnHero(Alpha)], _base.Chemin);

        Assert.Equal(new DateTime(2000, 1, 1), BaseSqlite.ListerHeros(_base.Chemin)[0].DateMaj);
    }

    [Fact]
    public void Enregistrer_HerosModifie_MetLaDateMajAJour()
    {
        BaseSqlite.Enregistrer([UnHero(Alpha)], _base.Chemin);
        EcrireDateMaj(DateAncienne);

        BaseSqlite.Enregistrer([new Hero(Alpha, 3, [], ClasseParDefaut, EquipementParDefaut)], _base.Chemin);

        Assert.True(BaseSqlite.ListerHeros(_base.Chemin)[0].DateMaj > new DateTime(2000, 1, 1));
    }

    [Fact]
    public void Enregistrer_OrdreDesFactionsInverse_CompteCommeUneModification()
    {
        BaseSqlite.Enregistrer([UnHero(Alpha, FactionOrdre, FactionChaos)], _base.Chemin);

        var resultat = BaseSqlite.Enregistrer([UnHero(Alpha, FactionChaos, FactionOrdre)], _base.Chemin);

        Assert.Single(resultat.MisAJour);
        Assert.Equal([FactionChaos, FactionOrdre], BaseSqlite.ListerHeros(_base.Chemin)[0].Hero.Factions);
    }

    [Fact]
    public void Enregistrer_ErreurAuMilieuDuLot_N_EcritAucunHeros()
    {
        // Classe nulle : la colonne est NOT NULL, l'insertion du second héros échoue après celle du premier.
        var invalide = new Hero(Beta, EtoilesParDefaut, [], null!, EquipementParDefaut);

        Assert.Throws<SqliteException>(() => BaseSqlite.Enregistrer([UnHero(Alpha), invalide], _base.Chemin));

        Assert.Empty(BaseSqlite.ListerHeros(_base.Chemin));
    }

    [Fact]
    public void AjouterHero_PuisListerHeros_RestitueToutLeHeros()
    {
        var hero = UnHero(Alpha, FactionOrdre, FactionChaos);

        var id = BaseSqlite.AjouterHero(hero, _base.Chemin);

        var stocke = Assert.Single(BaseSqlite.ListerHeros(_base.Chemin));
        Assert.Equal(id, stocke.Id);
        Assert.Equal(Alpha, stocke.Hero.Nom);
        Assert.Equal(EtoilesParDefaut, stocke.Hero.Etoiles);
        Assert.Equal([FactionOrdre, FactionChaos], stocke.Hero.Factions);
        Assert.Equal(ClasseParDefaut, stocke.Hero.Classe);
        Assert.Equal(EquipementParDefaut, stocke.Hero.Equipement);
        Assert.NotNull(stocke.DateMaj);
    }

    [Fact]
    public void AjouterHero_FactionEnDouble_EstEcarteeAuStockage()
    {
        BaseSqlite.AjouterHero(UnHero(Alpha, FactionOrdre, FactionChaos, FactionOrdre), _base.Chemin);

        Assert.Equal([FactionOrdre, FactionChaos], BaseSqlite.ListerHeros(_base.Chemin)[0].Hero.Factions);
    }

    [Fact]
    public void ModifierHero_RemplaceLesChampsEtLesFactions()
    {
        var id = BaseSqlite.AjouterHero(UnHero(Alpha, FactionOrdre), _base.Chemin);

        BaseSqlite.ModifierHero(id, new Hero(Beta, 3, [FactionChaos, FactionOrdre], "Tank", "Lourd"), _base.Chemin);

        var stocke = Assert.Single(BaseSqlite.ListerHeros(_base.Chemin));
        Assert.Equal(Beta, stocke.Hero.Nom);
        Assert.Equal(3, stocke.Hero.Etoiles);
        Assert.Equal([FactionChaos, FactionOrdre], stocke.Hero.Factions);
        Assert.Equal("Tank", stocke.Hero.Classe);
        Assert.Equal("Lourd", stocke.Hero.Equipement);
    }

    [Fact]
    public void SupprimerHero_RetireAussiSesAffectations()
    {
        var id = BaseSqlite.AjouterHero(UnHero(Alpha, FactionOrdre), _base.Chemin);
        BaseSqlite.EcrireAffectations([new Affectation(id, 0, 0)], _base.Chemin);
        Assert.Single(BaseSqlite.LireAffectations(_base.Chemin));

        BaseSqlite.SupprimerHero(id, _base.Chemin);

        Assert.Empty(BaseSqlite.ListerHeros(_base.Chemin));
        Assert.Empty(BaseSqlite.LireAffectations(_base.Chemin));
    }

    [Fact]
    public void EcrireLienAme_Persiste_EtUnEnregistrerUlterieurNeL_EcrasePas()
    {
        var id = BaseSqlite.AjouterHero(UnHero(Alpha), _base.Chemin);

        BaseSqlite.EcrireLienAme(id, true, _base.Chemin);
        Assert.True(BaseSqlite.ListerHeros(_base.Chemin)[0].LienAme);

        BaseSqlite.Enregistrer([new Hero(Alpha, 3, [], ClasseParDefaut, EquipementParDefaut)], _base.Chemin);

        Assert.True(BaseSqlite.ListerHeros(_base.Chemin)[0].LienAme);
    }

    [Fact]
    public void ListerHeros_DateMajIllisible_DonneNull()
    {
        BaseSqlite.AjouterHero(UnHero(Alpha), _base.Chemin);
        EcrireDateMaj("pas une date");

        Assert.Null(BaseSqlite.ListerHeros(_base.Chemin)[0].DateMaj);
    }

    public static TheoryData<Exception, bool> ExceptionsEtReconnaissance => new()
    {
        { new InvalidOperationException(), true },
        { new IOException(), true },
        { new SqliteException("erreur de base", 1), true },
        { new ArgumentException(), false },
    };

    [Theory]
    [MemberData(nameof(ExceptionsEtReconnaissance))]
    public void EstErreurAcces_ReconnaitLesErreursDeBaseEtDeFichier(Exception exception, bool attendu)
    {
        Assert.Equal(attendu, BaseSqlite.EstErreurAcces(exception));
    }
}
