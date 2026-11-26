using System.Globalization;
using System.Text.Json;

namespace MlaTools;

internal sealed record Zone(int X, int Y, int Largeur, int Hauteur);

internal static class Dispositions
{
    public const string Simple = "simple";
    public const string Hybride = "hybride";

    public const string ZoneNom = "Nom";
    public const string ZoneEtoiles = "Etoiles";
    public const string ZoneEveil = "EveilChiffre";
    public const string ZoneFaction = "Faction";
    public const string ZoneFaction1 = "Faction1";
    public const string ZoneFaction2 = "Faction2";
    public const string ZoneEquipement = "Equipement";
    public const string ZoneClasse = "Classe";

    // Un héros hybride affiche une ligne « Hybride » et deux factions : le bloc d'info descend, d'où deux mises en page.
    public static readonly Dictionary<string, string[]> ZonesParDisposition = new()
    {
        [Simple] = [ZoneNom, ZoneEtoiles, ZoneEveil, ZoneFaction, ZoneEquipement, ZoneClasse],
        [Hybride] = [ZoneNom, ZoneEtoiles, ZoneEveil, ZoneFaction1, ZoneFaction2, ZoneEquipement, ZoneClasse],
    };

    public const string ZoneFlecheSuivante = "FlecheSuivante";

    // La flèche « > » est au même endroit quelle que soit la disposition : elle se calibre une fois, hors des dispositions.
    public const string ZoneOngletCompetence = "OngletCompetence";
    public const string ZoneCompetence1 = "Competence1";
    public const string ZoneCompetence2 = "Competence2";
    public const string ZoneCompetence3 = "Competence3";
    public const string ZoneCompetence4 = "Competence4";
    // Bande du haut de la popup d'une compétence : type à gauche, pastilles de tags à droite.
    public const string ZonePopupTags = "PopupTags";

    public static readonly string[] ZonesCompetences = [ZoneCompetence1, ZoneCompetence2, ZoneCompetence3, ZoneCompetence4];

    // Chaque lot se calibre sur un écran de jeu différent (popup fermée ou ouverte) : trois calibrages, un lot chacun.
    public static readonly string[] LotFleche = [ZoneFlecheSuivante];
    public static readonly string[] LotCompetences = [ZoneOngletCompetence, .. ZonesCompetences];
    public static readonly string[] LotPopup = [ZonePopupTags];

    public static readonly string[] ZonesGlobales = [.. LotFleche, .. LotCompetences, .. LotPopup];

    // Texte affiché dans la fenêtre de calibrage pour la zone à tracer.
    public static readonly Dictionary<string, string> Consignes = new()
    {
        [ZoneNom] = "Nom du héros.",
        [ZoneEtoiles] = "Les étoiles du héros.",
        [ZoneEveil] = "Le chiffre du niveau d'éveil.",
        [ZoneFaction] = "L'icône de faction du héros.",
        [ZoneFaction1] = "L'icône de la première faction.",
        [ZoneFaction2] = "L'icône de la deuxième faction.",
        [ZoneEquipement] = "L'équipement du héros.",
        [ZoneClasse] = "La classe du héros.",
        [ZoneFlecheSuivante] = "La flèche « > » qui passe au héros suivant.",
        [ZoneOngletCompetence] = "L'onglet « Compétence » de la fiche du héros.",
        [ZoneCompetence1] = "L'icône de la 1re compétence.",
        [ZoneCompetence2] = "L'icône de la 2e compétence.",
        [ZoneCompetence3] = "L'icône de la 3e compétence.",
        [ZoneCompetence4] = "L'icône de la 4e compétence.",
        [ZonePopupTags] = "La bande du haut de la popup : le type de la compétence à gauche, les pastilles de tags à droite.",
    };
}

internal sealed class ConfigZones
{
    // Ancien fichier de calibrage, à côté de l'exe publié (hors dépôt) : source de l'import unique vers la base.
    private const string CheminImportZones = @"D:\Jeux\MLA\zones.json";

    private static readonly JsonSerializerOptions OptionsJson = new() { WriteIndented = true };

    public string TitreFenetre { get; set; } = "";
    public int Largeur { get; set; }
    public int Hauteur { get; set; }
    public Dictionary<string, Dictionary<string, Zone>> Dispositions { get; set; } = new();
    public Dictionary<string, Zone> Globales { get; set; } = new();

