using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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

    // Repère vertical : la popup est centrée et sa hauteur varie avec la description, la zone calibrée glisse donc d'une compétence à l'autre ; le trait séparateur sous le titre, lui, reste à distance fixe de la zone. Mesures sur une capture de 1765 x 993.
    private const int EcartSeparateurZone = 4;
    private const int MargeRecherche = 60;
    // Le bord haut de la popup est un autre trait pleine largeur, à distance fixe du séparateur : exiger les deux évite de confondre l'un avec l'autre.
    private const int EcartBordSeparateur = 71;
    private const int ToleranceEcartBord = 6;
    // Une ligne est un trait quand elle est nettement plus claire que celles situées 3 lignes au-dessus et au-dessous, sur la quasi-totalité de la largeur de la zone.
    private const int DistanceVoisines = 3;
    private const int SeuilContraste = 20;
    private const double FractionTrait = 0.8;
    private const int OctetsParPixel = 4;
    // L'OCR prend le « o » isolé pour un zéro (« A0E ») : entre deux lettres, un zéro ne peut être qu'un « o ».
    private const string ZeroEntreLettres = @"(?<=\p{L})0(?=\p{L})";
    private const string LettreO = "o";

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

        // Aucun trait trouvé : zone inchangée, l'alerte de lecture reste le filet de sécurité.
        zone = zone with { Y = zone.Y + DecalageVertical(capture, zone) };
        var etiquette = $"{PrefixeEtiquette}{numero}";
        Ocr.DiagnostiquerCapture(capture, zone, etiquette);
        var largeurType = (int)(zone.Largeur * FractionType);
        // Deux lectures distinctes : le type est gris clair sur fond sombre, les tags sont blancs sur des pastilles de couleur.
        var texteType = await Ocr.LireTexteClairAsync(capture, zone with { Largeur = largeurType }, etiquette + SuffixeType);
        var pastilles = await Ocr.LirePastillesAsync(capture, zone with { X = zone.X + largeurType, Largeur = zone.Largeur - largeurType }, etiquette + SuffixeTags);
        return Construire(numero, texteType, pastilles);
    }

    // Écart vertical entre le trait séparateur trouvé et celui attendu d'après la zone calibrée ; 0 si aucun trait n'est reconnu.
    internal static int DecalageVertical(Bitmap capture, Zone zone)
    {
        var attendu = zone.Y - EcartSeparateurZone;
        var premiere = Math.Max(DistanceVoisines, attendu - MargeRecherche - EcartBordSeparateur - ToleranceEcartBord);
        var derniere = Math.Min(capture.Height - 1 - DistanceVoisines, attendu + MargeRecherche);
        var gauche = Math.Max(0, zone.X);
        var droite = Math.Min(capture.Width, zone.X + zone.Largeur);
        if (derniere < premiere || droite <= gauche)
        {
            return 0;
        }

        var luminances = LireLuminances(capture, gauche, droite, premiere - DistanceVoisines, derniere + DistanceVoisines);
        var debutsTraits = new List<int>();
        var estTrait = false;
        for (var y = premiere; y <= derniere; y++)
        {
            var ligneTrait = FractionClaire(luminances, y - premiere + DistanceVoisines) >= FractionTrait;
            if (ligneTrait && !estTrait)
            {
                debutsTraits.Add(y);
            }
            estTrait = ligneTrait;
        }

        var separateurs = debutsTraits.Where(y => Math.Abs(y - attendu) <= MargeRecherche
            && debutsTraits.Any(bord => Math.Abs(y - bord - EcartBordSeparateur) <= ToleranceEcartBord)).ToList();
        return separateurs.Count == 0 ? 0 : separateurs.MinBy(y => Math.Abs(y - attendu)) - attendu;
    }

    // Part des colonnes où la ligne <paramref name="ligne"/> est nettement plus claire que celles situées DistanceVoisines au-dessus et au-dessous.
    private static double FractionClaire(int[][] luminances, int ligne)
    {
        var colonnes = luminances[ligne].Length;
        var claires = 0;
        for (var x = 0; x < colonnes; x++)
        {
            var voisines = (luminances[ligne - DistanceVoisines][x] + luminances[ligne + DistanceVoisines][x]) / 2;
            if (luminances[ligne][x] - voisines >= SeuilContraste)
            {
                claires++;
            }
        }
        return (double)claires / colonnes;
    }

    private static int[][] LireLuminances(Bitmap image, int gauche, int droite, int haut, int bas)
    {
        var largeur = droite - gauche;
        var lignes = new int[bas - haut + 1][];
        var donnees = image.LockBits(new Rectangle(gauche, haut, largeur, lignes.Length), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var octets = new byte[donnees.Stride * donnees.Height];
            Marshal.Copy(donnees.Scan0, octets, 0, octets.Length);
            for (var y = 0; y < lignes.Length; y++)
            {
                lignes[y] = new int[largeur];
                for (var x = 0; x < largeur; x++)
                {
                    var i = (y * donnees.Stride) + (x * OctetsParPixel);
                    // Ordre des octets en mémoire : bleu, vert, rouge, alpha.
                    lignes[y][x] = (int)Ocr.Gris(Color.FromArgb(octets[i + 2], octets[i + 1], octets[i]));
                }
            }
        }
        finally
        {
            image.UnlockBits(donnees);
        }
        return lignes;
    }

    // Un type ou un tag illisible n'arrête rien : la compétence est gardée telle quelle, avec une alerte pour la revoir.
    internal static CompetenceLue Construire(int numero, string texteType, IEnumerable<string> pastilles)
    {
        var type = Referentiel.Compacter(texteType);
        var tags = RetenirTags(pastilles);
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

    // Une pastille = un tag. Un texte sans lettre ni chiffre est du bruit d'OCR, un doublon est ignoré.
    internal static List<string> RetenirTags(IEnumerable<string> pastilles)
    {
        var tags = new List<string>();
        var dejaVus = new HashSet<string>();
        foreach (var pastille in pastilles)
        {
            var tag = Regex.Replace(Referentiel.Compacter(pastille), ZeroEntreLettres, LettreO);
            var norme = Referentiel.Normaliser(tag);
            if (norme.Length > 0 && dejaVus.Add(norme))
            {
                tags.Add(tag);
            }
        }
        return tags;
    }
}
