using Xunit;

namespace MlaTools.Tests;

public class LecteurCompetencesTests
{
    private const int NumeroCompetence = 2;

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
