namespace dotnet.openapi.generator;
internal static class Compatibility
{
#if !NET6_0_OR_GREATER
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
    {
        HashSet<TKey> set = new();

        foreach (TSource item in source)
        {
            if (set.Add(keySelector(item)))
            {
                yield return item;
            }
        }
    }
#endif
}
