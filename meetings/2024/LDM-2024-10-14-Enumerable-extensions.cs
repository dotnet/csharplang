namespace System.Linq;

extension Enumerable
{
    public TSource Aggregate<TSource>(Func<TSource, TSource, TSource> func) for IEnumerable<TSource> source;
    public TAccumulate Aggregate<TSource, TAccumulate>(TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func) for IEnumerable<TSource> source;
    public TResult Aggregate<TSource, TAccumulate, TResult>(TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func, Func<TAccumulate, TResult> resultSelector) for IEnumerable<TSource> source;

    public IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(Func<TSource, TKey> keySelector, TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func, IEqualityComparer<TKey>? keyComparer = null) for IEnumerable<TSource> source where TKey : notnull;
    public IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(Func<TSource, TKey> keySelector, Func<TKey, TAccumulate> seedSelector, Func<TAccumulate, TSource, TAccumulate> func, IEqualityComparer<TKey>? keyComparer = null) for IEnumerable<TSource> source where TKey : notnull;

    public bool All<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;

    public bool Any<TSource>() for IEnumerable<TSource> source;
    public bool Any<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;

    public IEnumerable<TSource> Append<TSource>(TSource element) for IEnumerable<TSource> source;

    public IEnumerable<TSource> AsEnumerable<TSource>() for IEnumerable<TSource> source;

    public float? Average<TSource>(Func<TSource, float?> selector) for IEnumerable<TSource> source;
    public double? Average<TSource>(Func<TSource, long?> selector) for IEnumerable<TSource> source;
    public double? Average<TSource>(Func<TSource, int?> selector) for IEnumerable<TSource> source;
    public double? Average<TSource>(Func<TSource, double?> selector) for IEnumerable<TSource> source;
    public decimal? Average<TSource>(Func<TSource, decimal?> selector) for IEnumerable<TSource> source;
    public double Average<TSource>(Func<TSource, long> selector) for IEnumerable<TSource> source;
    public double Average<TSource>(Func<TSource, int> selector) for IEnumerable<TSource> source;
    public double Average<TSource>(Func<TSource, double> selector) for IEnumerable<TSource> source;
    public decimal Average<TSource>(Func<TSource, decimal> selector) for IEnumerable<TSource> source;
    public double? Average() for IEnumerable<double?> source;
    public float? Average() for IEnumerable<float?> source;
    public double? Average() for IEnumerable<long?> source;
    public double? Average() for IEnumerable<int?> source;
    public float Average<TSource>(Func<TSource, float> selector) for IEnumerable<TSource> source;
    public decimal? Average() for IEnumerable<decimal?> source;
    public double Average() for IEnumerable<long> source;
    public double Average() for IEnumerable<int> source;
    public double Average() for IEnumerable<double> source;
    public decimal Average() for IEnumerable<decimal> source;
    public float Average() for IEnumerable<float> source;

    public IEnumerable<TResult> Cast<TResult>(this IEnumerable source);

    public IEnumerable<TSource[]> Chunk<TSource>(int size) for IEnumerable<TSource> source;

    public IEnumerable<TSource> Concat<TSource>(IEnumerable<TSource> second) for IEnumerable<TSource> first;

    public bool Contains<TSource>(TSource value) for IEnumerable<TSource> source;
    public bool Contains<TSource>(TSource value, IEqualityComparer<TSource>? comparer) for IEnumerable<TSource> source;

    public int Count<TSource>() for IEnumerable<TSource> source;
    public int Count<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;

    public IEnumerable<KeyValuePair<TKey, int>> CountBy<TSource, TKey>(Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? keyComparer = null) for IEnumerable<TSource> source where TKey : notnull;

    public IEnumerable<TSource?> DefaultIfEmpty<TSource>() for IEnumerable<TSource> source;
    public IEnumerable<TSource> DefaultIfEmpty<TSource>(TSource defaultValue) for IEnumerable<TSource> source;

    public IEnumerable<TSource> Distinct<TSource>() for IEnumerable<TSource> source;
    public IEnumerable<TSource> Distinct<TSource>(IEqualityComparer<TSource>? comparer) for IEnumerable<TSource> source;

