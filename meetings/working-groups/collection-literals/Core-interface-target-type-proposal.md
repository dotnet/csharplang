## Collection expression conversion to specific interface types.

### The problem we are trying to solve

What does it mean when the user has one of the following five types (the types implements by our core list-types like `T[]`, `List<T>`, `ImmutableArray<T>`):

1. `IEnumerable<T>`
2. `IReadOnlyCollection<T>`
3. `IReadOnlyList<T>`
4. `ICollection<T>`
5. `IList<T>`

and they try to convert a collection-expression to it.  For example:

```c#
// Passing to something that can only read data.
void DoSomething(IEnumerable<int> values) { ... }
DoSomething([1, 2, 3]);
```

// or:

```c#
class Order
{
    // Intended to be mutated.
    public IList<OrderId> ProductIds { get; } = [];
}
```

Because these are interfaces, unless the BCL chooses to put the `[CollectionBuilder]` attribute on them to bless a particular way of creating them, then the language itself needs to have understanding here to do the right thing.

Note: these interfaces are already well known to the language.  '1' is well known because of the built-in support for creating iterators.  1-5 are well known as we understand and respect that any array instance is [implicitly convertible](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/conversions.md#1028-implicit-reference-conversions) to any of the above:

> From a single-dimensional array type `S[]` to `IList<T>`, `IReadOnlyList<T>`, and their base interfaces, provided that there is an implicit identity or reference conversion from S to T.


### Context

Collection expressions aim to be a substantively 'complete' replacement for the myriad of ways people create collections today.  They are intended to be used for the *vast majority* of cases where people use collections, and default (as much as possible) to choices that fit the needs of the majority of the user base.  A strong desire here is to not add "just another way to create a collection" alongside everything else, but to provide a singular *replacement* that is then suitable to move almost all code wholesale to.  It aims to replace *all* of the following forms:

1. `new T[] { ... }`
1. `new[] { ... }`
1. `stackalloc T[] { ... }`
1. `stackalloc[] { ... }`
1. `new CollectionType<Etc> { ... }`
1. `ImmutableCollectionType.Create(...)`
1. `ImmutableCollection.CreateBuilder(); builder.Add(...); ...; builder.ToImmutable()`
1. And more... including things like: `ImmutableArray<T>.Empty.Add(x).AddRange(y).etc()`

An examination of the BCL and the top 20 NuGet packages (Newtonsoft, EF, Azure, Castle, AWS, AutoMapper, and more), all of which are >400m downloads, reveals very relevant data here.  Methods taking those interface collections account for roughly 28% of all methods taking some collection type (arrays, spans, other BCL collections), and `IEnumerable<T>` alone accounts for 25% of the collection-taking methods.  This is not surprising as our own practices, and general design guidance we give the community, are simply that methods should be extremely permissive in what they accept, and be precise in what they return.  `IEnumerable<T>` (and our other collection interfaces) act as that permissive type that we and our ecosystem have broadly adopted.

Indeed, if it were not for `params T[]` (a full 50% of all collection-taking methods), `IEnumerable<T>` would be the most commonly taken collection by far for the ecosystem.

Ideally, we would ship with support for everything, but we've currently made judicious moves from C#12 to C#13 based on complexity, but also based on impact.  For example, `Dictionary expressions` were moved to C#13 to lighten our load, and because data indicates that APIs that consume those dictionary types are only <3% of all apis that take collections in the first place.  `Natural type` support has also been pushed out because the complexity is felt to be substantive enough to warrant more time.

With how important these interface types are though, we do not believe pushing out from C# 12 will allow us to ship a viable and coherent story to customers.  

### What factors are at play?

In any interesting design, there are many factors that must be assessed and considered as a whole.  For collection expressions, these include but are not limited to:

1. Simplicity. Ideally the feature works in a fashion that is both simple to explain and simple for users to understand. Using some of our modern terminology, we'd like to avoid 'decoder rings' when people use it.

2. Universality.  This restates the background context.  Namely that we want to meet the literal 97% case at launch.  This means needing good stories for arrays, spans, BCL concrete collections *and* BCL core interfaces.

3. Brevity.  It is a strong goal of this feature that users be able to just write the simple, idiomatic, collection expression form without the need to do things like add coercive casts to commonly appease the compiler.  Specifically, for casts, once you add them (e.g. `(List<int>)[1, 2, 3]`) then the benefit of the feature as a whole is vastly diminished. Where the new form isn't substantively better than the existing form (just 2 characters saved over `new List<int> {1, 2, 3}`).  Unlike other features, the cliff here is very steep, often fundamentally negating the idea that this is a valuable feature in the first place.  Many parts of the design (especially broad adoption of target-typing) has been entirely around ensuring users can just write the simple expression form and almost never have to do things to appease the language.

4. Performance. A core pillar of collection expressions that we are both evangelizing it by, and which we are seeing customers resonate with, is the idea of: "Absent external information unavailable to the compiler, the compiler should almost always do as well or better than the user could.  Often much more so.  And almost certainly with clearer code for the user to write."  Because the user can write a simple `[a, b, c, d, e]` expression, without having to explicitly state what is going on, and because we can provide so much smart understanding to each situation, we can heavily optimize.  For example:

    - If the above 5-element collection were converted to an `ImmutableArray<T>`, our emitted code could would practically always be better than users using normal construction patterns.  We can also greatly leverage extremely fast and efficient systems under the covers (like synthesizing `Inline-Arrays` types) that would generally be extremely ugly and painful for users to do themselves.

    - What we emit can adopt ecosystem best practices around performance.  For instance, `[]` can emit as efficient singletons, not causing undesirable allocations.

    While many customers will not care about performance to this level, we still want customers that do to have confidence in this feature (and we definitely do not want to see the feature immediately banned).

    It would also be highly unfortunate if we shipped and our own analyzers immediately flagged the usage as being a problem.

    Finally, part of performance means being a good .NET citizen.  So the collections we produce should be able to pick up the optimizations the BCL has today for collection types.

5. Safety.  Specifically, keeping data safe from undesirable mutation. We broadly think of users as being in two categories.  The first category generally doesn't consider it to be a safety concern to return mutable instances via read-only interfaces, and thus would suffice with any solution on our part.  However, the second group (which is likely smaller, but present, vocal, and influential) absolutely wants a roadblock at runtime to keep their exposed data safe from mutation.  Similar to the perf concerns, we believe we need a solution for this group that fits their expectations, puts them at ease, and isn't immediately banned (or flagged by analyzers) for doing unsafe things.

## Options

Note: there are truly a plethora of options available here.  However, based on feedback from LDM and the working group meetings, we tried to whittle things down to a reasonable few that we feel warrant discussion and comparison.

### Option 1: Disallow target typing to these interface types.

This is the simplest and cheapest option we have at our disposal.  We could just say that this is an error, and force the user to specify the type they want to generate.  However, we believe this produces a negative result for every factor at play above.

Specifically:

```c#
void DoSomething(IEnumerable<int> values) { ... }

// Not allowed.
DoSomething([1, 2, 3]);

// Write this instead:
DoSomething((List<int>)[1, 2, 3]);
```

and

```c#
class Order
{
    // Not allowed:
    public IReadOnlyList<OrderTag> Tags { get; } = [];

    // Write this instead:
    public IReadOnlyList<OrderTag> Tags { get; } = (ImmutableArray<OrderTag>)[];

    // Which is worse than just:
    public IReadOnlyList<OrderTag> Tags { get; } = Array.Empty<OrderTag>();
}
```

First, this immediately fails the simplicity and universality concerns. From our own experience, and the data about the ecosystem, we know that users will need to interface with, well, interfaces :).  This will be an immediate hurdle that will deeply undercut the value of this feature space, potentially even deeply tainting it for the future.

