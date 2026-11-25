using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MlaTools;

internal sealed record Hero(string Nom, int Etoiles, IReadOnlyList<string> Factions, string Classe, string Equipement)
{
    /// <summary>L'égalité de record compare les listes par référence ; ici les factions se comparent dans l'ordre, doublons écartés comme au stockage.</summary>
    public bool Identique(Hero autre) =>
        Nom == autre.Nom && Etoiles == autre.Etoiles && Classe == autre.Classe && Equipement == autre.Equipement
        && Factions.Distinct().SequenceEqual(autre.Factions.Distinct());
}

/// <summary>Le lien d'âme est saisi à la main et vit hors de <see cref="Hero"/> : l'importation depuis le jeu ne doit pas pouvoir l'écraser. <paramref name="DateMaj"/> est null pour un héros antérieur à cette colonne.</summary>
internal sealed record HeroStocke(int Id, Hero Hero, bool LienAme, DateTime? DateMaj);

internal sealed record ResultatEnregistrement(IReadOnlyList<Hero> Ajoutes, IReadOnlyList<Hero> MisAJour, IReadOnlyList<Hero> Inchanges);

/// <summary>Place d'un héros : <see cref="Equipe"/> null = réserve.</summary>
internal sealed record Affectation(int HeroId, int? Equipe, int Position)
{
    /// <summary>Répartit en équipes et réserve les affectations des héros connus, avec au moins <paramref name="nombreMinimum"/> équipes ; rien de placé = aucune équipe.</summary>
    public static (List<List<int>> Equipes, List<int> Reserve) Repartir(IEnumerable<Affectation> affectations, Predicate<int> heroConnu, int nombreMinimum)
    {
        var placees = affectations.Where(a => heroConnu(a.HeroId)).OrderBy(a => a.Position).ToList();
        var equipes = new List<List<int>>();
        var reserve = new List<int>();
        if (placees.Count == 0)
        {
            return (equipes, reserve);
        }

        var nombre = Math.Max(nombreMinimum, placees.Max(a => a.Equipe ?? -1) + 1);
        equipes.AddRange(Enumerable.Range(0, nombre).Select(_ => new List<int>()));
        foreach (var a in placees)
        {
            (a.Equipe is { } equipe ? equipes[equipe] : reserve).Add(a.HeroId);
        }
        return (equipes, reserve);
    }

    /// <summary>Échange deux héros placés dans les équipes ou la réserve.</summary>
    public static void Echanger(IReadOnlyList<List<int>> equipes, List<int> reserve, int a, int b)
    {
        var listeA = ListeDe(equipes, reserve, a);
        var listeB = ListeDe(equipes, reserve, b);
        (listeA[listeA.IndexOf(a)], listeB[listeB.IndexOf(b)]) = (b, a);
    }

    /// <summary>
    /// Faux quand l'échange ferait entrer en équipe une forme dont le même héros combattrait déjà dans une équipe. Seul un échange entre une équipe et la réserve
    /// change la liste des combattants : entre deux équipes ou dans la réserve, il est toujours permis, ce qui laisse modifiable une composition déjà en doublon.
    /// </summary>
    public static bool EchangeAutorise(IReadOnlyList<List<int>> equipes, IReadOnlyList<int> reserve, int a, int b, Func<int, int> famille)
    {
        var aEnReserve = reserve.Contains(a);
        if (aEnReserve == reserve.Contains(b))
        {
            return true;
        }

        var (entrant, sortant) = aEnReserve ? (a, b) : (b, a);
        return !equipes.SelectMany(e => e).Any(h => h != sortant && famille(h) == famille(entrant));
    }

    /// <summary>Envoie un héros d'équipe en fin de réserve ; sans effet s'il n'est dans aucune équipe.</summary>
    public static bool EnvoyerEnReserve(IReadOnlyList<List<int>> equipes, List<int> reserve, int id)
    {
        var equipe = equipes.FirstOrDefault(e => e.Contains(id));
        if (equipe is null)
        {
            return false;
        }

        equipe.Remove(id);
        reserve.Add(id);
        return true;
    }

