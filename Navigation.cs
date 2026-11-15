using System.Runtime.InteropServices;

namespace MlaTools;

internal static class Navigation
{
    // Laisse finir l'animation de changement de héros avant la capture suivante.
    // Réglable dans la fenêtre : un jeu ou une machine plus lents demandent une attente plus longue.
    public const int DelaiParDefautMs = 800;
    public const int DelaiMinMs = 200;
    public const int DelaiMaxMs = 5000;

    private const uint TypeSouris = 0;
    private const uint ClicGaucheBas = 0x0002;
    private const uint ClicGaucheHaut = 0x0004;
    private const int NombreEvenements = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct EntreeSouris
    {
        public int Dx;
        public int Dy;
        public uint Donnees;
        public uint Drapeaux;
        public uint Horodatage;
        public IntPtr InfoSupplementaire;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Entree
    {
        public uint Type;
        public EntreeSouris Souris;
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nombre, Entree[] entrees, int taille);

    // Clic réel (entrée souris simulée) au centre de la flèche calibrée : le jeu ignore les messages postés à sa fenêtre.
    public static void CliquerFleche(IntPtr fenetre, ConfigZones config)
    {
        if (!config.Globales.TryGetValue(Dispositions.ZoneFlecheSuivante, out var fleche))
        {
            throw new InvalidOperationException("La flèche suivante n'est pas calibrée.");
        }

        config.VerifierTaille(FenetreCapture.TailleClient(fenetre), "La fenêtre");

        if (!FenetreCapture.EstAuPremierPlan(fenetre))
        {
            FenetreCapture.AmenerAuPremierPlan(fenetre);
        }

        var centre = FenetreCapture.ClientVersEcran(fenetre, new Point(fleche.X + fleche.Largeur / 2, fleche.Y + fleche.Hauteur / 2));
        if (!SetCursorPos(centre.X, centre.Y))
        {
            throw new InvalidOperationException("Impossible de placer le curseur sur la flèche.");
        }

        var entrees = new[]
        {
            new Entree { Type = TypeSouris, Souris = new EntreeSouris { Drapeaux = ClicGaucheBas } },
            new Entree { Type = TypeSouris, Souris = new EntreeSouris { Drapeaux = ClicGaucheHaut } },
        };
        if (SendInput(NombreEvenements, entrees, Marshal.SizeOf<Entree>()) != NombreEvenements)
        {
            throw new InvalidOperationException("Le clic a été refusé par Windows (fenêtre de niveau supérieur ?).");
        }
    }
}
