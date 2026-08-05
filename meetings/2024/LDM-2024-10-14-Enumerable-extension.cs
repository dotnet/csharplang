namespace System.Linq;

public extension Enumerable<T> for IEnumerable<T>
{
    public T Aggregate(Func<T, T, T> func);
    public TAccumulate Aggregate<TAccumulate>(TAccumulate seed, Func<TAccumulate, T, TAccumulate> func);
    public TResult Aggregate<TAccumulate, TResult>(TAccumulate seed, Func<TAccumulate, T, TAccumulate> func, Func<TAccumulate, TResult> resultSelector);

    public IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateBy<TKey, TAccumulate>(Func<T, TKey> keySelector, TAccumulate seed, Func<TAccumulate, T, TAccumulate> func, IEqualityComparer<TKey>? keyComparer = null) where TKey : notnull;
    public IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateBy<TKey, TAccumulate>(Func<T, TKey> keySelector, Func<TKey, TAccumulate> seedSelector, Func<TAccumulate, T, TAccumulate> func, IEqualityComparer<TKey>? keyComparer = null) where TKey : notnull;

    public bool All(Func<T, bool> predicate);

    public bool Any();
    public bool Any(Func<T, bool> predicate);

    public IEnumerable<T> Append(T element);

    public IEnumerable<T> AsEnumerable();

    public float? Average(Func<T, float?> selector);
    public double? Average(Func<T, long?> selector);
    public double? Average(Func<T, int?> selector);
    public double? Average(Func<T, double?> selector);
    public decimal? Average(Func<T, decimal?> selector);
    public double Average(Func<T, long> selector);
    public double Average(Func<T, int> selector);
    public double Average(Func<T, double> selector);
    public decimal Average(Func<T, decimal> selector);
    public float Average(Func<T, float> selector);

    public IEnumerable<T[]> Chunk(int size);

    public IEnumerable<T> Concat(IEnumerable<T> other);

    public bool Contains(T value);
    public bool Contains(T value, IEqualityComparer<T>? comparer);

