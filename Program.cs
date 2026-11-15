namespace MlaTools;

internal static class Constantes
{
    public const string TitreApplication = "MLA Tools";
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Sans cela, les coordonnées d'écran sont mises à l'échelle et la capture ne tombe pas sur les bons pixels.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.Run(new FormEquipes());
    }
}
