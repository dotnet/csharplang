## Collection expression working group questions and suggestions

### Ref safety

Discussion and examples do not apply to ReadOnlySpan of constant, blittable data.  That always has global-scope.

Q: What should the ref-safety scope be for:

```c#
Span<int> s = [x, y, z];
```

There are two primary directions we can go here:

1. Analyze how the variable is use that to inform the scenario. Specifically, we could use flow analysis to determine what scope a variable should have.  For example:

    ```c#
    void M()
    {
        // Local scoped, s does not escape.
        Span<int> s = [x, y, z];
        foreach (var v in s)
            Console.WriteLine(v);
    }
    ```

    ```c#
    ReadOnlySpan<int> M()
    {
        // Global scoped, since 's' escapes out of 'M'.
        Span<int> s = [x, y, z];
        UseSpan(s);
        return s;
    }
    ```

    Pros: Users can write spans + collection-expressions in a very simple fashion, and have the code "just work".

    Cons: Unclear about what's actually happening.  Minor changes could lead to very different outcomes. Analysis is likely also very non trivial (on the order of major flow analysis work).  Scoping constraints would have to flow backwards from escape points back to the origination point.  For example:

    ```c#
    ReadOnlySpan<int> M()
    {
        Span<int> s1 = [x, y, z];
        Span<int> s2 = X(s1);
        return s2;
    }

    Span<T> X<T>(Span<T> s) => s;
    ```

    Here, 's2' must have global scope in order to escape 'M'.  But that then must flow that constraint into the invocation of 'X'.  This will then have to flow the constraint into 's1' to finally choose that s1 is globally scoped.  Complexity rises greatly with all flow-analysis constructs. 

2. Decide on the scope directly from the declaration, instead of depending on how it is used.

    - Option A: Ref-struct local has global scope by default.  To have local-scope, add the `scoped` keyword:

        ```c#
        void M()
        {
            // 's' has 'global scope' but does not escape.  Will allocate.  If they want stack-allocation, add `scoped`
            Span<int> s = [x, y, z];
            foreach (var v in s)
                Console.WriteLine(v);
        }
        ```

        Ideally compiler tells the user to make these variables `scoped` when it can be.

        Pros: User gets the scoping they explicitly request.  When scope can be narrower, they are informed.

        Cons: Analysis is similarly complex due to requisite reverse flow analysis.  Also, WG believes the default position of users is to want a span + collection-expression to have local scope and be stack allocated.  However, to get that, users will have to write:

        ```c#
        void M()
        {
            // 's' has 'local scope'
            scoped Span<int> s = [x, y, z];
            foreach (var v in s)
                Console.WriteLine(v);
        }
        ```

        This feels punishing to the common/default case that users will want.  Importantly, it means that if a user wants to switch off from `stackalloc` to collection-expressions they get less pleasant code. 

    - Option B: Collection expressions of `ref struct` type have local scope.

        ```c#
        void M()
        {
            // 's' has 'local scope'.  Will get stack allocated.
            Span<int> s = [x, y, z];
            foreach (var v in s)
                Console.WriteLine(v);
        }
        ```

        Pros: A very simple and easy rule to explain to users.

        Default, simple, syntax gives best performance.

        No need for any sort of complex flow analysis.  Existing analysis "just works" and lets the user know if there are any problems.

        When code needs to allocate on the heap it is clear in the code that this is happening.

        Cons: if the user *does* want the span to have global scope, then they have to be explicit about that:

        ```c#
        Span<int> M()
        {
            // Either:
            Span<int> s1 = (int[])[x, y, z];
            Span<int> s1 = new[] { x, y, z };
            
            return x ? s1 : s2;
        }
        ```

        However, WG believe this is a good thing.  This will be the rare case, and having *it* be slightly more verbose is unlikely to be a burden.  In other words, we want the common, fast, case to sing with collection-expressions, while the less common, slower, case is ok to pay a small price.

Working group suggestion:

Option 2B. Collection expressions of `ref struct` type have local scope.

### Overload resolution priority

We know we want a `Span<T>` overload to be preferred over a `T[]` overload or an interface overload ( `IEnumerable<T>/IReadOnlyCollection<T>/IReadOnlyList<T>/ICollection<T>/IList<T>`).