    public IEnumerable<TSource> DistinctBy<TSource, TKey>(Func<TSource, TKey> keySelector) for IEnumerable<TSource> source;
    public IEnumerable<TSource> DistinctBy<TSource, TKey>(Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TSource> source;

    public TSource ElementAt<TSource>(Index index) for IEnumerable<TSource> source;
    public TSource ElementAt<TSource>(int index) for IEnumerable<TSource> source;

    public TSource? ElementAtOrDefault<TSource>(Index index) for IEnumerable<TSource> source;
    public TSource? ElementAtOrDefault<TSource>(int index) for IEnumerable<TSource> source;

    public IEnumerable<TResult> Empty<TResult>();

    public IEnumerable<TSource> Except<TSource>(IEnumerable<TSource> second) for IEnumerable<TSource> first;
    public IEnumerable<TSource> Except<TSource>(IEnumerable<TSource> second, IEqualityComparer<TSource>? comparer) for IEnumerable<TSource> first;

    public IEnumerable<TSource> ExceptBy<TSource, TKey>(IEnumerable<TKey> second, Func<TSource, TKey> keySelector) for IEnumerable<TSource> first;
    public IEnumerable<TSource> ExceptBy<TSource, TKey>(IEnumerable<TKey> second, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TSource> first;

    public TSource First<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;
    public TSource First<TSource>() for IEnumerable<TSource> source;

    public TSource FirstOrDefault<TSource>(Func<TSource, bool> predicate, TSource defaultValue) for IEnumerable<TSource> source;
    public TSource FirstOrDefault<TSource>(TSource defaultValue) for IEnumerable<TSource> source;
    public TSource? FirstOrDefault<TSource>() for IEnumerable<TSource> source;
    public TSource? FirstOrDefault<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;

    public IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(Func<TSource, TKey> keySelector, Func<TKey, IEnumerable<TSource>, TResult> resultSelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TSource> source;
    public IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TSource> source;
    public IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector) for IEnumerable<TSource> source;
    public IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(Func<TSource, TKey> keySelector, Func<TKey, IEnumerable<TSource>, TResult> resultSelector) for IEnumerable<TSource> source;
    public IEnumerable<IGrouping<TKey, TSource>> GroupBy<TSource, TKey>(Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TSource> source;
    public IEnumerable<IGrouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector) for IEnumerable<TSource> source;
    public IEnumerable<IGrouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TSource> source;
    public IEnumerable<IGrouping<TKey, TSource>> GroupBy<TSource, TKey>(Func<TSource, TKey> keySelector) for IEnumerable<TSource> source;

    public IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, IEnumerable<TInner>, TResult> resultSelector) for IEnumerable<TOuter> outer;
    public IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, IEnumerable<TInner>, TResult> resultSelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TOuter> outer;
    public IEnumerable<(int Index, TSource Item)> Index<TSource>() for IEnumerable<TSource> source;
    public IEnumerable<TSource> Intersect<TSource>(IEnumerable<TSource> second) for IEnumerable<TSource> first;
    public IEnumerable<TSource> Intersect<TSource>(IEnumerable<TSource> second, IEqualityComparer<TSource>? comparer) for IEnumerable<TSource> first;
    public IEnumerable<TSource> IntersectBy<TSource, TKey>(IEnumerable<TKey> second, Func<TSource, TKey> keySelector) for IEnumerable<TSource> first;
    public IEnumerable<TSource> IntersectBy<TSource, TKey>(IEnumerable<TKey> second, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TSource> first;

    public IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector) for IEnumerable<TOuter> outer;
    public IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TOuter> outer;
    
    public TSource Last<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;
    public TSource Last<TSource>() for IEnumerable<TSource> source;

    public TSource LastOrDefault<TSource>(Func<TSource, bool> predicate, TSource defaultValue) for IEnumerable<TSource> source;
    public TSource LastOrDefault<TSource>(TSource defaultValue) for IEnumerable<TSource> source;
    public TSource? LastOrDefault<TSource>() for IEnumerable<TSource> source;
    public TSource? LastOrDefault<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;

    public long LongCount<TSource>() for IEnumerable<TSource> source;
    public long LongCount<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;

    public decimal Max<TSource>(Func<TSource, decimal> selector) for IEnumerable<TSource> source;
    public double Max<TSource>(Func<TSource, double> selector) for IEnumerable<TSource> source;
    public TResult? Max<TSource, TResult>(Func<TSource, TResult> selector) for IEnumerable<TSource> source;
    public int Max<TSource>(Func<TSource, int> selector) for IEnumerable<TSource> source;
    public long Max<TSource>(Func<TSource, long> selector) for IEnumerable<TSource> source;
    public int? Max<TSource>(Func<TSource, int?> selector) for IEnumerable<TSource> source;
    public double? Max<TSource>(Func<TSource, double?> selector) for IEnumerable<TSource> source;
    public long? Max<TSource>(Func<TSource, long?> selector) for IEnumerable<TSource> source;
    public float? Max<TSource>(Func<TSource, float?> selector) for IEnumerable<TSource> source;
    public float Max<TSource>(Func<TSource, float> selector) for IEnumerable<TSource> source;
    public decimal? Max<TSource>(Func<TSource, decimal?> selector) for IEnumerable<TSource> source;
    public TSource? Max<TSource>(IComparer<TSource>? comparer) for IEnumerable<TSource> source;
    public float? Max() for IEnumerable<float?> source;
    public int Max() for IEnumerable<int> source;
    public TSource? Max<TSource>() for IEnumerable<TSource> source;
    public float Max() for IEnumerable<float> source;
    public long? Max() for IEnumerable<long?> source;
    public int? Max() for IEnumerable<int?> source;
    public double? Max() for IEnumerable<double?> source;
    public decimal? Max() for IEnumerable<decimal?> source;
    public long Max() for IEnumerable<long> source;
    public decimal Max() for IEnumerable<decimal> source;
    public double Max() for IEnumerable<double> source;

    public TSource? MaxBy<TSource, TKey>(Func<TSource, TKey> keySelector, IComparer<TKey>? comparer) for IEnumerable<TSource> source;
    public TSource? MaxBy<TSource, TKey>(Func<TSource, TKey> keySelector) for IEnumerable<TSource> source;

    public double Min<TSource>(Func<TSource, double> selector) for IEnumerable<TSource> source;
    public int Min<TSource>(Func<TSource, int> selector) for IEnumerable<TSource> source;
    public long Min<TSource>(Func<TSource, long> selector) for IEnumerable<TSource> source;
    public decimal? Min<TSource>(Func<TSource, decimal?> selector) for IEnumerable<TSource> source;
    public double? Min<TSource>(Func<TSource, double?> selector) for IEnumerable<TSource> source;
    public decimal Min<TSource>(Func<TSource, decimal> selector) for IEnumerable<TSource> source;
    public long? Min<TSource>(Func<TSource, long?> selector) for IEnumerable<TSource> source;
    public float? Min<TSource>(Func<TSource, float?> selector) for IEnumerable<TSource> source;
    public float Min<TSource>(Func<TSource, float> selector) for IEnumerable<TSource> source;
    public TResult? Min<TSource, TResult>(Func<TSource, TResult> selector) for IEnumerable<TSource> source;
    public int? Min<TSource>(Func<TSource, int?> selector) for IEnumerable<TSource> source;
    public TSource? Min<TSource>(IComparer<TSource>? comparer) for IEnumerable<TSource> source;
    public decimal Min() for IEnumerable<decimal> source;
    public long Min() for IEnumerable<long> source;
    public TSource? Min<TSource>() for IEnumerable<TSource> source;
    public float Min() for IEnumerable<float> source;
    public float? Min() for IEnumerable<float?> source;
    public long? Min() for IEnumerable<long?> source;
    public int? Min() for IEnumerable<int?> source;
    public double? Min() for IEnumerable<double?> source;
    public decimal? Min() for IEnumerable<decimal?> source;
    public double Min() for IEnumerable<double> source;
    public int Min() for IEnumerable<int> source;

    public TSource? MinBy<TSource, TKey>(Func<TSource, TKey> keySelector, IComparer<TKey>? comparer) for IEnumerable<TSource> source;
    public TSource? MinBy<TSource, TKey>(Func<TSource, TKey> keySelector) for IEnumerable<TSource> source;

    public IEnumerable<TResult> OfType<TResult>(this IEnumerable source);

    public IOrderedEnumerable<T> Order<T>(IComparer<T>? comparer) for IEnumerable<T> source;
    public IOrderedEnumerable<T> Order<T>() for IEnumerable<T> source;

    public IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(Func<TSource, TKey> keySelector) for IEnumerable<TSource> source;
    public IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(Func<TSource, TKey> keySelector, IComparer<TKey>? comparer) for IEnumerable<TSource> source;

    public IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey>(Func<TSource, TKey> keySelector) for IEnumerable<TSource> source;
    public IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey>(Func<TSource, TKey> keySelector, IComparer<TKey>? comparer) for IEnumerable<TSource> source;
    public IOrderedEnumerable<T> OrderDescending<T>(IComparer<T>? comparer) for IEnumerable<T> source;
    public IOrderedEnumerable<T> OrderDescending<T>() for IEnumerable<T> source;

    public IEnumerable<TSource> Prepend<TSource>(TSource element) for IEnumerable<TSource> source;

    public static IEnumerable<int> Range(int start, int count);

    public IEnumerable<TResult> Repeat<TResult>(TResult element, int count);

    public IEnumerable<TSource> Reverse<TSource>() for IEnumerable<TSource> source;

    public IEnumerable<TResult> Select<TSource, TResult>(Func<TSource, int, TResult> selector) for IEnumerable<TSource> source;
    public IEnumerable<TResult> Select<TSource, TResult>(Func<TSource, TResult> selector) for IEnumerable<TSource> source;

    public IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(Func<TSource, int, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector) for IEnumerable<TSource> source;
    public IEnumerable<TResult> SelectMany<TSource, TResult>(Func<TSource, int, IEnumerable<TResult>> selector) for IEnumerable<TSource> source;
    public IEnumerable<TResult> SelectMany<TSource, TResult>(Func<TSource, IEnumerable<TResult>> selector) for IEnumerable<TSource> source;
    public IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector) for IEnumerable<TSource> source;

