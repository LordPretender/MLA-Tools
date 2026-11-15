using static MlaTools.Constantes;

namespace MlaTools;

/// <summary>Dialogue de saisie d'un nom unique ; l'unicité est vérifiée par l'appelant, seul à connaître la base.</summary>
/// <remarks>Quand <c>nombreEquipesInitial</c> est fourni, le dialogue demande aussi un nombre d'équipes.</remarks>
internal sealed class FormSaisieNom : FormDialogue
{
    private const int HauteurNom = 90;
    private const int HauteurNomEtEquipes = 124;

    private readonly TextBox _nom = new() { Dock = DockStyle.Fill };

    private readonly NumericUpDown _equipes = new() { Minimum = 1, Maximum = FormOptionsGeneration.NombreEquipesMax, Width = 60 };

    private readonly Predicate<string> _nomPris;

    private readonly string _messageNomPris;

    public FormSaisieNom(string titre, string nomInitial, string messageNomPris, Predicate<string> nomPris, int? nombreEquipesInitial = null)
        : base(titre, new Size(380, nombreEquipesInitial is null ? HauteurNom : HauteurNomEtEquipes))
    {
        _nomPris = nomPris;
        _messageNomPris = messageNomPris;

        _nom.Text = nomInitial;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        OutilsDialogue.AjouterLigne(table, "Nom", _nom);
        if (nombreEquipesInitial is { } initial)
        {
            _equipes.Value = Math.Clamp(initial, (int)_equipes.Minimum, (int)_equipes.Maximum);
            OutilsDialogue.AjouterLigne(table, "Équipes", _equipes);
        }
        Controls.Add(table);
        Controls.Add(ConstruireBoutons(Valider));
    }

    public string? Resultat { get; private set; }

    public int NombreEquipes => (int)_equipes.Value;

    private void Valider()
    {
        var nom = _nom.Text.Trim();
        var erreur = nom.Length == 0 ? "Le nom est obligatoire."
            : _nomPris(nom) ? _messageNomPris
            : null;
        if (erreur is not null)
        {
            MessageBox.Show(this, erreur, TitreApplication, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Resultat = nom;
        DialogResult = DialogResult.OK;
    }
}