However, we have options on how to specify things to get the above outcome.

1. Option A: Span/ReadOnlySpan is preferred over arrays and those specific interfaces when the iteration type of the span is implicitly convertible to the iteration type of the array or interface.

    The general intuition here is Spans/Arrays/Interfaces have a natural ordering we can make an airtight case around.  Specifically:

    Spans are better than arrays as they're the fast form that can subsume arrays and also be on the stack.

    Arrays are better than all those interfaces because it's a more specific type that implements the interface.

    Spans are better than interfaces both because they're already better than arrays, and because they can be thought of as morally implementing those interfaces (which may also be literally true in the future).

    However, this has oddities:

    ```c#
    void M(List<int> list);
    void M(ReadOnlySpan<int> list);

    M([1, 2, 3]); // Ambiguous.
    ```

    This seems odd.  If there are collection overloads, and one is concrete, and one is a span, we would say it was ambiguous, when it seems clear that one exists for perf and should be preferred.

1. Option B: Same as option A, except that it applies to all ref-structs, not just Span/ReadOnlySpan.

    The general intuition is that anything in the ref-struct realm should be considered fast/span-like and then fit into this bucket.  

    With this approach the following would also hold:

    ```c#
    ref struct ValueList<T> { ... }

    void M(int[] list);
    void M(ValueList<int> list);

    M([1, 2, 3]); // calls the ValueList version.
    ```

    However, like Option A, this would become ambiguous with other concrete overloads:

    ```c#
    ref struct ValueList<T> { ... }

    void M(List<int> list);
    void M(ValueList<int> list);

    M([1, 2, 3]); // Ambiguous.
    ```

1. Option C: `ref struct` collection types are preferred over non-`ref struct` collection types when there is an implicit conversion from the iteration type of the `ref struct` type to the iteration type of the non-`ref struct` type.

    The general intuition here is that if the user provides a ref-struct overload that that is the 'fast' option, and thus would be preferred over other types.  This seems like the 'more likely than not' case that extends beyond just Span/ReadOnlySpan. Also seems to align with things like interpolated string handlers over System.String.

    For example:

    ```c#
    ref struct ValueSet<T> { ... }

    void M(ValueSet<int> list);
    void M(HashSet<int> list);

    M([x, y, z]); // calls the ValueList version.
    ```

    The general intuition here is that if the user provides a ref-struct overload that that is the 'fast' option, and thus would be preferred over other types.  This seems like the 'more likely than not' case that extends beyond just Span/ReadOnlySpan.

WG opinion: Leaning towards Option C, prefer ref-structs over all other types.

### Add support for `Memory<T>`

Should we add support for `Memory<T>` in C# 12.  For example:

```c#
Memory<int> m = [1, 2, 3];
```

Based on last LDM meeting, we are deciding "We will do if we have time, but it is low pri and can be cut if we do not have the time/resources"

1. We made a similar decision for inline-array types.  These types are not in the core majority of apis/use-cases, and can come later.

1. Adding support for a new type *is* a breaking change.  However, this is unavoidable in this space as any type can become a collection type in the future (through usage of the `[CollectionBuilder]` attribute).  As such, we do not think it critical to front load support to avoid this problem.

1. In practice `Memory<T>` is for async versions of `Span<T>` methods.  These are not generally overloads as one method is `Foo(Span<T>)` while the other is `FooAsync`.  So the concern about overload ambiguity in the future is low.

If we do support this in C# 12 we will do so through special-casing this exact type.  However, in the future we will likely add support for `CollectionBuilderAttribute` to point at the existing construction member like so:

```c#
[CollectionBuilder(typeof(Memory<>, "op_Implicit"))]
public readonly struct Memory<T>
{
    public static implicit operator Memory<T>(T[]? array) { ... } 
}
```

We would then define the Collection-Builder pattern as doing array-ownership-transfer (where the compiler would create the array and then give it to the final collection to own).  This would also likely apply to `ImmutableArray<T>` in the future as well, as it would point to `ImmutableCollectionMarshal.AsImmutable(T[])`