    public static ConfigZones Charger() => Charger(BaseSqlite.CheminParDefaut, CheminImportZones);

    public static ConfigZones Charger(string cheminBase, string cheminImport)
    {
        // Le marqueur évite de réimporter l'ancien fichier après un effacement volontaire du calibrage.
        if (!BaseSqlite.LireParametres(cheminBase).ContainsKey(BaseSqlite.CleCalibrageImporte) && File.Exists(cheminImport))
        {
            ChargerFichier(cheminImport).Enregistrer(cheminBase);
        }
        return LireBase(cheminBase);
    }

    public void Enregistrer() => Enregistrer(BaseSqlite.CheminParDefaut);

    public void Enregistrer(string cheminBase)
    {
        var parametres = new Dictionary<string, string>
        {
            [BaseSqlite.CleTitreFenetre] = TitreFenetre,
            [BaseSqlite.CleLargeur] = Largeur.ToString(CultureInfo.InvariantCulture),
            [BaseSqlite.CleHauteur] = Hauteur.ToString(CultureInfo.InvariantCulture),
            [BaseSqlite.CleCalibrageImporte] = BaseSqlite.MarqueurPresent,
        };
        var zones = Dispositions
            .SelectMany(d => d.Value.Select(z => new ZoneStockee(d.Key, z.Key, z.Value)))
            .Concat(Globales.Select(z => new ZoneStockee(BaseSqlite.PorteeGlobales, z.Key, z.Value)));
        BaseSqlite.EcrireCalibrage(parametres, zones, cheminBase);
    }

    // Oublie tout le calibrage mais garde le marqueur d'import, pour que l'ancien fichier ne revienne pas.
    public static void Effacer(string cheminBase) => new ConfigZones().Enregistrer(cheminBase);

    private static ConfigZones ChargerFichier(string chemin)
    {
        var config = JsonSerializer.Deserialize<ConfigZones>(File.ReadAllText(chemin), OptionsJson);
        if (config?.Dispositions is null || config.Globales is null || config.TitreFenetre is null)
        {
            throw new InvalidOperationException($"Fichier de zones illisible : {chemin}");
        }
        return config.Valider($"Fichier de zones incohérent : {chemin}");
    }

    private static ConfigZones LireBase(string cheminBase)
    {
        var parametres = BaseSqlite.LireParametres(cheminBase);
        var config = new ConfigZones
        {
            TitreFenetre = parametres.GetValueOrDefault(BaseSqlite.CleTitreFenetre, ""),
            Largeur = LireEntier(parametres, BaseSqlite.CleLargeur),
            Hauteur = LireEntier(parametres, BaseSqlite.CleHauteur),
        };
        foreach (var z in BaseSqlite.LireZones(cheminBase))
        {
            if (z.Portee == BaseSqlite.PorteeGlobales)
            {
                config.Globales[z.Nom] = z.Zone;
            }
            else
            {
                if (!config.Dispositions.TryGetValue(z.Portee, out var zones))
                {
                    zones = config.Dispositions[z.Portee] = new();
                }
                zones[z.Nom] = z.Zone;
            }
        }
        return config.Valider($"Calibrage en base incohérent : {cheminBase}");
    }

    private static int LireEntier(Dictionary<string, string> parametres, string cle) =>
        !parametres.TryGetValue(cle, out var texte) ? 0
        : int.TryParse(texte, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valeur) ? valeur
        : throw new InvalidOperationException($"Paramètre « {cle} » illisible en base : {texte}");

    // Une désérialisation ou une lecture sans erreur ne dit rien des valeurs : les zones servent ensuite à recadrer l'image.
    private ConfigZones Valider(string messageErreur)
    {
        var valide = Largeur >= 0 && Hauteur >= 0
            && Dispositions.Values.All(zones => zones is not null && zones.Values.All(EstDansFenetre))
            && Globales.Values.All(EstDansFenetre);
        return valide ? this : throw new InvalidOperationException(messageErreur);
    }

    private bool EstDansFenetre(Zone? zone) =>
        zone is not null && zone.X >= 0 && zone.Y >= 0 && zone.Largeur > 0 && zone.Hauteur > 0
        && zone.X + zone.Largeur <= Largeur && zone.Y + zone.Hauteur <= Hauteur;

