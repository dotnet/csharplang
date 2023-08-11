## Collection expressions - compiler synthesized types

Followup to https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/collection-literals/Core-interface-target-type-proposal.md.

when the user has one of the following types:

1. `IEnumerable<T>`
1. `IReadOnlyCollection<T>`
1. `IReadOnlyList<T>`
1. `ICollection<T>`
1. `IList<T>`

and they target that type with a collection-expression, we will choose a type on their behalf that satisfies the target interface.

### Simple C# 12 approach

Simplest solution for the short term (C# 12) would likely be:

1. For `IList<T>` and `ICollection<T>` just use `List<T>`.

    - Pros: The type is always available, tuned for the ecosystem and receives updates.  We likely could not do much better than it, and doing so would involve owning a large surface area of code to generate in the compiler.

    - Cons: If we do this, it is likely some code out there will take a dependency on this.  As such, changing to another type in the future may risk breaks.  Those breaks would be around code doing undocumented and unsupported things, but would still likely happen and could cause customer ire.

1. For `IEnumerable<T>`, `IReadOnlyCollection<T>` and `IReadOnlyList<T>`, synthesize a type for the user. The type should implement all those interfaces *and* `IList<T>` and `IList`.  These two interfaces help ensure the collections we produce are good citizens in the .Net ecosystem.  Lots of code checks for these two interfaces for varying reasons (perf optimizations, compat with winforms checks, etc.).  This also matches what the most common list-like BCL collections (`T[]`, `List<T>` and `ImmutableArray<T>`) do as well.  So this makes our type as usable as they are in practice.  Note: if the collection is empty, we can use just `Array.Empty<T>()`.

    - Pros: We have broad power in the future to change our implementations, producing more specialized types, or deferring to potential future BCL apis.

    - Cons: The BCL cannot specialize *as much* with an external synthesized type as they can with the types they know about.

### Simple synthesized type for read-only interfaces

For C# 12, the simplest synthesized type we should likely go with when the user target-types to `IEnumerable<T>`, `IReadOnlyCollection<T>` or `IReadOnlyList<T>` is:

```c#
// TODO: Attributes?
// [DebuggerDisplay("Count = {Count}")]
// [Serializable]
internal sealed class SimpleCompilerSynthesizedList<T> : IReadOnlyList<T>, IList<T>, IList
{
    private readonly T[] _values;

    public CompilerSynthesizedList(T[] values)
        => _values = values;

    // IEnumerable/IEnumerable<T>

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_values).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

    // ICollection/ICollection<T>/IReadOnlyCollection<T>

    public bool IsReadOnly => true;
    public bool IsSynchronized => false;
    public int Count => _values.Length;

    public bool Contains(T item) => _values.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);
    public void CopyTo(Array array, int index) => _values.CopyTo(array, index);

    public void Add(T item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public bool Remove(T item) => throw new NotSupportedException();

    // Not sure what this should return:
    public object SyncRoot => throw new NotSupportedException();

    // IList/IList<T>/IReadOnlyList<T>

    public bool IsFixedSize => true;

    public T this[int index] { get => _values[index]; set => throw new NotSupportedException(); }
    object? IList.this[int index] { get => _values[index]; set => throw new NotSupportedException(); }

    public int IndexOf(T item) => ((IList<T>)_values).IndexOf(item);
    public int IndexOf(object? value) => ((IList)_values).IndexOf(value);
    public bool Contains(object? value) => ((IList)_values).Contains(value);

    public int Add(object? value) => throw new NotSupportedException();
    public void Insert(int index, T item) => throw new NotSupportedException();
    public void Insert(int index, object? value) => throw new NotSupportedException();
    public void Remove(object? value) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
}
```

This is just a wrapper around an array.  Non-mutating methods delegate directly to the array.  Mutating ones throw (as allowed by the documentation for these apis). `ICollection<T>.IsReadOnly` properly indicates the expected behavior here.

### Future dictionary support

When dictionary support is added to collection expressions, we will likely want to follow a complimentary path. Fortunately, dictionaries are likely simpler to support.  Namely:

When when the user has one of the following types:

1. `IReadOnlyDictionary<TKey, TValue>`
1. `IDictionary<TKey, TValue>`

and they target that type with a collection-expression, we will choose a type on their behalf that satisfies the target interface.

1. For `IDictionary<TKey, TValue>` just use `Dictionary<TKey, TValue>`.  The pros/cons are the same as for using `List<T>` as the type chosed for `IList<T>/ICollection<T>`.

1. For `IReadOnlyDictionary<T>` synthesize a type for the user.  Unlike the list-like collection space, there is no need for the synthesized type to implement anything beyond this surface area (as we are unaware of code that tries down-casting to `IDictionary<,>` to perform optimizations). The types generated here can be heavily optimized.  For example:

    - We can synthesize specialized types for empty/small dictionaries (with no need for actual complex hashing strategies).

    - The implementations could return themselves for `Keys`.  e.g. `IEnumerable<TKey> Keys => this;`

### Future read-only list optimization ideas

1. Optimize for small, or fixed, collection expression counts.  While we will already optimize the empty collection expression case, we can also optimize small counts like so:

    ```c#
    internal sealed class SmallCompilerSynthesizedList2<T> : IReadOnlyList<T>, IList<T>, IList
    {
        private readonly T _v1;
        private readonly T _v2;

        public SmallCompilerSynthesizedList2(T v1, T v2)
            => (_v1 = v1, _v2 = v2);

        public int Count => 2;

        // remainder of methods optimized to just access fields directly.
    }
    ```

    This would avoid the array indirection, as well as the extra space taken up by the array overhead itself.

1. Optimize the synthesized collection for the common case where it is consumed only to be iterated one.  Specifically, for when the collection was passed *to* a consumer like:

    ```C#
    void DoWork(IEnumerable<int> values) { ... }

    DoWork([a, b, c]);
    ```
    
    In this case, we could synthesize the type like so:

    ```c#
    internal sealed class FastEnumerateCompilerSynthesizedList<T> : IReadOnlyList<T>, IList<T>, IList, /*new*/ IEnumerator<T>
    {
        private int _enumeratorIndex = -2;
        private int _initialThreadId = Environment.CurrentManagedThreadId;

        // actual underlying data, using whatever strategy we choose.

        public IEnumerator<T> GetEnumerator()
        {
            if (_enumeratorIndex == -2 && _initialThreadId == Environment.CurrentManagedThreadId)
            {
                _enumeratorIndex = -1;
                return this;
            }

            return new HeapAllocatedEnumerator(...);
        }

        public bool MoveNext()
            => ++_enumeratorIndex < this.Count;

        public T Current => this[_enumeratorIndex];
    }
    ```

    This follows the same strategy we take when creating `IEnumerable<T>` instances with `yield` iterators.  There we also produce an `IEnumerable<T>` which is its own non-allocating `IEnumerator<T>` for the first consumer that tries to enumerate it on the same thread.

    We would likely not do this for collections stored into a location (like into a field).  In that case, it seems more likely that the value might be enumerated many times.  So this would really only be beneficial for the collection-expressions passed to r-values.

1. Optimize patterns found in arguments to produce collections that do not actually need storage for those values.  For example:

    ```c#
    IEnumerable<int> values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    // Can be emitted as:
    IEnumerable<int> values = Enumerable.Range(1, 10);

    // Or with a specialized compiler synthesized impl that does the same.
    ```

    Similar types of optimizations may be possible depending on how deep we want to go with analysis of the argument values.

1. Optimize collection expressions of blittable constants.  For example:

    ```c#
    IEnumerable<char> values = ['a', 'b', 'c', 'd', 'e', 'f'];

    // Can be emitted as:

    internal sealed class BlittableWrapperX : IReadOnlyList<T>, IList<T>, IList
    {
        // Has no-alloc optimization
        private static ReadOnlySpan<char> __values => new char[] { 'a', 'b', 'c', 'd', 'e', 'f' };

        public IEnumerator<char> GetEnumerator()
        {
            for (int i, n = 6; i < n> i++)
                yield return __values[i];
        }
    }
    ```
