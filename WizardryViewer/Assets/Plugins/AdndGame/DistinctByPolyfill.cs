using System.Collections.Generic;

// Unity-only plumbing: Enumerable.DistinctBy is a .NET 6+ addition to System.Linq that Unity's
// older Mono/IL2CPP corelib doesn't ship. Living in the System.Linq namespace means any file
// with "using System.Linq;" (including via GlobalUsings.cs) picks this up as if it were the
// real BCL method - no call sites need touching. Not synced from either repo.
namespace System.Linq
{
    internal static class DistinctByPolyfill
    {
        internal static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            var seen = new HashSet<TKey>();
            foreach (var item in source)
            {
                if (seen.Add(keySelector(item)))
                    yield return item;
            }
        }
    }
}
