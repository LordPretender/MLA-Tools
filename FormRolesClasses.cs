namespace MlaTools;

/// <summary>Dialogue qui associe un rôle (DPS, Tank, Soutien) à chaque classe ; chaque changement est enregistré aussitôt par l'appelant.</summary>
internal sealed class FormRolesClasses : FormDialogue
{
    private const string ColClasse = "Classe";
    private const string ColRole = "Role";

    private readonly DataGridView _grille = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None,
    };

    private readonly Dictionary<string, string> _roles;

    // Retourne false quand l'enregistrement a échoué : la grille revient alors au rôle précédent.
    private readonly Func<string, string, bool> _enregistrer;

    private bool _chargement;

    public FormRolesClasses(IReadOnlyDictionary<string, string> roles, Func<string, string, bool> enregistrer)
        : base("Rôle de chaque classe", new Size(320, 480))
    {
        _roles = new Dictionary<string, string>(roles);
        _enregistrer = enregistrer;

        AjouterColonnes();
        Remplir();
        // Une liste déroulante ne valide sa valeur qu'en quittant la cellule : on force la validation au choix.
        _grille.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grille.IsCurrentCellDirty)
            {
                _grille.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _grille.CellValueChanged += ChangerRole;

        Controls.Add(_grille);
        Controls.Add(ConstruireBoutonFermer());
    }

    /// <summary>Vrai dès qu'un rôle a été changé : l'appelant recalcule alors ce qui en dépend.</summary>
    public bool Modifie { get; private set; }

    private void AjouterColonnes()
    {
        _grille.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ColClasse,
            HeaderText = "Classe",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
        });
        _grille.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = ColRole,
            HeaderText = "Rôle",
            DataSource = MoteurEquipes.Roles,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FlatStyle = FlatStyle.Flat,
        });
    }

    private void Remplir()
    {
        _chargement = true;
        try
        {
            foreach (var classe in Referentiel.Classes)
            {
                _grille.Rows.Add(classe, RoleDe(classe));
            }
        }
        finally
        {
            _chargement = false;
        }
    }

    private string RoleDe(string classe)
    {
        var role = _roles.GetValueOrDefault(classe, MoteurEquipes.RoleDps);
        return MoteurEquipes.Roles.Contains(role) ? role : MoteurEquipes.RoleDps;
    }

    private void ChangerRole(object? sender, DataGridViewCellEventArgs e)
    {
        if (_chargement || e.RowIndex < 0 || _grille.Columns[e.ColumnIndex].Name != ColRole)
        {
            return;
        }

        var ligne = _grille.Rows[e.RowIndex];
        var classe = (string)ligne.Cells[ColClasse].Value;
        if (ligne.Cells[ColRole].Value is not string role)
        {
            return;
        }

        if (_enregistrer(classe, role))
        {
            _roles[classe] = role;
            Modifie = true;
            return;
        }

        // La base garde l'ancien rôle : la grille y revient, sans relancer cet événement.
        _chargement = true;
        try
        {
            ligne.Cells[ColRole].Value = RoleDe(classe);
        }
        finally
        {
            _chargement = false;
        }
    }

    private FlowLayoutPanel ConstruireBoutonFermer()
    {
        var fermer = new Button { Text = "Fermer", AutoSize = true, DialogResult = DialogResult.OK };
        AcceptButton = fermer;
        CancelButton = fermer;
        return OutilsDialogue.BarreBoutons(fermer);
    }
}
