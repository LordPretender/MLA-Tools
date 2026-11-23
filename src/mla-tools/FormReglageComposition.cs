namespace MlaTools;

/// <summary>Dialogue des réglages propres à la composition en cours, un onglet par critère ; ils valent pour toutes ses équipes, qui peuvent les durcir dans leurs propres réglages.</summary>
internal sealed class FormReglageComposition : FormDialogue
{
    private const string OngletLienAme = "Lien d'âme";
    private const string OngletEtoiles = "Étoiles";
    private const string LibelleReinitialiser = "Réinitialiser";
    private const int LargeurChamp = 70;

    private readonly NumericUpDown _etoilesMiniHeros = new() { Minimum = 0, Maximum = FormOptionsGeneration.EtoilesMinimumMax, Width = LargeurChamp };
    private readonly CheckBox _lienAmeActif = new() { AutoSize = true, Margin = new Padding(3, 6, 3, 3) };

    public FormReglageComposition(int etoilesMiniHeros, bool lienAmeActif)
        : base("Réglages de la composition", new Size(440, 260))
    {
        _etoilesMiniHeros.Value = Math.Clamp(etoilesMiniHeros, 0, FormOptionsGeneration.EtoilesMinimumMax);
        _lienAmeActif.Checked = lienAmeActif;

        var onglets = new TabControl { Dock = DockStyle.Fill };
        onglets.TabPages.Add(OutilsDialogue.PageChamps(OngletLienAme, table => OutilsDialogue.AjouterChampAvecAide(
            table, "Lien d'âme actif", _lienAmeActif, "Décocher pour un mode de jeu sans lien d'âme (ex. Appel de l'aube) : un héros avec lien d'âme ne compte plus comme Ordre pour la résonance.")));
        onglets.TabPages.Add(OutilsDialogue.PageChamps(OngletEtoiles, table => OutilsDialogue.AjouterChampAvecAide(
            table, "Étoiles mini pour les héros", _etoilesMiniHeros, "Aucun héros d'une équipe n'a moins d'étoiles que ce seuil (0 = aucun seuil). Une équipe peut le relever dans ses propres réglages, jamais le baisser.")));

        var boutons = ConstruireBoutons(() => DialogResult = DialogResult.OK);
        var reinitialiser = new Button { Text = LibelleReinitialiser, AutoSize = true };
        reinitialiser.Click += (_, _) =>
        {
            var parDefaut = new ReglagesEquipes();
            _etoilesMiniHeros.Value = parDefaut.EtoilesMiniHerosComposition;
            _lienAmeActif.Checked = parDefaut.LienAmeActif;
        };
        boutons.Controls.Add(reinitialiser);
        Controls.Add(onglets);
        Controls.Add(boutons);
    }

    /// <summary>Seuil d'étoiles par héros pour toutes les équipes ; 0 = aucun.</summary>
    public int EtoilesMiniHeros => (int)_etoilesMiniHeros.Value;

    /// <summary>Faux pour un mode de jeu sans lien d'âme.</summary>
    public bool LienAmeActif => _lienAmeActif.Checked;
}