    /// <summary>Deux clics : le premier choisit un héros, le second l'échange avec l'autre (entre équipes ou avec la réserve). Retourne vrai quand un échange a eu lieu, donc qu'il reste à l'enregistrer.</summary>
    public static bool BasculerSelection(ref int? selection, IReadOnlyList<List<int>> equipes, List<int> reserve, int id)
    {
        if (selection is null)
        {
            selection = id;
            return false;
        }
        if (selection == id)
        {
            selection = null;
            return false;
        }

        Echanger(equipes, reserve, selection.Value, id);
        selection = null;
        return true;
    }

    private static List<int> ListeDe(IReadOnlyList<List<int>> equipes, List<int> reserve, int id) =>
        equipes.Append(reserve).First(l => l.Contains(id));

    /// <summary>Lignes à stocker pour une répartition : l'ordre des listes devient la position.</summary>
    public static List<Affectation> Aplatir(IReadOnlyList<List<int>> equipes, IReadOnlyList<int> reserve)
    {
        var affectations = new List<Affectation>();
        for (var i = 0; i < equipes.Count; i++)
        {
            var equipe = i;
            affectations.AddRange(equipes[i].Select((id, position) => new Affectation(id, equipe, position)));
        }
        affectations.AddRange(reserve.Select((id, position) => new Affectation(id, null, position)));
        return affectations;
    }
}

/// <param name="NombreEquipes">Stocké à part : une équipe vide ne laisse aucune ligne d'affectation.</param>
internal sealed record CompositionStockee(int Id, string Nom, int NombreEquipes);

internal sealed record ZoneStockee(string Portee, string Nom, Zone Zone);

internal static class BaseSqlite
{
    public const string PorteeGlobales = "globales";

    public const string CleTitreFenetre = "titre_fenetre";
    public const string CleLargeur = "largeur";
    public const string CleHauteur = "hauteur";
    public const string CleCalibrageImporte = "calibrage_importe";
    public const string CleNombreHerosALire = "import_nombre_heros";
    public const string CleDelaiApresClic = "import_delai_apres_clic_ms";
    public const string MarqueurPresent = "1";

