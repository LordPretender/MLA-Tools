using System.Runtime.InteropServices;
using System.Text;

namespace MlaTools;

internal static class FenetreCapture
{
    private const int DelaiPremierPlanMs = 1500;

    private delegate bool EnumWindowsProc(IntPtr fenetre, IntPtr parametre);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNatif
    {
        public int Gauche;
        public int Haut;
        public int Droite;
        public int Bas;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointNatif
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc rappel, IntPtr parametre);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr fenetre);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr fenetre);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr fenetre);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr fenetre, StringBuilder texte, int tailleMax);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr fenetre, out RectNatif rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr fenetre, ref PointNatif point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr fenetre);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    public static List<(IntPtr Fenetre, string Titre)> ListerFenetres()
    {
        var fenetres = new List<(IntPtr, string)>();
        EnumWindows((fenetre, _) =>
        {
            var longueur = GetWindowTextLength(fenetre);
            if (longueur > 0 && IsWindowVisible(fenetre))
            {
                var texte = new StringBuilder(longueur + 1);
                GetWindowText(fenetre, texte, texte.Capacity);
                fenetres.Add((fenetre, texte.ToString()));
            }
            return true;
        }, IntPtr.Zero);
        return fenetres;
    }

    // La capture lit les pixels de l'écran : si la console recouvre le jeu, elle se retrouverait dans l'image.
    public static void AmenerAuPremierPlan(IntPtr fenetre)
    {
        SetForegroundWindow(fenetre);
        Thread.Sleep(DelaiPremierPlanMs);
    }

    public static bool EstAuPremierPlan(IntPtr fenetre) => GetForegroundWindow() == fenetre;

    // Les zones calibrées sont relatives à la zone cliente : toute lecture ou clic part de cette taille.
    public static Size TailleClient(IntPtr fenetre)
    {
        if (IsIconic(fenetre))
        {
            throw new InvalidOperationException("La fenêtre est réduite : la restaurer avant la capture.");
        }

        GetClientRect(fenetre, out var rect);
        var largeur = rect.Droite - rect.Gauche;
        var hauteur = rect.Bas - rect.Haut;
        if (largeur <= 0 || hauteur <= 0)
        {
            throw new InvalidOperationException("La zone cliente de la fenêtre est vide.");
        }
        return new Size(largeur, hauteur);
    }

    public static Point ClientVersEcran(IntPtr fenetre, Point pointClient)
    {
        var point = new PointNatif { X = pointClient.X, Y = pointClient.Y };
        ClientToScreen(fenetre, ref point);
        return new Point(point.X, point.Y);
    }

    public static Bitmap Capturer(IntPtr fenetre)
    {
        var taille = TailleClient(fenetre);
        var origine = ClientVersEcran(fenetre, Point.Empty);

        var image = new Bitmap(taille.Width, taille.Height);
        using var graphique = Graphics.FromImage(image);
        graphique.CopyFromScreen(origine.X, origine.Y, 0, 0, image.Size);
        return image;
    }
}