    public int Count();
    public int Count(Func<T, bool> predicate);
    public IEnumerable<KeyValuePair<TKey, int>> CountBy<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey>? keyComparer = null) where TKey : notnull;

    public IEnumerable<T?> DefaultIfEmpty();
    public IEnumerable<T> DefaultIfEmpty(T defaultValue);

    public IEnumerable<T> Distinct();
    public IEnumerable<T> Distinct(IEqualityComparer<T>? comparer);
    public IEnumerable<T> DistinctBy<TKey>(Func<T, TKey> keySelector);
    public IEnumerable<T> DistinctBy<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer);

    public T ElementAt(Index index);
    public T ElementAt(int index);
    public T? ElementAtOrDefault(Index index);
    public T? ElementAtOrDefault(int index);

    public IEnumerable<T> Except(IEnumerable<T> other);
    public IEnumerable<T> Except(IEnumerable<T> other, IEqualityComparer<T>? comparer);
    public IEnumerable<T> ExceptBy<TKey>(IEnumerable<TKey> other, Func<T, TKey> keySelector);
    public IEnumerable<T> ExceptBy<TKey>(IEnumerable<TKey> other, Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer);

    public T First(Func<T, bool> predicate);
    public T First();
    public T FirstOrDefault(Func<T, bool> predicate, T defaultValue);
    public T FirstOrDefault(T defaultValue);
    public T? FirstOrDefault();
    public T? FirstOrDefault(Func<T, bool> predicate);

    public IEnumerable<TResult> GroupBy<TKey, TResult>(Func<T, TKey> keySelector, Func<TKey, IEnumerable<T>, TResult> resultSelector, IEqualityComparer<TKey>? comparer);
    public IEnumerable<TResult> GroupBy<TKey, TElement, TResult>(Func<T, TKey> keySelector, Func<T, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector, IEqualityComparer<TKey>? comparer);
    public IEnumerable<TResult> GroupBy<TKey, TElement, TResult>(Func<T, TKey> keySelector, Func<T, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector);
    public IEnumerable<TResult> GroupBy<TKey, TResult>(Func<T, TKey> keySelector, Func<TKey, IEnumerable<T>, TResult> resultSelector);
    public IEnumerable<IGrouping<TKey, T>> GroupBy<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer);
    public IEnumerable<IGrouping<TKey, TElement>> GroupBy<TKey, TElement>(Func<T, TKey> keySelector, Func<T, TElement> elementSelector);
    public IEnumerable<IGrouping<TKey, TElement>> GroupBy<TKey, TElement>(Func<T, TKey> keySelector, Func<T, TElement> elementSelector, IEqualityComparer<TKey>? comparer);
    public IEnumerable<IGrouping<TKey, T>> GroupBy<TKey>(Func<T, TKey> keySelector);

    public IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, IEnumerable<TInner>, TResult> resultSelector);
    public IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, IEnumerable<TInner>, TResult> resultSelector, IEqualityComparer<TKey>? comparer);

    public IEnumerable<(int Index, T Item)> Index();

    public IEnumerable<T> Intersect(IEnumerable<T> other);
    public IEnumerable<T> Intersect(IEnumerable<T> other, IEqualityComparer<T>? comparer);
    public IEnumerable<T> IntersectBy<TKey>(IEnumerable<TKey> other, Func<T, TKey> keySelector);
    public IEnumerable<T> IntersectBy<TKey>(IEnumerable<TKey> other, Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer);

    public IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector);
    public IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey>? comparer);

    public T Last(Func<T, bool> predicate);
    public T Last();
    public T LastOrDefault(Func<T, bool> predicate, T defaultValue);
    public T LastOrDefault(T defaultValue);
    public T? LastOrDefault();
    public T? LastOrDefault(Func<T, bool> predicate);

    public long LongCount();
    public long LongCount(Func<T, bool> predicate);

    public decimal Max(Func<T, decimal> selector);
    public double Max(Func<T, double> selector);
    public TResult? Max<TResult>(Func<T, TResult> selector);
    public int Max(Func<T, int> selector);
    public long Max(Func<T, long> selector);
    public int? Max(Func<T, int?> selector);
    public double? Max(Func<T, double?> selector);
    public long? Max(Func<T, long?> selector);
    public float? Max(Func<T, float?> selector);
    public float Max(Func<T, float> selector);
    public decimal? Max(Func<T, decimal?> selector);
    public T? Max(IComparer<T>? comparer);
    public T? Max();
    public T? MaxBy<TKey>(Func<T, TKey> keySelector, IComparer<TKey>? comparer);
    public T? MaxBy<TKey>(Func<T, TKey> keySelector);

    public double Min(Func<T, double> selector);
    public int Min(Func<T, int> selector);
    public long Min(Func<T, long> selector);
    public decimal? Min(Func<T, decimal?> selector);
    public double? Min(Func<T, double?> selector);
    public decimal Min(Func<T, decimal> selector);
    public long? Min(Func<T, long?> selector);
    public float? Min(Func<T, float?> selector);
    public float Min(Func<T, float> selector);
    public TResult? Min<TResult>(Func<T, TResult> selector);
    public int? Min(Func<T, int?> selector);
    public T? Min(IComparer<T>? comparer);
    public T? Min();
    public T? MinBy<TKey>(Func<T, TKey> keySelector, IComparer<TKey>? comparer);
    public T? MinBy<TKey>(Func<T, TKey> keySelector);

    public IOrderedEnumerable<T> Order(IComparer<T>? comparer);
    public IOrderedEnumerable<T> Order();
    public IOrderedEnumerable<T> OrderBy<TKey>(Func<T, TKey> keySelector);
    public IOrderedEnumerable<T> OrderBy<TKey>(Func<T, TKey> keySelector, IComparer<TKey>? comparer);
    public IOrderedEnumerable<T> OrderByDescending<TKey>(Func<T, TKey> keySelector);
    public IOrderedEnumerable<T> OrderByDescending<TKey>(Func<T, TKey> keySelector, IComparer<TKey>? comparer);
    public IOrderedEnumerable<T> OrderDescending(Comparer<T>? comparer);
    public IOrderedEnumerable<T> OrderDescending();

    public IEnumerable<T> Prepend(T element);

    public IEnumerable<T> Reverse();

    public IEnumerable<TResult> Select<TResult>(Func<T, int, TResult> selector);
    public IEnumerable<TResult> Select<TResult>(Func<T, TResult> selector);

    public IEnumerable<TResult> SelectMany<TCollection, TResult>(Func<T, int, IEnumerable<TCollection>> collectionSelector, Func<T, TCollection, TResult> resultSelector);
    public IEnumerable<TResult> SelectMany<TResult>(Func<T, int, IEnumerable<TResult>> selector);
    public IEnumerable<TResult> SelectMany<TResult>(Func<T, IEnumerable<TResult>> selector);
    public IEnumerable<TResult> SelectMany<TCollection, TResult>(Func<T, IEnumerable<TCollection>> collectionSelector, Func<T, TCollection, TResult> resultSelector);

    public bool SequenceEqual(IEnumerable<T> other);
    public bool SequenceEqual(IEnumerable<T> other, IEqualityComparer<T>? comparer);

    public T Single();
    public T Single(Func<T, bool> predicate);
    public T? SingleOrDefault();
    public T SingleOrDefault(T defaultValue);
    public T? SingleOrDefault(Func<T, bool> predicate);
    public T SingleOrDefault(Func<T, bool> predicate, T defaultValue);

    public IEnumerable<T> Skip(int count);
    public IEnumerable<T> SkipLast(int count);
    public IEnumerable<T> SkipWhile(Func<T, bool> predicate);
    public IEnumerable<T> SkipWhile(Func<T, int, bool> predicate);

    public float Sum(Func<T, float> selector);
    public int Sum(Func<T, int> selector);
    public long Sum(Func<T, long> selector);
    public decimal? Sum(Func<T, decimal?> selector);
    public double Sum(Func<T, double> selector);
    public int? Sum(Func<T, int?> selector);
    public long? Sum(Func<T, long?> selector);
    public float? Sum(Func<T, float?> selector);
    public double? Sum(Func<T, double?> selector);
    public decimal Sum(Func<T, decimal> selector);

    public IEnumerable<T> Take(Range range);
    public IEnumerable<T> Take(int count);
    public IEnumerable<T> TakeLast(int count);
    public IEnumerable<T> TakeWhile(Func<T, int, bool> predicate);
    public IEnumerable<T> TakeWhile(Func<T, bool> predicate);

    public T[] ToArray();

    public Dictionary<TKey, TElement> ToDictionary<TKey, TElement>(Func<T, TKey> keySelector, Func<T, TElement> elementSelector, IEqualityComparer<TKey>? comparer) where TKey : notnull;
    public Dictionary<TKey, TElement> ToDictionary<TKey, TElement>(Func<T, TKey> keySelector, Func<T, TElement> elementSelector) where TKey : notnull;
    public Dictionary<TKey, T> ToDictionary<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer) where TKey : notnull;
    public Dictionary<TKey, T> ToDictionary<TKey>(Func<T, TKey> keySelector) where TKey : notnull;

    public HashSet<T> ToHashSet();
    public HashSet<T> ToHashSet(IEqualityComparer<T>? comparer);

    public List<T> ToList();

    public ILookup<TKey, TElement> ToLookup<TKey, TElement>(Func<T, TKey> keySelector, Func<T, TElement> elementSelector);
    public ILookup<TKey, TElement> ToLookup<TKey, TElement>(Func<T, TKey> keySelector, Func<T, TElement> elementSelector, IEqualityComparer<TKey>? comparer);
    public ILookup<TKey, T> ToLookup<TKey>(Func<T, TKey> keySelector);
    public ILookup<TKey, T> ToLookup<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer);

    public bool TryGetNonEnumeratedCount(out int count);

    public IEnumerable<T> Union(IEnumerable<T> other);
    public IEnumerable<T> Union(IEnumerable<T> other, IEqualityComparer<T>? comparer);
    public IEnumerable<T> UnionBy<TKey>(IEnumerable<T> other, Func<T, TKey> keySelector);
    public IEnumerable<T> UnionBy<TKey>(IEnumerable<T> other, Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer);

    public IEnumerable<T> Where(Func<bool> predicate);
    public IEnumerable<T> Where(Func<int, bool> predicate);

    public IEnumerable<(T First, TSecond Second)> Zip<TSecond>(IEnumerable<TSecond> second);
    public IEnumerable<TResult> Zip<TSecond, TResult>(IEnumerable<TSecond> second, Func<T, TSecond, TResult> resultSelector);
    public IEnumerable<(T First, TSecond Second, TThird Third)> Zip<TSecond, TThird>(IEnumerable<TSecond> second, IEnumerable<TThird> third);
}

