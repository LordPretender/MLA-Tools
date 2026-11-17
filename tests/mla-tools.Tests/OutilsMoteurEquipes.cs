namespace MlaTools.Tests;

/// <summary>Constructeurs de données pour les tests du moteur d'équipes ; les classes ne sont pas celles du jeu, pour que les attentes ne dépendent pas de Referentiel.Classes.</summary>
internal static class OutilsMoteurEquipes
{
    public const string ClasseTank = "Gardien";
    public const string ClasseDps = "Mage";
    public const string ClasseSoutien = "Soigneur";
    public const string ClasseSansRole = "Inconnue";
    public const string EquipementLeger = "léger";

    public static readonly string FactionX = Referentiel.FactionsRegulieres[0];
    public static readonly string FactionY = Referentiel.FactionsRegulieres[1];

    public static readonly IReadOnlyDictionary<string, string> Roles = new Dictionary<string, string>
    {
        [ClasseTank] = MoteurEquipes.RoleTank,
        [ClasseDps] = MoteurEquipes.RoleDps,
        [ClasseSoutien] = MoteurEquipes.RoleSoutien,
    };

    public static HeroMoteur Hero(int id, string classe, string[]? factions = null, int etoiles = 1, string equipement = EquipementLeger) =>
        new(id, etoiles, classe, factions ?? [], equipement);

    // Héros chaos pur : sans autre faction, il ne résonne pas mais donne un bonus aux alliés.
    public static HeroMoteur HeroChaosPur(int id) => Hero(id, ClasseDps, [Referentiel.FactionChaos]);

    // Deux tanks, deux DPS et un soutien : aucune pénalité de rôles ; sert de base aux variantes qui en retirent un élément.
    public static HeroMoteur[] EquipeEquilibree() =>
    [
        Hero(1, ClasseTank), Hero(2, ClasseTank), Hero(3, ClasseDps), Hero(4, ClasseDps), Hero(5, ClasseSoutien),
    ];

    public static MoteurEquipes Moteur(params HeroMoteur[] heros) => new(heros, Roles);

    public static EvaluationEquipe EvaluerEquipe(params HeroMoteur[] equipe) =>
        Moteur(equipe).Evaluer([.. equipe.Select(h => h.Id)]);
}
