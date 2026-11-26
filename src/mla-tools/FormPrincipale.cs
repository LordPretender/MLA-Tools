using System.Globalization;
using System.Text.Json;
using static MlaTools.Constantes;

namespace MlaTools;

internal sealed class FormPrincipale : FormBase
{
    private const string LibelleSimple = "Calibrer un héros normal";
    private const string LibelleHybride = "Calibrer un héros hybride";
    private const string LibelleFleche = "Calibrer la flèche suivante";
    private const string LibelleTesterFleche = "Tester la flèche";
    private const string MessageChoisirFenetre = "Choisissez d'abord la fenêtre du jeu.";
    private const string EtatFait = "fait";
    private const string EtatAFaire = "à faire";
    private const int NombreHerosParDefaut = 50;
    private const int PasDelaiMs = 100;

    private readonly Font _police;
    private readonly List<(IntPtr Fenetre, string Titre)> _fenetresListees = [];
    private readonly ComboBox _selecteurFenetre = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
    private readonly Button _boutonRafraichir = new() { Text = "Rafraîchir", AutoSize = true };
    private readonly Button _boutonSimple = new() { AutoSize = true };
    private readonly Button _boutonHybride = new() { AutoSize = true };
    private readonly Button _boutonFleche = new() { AutoSize = true };
    private readonly Button _boutonTesterFleche = new() { Text = LibelleTesterFleche, AutoSize = true };
    private readonly Button _boutonLire = new() { Text = "Lire le héros affiché", AutoSize = true };
    private readonly Button _boutonLireTous = new() { Text = "Lire les héros", AutoSize = true };
    private readonly CheckBox _lireCompetences = new() { Text = "Lire aussi les compétences (recensement des tags)", AutoSize = true };
    private readonly NumericUpDown _nombreHeros = new()
    {
        Minimum = 1,
        Maximum = Scanner.LimiteHeros,
        Value = NombreHerosParDefaut,
        Width = 60,
    };
    private readonly NumericUpDown _delaiApresClic = new()
    {
        Minimum = Navigation.DelaiMinMs,
        Maximum = Navigation.DelaiMaxMs,
        Increment = PasDelaiMs,
        Value = Navigation.DelaiParDefautMs,
        Width = 70,
    };

    public FormPrincipale()
    {
        _police = new Font(OutilsDialogue.NomPolice, OutilsDialogue.TaillePolice);
        Font = _police;
        BackColor = OutilsDialogue.FondFenetre;
        Text = TitreApplication;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        ClientSize = new Size(580, 580);
        MinimumSize = Size;
        Controls.Add(ConstruireColonne());
        AbonnerEvenements();
    }

