namespace TextAnonymizer.Models.Diff;

/// <summary>
/// Algorithme de Myers (« An O(ND) Difference Algorithm and Its Variations », 1986), variante en espace linéaire.
/// Travaille sur des identifiants entiers (lignes, mots ou caractères déjà convertis) et marque les éléments
/// supprimés de A et insérés dans B ; les éléments non marqués forment une plus longue sous-suite commune.
/// <para>
/// Pour rester rapide sur de gros fichiers : préfixe et suffixe communs retirés, éléments sans équivalent
/// de l'autre côté écartés d'emblée (cela ne change pas le résultat), et au-delà de <c>costLimit</c>
/// différences dans une même zone, découpage approché au point le plus avancé (comme GNU diff).
/// </para>
/// </summary>
public static class MyersDiff
{
    public const int DefaultCostLimit = 4096;

    public static (bool[] Deleted, bool[] Inserted) Compute(
        int[] a,
        int[] b,
        CancellationToken cancellationToken = default,
        int costLimit = DefaultCostLimit)
    {
        var deleted = new bool[a.Length];
        var inserted = new bool[b.Length];
        if (a.Length == 0 || b.Length == 0)
        {
            Array.Fill(deleted, true);
            Array.Fill(inserted, true);
            return (deleted, inserted);
        }

        // Un élément absent de l'autre séquence ne peut jamais être apparié : on l'écarte avant la recherche.
        var maxId = 0;
        foreach (var id in a)
        {
            maxId = Math.Max(maxId, id);
        }

        foreach (var id in b)
        {
            maxId = Math.Max(maxId, id);
        }

        var inA = new bool[maxId + 1];
        var inB = new bool[maxId + 1];
        foreach (var id in a)
        {
            inA[id] = true;
        }

        foreach (var id in b)
        {
            inB[id] = true;
        }

        var mapA = Keep(a, inB, deleted);
        var mapB = Keep(b, inA, inserted);
        var reducedA = mapA.Select(i => a[i]).ToArray();
        var reducedB = mapB.Select(i => b[i]).ToArray();

        var search = new Search(reducedA, reducedB, cancellationToken, Math.Max(1, costLimit));
        search.Run();

        for (var i = 0; i < mapA.Length; i++)
        {
            deleted[mapA[i]] = search.Deleted[i];
        }

        for (var j = 0; j < mapB.Length; j++)
        {
            inserted[mapB[j]] = search.Inserted[j];
        }

        return (deleted, inserted);
    }

    private static int[] Keep(int[] sequence, bool[] presentInOther, bool[] discarded)
    {
        var kept = new List<int>(sequence.Length);
        for (var i = 0; i < sequence.Length; i++)
        {
            if (presentInOther[sequence[i]])
            {
                kept.Add(i);
            }
            else
            {
                discarded[i] = true;
            }
        }

        return [.. kept];
    }

    private sealed class Search
    {
        private readonly int[] _a;
        private readonly int[] _b;
        private readonly int[] _down;
        private readonly int[] _up;
        private readonly int _max;
        private readonly CancellationToken _cancellationToken;
        private readonly int _costLimit;

        public Search(int[] a, int[] b, CancellationToken cancellationToken, int costLimit)
        {
            _a = a;
            _b = b;
            _cancellationToken = cancellationToken;
            _costLimit = costLimit;
            _max = a.Length + b.Length + 1;
            _down = new int[(2 * _max) + 2];
            _up = new int[(2 * _max) + 2];
            Deleted = new bool[a.Length];
            Inserted = new bool[b.Length];
        }

        public bool[] Deleted { get; }

        public bool[] Inserted { get; }

