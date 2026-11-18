using System.Text.Json;
using Xunit;

namespace MlaTools.Tests;

public class ConfigZonesTests : IDisposable
{
    private const string Titre = "Titre de fenêtre";
    private const string TitreImporte = "Titre importé";
    private const string TitreAutreImport = "Autre titre";
    private const int LargeurFenetre = 100;
    private const int HauteurFenetre = 80;
    private const string ZoneNom = "Nom";
    private const string ZoneEtoiles = "Etoiles";
    private const string NomFichierImport = "zones.json";
    private const string NomFichierInexistant = "inexistant.json";
    private const string CleLargeur = "largeur";
    private const string SqlPoserParametre = "UPDATE parametres SET valeur = $valeur WHERE cle = $cle";

    private const int AbscisseValide = 10;
    private const int AbscisseHorsFenetre = 99;
    private const string SujetImage = "L'image";
    private const string TailleCalibree = "100x80";

    private static readonly Zone ZoneValide = new(AbscisseValide, 10, 20, 5);

    // Les accolades fermantes sont espacées : une chaîne brute interpolée n'en tolère pas trois d'affilée.
    private static string JsonImport(string titre = TitreImporte, int abscisse = AbscisseValide) =>
        $$$"""{"TitreFenetre":"{{{titre}}}","Largeur":{{{LargeurFenetre}}},"Hauteur":{{{HauteurFenetre}}},"Dispositions":{"simple":{"Nom":{"X":{{{abscisse}}},"Y":10,"Largeur":20,"Hauteur":5} } },"Globales":{"FlecheSuivante":{"X":1,"Y":2,"Largeur":3,"Hauteur":4} } }""";

    private static void AssertCalibrageVide(ConfigZones config)
    {
        Assert.Equal("", config.TitreFenetre);
        Assert.Equal(0, config.Largeur);
        Assert.Equal(0, config.Hauteur);
        Assert.Empty(config.Dispositions);
        Assert.Empty(config.Globales);
    }

    private readonly BaseTemporaire _base = new();
    private readonly string _dossierImport = Path.Combine(Path.GetTempPath(), "mla-tools-tests-import-" + Guid.NewGuid().ToString("N"));

    public ConfigZonesTests() => Directory.CreateDirectory(_dossierImport);

    public void Dispose()
    {
        _base.Dispose();
        Directory.Delete(_dossierImport, recursive: true);
    }

    private string CheminImportInexistant => Path.Combine(_dossierImport, NomFichierInexistant);

    private string EcrireImport(string contenu)
    {
        var chemin = Path.Combine(_dossierImport, NomFichierImport);
        File.WriteAllText(chemin, contenu);
        return chemin;
    }

    private static ConfigZones UneConfig() => new()
    {
        TitreFenetre = Titre,
        Largeur = LargeurFenetre,
        Hauteur = HauteurFenetre,
    };

    private static ConfigZones ConfigAvecZone(Zone zone)
    {
        var config = UneConfig();
        config.Dispositions[Dispositions.Simple] = new() { [ZoneNom] = zone };
        return config;
    }

    private ConfigZones EnregistrerPuisCharger(ConfigZones config)
    {
        config.Enregistrer(_base.Chemin);
        return ConfigZones.Charger(_base.Chemin, CheminImportInexistant);
    }

    [Fact]
    public void Enregistrer_PuisCharger_RestitueTitreEtTaille()
    {
        var chargee = EnregistrerPuisCharger(UneConfig());

        Assert.Equal(Titre, chargee.TitreFenetre);
        Assert.Equal(LargeurFenetre, chargee.Largeur);
        Assert.Equal(HauteurFenetre, chargee.Hauteur);
    }

    [Fact]
    public void Enregistrer_PuisCharger_RestitueLesZonesDesDispositionsEtLesGlobales()
    {
        var config = UneConfig();
        config.Dispositions[Dispositions.Simple] = new() { [ZoneNom] = ZoneValide, [ZoneEtoiles] = new Zone(40, 10, 10, 10) };
        config.Dispositions[Dispositions.Hybride] = new() { [ZoneNom] = new Zone(50, 50, 5, 5) };
        config.Globales[Dispositions.ZoneFlecheSuivante] = new Zone(0, 0, 100, 80);

        var chargee = EnregistrerPuisCharger(config);

        Assert.Equal(config.Dispositions[Dispositions.Simple], chargee.Dispositions[Dispositions.Simple]);
        Assert.Equal(config.Dispositions[Dispositions.Hybride], chargee.Dispositions[Dispositions.Hybride]);
        Assert.Equal(config.Globales, chargee.Globales);
    }

    [Fact]
    public void Charger_SansImportNiMarqueur_RetourneUnCalibrageVide()
    {
        AssertCalibrageVide(ConfigZones.Charger(_base.Chemin, CheminImportInexistant));
    }

    [Fact]
    public void Charger_ZoneCollantAuxBordsDeLaFenetre_EstValide()
    {
        var chargee = EnregistrerPuisCharger(ConfigAvecZone(new Zone(80, 75, 20, 5)));

        Assert.Equal(new Zone(80, 75, 20, 5), chargee.Dispositions[Dispositions.Simple][ZoneNom]);
    }

