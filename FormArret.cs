using static MlaTools.Constantes;

namespace MlaTools;

// Reste au premier plan sans voler le focus : le clic suivant doit atteindre le jeu, pas cette fenêtre.
internal sealed class FormArret : FormBase
{
    private const int MargeBas = 16;

    private readonly CancellationTokenSource _arret;
    private readonly Label _compteur = new() { Text = "0 héros lu", AutoSize = true, Margin = new Padding(8) };
    private readonly Button _boutonArret = new() { Text = "Arrêt", AutoSize = true };

    public FormArret(CancellationTokenSource arret)
    {
        _arret = arret;
        Text = TitreApplication;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(260, 44);
        MinimumSize = Size;

        var barre = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        barre.Controls.Add(_compteur);
        barre.Controls.Add(_boutonArret);
        Controls.Add(barre);

        _boutonArret.Click += (_, _) => Demander();
        Load += (_, _) => Positionner();
    }

    protected override bool ShowWithoutActivation => true;

    public void MettreAJour(int nombre) =>
        _compteur.Text = nombre > 1 ? $"{nombre} héros lus" : $"{nombre} héros lu";

    private void Demander()
    {
        _boutonArret.Enabled = false;
        _compteur.Text += " (arrêt en cours…)";
        _arret.Cancel();
    }

    private void Positionner()
    {
        var zone = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(zone.Left + (zone.Width - Width) / 2, zone.Bottom - Height - MargeBas);
    }
}
