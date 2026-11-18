using Xunit;
using static MlaTools.Tests.HerosDeTest;

namespace MlaTools.Tests;

public class BaseSqliteMigrationTests : IDisposable
{
    private const int VersionCourante = 5;
    private const string ClasseTank = "Tank";
    private const string RoleTank = "Tank";
    private const string ClasseSupport = "Support";
    private const string RoleSoutien = "Soutien";
    private const string Alpha = "Alpha";
    private const string Beta = "Beta";

    private const string CompositionTroisEquipes = "Trois equipes";
    private const string CompositionVide = "Vide";

    // Schéma de la table des héros avant l'ajout de lien_ame et date_maj.
    private const string SchemaHerosAncien = """
        CREATE TABLE heros (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            nom TEXT NOT NULL,
            nom_norme TEXT NOT NULL UNIQUE,
            etoiles INTEGER NOT NULL,
            classe TEXT NOT NULL,
            equipement TEXT NOT NULL);
        """;

    private readonly BaseTemporaire _base = new();

    public void Dispose() => _base.Dispose();

    private static string InsertionHeroAncien(int id, string nom) =>
        $"INSERT INTO heros(id, nom, nom_norme, etoiles, classe, equipement) VALUES({id}, '{nom}', '{nom}', {EtoilesParDefaut}, '{ClasseParDefaut}', '{EquipementParDefaut}');";

    [Fact]
    public void BaseVierge_PasseALaVersionCouranteAvecLesRolesParDefaut()
    {
        var roles = BaseSqlite.LireRoles(_base.Chemin);

        Assert.Equal(VersionCourante, _base.LireVersion());
        Assert.Equal(RoleTank, roles[ClasseTank]);
        Assert.Equal(RoleSoutien, roles[ClasseSupport]);
    }

    [Fact]
    public void BaseVersion0_SansLienAmeNiDateMaj_AjouteLesColonnesEtGardeLesHeros()
    {
        _base.ExecuterSql(SchemaHerosAncien + InsertionHeroAncien(1, Alpha));

        var stocke = Assert.Single(BaseSqlite.ListerHeros(_base.Chemin));

        Assert.Equal(Alpha, stocke.Hero.Nom);
        Assert.False(stocke.LienAme);
        Assert.Null(stocke.DateMaj);
        Assert.Equal(VersionCourante, _base.LireVersion());
    }

    [Fact]
    public void BaseSansNombreEquipes_RetroCalculeLeNombreDepuisLesAffectations()
    {
        _base.ExecuterSql($"""
            {SchemaHerosAncien}
            {InsertionHeroAncien(10, Alpha)}
            {InsertionHeroAncien(11, Beta)}
            CREATE TABLE compositions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nom TEXT NOT NULL,
                nom_norme TEXT NOT NULL UNIQUE);
            CREATE TABLE compositions_heros (
                composition_id INTEGER NOT NULL REFERENCES compositions(id) ON DELETE CASCADE,
                hero_id INTEGER NOT NULL REFERENCES heros(id) ON DELETE CASCADE,
                equipe INTEGER NULL,
                position INTEGER NOT NULL,
                PRIMARY KEY (composition_id, hero_id));
            INSERT INTO compositions(id, nom, nom_norme) VALUES(1, '{CompositionTroisEquipes}', '{CompositionTroisEquipes}'), (2, '{CompositionVide}', '{CompositionVide}');
            INSERT INTO compositions_heros(composition_id, hero_id, equipe, position) VALUES(1, 10, 0, 0), (1, 11, 2, 0);
            PRAGMA user_version = 4;
            """);

        var nombres = BaseSqlite.ListerCompositions(_base.Chemin).ToDictionary(c => c.Nom, c => c.NombreEquipes);

        Assert.Equal(3, nombres[CompositionTroisEquipes]);
        Assert.Equal(0, nombres[CompositionVide]);
    }

    [Fact]
    public void RoleSupprimeAvantUneMonteeDeVersion_NeReapparaitPas()
    {
        BaseSqlite.LireRoles(_base.Chemin);
        _base.ExecuterSql($"DELETE FROM roles WHERE classe = '{ClasseTank}'; PRAGMA user_version = 4;");

        var roles = BaseSqlite.LireRoles(_base.Chemin);

        Assert.DoesNotContain(ClasseTank, roles.Keys);
        Assert.Equal(VersionCourante, _base.LireVersion());
    }

    [Fact]
    public void OuvrirDeuxFoisLaMemeBase_NeChangeNiLesDonneesNiLaVersion()
    {
        BaseSqlite.Enregistrer([UnHero(Alpha)], _base.Chemin);
        var apresPremiere = BaseSqlite.ListerHeros(_base.Chemin).Select(h => (h.Id, h.Hero.Nom, h.DateMaj)).ToList();
        var rolesApresPremiere = BaseSqlite.LireRoles(_base.Chemin);

        var apresSeconde = BaseSqlite.ListerHeros(_base.Chemin).Select(h => (h.Id, h.Hero.Nom, h.DateMaj)).ToList();

        Assert.Equal(apresPremiere, apresSeconde);
        Assert.Equal(rolesApresPremiere, BaseSqlite.LireRoles(_base.Chemin));
        Assert.Equal(VersionCourante, _base.LireVersion());
    }

    [Fact]
    public void ClesEtrangeres_ActivesALaReouverture_LaSuppressionDuHerosRetireSesLignesDeComposition()
    {
        var idHero = BaseSqlite.AjouterHero(UnHero(Alpha), _base.Chemin);
        var idComposition = BaseSqlite.CreerComposition(CompositionVide, [new Affectation(idHero, 0, 0)], 1, _base.Chemin);
        Assert.Single(BaseSqlite.LireComposition(idComposition, _base.Chemin));

        BaseSqlite.SupprimerHero(idHero, _base.Chemin);

        Assert.Empty(BaseSqlite.LireComposition(idComposition, _base.Chemin));
    }
}