    private const string NomFichier = "mla-heros.db";
    private const string TableHeros = "heros";
    private const string TableCompositions = "compositions";
    private const string ColonneLienAme = "lien_ame";
    private const string ColonneNombreEquipes = "nombre_equipes";
    private const string ColonneDateMaj = "date_maj";
    // Heure locale en texte : la colonne se lit et se trie sans conversion de fuseau.
    private const string FormatDateMaj = "yyyy-MM-dd HH:mm:ss";
    private const int VersionSchema = 6;
    // Identifiant qu'aucune composition ne porte : « aucune composition à ignorer » dans la recherche d'un nom pris.
    private const int IdAucun = -1;

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS heros (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            nom TEXT NOT NULL,
            nom_norme TEXT NOT NULL UNIQUE,
            etoiles INTEGER NOT NULL,
            classe TEXT NOT NULL,
            equipement TEXT NOT NULL,
            lien_ame INTEGER NOT NULL DEFAULT 0,
            date_maj TEXT NULL);
        CREATE TABLE IF NOT EXISTS heros_factions (
            hero_id INTEGER NOT NULL REFERENCES heros(id) ON DELETE CASCADE,
            faction TEXT NOT NULL,
            rang INTEGER NOT NULL,
            PRIMARY KEY (hero_id, faction));
        CREATE TABLE IF NOT EXISTS roles (
            classe TEXT PRIMARY KEY,
            role TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS equipes (
            hero_id INTEGER PRIMARY KEY REFERENCES heros(id) ON DELETE CASCADE,
            equipe INTEGER NULL,
            position INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS compositions (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            nom TEXT NOT NULL,
            nom_norme TEXT NOT NULL UNIQUE,
            nombre_equipes INTEGER NOT NULL DEFAULT 0);
        CREATE TABLE IF NOT EXISTS compositions_heros (
            composition_id INTEGER NOT NULL REFERENCES compositions(id) ON DELETE CASCADE,
            hero_id INTEGER NOT NULL REFERENCES heros(id) ON DELETE CASCADE,
            equipe INTEGER NULL,
            position INTEGER NOT NULL,
            PRIMARY KEY (composition_id, hero_id));
        CREATE TABLE IF NOT EXISTS compositions_reglages (
            composition_id INTEGER NOT NULL REFERENCES compositions(id) ON DELETE CASCADE,
            cle TEXT NOT NULL,
            valeur TEXT NOT NULL,
            PRIMARY KEY (composition_id, cle));
        CREATE TABLE IF NOT EXISTS parametres (
            cle TEXT PRIMARY KEY,
            valeur TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS zones (
            portee TEXT NOT NULL,
            nom TEXT NOT NULL,
            x INTEGER,
            y INTEGER,
            largeur INTEGER,
            hauteur INTEGER,
            PRIMARY KEY (portee, nom));
        """;

    private static readonly Dictionary<string, string> RolesParDefaut = new()
    {
        ["Adc"] = MoteurEquipes.RoleDps, ["Tank"] = MoteurEquipes.RoleTank, ["Mage"] = MoteurEquipes.RoleDps,
        ["Combattant"] = MoteurEquipes.RoleDps, ["Support"] = MoteurEquipes.RoleSoutien, ["Sniper"] = MoteurEquipes.RoleDps,
        ["Ranger"] = MoteurEquipes.RoleDps, ["Garde d'acier"] = MoteurEquipes.RoleTank, ["Stratégiste"] = MoteurEquipes.RoleSoutien,
        ["Sorcier"] = MoteurEquipes.RoleDps, ["Arcaniste"] = MoteurEquipes.RoleSoutien, ["Chevalier Dragon"] = MoteurEquipes.RoleDps,
        ["Inquisiteur"] = MoteurEquipes.RoleDps, ["Esprit"] = MoteurEquipes.RoleSoutien, ["Oracle"] = MoteurEquipes.RoleSoutien,
    };

    public static string CheminParDefaut => Path.Combine(AppContext.BaseDirectory, NomFichier);

    /// <summary>Erreurs attendues d'un accès à la base (fichier inaccessible, base illisible) : à signaler à l'utilisateur plutôt que laisser planter.</summary>
    public static bool EstErreurAcces(Exception ex) => ex is InvalidOperationException or IOException or DbException;

    // Une transaction pour tout le lot : une erreur au milieu n'écrit aucun héros.
    public static ResultatEnregistrement Enregistrer(IEnumerable<Hero> heros, string chemin)
    {
        // Un doublon dans le lot suit la même règle qu'un héros déjà en base : le dernier l'emporte.
        var parNom = new Dictionary<string, Hero>();
        foreach (var hero in heros)
        {
            parNom[Referentiel.Normaliser(hero.Nom)] = hero;
        }

        using var connexion = Ouvrir(chemin);
        using var transaction = connexion.BeginTransaction();
        var ajoutes = new List<Hero>();
        var misAJour = new List<Hero>();
        var inchanges = new List<Hero>();
        foreach (var (nomNorme, hero) in parNom)
        {
            var id = TrouverId(connexion, transaction, nomNorme);
            if (id is null)
            {
                InsererHero(connexion, transaction, hero);
                ajoutes.Add(hero);
            }
            else
            {
                (MettreAJourSiChange(connexion, transaction, id.Value, hero) ? misAJour : inchanges).Add(hero);
            }
        }
        transaction.Commit();
        return new ResultatEnregistrement(ajoutes, misAJour, inchanges);
    }

    public static int AjouterHero(Hero hero, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        using var transaction = connexion.BeginTransaction();
        var id = InsererHero(connexion, transaction, hero);
        transaction.Commit();
        return id;
    }

    public static void ModifierHero(int id, Hero hero, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        using var transaction = connexion.BeginTransaction();
        MettreAJourSiChange(connexion, transaction, id, hero);
        transaction.Commit();
    }

    // Les lignes de heros_factions et d'equipes partent avec le héros (ON DELETE CASCADE).
    public static void SupprimerHero(int id, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        Executer(connexion, null, "DELETE FROM heros WHERE id = $id", ("$id", id));
    }

    public static IReadOnlyList<HeroStocke> ListerHeros(string chemin)
    {
        using var connexion = Ouvrir(chemin);
        using var commande = Creer(connexion, null, """
            SELECT h.id, h.nom, h.etoiles, h.classe, h.equipement, f.faction, h.lien_ame, h.date_maj
            FROM heros h LEFT JOIN heros_factions f ON f.hero_id = h.id
            ORDER BY h.id, f.rang
            """);
        using var lecteur = commande.ExecuteReader();

        var resultat = new List<HeroStocke>();
        var factions = new List<string>();
        HeroStocke? courant = null;
        while (lecteur.Read())
        {
            var id = lecteur.GetInt32(0);
            if (courant is null || courant.Id != id)
            {
                factions = [];
                courant = new HeroStocke(id, new Hero(lecteur.GetString(1), lecteur.GetInt32(2), factions, lecteur.GetString(3), lecteur.GetString(4)),
                    lecteur.GetInt32(6) != 0, LireDateMaj(lecteur, 7));
                resultat.Add(courant);
            }
            if (!lecteur.IsDBNull(5))
            {
                factions.Add(lecteur.GetString(5));
            }
        }
        return resultat;
    }

    public static void EcrireLienAme(int id, bool lienAme, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        Executer(connexion, null, "UPDATE heros SET lien_ame = $lien WHERE id = $id", ("$id", id), ("$lien", lienAme ? 1 : 0));
    }

    public static Dictionary<string, string> LireRoles(string chemin) =>
        LireDictionnaire(chemin, "SELECT classe, role FROM roles");

    public static void EcrireRole(string classe, string role, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        Executer(connexion, null,
            "INSERT INTO roles(classe, role) VALUES($classe, $role) ON CONFLICT(classe) DO UPDATE SET role = excluded.role",
            ("$classe", classe), ("$role", role));
    }

    public static IReadOnlyList<Affectation> LireAffectations(string chemin)
    {
        using var connexion = Ouvrir(chemin);
        using var commande = Creer(connexion, null, "SELECT hero_id, equipe, position FROM equipes ORDER BY equipe, position");
        using var lecteur = commande.ExecuteReader();
        return LireLignesAffectation(lecteur);
    }

    // Remplacement complet : la fenêtre Équipes écrit l'état entier, pas un différentiel.
    public static void EcrireAffectations(IEnumerable<Affectation> affectations, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        using var transaction = connexion.BeginTransaction();
        Executer(connexion, transaction, "DELETE FROM equipes");
        foreach (var a in affectations)
        {
            Executer(connexion, transaction, "INSERT INTO equipes(hero_id, equipe, position) VALUES($hero, $equipe, $position)",
                ("$hero", a.HeroId), ("$equipe", a.Equipe), ("$position", a.Position));
        }
        transaction.Commit();
    }

    public static IReadOnlyList<CompositionStockee> ListerCompositions(string chemin)
    {
        using var connexion = Ouvrir(chemin);
        using var commande = Creer(connexion, null, "SELECT id, nom, nombre_equipes FROM compositions ORDER BY nom_norme");
        using var lecteur = commande.ExecuteReader();

        var resultat = new List<CompositionStockee>();
        while (lecteur.Read())
        {
            resultat.Add(new CompositionStockee(lecteur.GetInt32(0), lecteur.GetString(1), lecteur.GetInt32(2)));
        }
        return resultat;
    }

    public static IReadOnlyList<Affectation> LireComposition(int id, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        return LireLignesComposition(connexion, null, id);
    }

    public static bool NomCompositionPris(string nom, int? idIgnore, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        using var commande = Creer(connexion, null, "SELECT COUNT(*) FROM compositions WHERE nom_norme = $norme AND id <> $ignore",
            ("$norme", Referentiel.Normaliser(nom)), ("$ignore", idIgnore ?? IdAucun));
        return Convert.ToInt32(commande.ExecuteScalar()) > 0;
    }

    /// <param name="reglages">Instantané des réglages de génération (clés de <c>parametres</c>) ; null quand la composition n'en embarque pas.</param>
    public static int CreerComposition(string nom, IEnumerable<Affectation> affectations, int nombreEquipes, string chemin, IReadOnlyDictionary<string, string>? reglages = null)
    {
        using var connexion = Ouvrir(chemin);
        using var transaction = connexion.BeginTransaction();
        var id = InsererComposition(connexion, transaction, nom, affectations, nombreEquipes);
        EcrireReglagesComposition(connexion, transaction, id, reglages ?? new Dictionary<string, string>());
        transaction.Commit();
        return id;
    }

    /// <summary>Vide quand la composition n'embarque aucun réglage (enregistrée avant cette fonction).</summary>
    public static Dictionary<string, string> LireReglagesComposition(int id, string chemin) =>
        LireDictionnaire(chemin, "SELECT cle, valeur FROM compositions_reglages WHERE composition_id = $id", ("$id", id));

    public static void RenommerComposition(int id, string nom, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        Executer(connexion, null, "UPDATE compositions SET nom = $nom, nom_norme = $norme WHERE id = $id",
            ("$id", id), ("$nom", nom), ("$norme", Referentiel.Normaliser(nom)));
    }

    // Remplacement complet, comme EcrireAffectations : l'appelant écrit l'état entier de la composition.
    public static void RemplacerComposition(int id, IEnumerable<Affectation> affectations, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        using var transaction = connexion.BeginTransaction();
        // Le nombre d'équipes ne change pas à l'édition : seul le contenu des équipes est réécrit.
        Executer(connexion, transaction, "DELETE FROM compositions_heros WHERE composition_id = $id", ("$id", id));
        EcrireLignesComposition(connexion, transaction, id, affectations);
        transaction.Commit();
    }

    public static int DupliquerComposition(int id, string nouveauNom, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        using var transaction = connexion.BeginTransaction();
        using var commande = Creer(connexion, transaction, "SELECT nombre_equipes FROM compositions WHERE id = $id", ("$id", id));
        var nombreEquipes = Convert.ToInt32(commande.ExecuteScalar());
        var copie = InsererComposition(connexion, transaction, nouveauNom, LireLignesComposition(connexion, transaction, id), nombreEquipes);
        // La copie garde les réglages de l'originale : sinon elle les perdrait sans que rien ne le dise.
        Executer(connexion, transaction,
            "INSERT INTO compositions_reglages(composition_id, cle, valeur) SELECT $copie, cle, valeur FROM compositions_reglages WHERE composition_id = $id",
            ("$copie", copie), ("$id", id));
        transaction.Commit();
        return copie;
    }

    // Les lignes de compositions_heros partent avec la composition (ON DELETE CASCADE).
    public static void SupprimerComposition(int id, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        Executer(connexion, null, "DELETE FROM compositions WHERE id = $id", ("$id", id));
    }

    public static Dictionary<string, string> LireParametres(string chemin) =>
        LireDictionnaire(chemin, "SELECT cle, valeur FROM parametres");

    public static void EcrireParametre(string cle, string valeur, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        EcrireParametre(connexion, null, cle, valeur);
    }

    public static IReadOnlyList<ZoneStockee> LireZones(string chemin)
    {
        using var connexion = Ouvrir(chemin);
        using var commande = Creer(connexion, null, "SELECT portee, nom, x, y, largeur, hauteur FROM zones");
        using var lecteur = commande.ExecuteReader();

        var resultat = new List<ZoneStockee>();
        while (lecteur.Read())
        {
            resultat.Add(new ZoneStockee(lecteur.GetString(0), lecteur.GetString(1),
                new Zone(lecteur.GetInt32(2), lecteur.GetInt32(3), lecteur.GetInt32(4), lecteur.GetInt32(5))));
        }
        return resultat;
    }

    // Paramètres et zones changent ensemble : une taille de fenêtre sans ses zones serait incohérente.
    public static void EcrireCalibrage(IReadOnlyDictionary<string, string> parametres, IEnumerable<ZoneStockee> zones, string chemin)
    {
        using var connexion = Ouvrir(chemin);
        using var transaction = connexion.BeginTransaction();
        foreach (var (cle, valeur) in parametres)
        {
            EcrireParametre(connexion, transaction, cle, valeur);
        }
        Executer(connexion, transaction, "DELETE FROM zones");
        foreach (var z in zones)
        {
            Executer(connexion, transaction, "INSERT INTO zones(portee, nom, x, y, largeur, hauteur) VALUES($portee, $nom, $x, $y, $largeur, $hauteur)",
                ("$portee", z.Portee), ("$nom", z.Nom), ("$x", z.Zone.X), ("$y", z.Zone.Y), ("$largeur", z.Zone.Largeur), ("$hauteur", z.Zone.Hauteur));
        }
        transaction.Commit();
    }

    private static SqliteConnection Ouvrir(string chemin)
    {
        var chaine = new SqliteConnectionStringBuilder { DataSource = chemin, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
        var connexion = new SqliteConnection(chaine);
        try
        {
            connexion.Open();
            // SQLite ignore les clés étrangères tant que ce réglage n'est pas posé, connexion par connexion.
            Executer(connexion, null, "PRAGMA foreign_keys = ON");
            InitialiserSchema(connexion);
            return connexion;
        }
        catch
        {
            connexion.Dispose();
            throw;
        }
    }

    private static void InitialiserSchema(SqliteConnection connexion)
    {
        int version;
        using (var lecture = Creer(connexion, null, "PRAGMA user_version"))
        {
            version = Convert.ToInt32(lecture.ExecuteScalar());
        }
        if (version >= VersionSchema)
        {
            return;
        }

        using var transaction = connexion.BeginTransaction();
        Executer(connexion, transaction, SchemaSql);
        if (version < 1)
        {
            // Les rôles par défaut ne s'insèrent qu'à la création : un rôle supprimé ensuite ne doit pas réapparaître.
            foreach (var (classe, role) in RolesParDefaut)
            {
                Executer(connexion, transaction, "INSERT OR IGNORE INTO roles(classe, role) VALUES($classe, $role)", ("$classe", classe), ("$role", role));
            }
        }
        // CREATE TABLE IF NOT EXISTS ne touche pas une table existante : la colonne du lien d'âme s'ajoute à part.
        if (!ColonneExiste(connexion, transaction, TableHeros, ColonneLienAme))
        {
            Executer(connexion, transaction, $"ALTER TABLE heros ADD COLUMN {ColonneLienAme} INTEGER NOT NULL DEFAULT 0");
        }
        // Les héros déjà en base gardent une date inconnue (NULL) : on ne peut pas deviner quand ils ont été saisis.
        if (!ColonneExiste(connexion, transaction, TableHeros, ColonneDateMaj))
        {
            Executer(connexion, transaction, $"ALTER TABLE heros ADD COLUMN {ColonneDateMaj} TEXT NULL");
        }
        // Les compositions déjà enregistrées reçoivent le nombre d'équipes que leurs lignes laissent deviner.
        if (!ColonneExiste(connexion, transaction, TableCompositions, ColonneNombreEquipes))
        {
            Executer(connexion, transaction, $"ALTER TABLE compositions ADD COLUMN {ColonneNombreEquipes} INTEGER NOT NULL DEFAULT 0");
            Executer(connexion, transaction, $"""
                UPDATE compositions SET {ColonneNombreEquipes} =
                    (SELECT COALESCE(MAX(equipe) + 1, 0) FROM compositions_heros WHERE composition_id = compositions.id)
                """);
        }
        // PRAGMA n'accepte pas de paramètre ; la valeur est une constante entière.
        Executer(connexion, transaction, $"PRAGMA user_version = {VersionSchema}");
        transaction.Commit();
    }

    // PRAGMA table_info n'accepte pas de paramètre ; le nom de table est une constante du code.
    private static bool ColonneExiste(SqliteConnection connexion, SqliteTransaction transaction, string table, string colonne)
    {
        using var commande = Creer(connexion, transaction, $"PRAGMA table_info({table})");
        using var lecteur = commande.ExecuteReader();
        while (lecteur.Read())
        {
            if (lecteur.GetString(1) == colonne)
            {
                return true;
            }
        }
        return false;
    }

    private static int? TrouverId(SqliteConnection connexion, SqliteTransaction transaction, string nomNorme)
    {
        using var commande = Creer(connexion, transaction, "SELECT id FROM heros WHERE nom_norme = $norme", ("$norme", nomNorme));
        var valeur = commande.ExecuteScalar();
        return valeur is null ? null : Convert.ToInt32(valeur);
    }

    private static int InsererHero(SqliteConnection connexion, SqliteTransaction transaction, Hero hero)
    {
        using var commande = Creer(connexion, transaction, """
            INSERT INTO heros(nom, nom_norme, etoiles, classe, equipement, date_maj)
            VALUES($nom, $norme, $etoiles, $classe, $equipement, $date);
            SELECT last_insert_rowid();
            """, ("$nom", hero.Nom), ("$norme", Referentiel.Normaliser(hero.Nom)), ("$etoiles", hero.Etoiles),
            ("$classe", hero.Classe), ("$equipement", hero.Equipement), ("$date", Horodatage()));
        var id = Convert.ToInt32(commande.ExecuteScalar());
        EcrireFactions(connexion, transaction, id, hero.Factions);
        return id;
    }

    // Retourne false sans rien écrire quand le héros stocké est déjà identique : la date de mise à jour reste celle du dernier vrai changement.
    private static bool MettreAJourSiChange(SqliteConnection connexion, SqliteTransaction transaction, int id, Hero hero)
    {
        if (LireHero(connexion, transaction, id).Identique(hero))
        {
            return false;
        }

        Executer(connexion, transaction, """
            UPDATE heros SET nom = $nom, nom_norme = $norme, etoiles = $etoiles, classe = $classe, equipement = $equipement, date_maj = $date
            WHERE id = $id
            """, ("$id", id), ("$nom", hero.Nom), ("$norme", Referentiel.Normaliser(hero.Nom)), ("$etoiles", hero.Etoiles),
            ("$classe", hero.Classe), ("$equipement", hero.Equipement), ("$date", Horodatage()));
        Executer(connexion, transaction, "DELETE FROM heros_factions WHERE hero_id = $id", ("$id", id));
        EcrireFactions(connexion, transaction, id, hero.Factions);
        return true;
    }

    private static Hero LireHero(SqliteConnection connexion, SqliteTransaction transaction, int id)
    {
        using var commande = Creer(connexion, transaction,
            "SELECT nom, etoiles, classe, equipement FROM heros WHERE id = $id", ("$id", id));
        using var lecteur = commande.ExecuteReader();
        lecteur.Read();
        var (nom, etoiles, classe, equipement) = (lecteur.GetString(0), lecteur.GetInt32(1), lecteur.GetString(2), lecteur.GetString(3));

        using var commandeFactions = Creer(connexion, transaction,
            "SELECT faction FROM heros_factions WHERE hero_id = $id ORDER BY rang", ("$id", id));
        using var lecteurFactions = commandeFactions.ExecuteReader();
        var factions = new List<string>();
        while (lecteurFactions.Read())
        {
            factions.Add(lecteurFactions.GetString(0));
        }
        return new Hero(nom, etoiles, factions, classe, equipement);
    }

    private static string Horodatage() => DateTime.Now.ToString(FormatDateMaj, CultureInfo.InvariantCulture);

    // NULL ou texte illisible = date inconnue.
    private static DateTime? LireDateMaj(SqliteDataReader lecteur, int colonne) =>
        !lecteur.IsDBNull(colonne)
        && DateTime.TryParseExact(lecteur.GetString(colonne), FormatDateMaj, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static void EcrireFactions(SqliteConnection connexion, SqliteTransaction transaction, int heroId, IReadOnlyList<string> factions)
    {
        // La clé primaire (hero_id, faction) refuse deux fois la même faction.
        var rang = 0;
        foreach (var faction in factions.Distinct())
        {
            Executer(connexion, transaction, "INSERT INTO heros_factions(hero_id, faction, rang) VALUES($hero, $faction, $rang)",
                ("$hero", heroId), ("$faction", faction), ("$rang", rang));
            rang++;
        }
    }

    private static int InsererComposition(SqliteConnection connexion, SqliteTransaction transaction, string nom, IEnumerable<Affectation> affectations, int nombreEquipes)
    {
        using var commande = Creer(connexion, transaction, """
            INSERT INTO compositions(nom, nom_norme, nombre_equipes) VALUES($nom, $norme, $equipes);
            SELECT last_insert_rowid();
            """, ("$nom", nom), ("$norme", Referentiel.Normaliser(nom)), ("$equipes", nombreEquipes));
        var id = Convert.ToInt32(commande.ExecuteScalar());
        EcrireLignesComposition(connexion, transaction, id, affectations);
        return id;
    }

    private static void EcrireLignesComposition(SqliteConnection connexion, SqliteTransaction transaction, int id, IEnumerable<Affectation> affectations)
    {
        foreach (var a in affectations)
        {
            Executer(connexion, transaction,
                "INSERT INTO compositions_heros(composition_id, hero_id, equipe, position) VALUES($id, $hero, $equipe, $position)",
                ("$id", id), ("$hero", a.HeroId), ("$equipe", a.Equipe), ("$position", a.Position));
        }
    }

    private static List<Affectation> LireLignesComposition(SqliteConnection connexion, SqliteTransaction? transaction, int id)
    {
        using var commande = Creer(connexion, transaction,
            "SELECT hero_id, equipe, position FROM compositions_heros WHERE composition_id = $id ORDER BY equipe, position", ("$id", id));
        using var lecteur = commande.ExecuteReader();
        return LireLignesAffectation(lecteur);
    }

    // Les requêtes d'affectations sélectionnent toutes (hero_id, equipe, position) dans cet ordre.
    private static List<Affectation> LireLignesAffectation(SqliteDataReader lecteur)
    {
        var resultat = new List<Affectation>();
        while (lecteur.Read())
        {
            resultat.Add(new Affectation(lecteur.GetInt32(0), lecteur.IsDBNull(1) ? null : lecteur.GetInt32(1), lecteur.GetInt32(2)));
        }
        return resultat;
    }

    private static void EcrireParametre(SqliteConnection connexion, SqliteTransaction? transaction, string cle, string valeur) =>
        Executer(connexion, transaction,
            "INSERT INTO parametres(cle, valeur) VALUES($cle, $valeur) ON CONFLICT(cle) DO UPDATE SET valeur = excluded.valeur",
            ("$cle", cle), ("$valeur", valeur));

    private static void EcrireReglagesComposition(SqliteConnection connexion, SqliteTransaction transaction, int id, IReadOnlyDictionary<string, string> reglages)
    {
        foreach (var (cle, valeur) in reglages)
        {
            Executer(connexion, transaction,
                "INSERT INTO compositions_reglages(composition_id, cle, valeur) VALUES($id, $cle, $valeur)",
                ("$id", id), ("$cle", cle), ("$valeur", valeur));
        }
    }

    private static Dictionary<string, string> LireDictionnaire(string chemin, string requete, params (string Nom, object? Valeur)[] parametres)
    {
        using var connexion = Ouvrir(chemin);
        using var commande = Creer(connexion, null, requete, parametres);
        using var lecteur = commande.ExecuteReader();

        var resultat = new Dictionary<string, string>();
        while (lecteur.Read())
        {
            resultat[lecteur.GetString(0)] = lecteur.GetString(1);
        }
        return resultat;
    }

    private static SqliteCommand Creer(SqliteConnection connexion, SqliteTransaction? transaction, string sql,
        params (string Nom, object? Valeur)[] parametres)
    {
        var commande = connexion.CreateCommand();
        commande.Transaction = transaction;
        commande.CommandText = sql;
        foreach (var (nom, valeur) in parametres)
        {
            commande.Parameters.AddWithValue(nom, valeur ?? DBNull.Value);
        }
        return commande;
    }

    private static void Executer(SqliteConnection connexion, SqliteTransaction? transaction, string sql,
        params (string Nom, object? Valeur)[] parametres)
    {
        using var commande = Creer(connexion, transaction, sql, parametres);
        commande.ExecuteNonQuery();
    }
}
