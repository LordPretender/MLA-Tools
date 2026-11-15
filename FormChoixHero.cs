using static MlaTools.Constantes;

namespace MlaTools;

/// <summary>Dialogue de choix d'un héros dans une liste filtrable par nom.</summary>
internal sealed class FormChoixHero : FormDialogue
{
    private readonly TextBox _recherche = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher un héros" };
    private readonly ListBox _liste = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly IReadOnlyList<HeroStocke> _candidats;

    public FormChoixHero(string titre, IReadOnlyList<HeroStocke> candidats)
        : base(titre, new Size(380, 380))
    {
        _candidats = candidats;

        _recherche.TextChanged += (_, _) => Filtrer();
        _liste.DoubleClick += (_, _) => Valider();

        var zone = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        zone.Controls.Add(_liste);
        zone.Controls.Add(_recherche);
        Controls.Add(zone);
        Controls.Add(ConstruireBoutons(Valider));
        Filtrer();
    }

    public int? Resultat { get; private set; }

    private void Filtrer()
    {
        var recherche = Referentiel.Normaliser(_recherche.Text.Trim());
        _liste.Items.Clear();
        _liste.Items.AddRange([.. _candidats
            .Where(c => Referentiel.Normaliser(c.Hero.Nom).Contains(recherche, StringComparison.Ordinal))
            .Select(c => new Choix(c.Id, c.Hero.Nom))]);
        if (_liste.Items.Count > 0)
        {
            _liste.SelectedIndex = 0;
        }
    }

    private void Valider()
    {
        if (_liste.SelectedItem is not Choix choix)
        {
            MessageBox.Show(this, "Choisis un héros dans la liste.", TitreApplication, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Resultat = choix.Id;
        DialogResult = DialogResult.OK;
    }

    private sealed record Choix(int Id, string Nom)
    {
        public override string ToString() => Nom;
    }
}