    [Theory]
    [InlineData(90, 10, 20, 5)]
    [InlineData(10, 78, 5, 5)]
    [InlineData(10, 10, 0, 5)]
    [InlineData(10, 10, -5, 5)]
    [InlineData(10, 10, 5, 0)]
    [InlineData(10, 10, 5, -5)]
    [InlineData(-1, 10, 5, 5)]
    [InlineData(10, -1, 5, 5)]
    public void Charger_ZoneDeDispositionInvalide_LeveInvalidOperationException(int x, int y, int largeur, int hauteur)
    {
        var config = ConfigAvecZone(new Zone(x, y, largeur, hauteur));

        Assert.Throws<InvalidOperationException>(() => EnregistrerPuisCharger(config));
    }

    [Fact]
    public void Charger_ZoneGlobaleHorsDeLaFenetre_LeveInvalidOperationException()
    {
        var config = UneConfig();
        config.Globales[Dispositions.ZoneFlecheSuivante] = new Zone(95, 10, 10, 5);

        Assert.Throws<InvalidOperationException>(() => EnregistrerPuisCharger(config));
    }

    [Fact]
    public void Charger_LargeurDeFenetreNegative_LeveInvalidOperationException()
    {
        var config = UneConfig();
        config.Largeur = -1;

        Assert.Throws<InvalidOperationException>(() => EnregistrerPuisCharger(config));
    }

    [Fact]
    public void Charger_HauteurDeFenetreNegative_LeveInvalidOperationException()
    {
        var config = UneConfig();
        config.Hauteur = -1;

        Assert.Throws<InvalidOperationException>(() => EnregistrerPuisCharger(config));
    }

    [Fact]
    public void Charger_LargeurNonNumeriqueEnBase_LeveInvalidOperationException()
    {
        UneConfig().Enregistrer(_base.Chemin);
        _base.ExecuterSql(SqlPoserParametre, ("$cle", CleLargeur), ("$valeur", "abc"));

        Assert.Throws<InvalidOperationException>(() => ConfigZones.Charger(_base.Chemin, CheminImportInexistant));
    }

    [Fact]
    public void Charger_MarqueurAbsentEtFichierValide_ImporteLeFichier()
    {
        var chargee = ConfigZones.Charger(_base.Chemin, EcrireImport(JsonImport()));

        Assert.Equal(TitreImporte, chargee.TitreFenetre);
        Assert.Equal(LargeurFenetre, chargee.Largeur);
        Assert.Equal(HauteurFenetre, chargee.Hauteur);
        Assert.Equal(ZoneValide, chargee.Dispositions[Dispositions.Simple][ZoneNom]);
        Assert.Equal(new Zone(1, 2, 3, 4), chargee.Globales[Dispositions.ZoneFlecheSuivante]);
    }

    [Fact]
    public void Charger_ApresUnPremierImport_NeReimportePasLeFichier()
    {
        var import = EcrireImport(JsonImport());
        ConfigZones.Charger(_base.Chemin, import);
        File.WriteAllText(import, JsonImport(titre: TitreAutreImport));

        var chargee = ConfigZones.Charger(_base.Chemin, import);

        Assert.Equal(TitreImporte, chargee.TitreFenetre);
    }

    [Fact]
    public void Effacer_ApresImport_VideLeCalibrageSansLeReimporter()
    {
        var import = EcrireImport(JsonImport());
        ConfigZones.Charger(_base.Chemin, import);

        ConfigZones.Effacer(_base.Chemin);
        AssertCalibrageVide(ConfigZones.Charger(_base.Chemin, import));
    }

    [Fact]
    public void Charger_FichierDImportAvecZoneHorsFenetre_LeveInvalidOperationExceptionAvecLeChemin()
    {
        var import = EcrireImport(JsonImport(abscisse: AbscisseHorsFenetre));

        var exception = Assert.Throws<InvalidOperationException>(() => ConfigZones.Charger(_base.Chemin, import));

        Assert.Contains(import, exception.Message);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{"TitreFenetre":null,"Largeur":1,"Hauteur":1,"Dispositions":{},"Globales":{}}""")]
    [InlineData("""{"TitreFenetre":"t","Largeur":1,"Hauteur":1,"Dispositions":null,"Globales":{}}""")]
    [InlineData("""{"TitreFenetre":"t","Largeur":1,"Hauteur":1,"Dispositions":{},"Globales":null}""")]
    public void Charger_FichierDImportIllisible_LeveInvalidOperationExceptionAvecLeChemin(string contenu)
    {
        var import = EcrireImport(contenu);

        var exception = Assert.Throws<InvalidOperationException>(() => ConfigZones.Charger(_base.Chemin, import));

        Assert.Contains(import, exception.Message);
    }

    [Fact]
    public void Charger_FichierDImportVide_ChargeUnCalibrageVide()
    {
        AssertCalibrageVide(ConfigZones.Charger(_base.Chemin, EcrireImport("{}")));
    }

    [Fact]
    public void Charger_FichierDImportJsonInvalide_LeveJsonException()
    {
        var import = EcrireImport("{ pas du json");

        Assert.Throws<JsonException>(() => ConfigZones.Charger(_base.Chemin, import));
    }

    [Fact]
    public void VerifierTaille_TailleIdentique_NeLevePasDException()
    {
        var exception = Record.Exception(() => UneConfig().VerifierTaille(new Size(LargeurFenetre, HauteurFenetre), SujetImage));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(101, 80, "101x80")]
    [InlineData(100, 81, "100x81")]
    public void VerifierTaille_TailleDifferente_LeveUneExceptionCitantLesDeuxTailles(int largeur, int hauteur, string tailleAttendue)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => UneConfig().VerifierTaille(new Size(largeur, hauteur), SujetImage));

        Assert.Contains(tailleAttendue, exception.Message);
        Assert.Contains(TailleCalibree, exception.Message);
    }
}