Second, collection expressions would not provide any sort of actual useful syntax or brevity for users in this case.  Because an explicit type would have to be provided, users would be left with syntax with nearly the same complexity and verbosity as what they would have to write today.  Users seeing this would rightfully ask "why would i pick this new form, that is really just the same as the old form, just with some tokens tweaked?"

Performance and safety though would be mostly neutral here.  Users who did not care about either would likely just use `List<T>`, and users who did would pick something appropriate for their domain.  However, this would still be a small tick in the 'negative' category as our claims about us making the "right, smart, best choices" for users would be undercut by then forcing the user to have to make those choices themselves.

### Option 2: Use `List<T>` for *all* of the possible interface targets.

This is the next simplest option.  It says that for any of those five interface cases we are target-typing to, we should *always* use `List<T>` as the concrete type actually created.  Specifically:

```c#
void DoSomething(IEnumerable<int> values) { ... }

// Legal. Is equivalent to `DoSomething(new List<int> { 1, 2, 3 })`
DoSomething([1, 2, 3]);
```

and

```c#
class Order
{
    // Legal. Is equivalent to: `Tags { get; } = new List<OrderTag>();
    public IReadOnlyList<OrderTag> Tags { get; } = [];
}
```

This option *nails* the "simplicity", "universality, and "brevity" aspects we are trying to solve.  Explaining to users what happens in these cases is trivial: "It makes a `List<T>`.  The part-and-parcel type of .Net that you've know about and have used for nearly 20 years now".  Similarly, it can be used for *all* these apis that take in one of these interfaces.  Finally, it always allows the nice short syntactic-form that really sells people on why collection-expressions are a superior choice to use over practically every other collection-creation constructs they can use today.

However, this also falls short in both the 'performance' and 'safety' domains.  Indeed, it does so so egregiously, that our own analyzers will flag this as inappropriate and push both the users who run these analyzers, and the users who just care about these facets of development, away from feeling they can trust this feature to live up to its recommendation as making the "right, smart, best choices" for them.   It will also likely lead to negative-evangelization, where voices in the community steer people away from the feature as a whole.

Next, for "performance", `List<T>` is actually a fairly undesirable type: 

1. It *already* comes with two allocations for itself.
2. Getting an iterator for it (through the `IEnumerable<T>.GetEnumerator` path) will produce another allocation.
3. It has excess overhead internally to support both being able to grow, and has overhead internally to ensure it is not mutated while it is being iterated.

`List<T>` is a great type when you need flexibility and permissiveness.  It is not a good choice when you know precisely what you are producing, and you have no need to access the flexible, mutation-oriented, capabilities it provides.

For "safety" `List<T>` is also an unacceptable choice for many (and our own analyzers will push you away from it).  Users who expose data currently (and safely) through the readonly interfaces `IEnumerable<T>/IReadOnlyCollection<T>/IReadOnlyList<T>` today will find that they cannot effectively move to collection expressions.  They will have to maintain the code the highly verbose and clunky code they have today, to use types like `ImmutableArray`, or things like `new List<int> { a, b, c }>.AsReadOnly()`.  This will make collection-expressions feel half baked for these users, again undercutting the story that they make "right, smart, best choices" for the user, and forcing them to lose the brevity and consistency of being able to use collection-expressions with confidence everwhere.

### Option 3: Be smart. :)

Deep discussion around the problems with Option 2 led the WG to come up with a tweak to the core idea it has, to ideally leverage nearly all its benefits, while curtailing its drawbacks.

First, it's important to look at the surface area of the read-only interfaces `IEnumerable<T>/IReadOnlyCollection<T>/IReadOnlyList<T>` and see that it is *entirely*:

```c#
{
    public IEnumerator<T> GetEnumerator(); // and the non-generic equivalent.
    public int Count { get; }
    public T this[int index] { get; }
}
```

That's it.  Just *three* members.  With that in mind the rules for target-typing an interface would be as follows:

1. We take a page from what we do today *already* for `IEnumerable<T>` and `yield` iterators, and we say that if you have a target-type of `IEnumerable<T>/IReadOnlyCollection<T>/IReadOnlyList<T>`, the language will state that you have no guarantee on any particular type being used at all.  But you will get an instance that efficiently implements the API surface area requested.  For example, the type that is used could be:

    - An unnameable, compiler synthesized type.  This could be something like what we do with arrays today (and Inline-Arrays for collection-expressions) where a specific type is generated for each specific size (including using Inline-Array tricks where available), producing values with practically no size overhead.  Or it could potentially be a generalized unnameable type that has contiguous storage (like an array) for the elements, but wraps it safely.

    - An unknown type provided by the runtime.  For example, through a method like `IEnumerable<T> Create(ReadOnlySpan<T> values)` (i.e. the builder-pattern applied to these interfaces).  This would allow the runtime itself to choose the most optimal internal representation for the data, and would also allow for it to do things like implement internal APIs it can query for specialized scenarios, or directly cast to the underlying type to do things like grab the contiguous data directly as a span.

    - Importantly, both of the above are possible, and we could even do one, then migrate to the other over different releases.  Because all that user could could ever know about is the target-interface type, there would be no source, binary, or runtime breaking changes here.  This means that in C# 12 we could use a compiler-synthesized type.  But also then migrate over to a BCL api if the BCL would like to own a canonical API for this purpose.

2. For a target-type of `ICollection<T>/IList<T>`, the type actually created would be guaranteed to be `List<T>`.

Effectively, this *intentionally* bifurcates the interfaces into two important categories.  The 'read only' interfaces, which support no mutation, and the 'mutable interfaces' which do.  Our feeling is that for the latter, there is no real better choice than `List<T>`.  It is the 'bread and butter' type the BCL has for this purpose that the entire ecosystem understands and feels comfortable with.  For the domain of mutable-sequences, it is extremely good and does its job well.


Specifically:

```c#
void DoSomething(IEnumerable<int> values) { ... }