        /// <summary>Découpage récursif, déroulé sur une pile explicite (pas de dépassement de pile sur de gros fichiers).</summary>
        public void Run()
        {
            var pending = new Stack<(int LowerA, int UpperA, int LowerB, int UpperB)>();
            pending.Push((0, _a.Length, 0, _b.Length));
            while (pending.Count > 0)
            {
                var (lowerA, upperA, lowerB, upperB) = pending.Pop();
                while (lowerA < upperA && lowerB < upperB && _a[lowerA] == _b[lowerB])
                {
                    lowerA++;
                    lowerB++;
                }

                while (lowerA < upperA && lowerB < upperB && _a[upperA - 1] == _b[upperB - 1])
                {
                    upperA--;
                    upperB--;
                }

                if (lowerA == upperA)
                {
                    for (var j = lowerB; j < upperB; j++)
                    {
                        Inserted[j] = true;
                    }
                }
                else if (lowerB == upperB)
                {
                    for (var i = lowerA; i < upperA; i++)
                    {
                        Deleted[i] = true;
                    }
                }
                else
                {
                    var (x, y) = MiddleSnake(lowerA, upperA, lowerB, upperB);
                    pending.Push((x, upperA, y, upperB));
                    pending.Push((lowerA, x, lowerB, y));
                }
            }
        }

        /// <summary>Point de partage du chemin d'édition minimal (recherche simultanée vers l'avant et vers l'arrière).</summary>
        private (int X, int Y) MiddleSnake(int lowerA, int upperA, int lowerB, int upperB)
        {
            var downK = lowerA - lowerB;
            var upK = upperA - upperB;
            var delta = (upperA - lowerA) - (upperB - lowerB);
            var oddDelta = (delta & 1) != 0;
            var downOffset = _max - downK;
            var upOffset = _max - upK;
            var maxD = ((upperA - lowerA + upperB - lowerB) / 2) + 1;

            _down[downOffset + downK + 1] = lowerA;
            _up[upOffset + upK - 1] = upperA;

            for (var d = 0; d <= maxD; d++)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                for (var k = downK - d; k <= downK + d; k += 2)
                {
                    int x;
                    if (k == downK - d)
                    {
                        x = _down[downOffset + k + 1];
                    }
                    else
                    {
                        x = _down[downOffset + k - 1] + 1;
                        if (k < downK + d && _down[downOffset + k + 1] >= x)
                        {
                            x = _down[downOffset + k + 1];
                        }
                    }

                    var y = x - k;
                    while (x < upperA && y < upperB && _a[x] == _b[y])
                    {
                        x++;
                        y++;
                    }

                    _down[downOffset + k] = x;
                    if (oddDelta && upK - d < k && k < upK + d && _up[upOffset + k] <= _down[downOffset + k])
                    {
                        return (_down[downOffset + k], _down[downOffset + k] - k);
                    }
                }

                for (var k = upK - d; k <= upK + d; k += 2)
                {
                    int x;
                    if (k == upK + d)
                    {
                        x = _up[upOffset + k - 1];
                    }
                    else
                    {
                        x = _up[upOffset + k + 1] - 1;
                        if (k > upK - d && _up[upOffset + k - 1] < x)
                        {
                            x = _up[upOffset + k - 1];
                        }
                    }

                    var y = x - k;
                    while (x > lowerA && y > lowerB && _a[x - 1] == _b[y - 1])
                    {
                        x--;
                        y--;
                    }

                    _up[upOffset + k] = x;
                    if (!oddDelta && downK - d <= k && k <= downK + d && _up[upOffset + k] <= _down[downOffset + k])
                    {
                        return (_down[downOffset + k], _down[downOffset + k] - k);
                    }
                }

                if (d >= _costLimit && FurthestForward(d, downK, downOffset, lowerA, upperA, lowerB, upperB) is { } split)
                {
                    return split;
                }
            }

            throw new InvalidOperationException("Myers : aucun point de partage trouvé.");
        }

        /// <summary>Zone trop coûteuse : point le plus avancé de la recherche avant, qui fait progresser le découpage.</summary>
        private (int X, int Y)? FurthestForward(int d, int downK, int downOffset, int lowerA, int upperA, int lowerB, int upperB)
        {
            (int X, int Y)? best = null;
            var bestSum = lowerA + lowerB;
            for (var k = downK - d; k <= downK + d; k += 2)
            {
                var x = _down[downOffset + k];
                var y = x - k;
                if (x < lowerA || x > upperA || y < lowerB || y > upperB || (x == upperA && y == upperB))
                {
                    continue;
                }

                if (x + y > bestSum)
                {
                    bestSum = x + y;
                    best = (x, y);
                }
            }

            return best;
        }
    }
}