    // Les zones sont des pixels : une image d'une autre taille que celle calibrée donnerait des recadrages et des clics décalés.
    public void VerifierTaille(Size taille, string sujet)
    {
        if (taille.Width != Largeur || taille.Height != Hauteur)
        {
            throw new InvalidOperationException($"{sujet} fait {taille.Width}x{taille.Height} mais la calibration est en {Largeur}x{Hauteur} : rétablissez la taille d'origine.");
        }
    }
}

internal static class Calibration
{
    public static bool Lancer(Bitmap image, string titreFenetre, string disposition, IWin32Window parent) =>
        Executer(image, titreFenetre, parent, Dispositions.ZonesParDisposition[disposition],
            config => config.Dispositions.GetValueOrDefault(disposition),
            (config, zones) => config.Dispositions[disposition] = zones);

    // Seules les zones du lot sont exigées pour enregistrer ; les autres zones globales déjà tracées sont conservées.
    public static bool LancerGlobales(Bitmap image, string titreFenetre, string[] lot, IWin32Window parent) =>
        Executer(image, titreFenetre, parent, lot,
            config => config.Globales,
            (config, zones) => config.Globales = zones);

    // Prend possession de l'image : elle est libérée dans tous les cas.
    private static bool Executer(Bitmap image, string titreFenetre, IWin32Window parent, string[] nomsZones,
        Func<ConfigZones, Dictionary<string, Zone>?> lireZones, Action<ConfigZones, Dictionary<string, Zone>> ecrireZones)
    {
        ConfigZones config;
        try
        {
            config = ConfigZones.Charger();
        }
        catch
        {
            image.Dispose();
            throw;
        }

        // Les zones sont des coordonnées en pixels : deux mises en page calibrées à des tailles différentes seraient incohérentes.
        var tailleDejaCalibree = config.Largeur != 0 && (config.Largeur != image.Width || config.Hauteur != image.Height);
        if (tailleDejaCalibree)
        {
            var message = $"La fenêtre fait {image.Width}x{image.Height} mais la calibration existante est en {config.Largeur}x{config.Hauteur}. Rétablissez la taille d'origine, ou effacez tout le calibrage pour recalibrer.\n\nEffacer tout le calibrage maintenant ?";
            image.Dispose();
            var reponse = MessageBox.Show(parent, message, Constantes.TitreApplication, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (reponse == DialogResult.Yes)
            {
                ConfigZones.Effacer(BaseSqlite.CheminParDefaut);
            }
            return false;
        }

        using var formulaire = new FormCalibration(image, nomsZones, lireZones(config) ?? new());
        if (formulaire.ShowDialog(parent) != DialogResult.OK)
        {
            return false;
        }

        config.TitreFenetre = titreFenetre;
        config.Largeur = formulaire.TailleImage.Width;
        config.Hauteur = formulaire.TailleImage.Height;
        ecrireZones(config, formulaire.Zones);
        config.Enregistrer();
        return true;
    }
}

internal sealed class FormCalibration : FormBase
{
    private readonly Bitmap _image;
    private readonly string[] _nomsZones;
    private readonly Surface _surface;
    private readonly ComboBox _selecteur = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly Label _consigne = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 2, 6, 6) };
    private readonly Button _boutonEnregistrer = new() { Text = "Enregistrer", AutoSize = true, DialogResult = DialogResult.OK };
    private readonly Button _boutonAnnuler = new() { Text = "Annuler", AutoSize = true, DialogResult = DialogResult.Cancel };

    public FormCalibration(Bitmap image, string[] nomsZones, Dictionary<string, Zone> zonesExistantes)
    {
        // Le formulaire devient propriétaire de l'image : elle est libérée avec lui.
        _image = image;
        _nomsZones = nomsZones;
        Zones = new Dictionary<string, Zone>(zonesExistantes);

        Text = "Calibration : choisir une zone puis la tracer à la souris sur l'image";
        ClientSize = new Size(1000, 700);
        MinimumSize = SizeFromClientSize(new Size(640, 400));
        AcceptButton = _boutonEnregistrer;
        CancelButton = _boutonAnnuler;

        _surface = new Surface(image, Zones);
        _surface.ZoneTracee += OnZoneTracee;

        _selecteur.Items.AddRange(nomsZones);
        _selecteur.SelectedIndex = 0;
        _selecteur.SelectedIndexChanged += (_, _) => SelectionnerZone();

        var barre = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
        barre.Controls.Add(new Label { Text = "Zone :", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        barre.Controls.Add(_selecteur);
        barre.Controls.Add(_boutonEnregistrer);
        barre.Controls.Add(_boutonAnnuler);

        // Un libellé automatique ne passe pas à la ligne seul : sa largeur maximale suit celle de la fenêtre.
        Resize += (_, _) => _consigne.MaximumSize = new Size(ClientSize.Width, 0);
        _consigne.MaximumSize = new Size(ClientSize.Width, 0);

        Controls.Add(_surface);
        Controls.Add(_consigne);
        Controls.Add(barre);
        SelectionnerZone();
    }

    public Dictionary<string, Zone> Zones { get; }

    public Size TailleImage => _image.Size;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image.Dispose();
        }
        base.Dispose(disposing);
    }

    private void SelectionnerZone()
    {
        _surface.ZoneCourante = (string?)_selecteur.SelectedItem;
        _consigne.Text = Dispositions.Consignes.GetValueOrDefault(_surface.ZoneCourante ?? "", "");
        _boutonEnregistrer.Enabled = _nomsZones.All(Zones.ContainsKey);
        _surface.Invalidate();
    }

    private void OnZoneTracee(Rectangle rect)
    {
        var nom = (string)_selecteur.SelectedItem!;
        Zones[nom] = new Zone(rect.X, rect.Y, rect.Width, rect.Height);

        var suivante = _nomsZones.FirstOrDefault(n => !Zones.ContainsKey(n));
        if (suivante is not null)
        {
            _selecteur.SelectedItem = suivante;
        }
        SelectionnerZone();
    }

    private sealed class Surface : Panel
    {
        private const int EpaisseurTrait = 2;

        private readonly Bitmap _image;
        private Point _debut;
        private Rectangle _trace = Rectangle.Empty;
        private bool _traceEnCours;

        public Surface(Bitmap image, Dictionary<string, Zone> zones)
        {
            _image = image;
            Zones = zones;
            Dock = DockStyle.Fill;
            AutoScroll = true;
            AutoScrollMinSize = image.Size;
            DoubleBuffered = true;
        }

        public Dictionary<string, Zone> Zones { get; }

        public string? ZoneCourante { get; set; }

        public event Action<Rectangle>? ZoneTracee;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            e.Graphics.DrawImage(_image, 0, 0, _image.Width, _image.Height);

            using var stylo = new Pen(Color.Lime, EpaisseurTrait);
            using var styloCourant = new Pen(Color.Red, EpaisseurTrait);
            foreach (var (nom, zone) in Zones)
            {
                var rect = new Rectangle(zone.X, zone.Y, zone.Largeur, zone.Hauteur);
                e.Graphics.DrawRectangle(nom == ZoneCourante ? styloCourant : stylo, rect);
                e.Graphics.DrawString(nom, Font, Brushes.Yellow, rect.X, rect.Y - Font.Height);
            }

            if (_traceEnCours)
            {
                e.Graphics.DrawRectangle(styloCourant, _trace);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || ZoneCourante is null)
            {
                return;
            }
            _debut = VersImage(e.Location);
            _trace = new Rectangle(_debut, Size.Empty);
            _traceEnCours = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_traceEnCours)
            {
                return;
            }
            var fin = VersImage(e.Location);
            _trace = Rectangle.FromLTRB(
                Math.Min(_debut.X, fin.X), Math.Min(_debut.Y, fin.Y),
                Math.Max(_debut.X, fin.X), Math.Max(_debut.Y, fin.Y));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_traceEnCours)
            {
                return;
            }
            _traceEnCours = false;

            var rect = Rectangle.Intersect(_trace, new Rectangle(Point.Empty, _image.Size));
            if (rect.Width > 0 && rect.Height > 0)
            {
                ZoneTracee?.Invoke(rect);
            }
            Invalidate();
        }

        private Point VersImage(Point ecran) =>
            new(ecran.X - AutoScrollPosition.X, ecran.Y - AutoScrollPosition.Y);
    }
}