// Allowed, an efficient type is used here, but you will not know what it is, or be able
// to take a dependency on it.
DoSomething([1, 2, 3]);
```

and

```c#
class Order
{
    // Legal. an efficient 'empty' instance will be used.
    public IReadOnlyList<OrderTag> Tags { get; } = [];

    // Legtal. Equivalent to: `{ get; } = new List<int>()`
    public IList<int> ProductIds { get; } = [];
}
```

With respect to the factors we care about, here's how the above falls out:

For 'brevity/universality', this still strongly satisfies our goals.  Users will be able to use the succinct collection-expression form for all these APIs with ease. However, compared to option 2, we now see *large* wins in both safety and performance. 

Starting with safety, we now have a good option for everyone exposing data through any of those three interfaces.  Data is safe by default (a good choice for all users).  Analyzers do not trigger, preventing our own tooling from pushing people away from using our cohesive story here.  Users who do not care about safety continue not to care, while users for whom it is very important are extremely satisfied that they can trust this feature and that it lives up to the stated goal that it will make the smart choices they can depend on, and they do not have to ban this feature, or recommend others steer away from it.  This approach also naturally extends out to when we do dictionary-literals.  There, it will also be the case that for `IReadOnlyDictionary<TKey, Value>` we will not want to expose a mutable type (like `Dictionary<TKey, TValue>`) for safety reasons.

While the wins now with 'safety' are definitely welcome, the largest benefits come in the "performance" category.  Specifically, not having to name a particular type for the read-only interfaces opens up many areas of optimization that would simply be unavailable when having to pick an existing generalized type. This would also live up to the promise that we will do an excellent job with perf, and that it would be *very* hard for a user to do the same, and practically impossible for them to do so with simple syntax.  Specifically, all of the following are available as things we could do to heavily optimize here when target-typing `IEnumerable<T>/IReadOnlyCollection<T>/IReadOnlyList<T>:

