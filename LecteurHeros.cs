using static MlaTools.Dispositions;

namespace MlaTools;

internal sealed record HeroLu(
    string Nom,
    int? Etoiles,
    IReadOnlyList<string> Factions,
    string? Classe,
    string? Equipement,
    IReadOnlyList<string> Alertes);

internal static class LecteurHeros
{
    private const string MotEquipement = "Équipement";

    private static readonly string[] ZonesFaction = [ZoneFaction, ZoneFaction1, ZoneFaction2];

    private static readonly (int Decalage, int Hauteur)[] VariantesFaction = [(0, 0), (0, 4), (10, 0), (10, 4)];

    private static readonly int[] DecalagesNom = [0, -5, 5, -10, 10];
    private static readonly int[] DecalagesClasse = [2, 4, 6, 8, 16];

    // Prend une capture déjà prise : la lecture ne touche ni la fenêtre du jeu ni le disque.
    public static async Task<HeroLu> LireAsync(Bitmap capture, ConfigZones config)
    {
        config.VerifierTaille(capture.Size, "La capture");

        var alertes = new List<string>();
        var disposition = await DetecterDispositionAsync(capture, config);
        if (disposition is null)
        {
            alertes.Add("Disposition indéterminée (zone Équipement non reconnue) : héros à lire à la main");
            return new HeroLu("", null, [], null, null, alertes);
        }

        var zones = config.Dispositions[disposition];
        var nom = await LireNomAsync(capture, zones[ZoneNom]);
        if (nom.Length == 0)
        {
            alertes.Add($"Nom illisible : lu « {nom} »");
        }

        var etoiles = await LecteurEtoiles.LireAsync(capture, zones[ZoneEtoiles], zones[ZoneEveil]);
        if (etoiles.Alerte is not null)
        {
            alertes.Add(etoiles.Alerte);
        }

        var factions = await LireFactionsAsync(capture, zones, alertes);
        var (classe, equipement) = await LireClasseEtEquipementAsync(capture, zones, alertes);
        return new HeroLu(nom, etoiles.Valeur, factions, classe, equipement, alertes);
    }

    private static async Task<string> LireNomAsync(Bitmap capture, Zone zone)
    {
        var lectures = await LireDecaleesAsync(capture, zone, DecalagesNom);
        var textes = lectures.Select(Referentiel.Compacter).Where(t => t.Length > 0);
        return PlusFrequent(textes, Referentiel.Normaliser) ?? "";
    }

    // Le moteur est instable : un décalage de quelques pixels change la lecture (« Ruby » devient « qubxl »). On lit à plusieurs décalages du bord gauche.
    private static Task<List<string>> LireDecaleesAsync(Bitmap capture, Zone zone, int[] decalages) =>
        LireVariantesAsync(capture, zone, decalages.Select(d => (d, 0)).ToArray());

    // Variante = (décalage du bord gauche, hauteur ajoutée) : une ligne de texte rognée en bas (« Arts » sans « Martiaux ») se lit parfois mieux avec quelques pixels de plus.
    private static async Task<List<string>> LireVariantesAsync(Bitmap capture, Zone zone, (int Decalage, int Hauteur)[] variantes)
    {
        var lectures = new List<string>();
        foreach (var (decalage, hauteur) in variantes)
        {
            lectures.Add(await Ocr.LireAsync(capture, zone with { X = zone.X + decalage, Largeur = zone.Largeur - decalage, Hauteur = zone.Hauteur + hauteur }));
        }
        return lectures;
    }

    // À égalité, la première lecture rencontrée l'emporte : l'ordre des décalages fixe donc la priorité.
    private static string? PlusFrequent(IEnumerable<string> valeurs, Func<string, string> cle) =>
        valeurs.GroupBy(cle).OrderByDescending(g => g.Count()).Select(g => g.First()).FirstOrDefault();

    private static async Task<List<string>> LireFactionsAsync(Bitmap capture, Dictionary<string, Zone> zones, List<string> alertes)
    {
        var factions = new List<string>();
        foreach (var nomZone in ZonesFaction.Where(zones.ContainsKey))
        {
            var lectures = await LireVariantesAsync(capture, zones[nomZone], VariantesFaction);
            var faction = PlusFrequent(lectures.Select(t => Referentiel.Rapprocher(t, Referentiel.Factions)).OfType<string>(), f => f);
            if (faction is null)
            {
                alertes.Add($"Faction illisible ({nomZone}) : lu « {string.Join(" | ", lectures)} »");
            }
            else
            {
                factions.Add(faction);
            }
        }
        return factions;
    }

    private static async Task<(string? Classe, string? Equipement)> LireClasseEtEquipementAsync(Bitmap capture, Dictionary<string, Zone> zones, List<string> alertes)
    {
        var lectures = await LireDecaleesAsync(capture, zones[ZoneClasse], DecalagesClasse);
        var classe = PlusFrequent(lectures.Select(t => Referentiel.Rapprocher(t, Referentiel.Classes)).OfType<string>(), c => c);
        if (classe is null)
        {
            alertes.Add("Classe illisible");
        }

        var texteEquipement = Referentiel.Compacter(await Ocr.LireAsync(capture, zones[ZoneEquipement]));
        var equipement = Referentiel.Rapprocher(texteEquipement.Split(' ')[^1], Referentiel.Equipements);
        if (equipement is null)
        {
            alertes.Add("Équipement illisible");
        }
        return (classe, equipement);
    }

    // La zone Équipement des deux dispositions tombe à des hauteurs différentes : une seule affiche le mot « Équipement ».
    private static async Task<string?> DetecterDispositionAsync(Bitmap capture, ConfigZones config)
    {
        var trouvees = new List<string>();
        foreach (var (disposition, zones) in config.Dispositions)
        {
            var attendues = ZonesParDisposition[disposition];
            if (!attendues.All(zones.ContainsKey))
            {
                throw new InvalidOperationException($"La disposition « {disposition} » est incomplète : recalibrez-la.");
            }

            var premierMot = Referentiel.Compacter(await Ocr.LireAsync(capture, zones[ZoneEquipement])).Split(' ')[0];
            if (Referentiel.Rapprocher(premierMot, [MotEquipement]) is not null)
            {
                trouvees.Add(disposition);
            }
        }
        return trouvees.Count == 1 ? trouvees[0] : null;
    }
}
