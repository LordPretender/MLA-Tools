using static MlaTools.Constantes;

namespace MlaTools;

/// <summary>Formulaire d'ajout ou de modification d'un héros ; le nom est vérifié par l'appelant, seul à connaître la base.</summary>
internal sealed class FormSaisieHero : FormDialogue
{
    // La ligne « Dernière mise à jour » de la modification ajoute environ 30 px.
    private const int HauteurAjout = 330;
    private const int HauteurModification = 360;

    private readonly TextBox _nom = new() { Dock = DockStyle.Fill };

    private readonly ComboBox _etoiles = Liste();

    private readonly ComboBox _classe = Liste();

    private readonly ComboBox _equipement = Liste();

    private readonly List<CheckBox> _factions = [.. Referentiel.Factions.Select(f => new CheckBox { Text = f, AutoSize = true })];

    private readonly Predicate<string> _nomPris;

    private readonly bool _modification;

    private readonly DateTime? _dateMaj;

    /// <param name="dateMaj">Affichée en lecture seule en modification ; sans objet à l'ajout.</param>
    public FormSaisieHero(Hero? hero, Predicate<string> nomPris, DateTime? dateMaj = null)
        : base(hero is null ? "Ajouter un héros" : "Modifier un héros", new Size(420, hero is null ? HauteurAjout : HauteurModification))
    {
        _nomPris = nomPris;
        _modification = hero is not null;
        _dateMaj = dateMaj;

        // DisplayMember et ValueMember doivent précéder DataSource, sans quoi la liste affiche le type de l'objet.
        _etoiles.DisplayMember = nameof(Palier.Libelle);
        _etoiles.ValueMember = nameof(Palier.Valeur);
        _etoiles.DataSource = LecteurEtoiles.Paliers;
        _classe.DataSource = Referentiel.Classes;
        _equipement.DataSource = Referentiel.Equipements;

        Remplir(hero);
        Controls.Add(ConstruireChamps());
        Controls.Add(ConstruireBoutons(Valider));
    }

    public Hero? Resultat { get; private set; }

    private static ComboBox Liste() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };

    private void Remplir(Hero? hero)
    {
        if (hero is null)
        {
            return;
        }
        _nom.Text = hero.Nom;
        _etoiles.SelectedValue = hero.Etoiles;
        _classe.SelectedItem = hero.Classe;
        _equipement.SelectedItem = hero.Equipement;
        foreach (var faction in _factions)
        {
            faction.Checked = hero.Factions.Contains(faction.Text);
        }
    }

    private TableLayoutPanel ConstruireChamps()
    {
        var table = OutilsDialogue.TableChamps(10);


        var cases = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        cases.Controls.AddRange([.. _factions]);

        OutilsDialogue.AjouterLigne(table, "Nom", _nom);
        OutilsDialogue.AjouterLigne(table, "Étoiles", _etoiles);
        OutilsDialogue.AjouterLigne(table, "Classe", _classe);
        OutilsDialogue.AjouterLigne(table, "Équipement", _equipement);
        OutilsDialogue.AjouterLigne(table, "Factions", cases);
        if (_modification)
        {
            OutilsDialogue.AjouterLigne(table, "Dernière mise à jour",
                new Label { Text = OutilsDialogue.LibelleDate(_dateMaj), AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
        }
        return table;
    }

    private void Valider()
    {
        var nom = _nom.Text.Trim();
        var factions = _factions.Where(c => c.Checked).Select(c => c.Text).ToList();
        var erreur = nom.Length == 0 ? "Le nom est obligatoire."
            : factions.Count == 0 ? "Au moins une faction est obligatoire."
            : _nomPris(nom) ? "Un héros porte déjà ce nom."
            : null;
        if (erreur is not null)
        {
            MessageBox.Show(this, erreur, TitreApplication, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Resultat = new Hero(nom, (int)_etoiles.SelectedValue!, factions, (string)_classe.SelectedItem!, (string)_equipement.SelectedItem!);
        DialogResult = DialogResult.OK;
    }
}
