using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace MlaTools;

/// <summary>Mot reconnu par l'OCR, avec sa position dans l'image soumise.</summary>
internal readonly record struct MotLu(string Texte, double Gauche, double Largeur, double Hauteur);

internal static class Ocr
{
    private const string CodeLangue = "fr";

    // Le moteur OCR lit mal les petits caractères : on agrandit la zone avant de la lui soumettre.
    private const int Agrandissement = 3;

    // Le moteur ne lit pas un texte qui touche les bords de l'image (« Nana » remplit presque toute la hauteur de sa zone).
    private const int MargeContraste = 40;

    private const int SeuilLuminositeTexteClair = 200;
    private const int SeuilLuminositeMinimal = 120;
    // Texte blanc d'une pastille : les trois canaux sont hauts, alors qu'un reflet sur une pastille colorée garde au moins un canal bas tout en étant très lumineux.
    private const int SeuilCanalTexteBlanc = 180;
    // Écart horizontal, en hauteurs de texte, sous lequel deux mots appartiennent à la même pastille (l'OCR coupe parfois « AoE ») ; deux pastilles sont bien plus espacées.
    private const double EcartMaxMotsParHauteur = 0.8;
    private const int NiveauxGris = 256;
    private const int EpaisseurCadreDiagnostic = 3;
    private const string VariableDiagnostic = "MLA_OCR_DIAGNOSTIC";
    private const string SuffixeBrut = "_brut";
    private const string SuffixeSeuil = "_seuil";
    private const string SuffixeCapture = "_capture";
    private const string ExtensionImage = ".png";

    // Bleu mesuré sur Édith : chiffre 0 à 16, fond du jeu 40 à 60, corps de l'étoile environ 100. À ajuster sur d'autres héros.
    private const int SeuilBleuChiffre = 25;
    private const int MargeChiffre = 30;
    private const int EcartChiffre = 40;

    // Le moteur lit ou non un chiffre répété selon l'échelle et le nombre de répétitions, sans règle prévisible : « 1 » d'Angela
    // n'est lu qu'avec 3 répétitions, d'autres chiffres avec 5. On essaie donc les combinaisons jusqu'à obtenir un texte.
    private static readonly (int Echelle, int Repetitions)[] ReglagesChiffre = [(4, 3), (2, 5), (6, 3), (3, 3), (2, 3), (4, 5)];

    private static readonly Lazy<OcrEngine?> Moteur = new(() => OcrEngine.TryCreateFromLanguage(new Language(CodeLangue)));

    public static async Task<string> LireAsync(Bitmap capture, Zone zone)
    {
        var moteur = ObtenirMoteur();
        using var agrandie = Agrandir(capture, zone);
        return await LireAvecContrasteAsync(agrandie, image => ReconnaitreAsync(moteur, image), texte => texte.Length == 0);
    }

    // Lecture de l'image telle quelle puis, si rien n'en sort, de sa version contrastée.
    private static async Task<T> LireAvecContrasteAsync<T>(Bitmap image, Func<Bitmap, Task<T>> lire, Func<T, bool> estVide)
    {
        var resultat = await lire(image);
        if (!estVide(resultat))
        {
            return resultat;
        }

        using var contraste = Contraster(image);
        return await lire(contraste);
    }

    /// <summary>
    /// Lit un texte gris clair sur fond sombre (type de compétence) : le seuil de luminosité se déduit de la zone.
    /// Sans résultat, retombe sur la lecture ordinaire.
    /// </summary>
    public static async Task<string> LireTexteClairAsync(Bitmap capture, Zone zone, string? etiquette = null)
    {
        var moteur = ObtenirMoteur();
        using var agrandie = Agrandir(capture, zone);
        var seuil = SeuilAdaptatif(agrandie);
        using var seuillee = IsolerTexteClair(agrandie, c => Gris(c) >= seuil);
        Diagnostiquer(etiquette, seuil, agrandie, seuillee);
        var texte = await ReconnaitreAsync(moteur, seuillee);
        return texte.Length > 0 ? texte : await LireAsync(capture, zone);
    }

