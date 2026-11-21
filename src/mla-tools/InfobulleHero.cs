namespace MlaTools;

/// <summary>Texte de l'infobulle d'un héros : toutes ses caractéristiques, identique dans le Générateur et dans les compositions enregistrées.</summary>
internal static class InfobulleHero
{
    private const string SeparateurFactions = ", ";
    private const string Oui = "oui";
    private const string Non = "non";
    private const string SuffixeLienAme = " (lien d'âme)";

    public static string Texte(HeroStocke stocke, string role)
    {
        var hero = stocke.Hero;
        var effectives = MoteurEquipes.FactionsEffectives(hero.Factions, stocke.LienAme);
        // Le lien d'âme ajoute Ordre aux critères sans le changer dans les factions du héros : on le dit pour que l'infobulle explique les écarts.
        var factions = string.Join(SeparateurFactions, hero.Factions.Concat(effectives.Except(hero.Factions).Select(f => f + SuffixeLienAme)));

        return string.Join(Environment.NewLine,
            hero.Nom,
            $"Étoiles : {CartesEquipes.LibelleEtoiles(hero.Etoiles)}",
            $"Classe : {hero.Classe}",
            $"Rôle : {role}",
            $"Équipement : {hero.Equipement}",
            $"Factions : {factions}",
            $"Lien d'âme : {(stocke.LienAme ? Oui : Non)}",
            $"Mis à jour : {OutilsDialogue.LibelleDate(stocke.DateMaj)}");
    }
}