1. Empty collection expressions can always be collapsed down to a single empty singleton (per `T` element type) across the entire program.

1. We can use features like inline-arrays to generate instances that inline all their elements are literally no larger than space for the elements themselves.  A count would not even need to be stored as it could just be hard-coded as the value returned by `int Count { get; }`.  While this might generate many types in the assembly (one type per unique count), it's worth noting that this is what we *already* do for arrays *today*, and what we will be doing for the inline-array types we generate for spans/params-spans.

1. We can borrow a trick from `yield iterators` where the iterator returned from `IEnumerator<T> GetEnumerator()` literally *reuses* the `this` instance to be its own iterator *if* it has never been iterated yet, and the calling thread is the same one that generated the `IEnumerable<T>`.  This means that if you have:

    ```c#
    DoSomething([1, 2, 3]);
    ```

    Then this may be literally a *single* allocation, *including* for when it is likely almost immediately iterated.

1. We do not need any code anywhere that sits in service of supporting variable sizes, or supporting mutation.  This means no overheads of checks, or invalidation of iterators, etc.

    Overall, by not specifying explicit types, we actually open up a wealth of optimizations that both our own compiler or the BCL can choose to do.  While synthesizing types may initially seem oogy, we do already do it for many cases today, including when we already generate `IEnumerables` with `yield`, and also for every array type of a different count generated.  The surface area here is also exceptionally small.  There are literally only three, extremely basic, operations to support.  So the burden on the compiler to generate types for this feels completely acceptable, especially for the large wins we can achieve.  

    There are still benefits though with the BCL taking this over.  Specifically, if they do so, they can then implement internal interfaces they may query for.  Or they can use their knowledge of the exact internal types they use to get to the underlying data in even more efficient ways (including potentially being able to grab spans to it).  That said, if there was such an API to grab a span from an collection type, it would be good for that to be public so that the compiler could just implement that itself.

    This approach also naturally extends out to when we do dictionary-literals.  There, it will also be the case that for `IReadOnlyDictionary<TKey, Value>` we (and the BCL) could likely heavily optimize.  For example, it will often be the case that read-only dictionaries may be small (or empty).  Having specialized implementations that eschew complex wasted-space strategies for contiguous linear lookup may be highly beneficial.  Reserving that read-only implementations may be unknown, specialized, types allows for these sorts of wins to happen automatically.