    /// <summary>
    /// Lit des pastilles de tags alignées, de couleurs différentes : seul le texte blanc reste, en noir sur blanc.
    /// Retourne un texte par pastille, de gauche à droite.
    /// </summary>
    public static async Task<IReadOnlyList<string>> LirePastillesAsync(Bitmap capture, Zone zone, string? etiquette = null)
    {
        var moteur = ObtenirMoteur();
        using var agrandie = Agrandir(capture, zone);
        using var seuillee = IsolerTexteClair(agrandie, c => Math.Min(c.R, Math.Min(c.G, c.B)) >= SeuilCanalTexteBlanc);
        Diagnostiquer(etiquette, SeuilCanalTexteBlanc, agrandie, seuillee);
        var mots = await ReconnaitreMotsAsync(moteur, seuillee);
        if (mots.Count == 0)
        {
            mots = await LireAvecContrasteAsync(agrandie, image => ReconnaitreMotsAsync(moteur, image), lus => lus.Count == 0);
        }
        return RegrouperMots(mots);
    }

    // Les mots proches sont réunis : ils viennent d'une même pastille.
    internal static List<string> RegrouperMots(IEnumerable<MotLu> mots)
    {
        var groupes = new List<string>();
        MotLu? precedent = null;
        foreach (var mot in mots.OrderBy(m => m.Gauche))
        {
            if (precedent is { } p && mot.Gauche - (p.Gauche + p.Largeur) <= EcartMaxMotsParHauteur * Math.Max(p.Hauteur, mot.Hauteur))
            {
                groupes[^1] = $"{groupes[^1]} {mot.Texte}";
            }
            else
            {
                groupes.Add(mot.Texte);
            }
            precedent = mot;
        }
        return groupes;
    }

    internal static float Gris(Color c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;

    // Un texte gris clair sur fond sombre (type de compétence) passe sous le seuil fixe : le seuil se déduit de la zone, borné pour ne pas prendre du bruit de fond pour du texte.
    private static int SeuilAdaptatif(Bitmap source)
    {
        var histogramme = new int[NiveauxGris];
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                histogramme[(int)Gris(source.GetPixel(x, y))]++;
            }
        }