    public bool SequenceEqual<TSource>(IEnumerable<TSource> second) for IEnumerable<TSource> first;
    public bool SequenceEqual<TSource>(IEnumerable<TSource> second, IEqualityComparer<TSource>? comparer) for IEnumerable<TSource> first;

    public TSource Single<TSource>() for IEnumerable<TSource> source;
    public TSource Single<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;

    public TSource? SingleOrDefault<TSource>() for IEnumerable<TSource> source;
    public TSource SingleOrDefault<TSource>(TSource defaultValue) for IEnumerable<TSource> source;
    public TSource? SingleOrDefault<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;
    public TSource SingleOrDefault<TSource>(Func<TSource, bool> predicate, TSource defaultValue) for IEnumerable<TSource> source;

    public IEnumerable<TSource> Skip<TSource>(int count) for IEnumerable<TSource> source;

    public IEnumerable<TSource> SkipLast<TSource>(int count) for IEnumerable<TSource> source;

    public IEnumerable<TSource> SkipWhile<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;
    public IEnumerable<TSource> SkipWhile<TSource>(Func<TSource, int, bool> predicate) for IEnumerable<TSource> source;

    public float Sum<TSource>(Func<TSource, float> selector) for IEnumerable<TSource> source;
    public int Sum<TSource>(Func<TSource, int> selector) for IEnumerable<TSource> source;
    public long Sum<TSource>(Func<TSource, long> selector) for IEnumerable<TSource> source;
    public decimal? Sum<TSource>(Func<TSource, decimal?> selector) for IEnumerable<TSource> source;
    public double Sum<TSource>(Func<TSource, double> selector) for IEnumerable<TSource> source;
    public int? Sum<TSource>(Func<TSource, int?> selector) for IEnumerable<TSource> source;
    public long? Sum<TSource>(Func<TSource, long?> selector) for IEnumerable<TSource> source;
    public float? Sum<TSource>(Func<TSource, float?> selector) for IEnumerable<TSource> source;
    public double? Sum<TSource>(Func<TSource, double?> selector) for IEnumerable<TSource> source;
    public decimal Sum<TSource>(Func<TSource, decimal> selector) for IEnumerable<TSource> source;
    public double? Sum() for IEnumerable<double?> source;
    public float? Sum() for IEnumerable<float?> source;
    public long? Sum() for IEnumerable<long?> source;
    public int? Sum() for IEnumerable<int?> source;
    public decimal? Sum() for IEnumerable<decimal?> source;
    public long Sum() for IEnumerable<long> source;
    public int Sum() for IEnumerable<int> source;
    public double Sum() for IEnumerable<double> source;
    public decimal Sum() for IEnumerable<decimal> source;
    public float Sum() for IEnumerable<float> source;

