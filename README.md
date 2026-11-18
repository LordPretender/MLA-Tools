# MLA Tools

Un assistant pour Mobile Legends: Adventure : il lit tes héros directement dans le jeu, puis te propose des équipes toutes prêtes à partir de ta collection.

## À quoi ça sert

- Fini la saisie à la main : l'outil lit la fiche de chaque héros affichée à l'écran (nom, factions, étoiles) et retient ta collection
- Il compose des équipes équilibrées, que tu peux régler héros par héros
- Tu enregistres autant de compositions que tu veux, pour les retrouver, les renommer ou les dupliquer
- Il ne touche à rien dans le jeu : il se contente de regarder l'écran, comme toi

## Comment l'utiliser

Fonctionne sous Windows uniquement. Pour l'instant, l'outil se compile soi-même (voir « Compiler et lancer » plus bas).

1. Dans le jeu, ouvre la fiche « Info » d'un héros
2. Dans MLA Tools, choisis la fenêtre du jeu puis clique sur « Lire les héros »
3. Ouvre l'écran des équipes et clique sur « Générer »

## Pour les développeurs

### Prérequis

- Windows
- SDK .NET 8 ou supérieur

### Compiler et lancer

    dotnet build MLA-Tools.sln
    dotnet run --project src/mla-tools

### Structure

- `src/mla-tools/` : code et projet de l'application
- `tests/mla-tools.Tests/` : tests xUnit (moteur d'équipes, référentiel, composantes, affectation, base SQLite, zones, compteur d'étoiles) ; l'OCR, la fenêtre du jeu et l'interface ne sont pas testés automatiquement
- `MLA-Tools.sln` : solution, à la racine

### Tester

Depuis la racine du dépôt principal :

    scripts\test.ps1 -Cible MLA-Tools/MLA-Tools.sln