Finally, we come to the one area where the above proposal ticks downward: "simplicity".  It is certainly the case that "Use `List<T>` for everything" approach is much simpler to explain.  However, we believe the above to be *acceptably* complex to explain, with enough benefits to point at to convince users that this was the right choice for them.  Specifically, we believe the best way to explain it is:

1. Are you using one of the mutation-supporting interfaces (`ICollection<T>/IList<T>`)?  If so, you get `List<T>`.  This is easy to explain and we can justify it as `List<T>` being just the best general purpose type for all the cases where mutation is desired.
2. Are you using the read-only interfaces (`IEnumerable<T>/IReadOnlyCollection<T>/IReadOnlyList<T>`)?  If so, you get a *safe*, *high performance* impl that supports the API shape needed.  This is right in line with what we already do for you today when you use `IEnumerable<T>` with `yield`.  Your instance will be safe, with no concern about data changing out from you unexpectedly.  And, if you use collection-expressions you will just get performance wins *for free*.  In other words, compared to virtually any other approach you're taking today (outside of literally hand-writing custom types for everything, and initializing them all with exceptionally ugly code), this system will work better.  And, if you are the user that was hand-writing everything before, this system is also so much better because now you get the same perf, with exceptional clarity and brevity.

## Conclusion

The working group strongly thinks that we should go with option 3 and that we should do so in C#12.  The other options come with enormously painful caveats that heavily undercut the core messaging of this feature entirely.  And missing C#12 on this also dramatically limits the usefulness of this feature space, and will immediately cause customer confusion and frustration that such a core scenario feels unaddressed by us in the initial release.