    public IEnumerable<TSource> Take<TSource>(Range range) for IEnumerable<TSource> source;
    public IEnumerable<TSource> Take<TSource>(int count) for IEnumerable<TSource> source;

    public IEnumerable<TSource> TakeLast<TSource>(int count) for IEnumerable<TSource> source;

    public IEnumerable<TSource> TakeWhile<TSource>(Func<TSource, int, bool> predicate) for IEnumerable<TSource> source;
    public IEnumerable<TSource> TakeWhile<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;

    public IOrderedEnumerable<TSource> ThenBy<TSource, TKey>(this IOrderedEnumerable<TSource> source, Func<TSource, TKey> keySelector);
    public IOrderedEnumerable<TSource> ThenBy<TSource, TKey>(this IOrderedEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer);

    public IOrderedEnumerable<TSource> ThenByDescending<TSource, TKey>(this IOrderedEnumerable<TSource> source, Func<TSource, TKey> keySelector);
    public IOrderedEnumerable<TSource> ThenByDescending<TSource, TKey>(this IOrderedEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer);

    public TSource[] ToArray<TSource>() for IEnumerable<TSource> source;

    public Dictionary<TKey, TElement> ToDictionary<TSource, TKey, TElement>(Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TSource> source where TKey : notnull;
    public Dictionary<TKey, TElement> ToDictionary<TSource, TKey, TElement>(Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector) for IEnumerable<TSource> source where TKey : notnull;
    public Dictionary<TKey, TSource> ToDictionary<TSource, TKey>(Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TSource> source where TKey : notnull;
    public Dictionary<TKey, TSource> ToDictionary<TSource, TKey>(Func<TSource, TKey> keySelector) for IEnumerable<TSource> source where TKey : notnull;
    public Dictionary<TKey, TValue> ToDictionary<TKey, TValue>([TupleElementNames(new[] { "Key", "Value" })] this IEnumerable<(TKey Key, TValue Value)> source) where TKey : notnull;
    public Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source, IEqualityComparer<TKey>? comparer) where TKey : notnull;
    public Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source) where TKey : notnull;
    public Dictionary<TKey, TValue> ToDictionary<TKey, TValue>([TupleElementNames(new[] { "Key", "Value" })] this IEnumerable<(TKey Key, TValue Value)> source, IEqualityComparer<TKey>? comparer) where TKey : notnull;

