using Xunit;

namespace MlaTools.Tests;

public class LecteurCompetencesTests
{
    private const int NumeroCompetence = 2;

    private const int LargeurImage = 800;
    private const int HauteurImage = 450;
    private const int Fond = 40;
    private const int Trait = 120;
    private const int EpaisseurTrait = 2;
    private const int EcartSeparateurZone = 4;
    private const int EcartBordSeparateur = 71;
    private static readonly Zone ZonePopup = new(50, 200, 700, 60);
    private static readonly int SeparateurAttendu = ZonePopup.Y - EcartSeparateurZone;

    private static Bitmap ImageAvecTraits(params int[] lignes)
    {
        var image = new Bitmap(LargeurImage, HauteurImage);
        using var g = Graphics.FromImage(image);
        g.Clear(Color.FromArgb(Fond, Fond, Fond));
        using var pinceau = new SolidBrush(Color.FromArgb(Trait, Trait, Trait));
        foreach (var y in lignes)
        {
            g.FillRectangle(pinceau, ZonePopup.X, y, ZonePopup.Largeur, EpaisseurTrait);
        }
        return image;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    [InlineData(-25)]
    public void DecalageVertical_SeparateurEtBordPresents_RendLEcartAuxPositionsAttendues(int ecart)
    {
        var separateur = SeparateurAttendu + ecart;
        using var image = ImageAvecTraits(separateur - EcartBordSeparateur, separateur);

        Assert.Equal(ecart, LecteurCompetences.DecalageVertical(image, ZonePopup));
    }

    [Fact]
    public void DecalageVertical_AucunTrait_Zero()
    {
        using var image = ImageAvecTraits();

        Assert.Equal(0, LecteurCompetences.DecalageVertical(image, ZonePopup));
    }

    [Fact]
    public void DecalageVertical_UnTraitSansBord_Zero()
    {
        using var image = ImageAvecTraits(SeparateurAttendu + 20);

        Assert.Equal(0, LecteurCompetences.DecalageVertical(image, ZonePopup));
    }

    private const double HauteurMot = 60;

    private static MotLu Mot(string texte, double gauche, double largeur) => new(texte, gauche, largeur, HauteurMot);

    [Fact]
    public void RegrouperMots_MotCoupeParLOcr_UneSeulePastille()
    {
        Assert.Equal(["Contrôler", "AO E"], Ocr.RegrouperMots([Mot("Contrôler", 0, 320), Mot("AO", 490, 100), Mot("E", 608, 40)]));
    }

    [Fact]
    public void RegrouperMots_PastillesEspacees_RestentDistinctesDeGaucheADroite()
    {
        Assert.Equal(["Contrôler", "Dégâts"], Ocr.RegrouperMots([Mot("Dégâts", 430, 280), Mot("Contrôler", 0, 320)]));
    }

    [Fact]
    public void RegrouperMots_AucunMot_ListeVide()
    {
        Assert.Empty(Ocr.RegrouperMots([]));
    }

    [Fact]
    public void RetenirTags_PastilleDePlusieursMots_UnSeulTag()
    {
        Assert.Equal(["Contrôle total", "AoE"], LecteurCompetences.RetenirTags(["  Contrôle \r\n  total ", "AoE"]));
    }

    [Fact]
    public void RetenirTags_TexteSansLettreNiChiffre_EstIgnore()
    {
        Assert.Equal(["Soigner"], LecteurCompetences.RetenirTags(["Soigner", "|", "."]));
    }

    [Fact]
    public void RetenirTags_DoublonALaCasseEtAuxAccentsPres_GardeLePremier()
    {
        Assert.Equal(["Contrôler"], LecteurCompetences.RetenirTags(["Contrôler", "controler", "CONTROLER"]));
    }

    [Theory]
    [InlineData("A0E", "AoE")]
    [InlineData("Niveau 10", "Niveau 10")]
    public void RetenirTags_ZeroEntreDeuxLettres_DevientUnO(string lu, string attendu)
    {
        Assert.Equal([attendu], LecteurCompetences.RetenirTags([lu]));
    }

    [Fact]
    public void RetenirTags_AucunePastille_ListeVide()
    {
        Assert.Empty(LecteurCompetences.RetenirTags([]));
    }

    [Fact]
    public void Construire_TypeEtTagsLus_SansAlerte()
    {
        var competence = LecteurCompetences.Construire(NumeroCompetence, "Ulti", ["Contrôler", "AoE"]);

        Assert.Equal(NumeroCompetence, competence.Numero);
        Assert.Equal("Ulti", competence.Type);
        Assert.Equal(["Contrôler", "AoE"], competence.Tags);
        Assert.Empty(competence.Alertes);
    }

    [Fact]
    public void Construire_TypeIllisible_GardeLesTagsEtAlerte()
    {
        var competence = LecteurCompetences.Construire(NumeroCompetence, "  ", ["Soigner"]);

        Assert.Equal("", competence.Type);
        Assert.Equal(["Soigner"], competence.Tags);
        Assert.Equal([$"Compétence {NumeroCompetence} : type illisible"], competence.Alertes);
    }

    [Fact]
    public void Construire_AucunTag_GardeLeTypeEtAlerte()
    {
        var competence = LecteurCompetences.Construire(NumeroCompetence, "Compétence", []);

        Assert.Equal("Compétence", competence.Type);
        Assert.Empty(competence.Tags);
        Assert.Equal([$"Compétence {NumeroCompetence} : aucun tag lu"], competence.Alertes);
    }

    [Fact]
    public void Construire_RienDeLisible_DeuxAlertes()
    {
        var competence = LecteurCompetences.Construire(NumeroCompetence, "", []);

        Assert.Equal(2, competence.Alertes.Count);
    }
}
