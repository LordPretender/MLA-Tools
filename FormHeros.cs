using System.Data.Common;
using static MlaTools.Constantes;

namespace MlaTools;

// Tableau éditable : la lecture OCR est une proposition, l'utilisateur corrige avant tout enregistrement en base.
internal sealed class FormHeros : FormBase
{
    private const string SeparateurSaisie = ",";
    private const string SeparateurFactions = SeparateurSaisie + " ";
    private const string SeparateurAlertes = " ; ";
    private const string ColOrdre = "Ordre";
    private const string ColNom = "Nom";
    private const string ColEtoiles = "Etoiles";
    private const string ColFactions = "Factions";
    private const string ColClasse = "Classe";
    private const string ColEquipement = "Equipement";
    private const string ColARevoir = "ARevoir";
    private const string ColEtat = "Etat";
    private const string LibelleAjout = "Ajout";
    private const string LibelleModification = "Modification";
    private const string LibelleInchange = "Inchangé";

    private readonly DataGridView _grille = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
    };

    private readonly bool _modeListe;

    // Héros en base par nom normalisé, lus une fois à l'ouverture puis après chaque enregistrement ; null = base illisible, colonne « État » laissée vide.
    private Dictionary<string, Hero>? _herosConnus;

    private static readonly string[] ColonnesEtat = [ColNom, ColEtoiles, ColFactions, ColClasse, ColEquipement];

    private readonly Button _boutonEnregistrer = new() { AutoSize = true };

    private readonly Button _boutonFermer = new() { Text = "Fermer", AutoSize = true, DialogResult = DialogResult.Cancel };

    public FormHeros(IReadOnlyList<HeroLu> heros)
    {
        _modeListe = heros.Count > 1;
        _boutonEnregistrer.Text = _modeListe ? "Tout enregistrer" : "Enregistrer";
        Text = _modeListe ? $"{TitreApplication} — {heros.Count} héros lus" : TitreApplication;
        ClientSize = new Size(900, 360);
        MinimumSize = SizeFromClientSize(new Size(640, 240));
        CancelButton = _boutonFermer;

        AjouterColonnes();
        ChargerHerosConnus();
        // Ouverture dans l'ordre de lecture (« # ») ; un clic sur « À revoir » regroupe les lignes à corriger.
        foreach (var (hero, ordre) in heros.Select((h, i) => (h, i + 1)))
        {
            AjouterLigne(hero, ordre);
        }
        _grille.Columns[ColOrdre].HeaderCell.SortGlyphDirection = SortOrder.Ascending;
        // Abonné après le remplissage initial : AjouterLigne calcule déjà la colonne pour les lignes lues.
        _grille.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && ColonnesEtat.Contains(_grille.Columns[e.ColumnIndex].Name))
            {
                ActualiserEtat(_grille.Rows[e.RowIndex]);
            }
        };

        var barre = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
        barre.Controls.Add(_boutonEnregistrer);
        barre.Controls.Add(_boutonFermer);
        Controls.Add(_grille);
        Controls.Add(barre);

        _boutonEnregistrer.Click += (_, _) => Enregistrer();
    }

    private void Enregistrer()
    {
        _grille.EndEdit();
        var valides = new List<(DataGridViewRow Ligne, Hero Hero)>();
        var refusees = new List<string>();
        foreach (DataGridViewRow ligne in _grille.Rows)
        {
            var hero = LireLigne(ligne, out var erreur);
            if (hero is null)
            {
                refusees.Add($"ligne {ligne.Index + 1} : {erreur}");
            }
            else
            {
                valides.Add((ligne, hero));
            }
        }

        // Une ligne fautive ne bloque pas les autres : elle reste dans le tableau pour être corrigée.
        var message = new List<string>();
        if (valides.Count > 0 && !TenterEnregistrement(valides, message))
        {
            return;
        }
        if (refusees.Count > 0)
        {
            message.Add($"Non enregistrés, à corriger :\n{string.Join("\n", refusees)}");
        }
        MessageBox.Show(this, string.Join("\n\n", message), TitreApplication);
    }

    // Retourne false après avoir signalé une erreur de base : le tableau reste intact.
    private bool TenterEnregistrement(List<(DataGridViewRow Ligne, Hero Hero)> valides, List<string> message)
    {
        try
        {
            var resultat = BaseSqlite.Enregistrer(valides.Select(v => v.Hero), BaseSqlite.CheminParDefaut);
            message.Add($"{resultat.Ajoutes.Count} héros ajouté(s), {resultat.MisAJour.Count} mis à jour, {resultat.Inchanges.Count} inchangé(s).");
            // Un héros qui vient d'être écrit ne doit pas rester marqué « Ajout » ou « Modification » dans les lignes encore affichées.
            ChargerHerosConnus();
            foreach (DataGridViewRow ligne in _grille.Rows)
            {
                ActualiserEtat(ligne);
            }

            // En mode liste, les lignes enregistrées (ajoutées ou mises à jour) sortent du tableau : il ne reste que le travail à faire.
            if (_modeListe)
            {
                foreach (var (ligne, _) in valides)
                {
                    _grille.Rows.Remove(ligne);
                }
            }
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or DbException)
        {
            MessageBox.Show(this, ex.Message, TitreApplication, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    // Une cellule complétée à la main peut être vide ou fautive : la ligne est refusée plutôt qu'écrite en base.
    private static Hero? LireLigne(DataGridViewRow ligne, out string erreur)
    {
        var nom = ((string?)ligne.Cells[ColNom].Value)?.Trim() ?? "";
        var classe = (string?)ligne.Cells[ColClasse].Value;
        var equipement = (string?)ligne.Cells[ColEquipement].Value;
        var etoiles = ligne.Cells[ColEtoiles].Value as int?;

        var textesFactions = ((string?)ligne.Cells[ColFactions].Value ?? "")
            .Split(SeparateurSaisie, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var factions = textesFactions.Select(t => Referentiel.Rapprocher(t, Referentiel.Factions)).ToList();

        erreur = "";
        if (nom.Length == 0)
        {
            erreur = "nom manquant.";
        }
        else if (etoiles is null)
        {
            erreur = "palier d'étoiles manquant.";
        }
        else if (factions.Count == 0 || factions.Any(f => f is null))
        {
            erreur = "faction manquante ou inconnue.";
        }
        else if (classe is null || equipement is null)
        {
            erreur = "classe ou équipement manquant.";
        }
        return erreur.Length > 0 ? null : new Hero(nom, etoiles!.Value, factions!, classe!, equipement!);
    }

    private void AjouterColonnes()
    {
        _grille.Columns.Add(new DataGridViewTextBoxColumn { Name = ColOrdre, HeaderText = "#", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        _grille.Columns.Add(new DataGridViewTextBoxColumn { Name = ColNom, HeaderText = "Nom", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        _grille.Columns.Add(Liste(ColEtoiles, "Étoiles", LecteurEtoiles.Paliers, nameof(Palier.Libelle), nameof(Palier.Valeur)));
        _grille.Columns.Add(new DataGridViewTextBoxColumn { Name = ColFactions, HeaderText = "Factions", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        _grille.Columns.Add(Liste(ColClasse, "Classe", Referentiel.Classes));
        _grille.Columns.Add(Liste(ColEquipement, "Équipement", Referentiel.Equipements));
        _grille.Columns.Add(new DataGridViewTextBoxColumn { Name = ColEtat, HeaderText = "État", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        // Dernière colonne : elle absorbe la largeur excédentaire quand la fenêtre est maximisée.
        _grille.Columns.Add(new DataGridViewTextBoxColumn { Name = ColARevoir, HeaderText = "À revoir", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
    }

    private static DataGridViewComboBoxColumn Liste(string nom, string titre, object source, string? affichage = null, string? valeur = null) =>
        new()
        {
            Name = nom,
            HeaderText = titre,
            DataSource = source,
            DisplayMember = affichage ?? "",
            ValueMember = valeur ?? "",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
            FlatStyle = FlatStyle.Flat,
        };

    // Une base illisible ne doit pas empêcher de contrôler ni d'enregistrer la lecture : seule la colonne « État » reste vide.
    private void ChargerHerosConnus()
    {
        try
        {
            _herosConnus = BaseSqlite.ListerHeros(BaseSqlite.CheminParDefaut)
                .ToDictionary(h => Referentiel.Normaliser(h.Hero.Nom), h => h.Hero);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or DbException)
        {
            _herosConnus = null;
        }
    }

    // Même règle que BaseSqlite.Enregistrer : appariement par nom normalisé, puis comparaison des valeurs. Une ligne incomplète n'a pas d'état : elle serait refusée.
    private void ActualiserEtat(DataGridViewRow ligne)
    {
        var hero = LireLigne(ligne, out _);
        string etat;
        if (_herosConnus is null || hero is null)
        {
            etat = "";
        }
        else if (!_herosConnus.TryGetValue(Referentiel.Normaliser(hero.Nom), out var stocke))
        {
            etat = LibelleAjout;
        }
        else
        {
            etat = stocke.Identique(hero) ? LibelleInchange : LibelleModification;
        }
        ligne.Cells[ColEtat].Value = etat;
    }

    private void AjouterLigne(HeroLu hero, int ordre)
    {
        var index = _grille.Rows.Add();
        var ligne = _grille.Rows[index];
        ligne.Cells[ColOrdre].Value = ordre;
        ligne.Cells[ColNom].Value = hero.Nom;
        ligne.Cells[ColFactions].Value = string.Join(SeparateurFactions, hero.Factions);
        ligne.Cells[ColARevoir].Value = string.Join(SeparateurAlertes, hero.Alertes);

        // Une cellule de liste refuse toute valeur absente de sa source : une lecture ratée reste vide.
        if (hero.Etoiles is { } etoiles)
        {
            ligne.Cells[ColEtoiles].Value = etoiles;
        }
        if (hero.Classe is not null)
        {
            ligne.Cells[ColClasse].Value = hero.Classe;
        }
        if (hero.Equipement is not null)
        {
            ligne.Cells[ColEquipement].Value = hero.Equipement;
        }
        ActualiserEtat(ligne);
    }
}
