using static MlaTools.Dispositions;

namespace MlaTools;

/// <summary>Résultat brut de la lecture d'une compétence : <paramref name="Type"/> vide et <paramref name="Tags"/> vide quand la popup n'a rien donné.</summary>
internal sealed record CompetenceLue(int Numero, string Type, IReadOnlyList<string> Tags, IReadOnlyList<string> Alertes);

internal static class LecteurCompetences
{
    // Part gauche de la bande de la popup occupée par le type (« Compétence », « Ulti »), les pastilles de tags étant à droite. À ajuster sur le jeu.
    private const double FractionType = 0.3;

    // Noms des images du diagnostic OCR (voir Ocr.DiagnostiquerCapture).
    private const string PrefixeEtiquette = "competence";
    private const string SuffixeType = "_type";
    private const string SuffixeTags = "_tags";

    // Ouvre chaque popup de compétence, la lit puis la ferme : l'onglet Compétence doit déjà être affiché.
    public static async Task<IReadOnlyList<CompetenceLue>> LireAsync(IntPtr fenetre, ConfigZones config, int delaiApresClicMs)
    {
        var competences = new List<CompetenceLue>();
        for (var i = 0; i < ZonesCompetences.Length; i++)
        {
            Navigation.Cliquer(fenetre, config, ZonesCompetences[i]);
            await Task.Delay(delaiApresClicMs);

            using (var capture = FenetreCapture.Capturer(fenetre))
            {
                competences.Add(await LireAsync(capture, config, i + 1));
            }

            // La popup se ferme en recliquant sur l'onglet Compétence.
            Navigation.Cliquer(fenetre, config, ZoneOngletCompetence);
            await Task.Delay(delaiApresClicMs);
        }
        return competences;
    }

    // Prend une capture déjà prise : la lecture ne touche ni la fenêtre du jeu ni le disque.
    public static async Task<CompetenceLue> LireAsync(Bitmap capture, ConfigZones config, int numero)
    {
        config.VerifierTaille(capture.Size, "La capture");
        if (!config.Globales.TryGetValue(ZonePopupTags, out var zone))
        {
            throw new InvalidOperationException($"La zone « {ZonePopupTags} » n'est pas calibrée.");
        }

        var etiquette = $"{PrefixeEtiquette}{numero}";
        Ocr.DiagnostiquerCapture(capture, zone, etiquette);
        var largeurType = (int)(zone.Largeur * FractionType);
        // Seul le type est lu au seuil adaptatif : à droite, abaisser le seuil remplirait les pastilles claires (orange) et fondrait le texte dedans.
        var texteType = await Ocr.LireTexteClairAsync(capture, zone with { Largeur = largeurType }, seuilAdaptatif: true, etiquette: etiquette + SuffixeType);
        var texteTags = await Ocr.LireTexteClairAsync(capture, zone with { X = zone.X + largeurType, Largeur = zone.Largeur - largeurType }, etiquette: etiquette + SuffixeTags);
        return Construire(numero, texteType, texteTags);
    }

    // Un type ou un tag illisible n'arrête rien : la compétence est gardée telle quelle, avec une alerte pour la revoir.
    internal static CompetenceLue Construire(int numero, string texteType, string texteTags)
    {
        var type = Referentiel.Compacter(texteType);
        var tags = DecouperTags(texteTags);
        var alertes = new List<string>();
        if (type.Length == 0)
        {
            alertes.Add($"Compétence {numero} : type illisible");
        }
        if (tags.Count == 0)
        {
            alertes.Add($"Compétence {numero} : aucun tag lu");
        }
        return new CompetenceLue(numero, type, tags, alertes);
    }

    // Un mot = un tag : le texte lu à droite du type enchaîne les pastilles. Un mot sans lettre ni chiffre est du bruit d'OCR, un doublon est ignoré.
    internal static List<string> DecouperTags(string texte)
    {
        var tags = new List<string>();
        var dejaVus = new HashSet<string>();
        foreach (var mot in Referentiel.Compacter(texte).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var norme = Referentiel.Normaliser(mot);
            if (norme.Length > 0 && dejaVus.Add(norme))
            {
                tags.Add(mot);
            }
        }
        return tags;
    }
}