public extension Enumerable for IEnumerable
{
    public IEnumerable<TResult> Cast<TResult>();
    public IEnumerable<TResult> OfType<TResult>();

    // Non-extension static methods - can they live here too?
    public abstract static IEnumerable<TResult> Empty<TResult>();
    public abstract static IEnumerable<int> Range(int start, int count);
    public abstract static IEnumerable<TResult> Repeat<TResult>(TResult element, int count);
}

public extension OrderedEnumerable<T> for IOrderedEnumerable<T>
{
    public IOrderedEnumerable<T> ThenBy<TKey>(Func<T, TKey> keySelector);
    public IOrderedEnumerable<T> ThenBy<TKey>(Func<T, TKey> keySelector, IComparer<TKey>? comparer);
    public IOrderedEnumerable<T> ThenByDescending<TKey>(Func<T, TKey> keySelector);
    public IOrderedEnumerable<T> ThenByDescending<TKey>(Func<T, TKey> keySelector, IComparer<TKey>? comparer);
}

public extension KeyValuePairEnumerable<TKey, TValue> for IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : notnull
{
    public Dictionary<TKey, TValue> ToDictionary();
    public Dictionary<TKey, TValue> ToDictionary(IEqualityComparer<TKey>? comparer);
}

public extension KeyValueTupleEnumerable<TKey, TValue> for IEnumerable<(TKey, TValue)>
    where TKey : notnull
{
    public Dictionary<TKey, TValue> ToDictionary();
    public Dictionary<TKey, TValue> ToDictionary(IEqualityComparer<TKey>? comparer);
}

