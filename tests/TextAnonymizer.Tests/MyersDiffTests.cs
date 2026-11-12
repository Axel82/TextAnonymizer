using TextAnonymizer.Models.Diff;

namespace TextAnonymizer.Tests;

public class MyersDiffTests
{
    /// <summary>Longueur de la plus longue sous-suite commune, par programmation dynamique (référence).</summary>
    private static int ReferenceLcs(int[] a, int[] b)
    {
        var table = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                table[i, j] = a[i] == b[j] ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table[0, 0];
    }

    /// <summary>Vérifie que les éléments non marqués forment une sous-suite commune, et renvoie sa longueur.</summary>
    private static int CommonLength(int[] a, int[] b, bool[] deleted, bool[] inserted)
    {
        var keptA = a.Where((_, i) => !deleted[i]).ToArray();
        var keptB = b.Where((_, j) => !inserted[j]).ToArray();
        Assert.Equal(keptA, keptB);
        return keptA.Length;
    }

    [Fact]
    public void RandomSequences_GiveALongestCommonSubsequence()
    {
        var random = new Random(7);
        for (var round = 0; round < 300; round++)
        {
            var a = Enumerable.Range(0, random.Next(0, 40)).Select(_ => random.Next(0, 6)).ToArray();
            var b = Enumerable.Range(0, random.Next(0, 40)).Select(_ => random.Next(0, 6)).ToArray();

            var (deleted, inserted) = MyersDiff.Compute(a, b);

            Assert.Equal(ReferenceLcs(a, b), CommonLength(a, b, deleted, inserted));
        }
    }

    [Fact]
    public void CostLimit_StillGivesACommonSubsequence()
    {
        var random = new Random(11);
        for (var round = 0; round < 100; round++)
        {
            var a = Enumerable.Range(0, random.Next(10, 200)).Select(_ => random.Next(0, 4)).ToArray();
            var b = Enumerable.Range(0, random.Next(10, 200)).Select(_ => random.Next(0, 4)).ToArray();

            var (deleted, inserted) = MyersDiff.Compute(a, b, costLimit: 3);

            // Approché : sous-suite commune valide, pas forcément la plus longue.
            Assert.InRange(CommonLength(a, b, deleted, inserted), 0, ReferenceLcs(a, b));
        }
    }

    [Fact]
    public void EmptySides_AreAllDeletedOrInserted()
    {
        var (deleted, inserted) = MyersDiff.Compute([1, 2], []);
        Assert.All(deleted, Assert.True);
        Assert.Empty(inserted);

        (deleted, inserted) = MyersDiff.Compute([], [3]);
        Assert.Empty(deleted);
        Assert.All(inserted, Assert.True);
    }
}
