using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace MlaTools;

internal static class Ocr
{
    private const string CodeLangue = "fr";

    // Le moteur OCR lit mal les petits caractères : on agrandit la zone avant de la lui soumettre.
    private const int Agrandissement = 3;

    // Le moteur ne lit pas un texte qui touche les bords de l'image (« Nana » remplit presque toute la hauteur de sa zone).
    private const int MargeContraste = 40;

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
        var texte = await ReconnaitreAsync(moteur, agrandie);
        if (texte.Length > 0)
        {
            return texte;
        }

        using var contraste = Contraster(agrandie);
        return await ReconnaitreAsync(moteur, contraste);
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
                gris[y * largeur + x] = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
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

    private static async Task<string> ReconnaitreAsync(OcrEngine moteur, Bitmap image)
    {
        using var flux = new MemoryStream();
        image.Save(flux, ImageFormat.Bmp);
        flux.Position = 0;

        var decodeur = await BitmapDecoder.CreateAsync(flux.AsRandomAccessStream());
        using var logiciel = await decodeur.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var resultat = await moteur.RecognizeAsync(logiciel);
        return resultat.Text.Trim();
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
