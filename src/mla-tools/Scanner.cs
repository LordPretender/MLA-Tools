namespace MlaTools;

internal enum MotifArret
{
    RetourAuPremier,
    ClicSansEffet,
    NombreAtteint,
    ArretDemande,
}

internal sealed record ResultatScan(IReadOnlyList<HeroLu> Heros, MotifArret Motif);

internal static class Scanner
{
    // Maximum que l'utilisateur peut demander : la souris restant sur la flèche, le nombre à lire se fixe avant le scan.
    public const int LimiteHeros = 600;

    // Lit le héros affiché puis clique sur la flèche suivante, jusqu'au nombre demandé ou à une autre condition d'arrêt.
    public static async Task<ResultatScan> LireTousAsync(IntPtr fenetre, ConfigZones config, int nombre, int delaiApresClicMs, IProgress<int> progression, CancellationToken arret)
    {
        var heros = new List<HeroLu>();
        string? premier = null;
        string? precedent = null;

        FenetreCapture.AmenerAuPremierPlan(fenetre);
        while (true)
        {
            using (var capture = FenetreCapture.Capturer(fenetre))
            {
                var hero = await LecteurHeros.LireAsync(capture, config);
                var cle = Cle(hero.Nom);
                var motif = MotifFin(cle, premier, precedent);
                if (motif is not null)
                {
                    return new ResultatScan(heros, motif.Value);
                }

                heros.Add(hero);
                progression.Report(heros.Count);
                premier ??= cle;
                precedent = cle;
            }

            if (heros.Count >= nombre)
            {
                return new ResultatScan(heros, MotifArret.NombreAtteint);
            }
            if (arret.IsCancellationRequested)
            {
                return new ResultatScan(heros, MotifArret.ArretDemande);
            }

            Navigation.CliquerFleche(fenetre, config);
            await Task.Delay(delaiApresClicMs);
        }
    }

    // Un nom illisible (clé vide) ne conclut rien : le héros est gardé, à revoir à la main.
    private static MotifArret? MotifFin(string cle, string? premier, string? precedent)
    {
        if (cle.Length == 0)
        {
            return null;
        }
        if (cle == premier)
        {
            return MotifArret.RetourAuPremier;
        }
        return cle == precedent ? MotifArret.ClicSansEffet : null;
    }

    private static string Cle(string nom) => Referentiel.Normaliser(Referentiel.Compacter(nom));
}