    private TableLayoutPanel ConstruireColonne()
    {
        var consigne = new Label
        {
            Text = "1. Dans le jeu, affichez la fiche « Info » d'un héros, puis cliquez sur le bouton de cette fiche "
                 + "qui réduit la taille des héros (sans cela, les noms sont mal lus).\n"
                 + "2. Choisissez ci-dessous la fenêtre du jeu.\n"
                 + "3. Cliquez sur le bouton qui correspond au héros affiché (un héros « Hybride » a deux factions).",
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Margin = new Padding(0, 0, 0, 12),
        };

        // Une colonne pleine largeur dont chaque ligne suit son contenu : maximisée, les groupes s'étirent sans disperser les boutons.
        var colonne = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12), AutoScroll = true };
        colonne.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        colonne.Controls.Add(consigne);

        var ligneFenetre = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        ligneFenetre.Controls.Add(_selecteurFenetre);
        ligneFenetre.Controls.Add(_boutonRafraichir);
        colonne.Controls.Add(OutilsDialogue.Groupe("Fenêtre du jeu", ligneFenetre));

        colonne.Controls.Add(OutilsDialogue.Groupe("Calibrage",
            OutilsDialogue.Pile(_boutonSimple, _boutonHybride, _boutonFleche, _boutonTesterFleche)));

        var ligneLireTous = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        ligneLireTous.Controls.Add(_boutonLireTous);
        ligneLireTous.Controls.Add(new Label { Text = "Nombre de héros à lire :", AutoSize = true, Margin = new Padding(12, 8, 3, 0) });
        ligneLireTous.Controls.Add(_nombreHeros);

        var ligneDelai = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        ligneDelai.Controls.Add(new Label { Text = "Délai après chaque clic (ms) :", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
        ligneDelai.Controls.Add(_delaiApresClic);
        colonne.Controls.Add(OutilsDialogue.Groupe("Lecture", OutilsDialogue.Pile(_boutonLire, ligneLireTous, _lireCompetences, ligneDelai)));
        return colonne;
    }

    private void AbonnerEvenements()
    {
        _boutonRafraichir.Click += (_, _) => RafraichirFenetres();
        _boutonSimple.Click += (_, _) => Calibrer(Dispositions.Simple);
        _boutonHybride.Click += (_, _) => Calibrer(Dispositions.Hybride);
        _boutonFleche.Click += (_, _) => CalibrerFleche();
        _boutonTesterFleche.Click += (_, _) => TesterFleche();
        _boutonLire.Click += OnLireHeros;
        _boutonLireTous.Click += OnLireTous;
        Load += (_, _) =>
        {
            RafraichirFenetres();
            MettreAJourEtats();
            RestaurerReglagesLecture();
        };
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _police.Dispose();
        }
    }

    private ConfigZones LireConfig()
    {
        try
        {
            return ConfigZones.Charger();
        }
        catch (Exception ex) when (EstErreurAttendue(ex))
        {
            AfficherAvertissement(ex.Message);
            return new ConfigZones();
        }
    }

    private static bool EstErreurAttendue(Exception ex) =>
        BaseSqlite.EstErreurAcces(ex) || ex is JsonException;

    // Une base illisible ou une valeur absente laisse les valeurs par défaut : ces réglages ne valent pas un blocage.
    private void RestaurerReglagesLecture()
    {
        Dictionary<string, string> parametres;
        try
        {
            parametres = BaseSqlite.LireParametres(BaseSqlite.CheminParDefaut);
        }
        catch (Exception ex) when (EstErreurAttendue(ex))
        {
            return;
        }

        Restaurer(_nombreHeros, parametres, BaseSqlite.CleNombreHerosALire);
        Restaurer(_delaiApresClic, parametres, BaseSqlite.CleDelaiApresClic);
    }

    private static void Restaurer(NumericUpDown champ, Dictionary<string, string> parametres, string cle)
    {
        if (parametres.TryGetValue(cle, out var texte) && int.TryParse(texte, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valeur))
        {
            champ.Value = Math.Clamp(valeur, champ.Minimum, champ.Maximum);
        }
    }

    private void MemoriserReglagesLecture()
    {
        try
        {
            BaseSqlite.EcrireParametre(BaseSqlite.CleNombreHerosALire, _nombreHeros.Value.ToString(CultureInfo.InvariantCulture), BaseSqlite.CheminParDefaut);
            BaseSqlite.EcrireParametre(BaseSqlite.CleDelaiApresClic, _delaiApresClic.Value.ToString(CultureInfo.InvariantCulture), BaseSqlite.CheminParDefaut);
        }
        catch (Exception ex) when (EstErreurAttendue(ex))
        {
            AfficherAvertissement($"Réglages de lecture non mémorisés : {ex.Message}");
        }
    }

    private void AfficherAvertissement(string message) =>
        MessageBox.Show(this, message, TitreApplication, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private void RafraichirFenetres()
    {
        var titreVoulu = (string?)_selecteurFenetre.SelectedItem;
        if (titreVoulu is null)
        {
            var memorise = LireConfig().TitreFenetre;
            titreVoulu = memorise.Length > 0 ? memorise : null;
        }

        _fenetresListees.Clear();
        _selecteurFenetre.Items.Clear();
        foreach (var fenetre in FenetreCapture.ListerFenetres().Where(f => f.Fenetre != Handle))
        {
            _fenetresListees.Add(fenetre);
            _selecteurFenetre.Items.Add(fenetre.Titre);
        }

        _selecteurFenetre.SelectedIndex = titreVoulu is null ? -1 : _selecteurFenetre.Items.IndexOf(titreVoulu);
    }

    private void MettreAJourEtats()
    {
        var config = LireConfig();
        _boutonSimple.Text = $"{LibelleSimple} ({Etat(config, Dispositions.Simple)})";
        _boutonHybride.Text = $"{LibelleHybride} ({Etat(config, Dispositions.Hybride)})";
        var flecheFaite = config.Globales.ContainsKey(Dispositions.ZoneFlecheSuivante);
        _boutonFleche.Text = $"{LibelleFleche} ({(flecheFaite ? EtatFait : EtatAFaire)})";
    }

    private static string Etat(ConfigZones config, string disposition) =>
        config.Dispositions.ContainsKey(disposition) ? EtatFait : EtatAFaire;

    // Retourne null après avoir signalé le problème à l'utilisateur.
    private Bitmap? CapturerJeu(out string titre)
    {
        titre = "";
        if (FenetreChoisie() is not { } fenetre)
        {
            return null;
        }

        titre = _fenetresListees[_selecteurFenetre.SelectedIndex].Titre;
        Bitmap? image = null;
        var erreur = MasquerPendant(() =>
        {
            FenetreCapture.AmenerAuPremierPlan(fenetre);
            image = FenetreCapture.Capturer(fenetre);
        });

        if (image is null)
        {
            AfficherAvertissement(erreur!);
        }
        return image;
    }

    // Retourne null après avoir averti l'utilisateur.
    private IntPtr? FenetreChoisie()
    {
        if (_selecteurFenetre.SelectedIndex < 0)
        {
            AfficherAvertissement(MessageChoisirFenetre);
            return null;
        }
        return _fenetresListees[_selecteurFenetre.SelectedIndex].Fenetre;
    }

    // Cette fenêtre se réduit pour ne pas apparaître dans la capture du jeu ni recevoir le clic à sa place.
    // Hide() sur une fenêtre ouverte par ShowDialog la ferme : le Show() suivant lève alors ObjectDisposedException.
    // Retourne le message d'erreur attendue, ou null si l'action a réussi.
    private string? MasquerPendant(Action action)
    {
        WindowState = FormWindowState.Minimized;
        try
        {
            action();
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
        finally
        {
            WindowState = FormWindowState.Normal;
        }
    }

    private void TesterFleche()
    {
        if (FenetreChoisie() is not { } fenetre)
        {
            return;
        }

        var config = LireConfig();
        var erreur = MasquerPendant(() => Navigation.CliquerFleche(fenetre, config));
        if (erreur is not null)
        {
            AfficherAvertissement(erreur);
        }
    }

    private async void OnLireHeros(object? sender, EventArgs e)
    {
        var config = LireConfig();
        if (config.Dispositions.Count == 0)
        {
            AfficherAvertissement("Calibrez d'abord au moins une disposition.");
            return;
        }

        using var image = CapturerJeu(out _);
        if (image is null)
        {
            return;
        }

        _boutonLire.Enabled = false;
        try
        {
            var hero = await LecteurHeros.LireAsync(image, config);
            using var formulaire = new FormHeros([hero]);
            formulaire.ShowDialog(this);
        }
        catch (Exception ex) when (EstErreurAttendue(ex))
        {
            AfficherAvertissement(ex.Message);
        }
        finally
        {
            _boutonLire.Enabled = true;
        }
    }

    private async void OnLireTous(object? sender, EventArgs e)
    {
        var config = LireConfig();
        if (config.Dispositions.Count == 0 || !config.Globales.ContainsKey(Dispositions.ZoneFlecheSuivante))
        {
            AfficherAvertissement("Calibrez d'abord au moins une disposition et la flèche suivante.");
            return;
        }
        if (_lireCompetences.Checked && !Dispositions.ZonesGlobales.All(config.Globales.ContainsKey))
        {
            AfficherAvertissement($"Pour lire les compétences, calibrez d'abord les zones globales (bouton « {LibelleFleche} »).");
            return;
        }
        if (FenetreChoisie() is not { } fenetre)
        {
            return;
        }

        MemoriserReglagesLecture();
        _boutonLireTous.Enabled = false;
        var (resultat, erreur) = await LancerScanAsync(fenetre, config, (int)_nombreHeros.Value, (int)_delaiApresClic.Value, _lireCompetences.Checked);
        _boutonLireTous.Enabled = true;

        if (erreur is not null)
        {
            AfficherAvertissement(erreur);
        }
        else if (resultat is not null)
        {
            AfficherResultatScan(resultat);
        }
    }

    // Cette fenêtre se réduit pour ne pas apparaître dans les captures ni recevoir les clics.
    // Hide() sur une fenêtre ouverte par ShowDialog la ferme, d'où l'ObjectDisposedException au Show() suivant.
    private async Task<(ResultatScan? Resultat, string? Erreur)> LancerScanAsync(IntPtr fenetre, ConfigZones config, int nombre, int delaiApresClicMs, bool lireCompetences)
    {
        WindowState = FormWindowState.Minimized;
        using var arret = new CancellationTokenSource();
        using var fenetreArret = new FormArret(arret);
        fenetreArret.Show();
        try
        {
            var progression = new Progress<int>(fenetreArret.MettreAJour);
            return (await Scanner.LireTousAsync(fenetre, config, nombre, delaiApresClicMs, lireCompetences, progression, arret.Token), null);
        }
        catch (Exception ex) when (EstErreurAttendue(ex))
        {
            return (null, ex.Message);
        }
        finally
        {
            fenetreArret.Close();
            WindowState = FormWindowState.Normal;
        }
    }

    private void AfficherResultatScan(ResultatScan resultat)
    {
        var aRevoir = resultat.Heros.Count(h => h.Alertes.Count > 0);
        var motif = resultat.Motif switch
        {
            MotifArret.RetourAuPremier => "retour au premier héros, la liste est complète",
            MotifArret.ClicSansEffet => "le clic sur la flèche n'a pas changé de héros (fin de liste ?)",
            MotifArret.NombreAtteint => "nombre de héros demandé atteint",
            _ => "arrêt demandé",
        };
        MessageBox.Show(this, $"Scan terminé : {motif}.\n{resultat.Heros.Count} héros lus, dont {aRevoir} à revoir.", TitreApplication);

        if (resultat.Heros.Count == 0)
        {
            return;
        }
        using var formulaire = new FormHeros(resultat.Heros.ToList());
        formulaire.ShowDialog(this);
    }

    private void Calibrer(string disposition) =>
        Calibrer((image, titre) => Calibration.Lancer(image, titre, disposition, this));

    private void CalibrerFleche() =>
        Calibrer((image, titre) => Calibration.LancerGlobales(image, titre, this));

    private void Calibrer(Func<Bitmap, string, bool> lancer)
    {
        var image = CapturerJeu(out var titre);
        if (image is null)
        {
            return;
        }

        try
        {
            lancer(image, titre);
        }
        catch (Exception ex) when (EstErreurAttendue(ex))
        {
            AfficherAvertissement(ex.Message);
        }
        MettreAJourEtats();
    }
}
