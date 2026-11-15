namespace MlaTools;

/// <summary>Parcours des composantes connexes d'un masque de pixels, partagé par la lecture des étoiles et celle du chiffre d'éveil.</summary>
internal static class Composantes
{
    /// <summary>Rend chaque composante connexe de pixels vrais du masque, sous forme d'index (ligne × largeur + colonne).</summary>
    public static IEnumerable<List<int>> Lister(bool[] masque, int largeur, int hauteur, bool huitVoisins)
    {
        var vu = new bool[masque.Length];
        for (var depart = 0; depart < masque.Length; depart++)
        {
            if (masque[depart] && !vu[depart])
            {
                yield return Parcourir(depart, masque, vu, largeur, hauteur, huitVoisins);
            }
        }
    }

    private static List<int> Parcourir(int depart, bool[] masque, bool[] vu, int largeur, int hauteur, bool huitVoisins)
    {
        var composante = new List<int>();
        var pile = new Stack<int>();
        pile.Push(depart);
        vu[depart] = true;
        while (pile.Count > 0)
        {
            var p = pile.Pop();
            composante.Add(p);
            foreach (var voisin in Voisins(p, largeur, hauteur, huitVoisins))
            {
                if (masque[voisin] && !vu[voisin])
                {
                    vu[voisin] = true;
                    pile.Push(voisin);
                }
            }
        }

        return composante;
    }

    private static IEnumerable<int> Voisins(int p, int largeur, int hauteur, bool huitVoisins)
    {
        var px = p % largeur;
        var py = p / largeur;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var nx = px + dx;
                var ny = py + dy;
                var estVoisin = (dx != 0 || dy != 0) && (huitVoisins || dx == 0 || dy == 0);
                if (estVoisin && nx >= 0 && nx < largeur && ny >= 0 && ny < hauteur)
                {
                    yield return ny * largeur + nx;
                }
            }
        }
    }
}