        return Math.Clamp(CalculerSeuilOtsu(histogramme), SeuilLuminositeMinimal, SeuilLuminositeTexteClair);
    }

    // Méthode d'Otsu : le seuil qui sépare le mieux les niveaux de gris en deux classes (fond, texte) ; un pixel est du texte à partir de la valeur retournée.
    internal static int CalculerSeuilOtsu(int[] histogramme)
    {
        long total = histogramme.Sum();
        long sommeTotale = 0;
        for (var i = 0; i < histogramme.Length; i++)
        {
            sommeTotale += (long)i * histogramme[i];
        }

        long poidsFond = 0;
        long sommeFond = 0;
        var meilleurSeuil = 0;
        var meilleureVariance = -1.0;
        for (var i = 0; i < histogramme.Length; i++)
        {
            poidsFond += histogramme[i];
            sommeFond += (long)i * histogramme[i];
            var poidsTexte = total - poidsFond;
            if (poidsFond == 0 || poidsTexte == 0)
            {
                continue;
            }

            var ecart = (double)sommeFond / poidsFond - (double)(sommeTotale - sommeFond) / poidsTexte;
            var variance = (double)poidsFond * poidsTexte * ecart * ecart;
            if (variance > meilleureVariance)
            {
                meilleureVariance = variance;
                meilleurSeuil = i + 1;
            }
        }

        return meilleurSeuil;
    }

    // Les découpes brute et seuillée sont écrites quand la variable d'environnement désigne un dossier : on calibre le seuil sur ce que l'OCR reçoit vraiment.
    private static void Diagnostiquer(string? etiquette, int seuil, Bitmap brute, Bitmap seuillee)
    {
        var dossier = DossierDiagnostic();
        if (etiquette is null || dossier is null)
        {
            return;
        }

        brute.Save(Path.Combine(dossier, $"{etiquette}{SuffixeBrut}{ExtensionImage}"), ImageFormat.Png);
        seuillee.Save(Path.Combine(dossier, $"{etiquette}{SuffixeSeuil}{seuil}{ExtensionImage}"), ImageFormat.Png);
    }

    /// <summary>Écrit la capture entière avec la zone lue encadrée : on voit où la zone calibrée tombe par rapport à ce que le jeu affichait.</summary>
    public static void DiagnostiquerCapture(Bitmap capture, Zone zone, string etiquette)
    {
        var dossier = DossierDiagnostic();
        if (dossier is null)
        {
            return;
        }

        using var annotee = new Bitmap(capture);
        using (var g = Graphics.FromImage(annotee))
        using (var stylo = new Pen(Color.Red, EpaisseurCadreDiagnostic))
        {
            g.DrawRectangle(stylo, zone.X, zone.Y, zone.Largeur, zone.Hauteur);
        }

        annotee.Save(Path.Combine(dossier, $"{etiquette}{SuffixeCapture}{ExtensionImage}"), ImageFormat.Png);
    }

    private static string? DossierDiagnostic()
    {
        var dossier = Environment.GetEnvironmentVariable(VariableDiagnostic);
        if (string.IsNullOrEmpty(dossier))
        {
            return null;
        }

        Directory.CreateDirectory(dossier);
        return dossier;
    }

    // Les pixels reconnus comme texte passent en noir, tout le reste en blanc.
    private static Bitmap IsolerTexteClair(Bitmap source, Func<Color, bool> estTexte)
    {
        var sortie = new Bitmap(source.Width + 2 * MargeContraste, source.Height + 2 * MargeContraste);
        using (var g = Graphics.FromImage(sortie))
        {
            g.Clear(Color.White);
        }

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                if (estTexte(source.GetPixel(x, y)))
                {
                    sortie.SetPixel(MargeContraste + x, MargeContraste + y, Color.Black);
                }
            }
        }

        return sortie;
    }

    // Le moteur ne lit rien d'un texte au contraste trop faible (« Arts Martiaux » rouge sombre sur fond nuit) :
    // on étire les niveaux de gris puis on met le fond en blanc, quelle que soit sa polarité d'origine.
    private static Bitmap Contraster(Bitmap source)
    {
        var largeur = source.Width;
        var hauteur = source.Height;
        var gris = new float[largeur * hauteur];
        for (var y = 0; y < hauteur; y++)
        {
            for (var x = 0; x < largeur; x++)
            {
                var c = source.GetPixel(x, y);
                gris[y * largeur + x] = Gris(c);
            }
        }

        // Le fond est la valeur médiane ; l'écart au fond, ramené à son 90e centile, donne le noir : le texte ressort
        // quelle que soit sa polarité, sans que l'icône (extrême de la zone) ne fixe l'échelle.
        var tries = (float[])gris.Clone();
        Array.Sort(tries);
        var fond = tries[tries.Length / 2];
        var ecarts = gris.Select(g => Math.Abs(g - fond)).ToArray();
        Array.Sort(ecarts);
        var echelle = Math.Max(1f, ecarts[(int)(ecarts.Length * 0.9)]);

        var sortie = new Bitmap(largeur + 2 * MargeContraste, hauteur + 2 * MargeContraste);
        using (var g = Graphics.FromImage(sortie))
        {
            g.Clear(Color.White);
        }

        for (var i = 0; i < gris.Length; i++)
        {
            var niveau = (int)((1f - Math.Min(1f, Math.Abs(gris[i] - fond) / echelle)) * 255);
            sortie.SetPixel(MargeContraste + i % largeur, MargeContraste + i / largeur, Color.FromArgb(niveau, niveau, niveau));
        }

        return sortie;
    }

    private static OcrEngine ObtenirMoteur() => Moteur.Value
        ?? throw new InvalidOperationException("Le pack de langue français de Windows est absent : installez-le (Paramètres, Heure et langue, Langue) puis relancez l'outil.");

    /// <summary>
    /// Lit le chiffre sombre du badge d'éveil sur son étoile claire : le moteur ne lit rien sur l'image brute,
    /// on la réduit donc à du noir sur blanc d'après le canal bleu, bas pour le chiffre et haut pour l'étoile.
    /// </summary>
    public static async Task<string> LireChiffreAsync(Bitmap capture, Zone zone)
    {
        var moteur = ObtenirMoteur();
        using var agrandie = Agrandir(capture, zone);
        using var chiffre = IsolerChiffre(agrandie);
        if (chiffre is null)
        {
            return string.Empty;
        }

        foreach (var (echelle, repetitions) in ReglagesChiffre)
        {
            using var repete = RepeterChiffre(chiffre, echelle, repetitions);
            var texte = await ReconnaitreAsync(moteur, repete);
            if (texte.Any(char.IsDigit))
            {
                return texte;
            }
        }

        return string.Empty;
    }

    // Le moteur ignore un caractère isolé ou trop petit : agrandi et répété sur une ligne, il ressemble à du texte.
    private static Bitmap RepeterChiffre(Bitmap chiffre, int echelle, int repetitions)
    {
        var largeur = chiffre.Width * echelle;
        var hauteur = chiffre.Height * echelle;
        var sortie = new Bitmap(
            2 * MargeChiffre + repetitions * largeur + (repetitions - 1) * EcartChiffre,
            hauteur + 2 * MargeChiffre);
        using var g = Graphics.FromImage(sortie);
        g.Clear(Color.White);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        for (var i = 0; i < repetitions; i++)
        {
            g.DrawImage(chiffre, new Rectangle(MargeChiffre + i * (largeur + EcartChiffre), MargeChiffre, largeur, hauteur));
        }

        return sortie;
    }

    private static async Task<string> ReconnaitreAsync(OcrEngine moteur, Bitmap image) =>
        (await AnalyserAsync(moteur, image)).Text.Trim();

    private static async Task<List<MotLu>> ReconnaitreMotsAsync(OcrEngine moteur, Bitmap image) =>
        (await AnalyserAsync(moteur, image)).Lines
            .SelectMany(ligne => ligne.Words)
            .Select(mot => new MotLu(mot.Text, mot.BoundingRect.X, mot.BoundingRect.Width, mot.BoundingRect.Height))
            .ToList();

    private static async Task<OcrResult> AnalyserAsync(OcrEngine moteur, Bitmap image)
    {
        using var flux = new MemoryStream();
        image.Save(flux, ImageFormat.Bmp);
        flux.Position = 0;

        var decodeur = await BitmapDecoder.CreateAsync(flux.AsRandomAccessStream());
        using var logiciel = await decodeur.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        return await moteur.RecognizeAsync(logiciel);
    }

    // Le chiffre est la plus grande tache sombre de la zone : les traînées et points isolés sont écartés.
    private static Bitmap? IsolerChiffre(Bitmap source)
    {
        var largeur = source.Width;
        var hauteur = source.Height;
        var sombre = new bool[largeur * hauteur];
        for (var y = 0; y < hauteur; y++)
        {
            for (var x = 0; x < largeur; x++)
            {
                sombre[y * largeur + x] = source.GetPixel(x, y).B < SeuilBleuChiffre;
            }
        }

        var composante = Composantes.Lister(sombre, largeur, hauteur, huitVoisins: true).MaxBy(c => c.Count);
        if (composante is null)
        {
            return null;
        }

        var minX = composante.Min(p => p % largeur);
        var maxX = composante.Max(p => p % largeur);
        var minY = composante.Min(p => p / largeur);
        var maxY = composante.Max(p => p / largeur);

        var chiffre = new Bitmap(maxX - minX + 1, maxY - minY + 1);
        using (var g = Graphics.FromImage(chiffre))
        {
            g.Clear(Color.White);
        }

        foreach (var p in composante)
        {
            chiffre.SetPixel(p % largeur - minX, p / largeur - minY, Color.Black);
        }

        return chiffre;
    }

    private static Bitmap Agrandir(Bitmap capture, Zone zone)
    {
        var source = new Rectangle(zone.X, zone.Y, zone.Largeur, zone.Hauteur);
        var agrandie = new Bitmap(zone.Largeur * Agrandissement, zone.Hauteur * Agrandissement);
        using var g = Graphics.FromImage(agrandie);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(capture, new Rectangle(Point.Empty, agrandie.Size), source, GraphicsUnit.Pixel);
        return agrandie;
    }
}