    public HashSet<TSource> ToHashSet<TSource>() for IEnumerable<TSource> source;
    public HashSet<TSource> ToHashSet<TSource>(IEqualityComparer<TSource>? comparer) for IEnumerable<TSource> source;

    public List<TSource> ToList<TSource>() for IEnumerable<TSource> source;

    public ILookup<TKey, TElement> ToLookup<TSource, TKey, TElement>(Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector) for IEnumerable<TSource> source;
    public ILookup<TKey, TElement> ToLookup<TSource, TKey, TElement>(Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TSource> source;

    public ILookup<TKey, TSource> ToLookup<TSource, TKey>(Func<TSource, TKey> keySelector) for IEnumerable<TSource> source;
    public ILookup<TKey, TSource> ToLookup<TSource, TKey>(Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TSource> source;

    public bool TryGetNonEnumeratedCount<TSource>(out int count) for IEnumerable<TSource> source;
    
    public IEnumerable<TSource> Union<TSource>(IEnumerable<TSource> second) for IEnumerable<TSource> first;
    public IEnumerable<TSource> Union<TSource>(IEnumerable<TSource> second, IEqualityComparer<TSource>? comparer) for IEnumerable<TSource> first;

    public IEnumerable<TSource> UnionBy<TSource, TKey>(IEnumerable<TSource> second, Func<TSource, TKey> keySelector) for IEnumerable<TSource> first;
    public IEnumerable<TSource> UnionBy<TSource, TKey>(IEnumerable<TSource> second, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer) for IEnumerable<TSource> first;

    public IEnumerable<TSource> Where<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source;
    public IEnumerable<TSource> Where<TSource>(Func<TSource, int, bool> predicate) for IEnumerable<TSource> source;

    public IEnumerable<(TFirst First, TSecond Second, TThird Third)> Zip<TFirst, TSecond, TThird>(IEnumerable<TSecond> second, IEnumerable<TThird> third) for IEnumerable<TFirst> first;
    public IEnumerable<(TFirst First, TSecond Second)> Zip<TFirst, TSecond>(IEnumerable<TSecond> second) for IEnumerable<TFirst> first;
    public IEnumerable<TResult> Zip<TFirst, TSecond, TResult>(IEnumerable<TSecond> second, Func<TFirst, TSecond, TResult> resultSelector) for IEnumerable<TFirst> first;
}
