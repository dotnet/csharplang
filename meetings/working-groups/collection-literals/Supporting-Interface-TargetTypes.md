## Notes for supporting interfaces.

1.  Interfaces are important. For apis that take collections, outside of the large set of api the BCL has that deal with contiguous data (and thus take arrays or spans), the `System.Collections.XXX` collection types are the next most commonly used set.   Of the types in that namespace, the interface collections `IEnumerable<T>/ICollection<T>/IReadOnlyCollection<T>/IList<T>/IReadOnlyList<T>` are used ~90% of the time.  And when used, 90% of those are `IEnumerable<T>` itself.  If we include linq-extensions this number shoots even higher up.  But we've cut those out as *currently* you cannot call extensions on collection-exprs.  

2. There is a coherent, and explainable difference between "natural types" and interfaces.  While both relate to the idea of "a type must be picked for you", the needs and choices differ between the two.

   For natural types we believe the primary case where it is picked is for `var`.  In the `var` case the code exists in a mutable location where users generally have full control to manipulate as they desire.  Collections can be used to buffer, manipulate, filter, and generally act as a temporary scratch location to produce a final result.  As such, a mutable type (like `List<T>`) is the most 'natural' type here given the need for general flexibility.  `List<T>` itself is the most commonly used collection type for this purpose, and it naturally aligns semantically with the sequence of elements that a collection expression syntactically represents.  Like `Dictionary<K,V>` it presents what the ecosystem thinks of as *the* collection type when trying to manipulate values.

   Note that natural types do also appear both for dynamic and object.  However, It does not feel like these cases are substantively different or interesting enough to warrant a different 'natural type' (though the point is open for debate).

   Conversely, we expect interface-target types to be most commonly used when passing data to other APIs, or when used to store data for properties the user has (and exposes) of those interface types.  In these cases, mutability is undesirable.  And, indeed, can be a source of problems (as external consumers might be able to change your data).

   Because they core scenarios differ so wildly on their expectations around mutability and around the problems that arise (for either) by aligning on what the other does, it seems reasonable to state and explain things if we go down a path where there are different types and choices between natural and target types.

3. Furthermore, alignment between the two systems (target and natural typing) could actually be highly negative if we promise a concrete collection type. For example, if we promise a `List<T>` type, then that eliminates the ability to use a singleton for the *very* common and *core* use-case of passing `[]` as an argument to `IEnumerable` apis (or initializing an `IEnumerable` property).  This would force users away from this, and require them to be as verbose as today to be explicit to get the desired savings.  This deeply undercuts are core narrative that you can use collection expressions and they can be the *efficient* type you can depend on.

   Conversely, by not promising a concrete type, we get more opportunities to optimize, even for *non* empty cases.  For example, with:

      ```c#
      Goo(IEnumerable<int> values);
      Goo([1, 2, 3])
      ```

   We can optimize in a way similar to what we do with `yield`ing iterators.  Namely, you would an `IEnumerable<T>` instance that was itself its own `IEnumerator<T>` instance (in the case where it has not been enumerated yet, and was being enumerated on the same thread that created it).  This would again be much more efficient than what you get today with any normal concrete type a user uses here (lists, arrays, etc.)   This again ties heavily into our story that by using collection expressions you can trust you're going to get the best results.

   As before, promising too much can hurt us (and defy user expectations around performance and safety).

4. Certain concrete types (specifically `List<T>` or `T[]`) have highly undesirable downsides for an interface.  Specifically, it would make it a very dangerous footgun for any API to expose an `IEnumerable<T>/IReadOnlyList<T>` property that was instantiated with a collection expression.  A set of users would be very unhappy and perturbed to discover that they switched away from an existing read-collection type to a collection expression and now had things become unsafe.  This would tie into the feeling that collection expressions could not be trusted to make sound decisions that people could trust.

## Options

If we picked an existing concrete collection type to instantiate when targeting an interface type, the choices would likely be the following:

1. `List<T>`.  The most flexible.  But also not very efficient, and definitely unsafe to expose through an `IEnumerable<T>/IReadOnlyList<T>` property.  
2. `T[]`.  A strange middle ground.  Only partially flexibly.  Can mutate the values in the array themselves, but cannot do anything affecting the length of the array.  Also unsafe to expose through an`IEnumerable<T>/IReadOnlyList<T>` property.  
3. `ImmutableArray<T>`.  The least flexible.  Provides safety for elements.  But now does not work well for the case of passing to `ICollection<T>/IList<T>` (which can mutate the number of elements).

If we don't pick a concrete type, we have a few options at our disposal:

1. State you only get an unnamed impl of *only* that target interface, and nothing else.  For example, if you target typed to `IEnumerable<T>`, you would only get a type type implemented `IEnumerable<T>` and nothing else.  This would be entirely safe (no worry about something mutating your values, or taking a dependency on a concrete type).  However, it would also leave perf on the table.  Many BCL apis optimize themselves by querying for `IList<T>` to take a fast path if the value exposes that interface.

2. State you get an unnamed impl of a type that is guaranteed to implement both `IReadOnlyList<T>` and `IList<T>` *and* that that type returns `true` for `ICollection<T>.IsReadOnly` or not, depending on what you were targeting.  If you were targeting only an `IEnumerable<T>/IReadOnlyCollection<T>/IReadOnlyList<T>` you would get something read-only.  If you asked for an `IList<T>/ICollection<T>` it would be non-read-only.  This would have the benefit of being entirely safe for the non-mutation cases (because the collection IsReadOnly, consumers could not mutate elements).  It would also be something highly optimizable.  BCL methods would query for `IList<T>` and do fast paths for it.  We could also still fast-path our `GetEnumerator`` impl to be low allocs.

    Note: generating our own type that implements `IList<T>` is really not a big deal.  The entire* API for `IList<T>` is as follows:

    ```c#
    // From IEnumerable<T>
    public System.Collections.Generic.IEnumerator<out T> GetEnumerator();

    // From ICollection<T>
    public int Count { get; }
    public bool IsReadOnly { get; }
    public void Add(T item);
    public void Clear();
    public bool Contains(T item);
    public void CopyTo(T[] array, int arrayIndex);
    public bool Remove(T item);

    // From IList<T>
    public T this[int index] { get; set; }
    public int IndexOf(T item);
    public void Insert(int index, T item);
    public void Insert(int index, T item);
    ```

This is really not a large api surface area to support.  We generate similar levels of support
when we do things like records (with ToString/Equals/GetHashCode/Deconstruct/op_Equality/PrintMembers/Clone/EqualityContract).

With those options above, this suggestions a potential intriguing option then to have natural-type and target-typed interfaces fall out. We could consider that the natural type was `IList<T>`.  If we had that then we'd have a clear way to give an impl that would work for `IEnumerable<T>/ICollection<T>/IReadOnlyCollection<T>/IReadOnlyList<T>/IList<T>`, and we'd have a natural type that you could use locally.  This would have the downside of methods on this then involving interface dispatch by default.  But perhaps that's ok for a local type intended for scratch space.  We could also optimize cases where we saw the variable did not escape to just use `List<T>` and make direct calls to methods on it.

