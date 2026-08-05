## Collection expression conversion to specific interface types.

### The problem we are trying to solve

What does it mean when the user has one of the following five types (the types implemented by our core list-like-types like `T[]`, `List<T>`, `ImmutableArray<T>`):

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
1. `new CollectionType<Dictionary<(int x, int y), string[]>> { ... }`
1. `new() { ... }`
1. `ImmutableCollectionType.Create(...)`
1. `ImmutableCollection.CreateBuilder(); builder.Add(...); ...; builder.ToImmutable()`.  (Wasteful, and doesn't work for base cases like a field initializer).
1. And more... including things like: `ImmutableArray<T>.Empty.Add(x).AddRange(y).etc()`

An examination of the BCL and the top 20 NuGet packages (Newtonsoft, EF, Azure, Castle, AWS, AutoMapper, and more), all of which are >400m downloads, reveals very relevant data here.  Methods taking those interface collections account for roughly 28% of all methods taking some collection type (arrays, spans, other BCL collections), and `IEnumerable<T>` alone accounts for 25% of the collection-taking methods.  This is not surprising as our own practices, and general design guidance we give the community, are simply that:

> Methods should be permissive in what they accept, and be precise in what they return.

`IEnumerable<T>` (and our other collection interfaces) act as that permissive type that we and our ecosystem have broadly adopted. Indeed, if it were not for `(params) T[]` (a full 50% of all collection-taking methods), `IEnumerable<T>` would be the most commonly taken collection by far for the ecosystem.

Ideally, we would ship with support for everything, but we've currently made judicious moves from C#12 to C#13 based on complexity, but also based on impact.  For example, `Dictionary expressions` were moved to C#13 to lighten our load, and because data indicates that APIs that consume those dictionary types are only <3% of all apis that take collections in the first place.  `Natural type` support has also been pushed out because the complexity is felt to be substantive enough to warrant more time.

With how important these interface types are though, we do not believe pushing out from C# 12 will allow us to ship a viable and coherent story to customers.  

### What factors are at play?

In any interesting design, there are many factors that must be assessed and considered as a whole.  For collection expressions, these include but are not limited to:

1. Simplicity. Ideally the feature works in a fashion that is both simple to explain and simple for users to understand. Using some of our modern terminology, we'd like to avoid 'decoder rings' when people use it.

2. Universality.  This restates the background context. We want to meet the literal 97% case at launch, not the 69% which would be the case if interfaces can't be targeted.  This means needing good stories for arrays, spans, BCL concrete collections *and* BCL core interfaces.

3. Brevity.  It is a strong goal of this feature that users be able to just write the simple, idiomatic, collection expression form without the need to do things like add coercive casts to commonly appease the compiler.  Specifically, for casts, once you add them (e.g. `(List<int>)[1, 2, 3]`) then the benefit of the feature as a whole is vastly diminished. In these cases, the new form isn't substantively better than the existing form (just 2 characters saved over `new List<int> {1, 2, 3}`).  Unlike other features, the cliff here is very steep, often fundamentally negating the idea that this is a valuable feature in the first place.  Many parts of the design (especially broad adoption of target-typing) have been entirely around ensuring users can just write the simple expression form and almost never have to do things to appease the language.

4. Performance. A core pillar of collection expressions that we are both evangelizing it by, and which we are seeing customers resonate with, is the idea of

    > Absent external information unavailable to the compiler, the compiler should almost always do as well or better than the user could.  Often much more so.  And almost certainly with clearer code for the user to write.
    
    Because the user can write a simple `[a, b, c, d, e]` expression, without having to explicitly state what is going on, and because we can provide so much smart understanding to each situation, we can heavily optimize.  For example:

    - If the above 5-element collection were converted to an `ImmutableArray<T>`, our emitted code could would practically always be better than users using normal construction patterns.  We can also greatly leverage extremely fast and efficient systems under the covers (like synthesizing `Inline-Arrays` types) that would generally be extremely ugly and painful for users to do themselves.

    - What we emit can adopt ecosystem best practices around performance.  For instance, `[]` can emit as efficient singletons, not causing undesirable allocations.

    While many customers will not care about performance to this level, we still want customers that do to have confidence in this feature (and we definitely do not want to see the feature immediately banned).

    It would also be highly unfortunate if we shipped and our own analyzers immediately flagged the usage as being a problem.

    Finally, part of performance means being a good .NET citizen.  So the collections we produce should be able to pick up the optimizations the BCL has today for collection types.

5. Safety.  Specifically, keeping data safe from undesirable mutation. We broadly think of users as being in two categories.  The first category generally doesn't consider it to be a safety concern to return mutable instances via read-only interfaces, and thus would suffice with any solution on our part.  However, the second group (which is likely smaller, but present, vocal, and influential) absolutely wants a roadblock at runtime to keep their exposed data safe from mutation.  Similar to the perf concerns, we believe we need a solution for this group that fits their expectations, puts them at ease, and isn't immediately banned (or flagged by analyzers) for doing unsafe things.

## Options

Based on feedback from LDM and the working group meetings, we tried to whittle a large number of options down to a reasonable few that we feel warrant discussion and comparison.

### Option 1: Disallow target typing to these interface types.

This is the simplest and cheapest option we have at our disposal.  We could just say that these assignments would be an error, and force the user to specify the type they want to generate.  However, we believe this produces a negative result for every factor at play above.

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
class Order : ITaggable
{
    // Not allowed:
    public IReadOnlyList<OrderTag> Tags { get; } = [];

    // Write this instead:
    public IReadOnlyList<OrderTag> Tags { get; } = (ImmutableArray<OrderTag>)[];

    // Which is worse than just:
    public IReadOnlyList<OrderTag> Tags { get; } = Array.Empty<OrderTag>();
}
```

First, this immediately fails the simplicity and universality concerns. From our own experience, and the data about the ecosystem, we know that users will need to interface with, well, interfaces :).  This will be an immediate hurdle that will deeply undercut the value of this feature space, potentially even deeply tainting it for the future.  This also undercuts the core design principle we give to people of "be permissive in what you accept".  It would now be:

> Be permissive in what you accept, but also accept a concrete type so that it can be called using collection expressions

Second, collection expressions would not provide any sort of actual useful syntax or brevity for users in this case.  Because an explicit type would have to be provided, users would be left with syntax with nearly the same complexity and verbosity as what they would have to write today.  Users seeing this would rightfully ask:

> Why would I pick this new form, that is really just the same as the old form, just with some tokens tweaked?

Performance and safety though would be mostly neutral here.  Users who did not care about either would likely just use `List<T>`, and users who did would pick something appropriate for their domain.  However, this would still be a small tick in the 'negative' category as our claims about us making the "right, smart, best choices" for users would be undercut by then forcing the user to have to make those choices themselves.

### Option 2: Specify explicit concrete types to use for the possible interface targets.

The idea here is to pick either a single concrete type, or potentially a few distinct types, to support constructing any of the above interfaces.  Examples of types that could work would be:

1. `List<T>`
2. `T[]`
3. `ImmutableArray<T>`

For the purposes of discussion, we'll use `List<T>` as the example of the type to pick.  If we were to go this route, it would likely need to be at least the choice picked for `ICollection<T>/IList<T>` as otherwise you could create those mutable-types, and then find yourself unable to mutate them.

In practice this would look like:

```c#
void DoSomething(IEnumerable<int> values) { ... }

// Legal. Is equivalent to `DoSomething(new List<int> { 1, 2, 3 })`
DoSomething([1, 2, 3]);
```

and

```c#
class Order : ITaggable
{
    // Legal. Is equivalent to: `Tags { get; } = new List<OrderTag>();
    public IReadOnlyList<OrderTag> Tags { get; } = [];
}
```

This option *nails* the "simplicity", "universality, and "brevity" aspects we are trying to solve.  Explaining to users what happens in these cases is trivial:

> It makes a `List<T>`.  The part-and-parcel type of .NET that you've known about and have used for nearly 20 years now.

Similarly, it can be used for *all* these APIs that take in one of these interfaces.  Finally, it always allows the nice short syntactic form that really sells people on why collection expressions are a superior choice to use over practically every other collection creation construct they can use today.

However, this also falls short in both the 'performance' and 'safety' domains.  Indeed, it does so so egregiously, that our own analyzers will flag this as inappropriate and push both the users who run these analyzers, and the users who just care about these facets of development, away from feeling they can trust this feature to live up to its recommendation as making the "right, smart, best choices" for them.   It will also likely lead to negative-evangelization, where voices in the community steer people away from the feature as a whole, proclaiming it as harmful.

#### Option 2 - Performance

For "performance", `List<T>` has particular issues: 

1. It *already* comes with two allocations for itself.
2. Getting an iterator for it (through the `IEnumerable<T>.GetEnumerator` path) will produce another allocation.
3. It has excess overhead internally to support both being able to grow, and has overhead internally to ensure it is not mutated while it is being iterated.
4. It allocates for the incredibly common case of an empty collection.

While `List<T>` is a great type when you need flexibility and permissiveness, it is not a good choice when you know precisely what you are producing, and you have no need to access the flexible, mutation-oriented, capabilities it provides.

#### Option 2 - Safety

For "safety" `List<T>` is also an unacceptable choice for many (and our own analyzers will push you away from it).  Users who expose data safely today through the readonly interfaces `IEnumerable<T>/IReadOnlyCollection<T>/IReadOnlyList<T>` today will find that they cannot effectively move to collection expressions.  They will have to keep the highly verbose and clunky code they have today, to use types like `ImmutableArray`, or things like `new List<int> { a, b, c }>.AsReadOnly()`.  This will make collection-expressions feel half-baked for these users, again undercutting the story that this new feature makes the  "right, smart, best choices" for the user.  It will also force them to lose the brevity and consistency of being able to use collection-expressions with confidence everywhere.

Note: we could potentially choose *different* types depending in the end interface we were assigning to.  For example, `List<T>` for the mutable ones, and `ImmutableArray<T>` for the non-mutable ones.  However, this would certainly now start getting less simple, with the need for a 'decoder ring' rising.  It also likely would not play nicely with when we get to dictionaries.  While `Dictionary<K,V>` would be a natural analog to `List<T>`, with great familiarity to the ecosystem, `ImmutableDictionary<K,V>` would be a disaster in a great number of cases as the read-only dictionary analog.

### Option 3: Do *not* specify concrete types to use for the possible interface targets.

Deep discussion around the problems with Option 2 led the WG and partners to come up with a variant of '2', leveraging the parts it does well at, while curtailing its drawbacks.

First, it's important to look at the surface area of the read-only interfaces `IEnumerable<T>/IReadOnlyCollection<T>/IReadOnlyList<T>` and see that it is only the following *three* members:

```c#
{
    public IEnumerator<T> GetEnumerator(); // and the non-generic equivalent.
    public int Count { get; }
    public T this[int index] { get; }
}
```

Basically, exactly the same as `IEnuemrable<T>` with *just* enough to think of it as an indexable sequence. With that in mind the rules for target-typing an interface would be as follows:

1. We take a page from what we do today *already* for `IEnumerable<T>` and `yield` iterators, and we say that if you have a target-type of `IEnumerable<T>/IReadOnlyCollection<T>/IReadOnlyList<T>`, the language will state that you have no guarantee on any particular type being used at all.  But you will get an instance that efficiently implements the API surface area requested.  For example, the type that is used could be:

    - An unnameable, compiler synthesized type.  This could be something like what we do with arrays today (and Inline-Arrays for collection-expressions) where a specific type is generated for each specific size (including using Inline-Array tricks where available), producing values with practically no size overhead.  Or it could potentially be a generalized unnameable type that has contiguous storage (like an array) for the elements, but wraps it safely.

    - An unknown type provided by the runtime.  For example, through a method like `IEnumerable<T> Create(ReadOnlySpan<T> values)` (i.e. the builder-pattern applied to these interfaces).  This would allow the runtime itself to choose the most optimal internal representation for the data, and would also allow for it to do things like implement internal APIs it can query for specialized scenarios, or directly cast to the underlying type to do things like grab the contiguous data directly as a span.

    - Importantly, both of the above are possible, and we could even do one, then migrate to the other over different releases.  Because all that user could could ever know about is the target-interface type, there would be no source, binary, or runtime breaking changes here.  This means that in C# 12 we could use a compiler-synthesized type.  But also then migrate over to a BCL api if the BCL would like to own a canonical API for this purpose.

    - Also, we have enormous flexibility here in terms of what does happen.  For example, if the compiler synthesizes types, it could choose a handful to make extremely efficient (for example, for collections expression of less then eight elements), and then fallback to a single type that handled the rest of the cases.  These approaches could change over time based on data (similar to how we've adapted how we emit switch statements/expressions, and hashing). 

2. For a target-type of `ICollection<T>/IList<T>`, we would state the same thing.  That no type was guaranteed, but you would be certain to get an good implementation that supported mutation.  In practice though, we would be *highly* likely to just default this to `List<T>` as it would satisfy these requirements, while being an excellent implementation that is already heavily optimized for the flexible cases these interfaces would need.  This would also lower the burden on the compiler side in terms of codegen and complexity.

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
class Order : ITaggable
{
    // Legal. An efficient 'empty', read-only singleton will be used.
    public IReadOnlyList<OrderTag> Tags { get; } = [];

    // Legal. Equivalent to: `{ get; } = new List<int>()`, 
    // though we technically would not guarantee `List<T>`
    public IList<int> ProductIds { get; } = [];
}
```

With respect to the factors we care about, here's how the above falls out:

For 'brevity/universality', this still strongly satisfies our goals.  Users will be able to use the succinct collection-expression form for all these APIs with ease. However, compared to option 2, we now see wins in both safety and performance. 

#### Option 3 - Safety

Starting with safety, we now have a good option for everyone exposing data through any of those three read-only interfaces.  Data is safe by default, which is good for the users that care, and non-harmful for the users that do not care.  Users who care are then satisfied that they can trust this feature and that it lives up to the stated goal that it will make the smart choices they can depend on and they do not have to ban this feature, or recommend others steer away from it.  Importantly, analyzers do not trigger, preventing our own tooling from pushing people away from using our cohesive story here.

This approach also naturally extends out to when we do dictionary-literals.  There, it will also be the case that for `IReadOnlyDictionary<TKey, Value>` we will not want to expose a mutable type (like `Dictionary<TKey, TValue>`) for safety reasons.

#### Option 3 - Performance

While the wins now with 'safety' are definitely welcome, the largest benefits come in the "performance" category.

Specifically, not having to name a particular type for the read-only interfaces opens up many areas of optimization that would simply be unavailable when having to pick an existing generalized type. This would also live up to the promise that we will do an excellent job with perf, and that it would be *very* hard for a user to do the same, and practically impossible for them to do so with simple syntax.  Specifically, all of the following are available as things we could do to heavily optimize here when target-typing `IEnumerable<T>/IReadOnlyCollection<T>/IReadOnlyList<T>`.

1. Empty collection expressions can always be collapsed down to a single empty singleton (per `T` element type) across the entire program.

1. We can use features like inline-arrays to generate instances that inline all their elements and are no larger than space for the elements themselves.  A count would not even need to be stored as it could just be hard-coded as the value returned by `int Count { get; }`.  While this might generate many types in the assembly (one type per unique count), it's worth noting that this is what we *already* do for arrays *today*, and what we will be doing for the inline-array types we generate for spans/params-spans.

1. We can borrow a trick from `yield iterators` where the iterator returned from `IEnumerator<T> GetEnumerator()` *reuses* the `this` instance to be its own iterator *if* it has never been iterated yet and the calling thread is the same one that generated the `IEnumerable<T>`.  This means that if you have:

    ```c#
    DoSomething([1, 2, 3]);
    ```

    Then this may be a *single* allocation, *including* for when it is immediately iterated.

1. We do not need any code anywhere that sits in service of supporting variable sizes, or supporting mutation.  This means no overheads of checks, or invalidation of iterators, etc.

1. Collections full of constants, with well-known patterns, could potentially be implemented without allocating contiguous storage for them.  For example `[1, 2, 3, 4, 5, 6, 7, 8, 9, 10]` could be generated as `Enumerable.Range(1, 10)`, and so on.

    Overall, by not specifying explicit types, we actually open up a wealth of optimizations that both our own compiler or the BCL can choose to do.  While synthesizing types may initially seem concerning, we do already do it for many cases today, including when we already generate `IEnumerables` with `yield`, and also for every array creation that has an initializer (like `new int[] { 1, 2, 3 }`).  The surface area here is also exceptionally small.  There are only three extremely basic operations to support (`GetEnumerator(),Count,this[]`).  So the burden on the compiler to generate types for this feels completely acceptable, especially for the large wins we can achieve.  

    There are still benefits though with the BCL taking this over.  Specifically, if they do so, they can then implement internal interfaces they may query for.  Or they can use their knowledge of the exact internal types they use to get to the underlying data in even more efficient ways (including potentially being able to grab spans to it).  That said, if there was such an API to grab a span from an collection type, it would be good for that to be public so that the compiler could just implement that itself.

    This approach also naturally extends out to when we do dictionary-literals.  There, it will also be the case that for `IReadOnlyDictionary<TKey, Value>` we (and the BCL) could likely heavily optimize.  For example, it will often be the case that read-only dictionaries may be small (or empty).  Having specialized implementations that eschew complex wasted-space strategies for contiguous linear lookup may be highly beneficial.  Reserving that read-only implementations may be unknown, specialized, types allows for these sorts of wins to happen automatically.

    Ultimately, by being an internal implementation detail, we also have the flexibility to pick and choose any of these optimizations we feel worthwhile at any point.  We can, for example, leave these (or more) to C# 13, 14 and beyond.  And data can be used to help identify potential optimizations and indicate how worthwhile they would be.

#### Option 3 - Simplicity

Finally, we come to the one area where the above proposal ticks downward: "simplicity".  It is certainly the case that "Use `List<T>` for everything" approach is much simpler to explain.  However, we believe the above to be *acceptably* complex to explain, with enough benefits to point at to convince users that this was the right choice for them.  Specifically, we believe the best way to explain it is:

1. Are you using one of the mutation-supporting interfaces (`ICollection<T>/IList<T>`)?  If so, you'll get something great.  A high quality mutable type that supports that API and will work great with the rest of the ecosystem.
2. Are you using the read-only interfaces (`IEnumerable<T>/IReadOnlyCollection<T>/IReadOnlyList<T>`)?  If so, you get a *safe*, *high performance* impl that supports the API shape needed.  This is right in line with what we already do for you today when you use `IEnumerable<T>` with `yield`.

    Your instance will be safe, with no concern about data changing out from you unexpectedly.
    
    And, by switching to collection-expressions you will just get performance wins *for free*.  In other words, compared to virtually any other approach you're taking today (outside of hand-writing custom types for everything, and initializing them all with exceptionally ugly code), this system will work better.  And, if you are the user that was hand-writing everything before, this system is also so much better because now you get the same perf, with exceptional clarity and brevity.

We feel there is nothing particularly strange or difficult to explain here to users. C# and .Net is replete with APIs and patterns that do not let you know the specific type being constructed. For example, from C# 3 onwards, `Linq` very much put forth the idea of:

> You will just get an `IEnumerable<T>`, but you won't know what the actual concrete type is. This abstraction also allows the underlying system to heavily optimize.  `Linq` itself uses this to great effect, using many different specialized implementations under the covers to provide better performance.

Similarly, with several of our latest releases, we've started more heavily pushing the idea that the language has smart semantics that will do what you ask it to do, very efficiently, without overspecifying exactly how that must be done.  Patterns in general, and List-patterns (the analog to list-construction) heavily push this idea, stating explicitly that assumptions about well-behavedness will be made and that the compiler will pick the best way to do things.

So we feel that collection-expressions fit well into both the historical, and modern, way that C# and .Net works.  Where you can declaratively tell us what you want (like with `Linq`), and the systems coordinate to produce great results by default.

## Conclusion

Based on all of this, the working group strongly thinks that we should go with option 3 and that we should do so in C#12.  The other options come with enormously painful caveats that heavily undercut the core messaging of this feature entirely.  Missing C# 12 on this also dramatically limits the usefulness of this feature space, and will immediately cause customer confusion and frustration that such a core scenario feels unaddressed by us in the initial release.