public extension IntEnumerable for IEnumerable<int>
{
    public double Average();
    public int Max();
    public int Min();
    public int Sum();
}

public extension LongEnumerable for IEnumerable<long>
{
    public double Average();
    public long Max();
    public long Min();
    public long Sum();
}

public extension FloatEnumerable for IEnumerable<float>
{
    public float Average();
    public float Max();
    public float Min();
    public float Sum();
}

public extension DoubleEnumerable for IEnumerable<double>
{
    public double Average();
    public double Max();
    public double Min();
    public double Sum();
}

public extension DecimalEnumerable for IEnumerable<decimal>
{
    public decimal Average();
    public decimal Max();
    public decimal Min();
    public decimal Sum();
}

public extension NullableIntEnumerable for IEnumerable<int?>
{
    public double? Average();
    public int? Max();
    public int? Min();
    public int? Sum();
}

public extension NullableLongEnumerable for IEnumerable<long?>
{
    public double? Average();
    public long? Max();
    public long? Min();
    public long? Sum();
}

public extension NullableFloatEnumerable for IEnumerable<float?>
{
    public float? Average();
    public float? Max();
    public float? Min();
    public float? Sum();
}

public extension NullableDoubleEnumerable for IEnumerable<double?>
{
    public double? Average();
    public double? Max();
    public double? Min();
    public double? Sum();
}

public extension NullableDecimalEnumerable for IEnumerable<decimal?>
{
    public decimal? Average();
    public decimal? Max();
    public decimal? Min();
    public decimal? Sum();
}
