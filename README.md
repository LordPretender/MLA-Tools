# MLA Tools

Application Windows (WinForms, .NET 8) qui lit les héros du jeu par OCR, les stocke dans une base SQLite et génère des compositions d'équipes.

## Prérequis

- Windows
- SDK .NET 8 ou supérieur

## Compiler et lancer

    dotnet build MLA-Tools.sln
    dotnet run --project src/mla-tools

## Structure

- `src/mla-tools/` : code et projet de l'application
- `MLA-Tools.sln` : solution, à la racine
