using Microsoft.Data.Sqlite;

namespace MlaTools.Tests;

/// <summary>Base SQLite jetable : un dossier temporaire propre à chaque instance, nettoyé à la libération.</summary>
internal sealed class BaseTemporaire : IDisposable
{
    private const string PrefixeDossier = "mla-tools-tests-";
    private const string NomFichier = "test.db";

    private readonly string _dossier = Path.Combine(Path.GetTempPath(), PrefixeDossier + Guid.NewGuid().ToString("N"));

    public BaseTemporaire()
    {
        Directory.CreateDirectory(_dossier);
        Chemin = Path.Combine(_dossier, NomFichier);
    }

    public string Chemin { get; }

    // Écriture directe hors de BaseSqlite : seul moyen de fabriquer une base d'ancienne version ou de fixer une valeur que la production ne produirait pas.
    public void ExecuterSql(string sql, params (string Nom, object Valeur)[] parametres)
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = sql;
        foreach (var (nom, valeur) in parametres)
        {
            commande.Parameters.AddWithValue(nom, valeur);
        }
        commande.ExecuteNonQuery();
    }

    public long LireVersion()
    {
        using var connexion = Ouvrir();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "PRAGMA user_version";
        return (long)commande.ExecuteScalar()!;
    }

    private SqliteConnection Ouvrir()
    {
        var connexion = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Chemin }.ToString());
        connexion.Open();
        return connexion;
    }

    public void Dispose()
    {
        // Le pool de connexions garde le fichier ouvert : sous Windows, sans ce vidage, la suppression échoue.
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dossier, recursive: true);
    }
}
