namespace MlaTools;

/// <summary>Base de toutes les fenêtres de l'application : leur donne l'icône de l'exécutable et le style commun des boutons. Toute nouvelle fenêtre en hérite.</summary>
internal class FormBase : Form
{
    // Partagée par toutes les fenêtres et vivante autant que l'application : aucune fenêtre n'en est propriétaire, donc aucune ne la libère.
    private static readonly Icon? IconeApplication = ChargerIconeApplication();

    public FormBase()
    {
        if (IconeApplication is not null)
        {
            Icon = IconeApplication;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        OutilsDialogue.StyliserBoutons(this);
    }

    // ProcessPath peut être nul sous un hôte non standard : les fenêtres gardent alors l'icône par défaut.
    private static Icon? ChargerIconeApplication()
    {
        var chemin = Environment.ProcessPath;
        return chemin is null ? null : Icon.ExtractAssociatedIcon(chemin);
    }
}
