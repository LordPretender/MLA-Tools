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

    [Fact]
    public void DecouperTags_PlusieursMots_UnTagParMot()
    {
        Assert.Equal(["Contrôler", "AoE"], LecteurCompetences.DecouperTags("Contrôler AoE"));
    }

    [Fact]
    public void DecouperTags_EspacesEtRetoursALaLigne_SontCompactes()
    {
        Assert.Equal(["Soigner", "AoE"], LecteurCompetences.DecouperTags("  Soigner \r\n  AoE "));
    }

    [Fact]
    public void DecouperTags_MotSansLettreNiChiffre_EstIgnore()
    {
        Assert.Equal(["Soigner"], LecteurCompetences.DecouperTags("Soigner | ."));
    }

    [Fact]
    public void DecouperTags_DoublonALaCasseEtAuxAccentsPres_GardeLePremier()
    {
        Assert.Equal(["Contrôler"], LecteurCompetences.DecouperTags("Contrôler controler CONTROLER"));
    }

    [Fact]
    public void DecouperTags_TexteVide_RendUneListeVide()
    {
        Assert.Empty(LecteurCompetences.DecouperTags(""));
    }

    [Fact]
    public void Construire_TypeEtTagsLus_SansAlerte()
    {
        var competence = LecteurCompetences.Construire(NumeroCompetence, "Ulti", "Contrôler AoE");

        Assert.Equal(NumeroCompetence, competence.Numero);
        Assert.Equal("Ulti", competence.Type);
        Assert.Equal(["Contrôler", "AoE"], competence.Tags);
        Assert.Empty(competence.Alertes);
    }

    [Fact]
    public void Construire_TypeIllisible_GardeLesTagsEtAlerte()
    {
        var competence = LecteurCompetences.Construire(NumeroCompetence, "  ", "Soigner");

        Assert.Equal("", competence.Type);
        Assert.Equal(["Soigner"], competence.Tags);
        Assert.Equal([$"Compétence {NumeroCompetence} : type illisible"], competence.Alertes);
    }

    [Fact]
    public void Construire_AucunTag_GardeLeTypeEtAlerte()
    {
        var competence = LecteurCompetences.Construire(NumeroCompetence, "Compétence", "");

        Assert.Equal("Compétence", competence.Type);
        Assert.Empty(competence.Tags);
        Assert.Equal([$"Compétence {NumeroCompetence} : aucun tag lu"], competence.Alertes);
    }

    [Fact]
    public void Construire_RienDeLisible_DeuxAlertes()
    {
        var competence = LecteurCompetences.Construire(NumeroCompetence, "", "");

        Assert.Equal(2, competence.Alertes.Count);
    }
}
