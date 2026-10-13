# Final initializers

Champion issue: <https://github.com/dotnet/csharplang/issues/10033>  
Discussion: <https://github.com/dotnet/csharplang/discussions/6591>  
Original proposal: [final-initializers.md](./final-initializers.md)  

## Summary

Allow a class or struct to declare a *final initializer*, `init { ... }`, which runs at the end of an object's initialization, after constructors, object and collection initializers, and `with` expressions:

```cs
var marie = new Person { FirstName = " Marie ", LastName = "Curie" };
Console.WriteLine(marie);                         // Marie Curie
var pierre = marie with { FirstName = "Pierre" }; // the final initializer runs again on the copy
Console.WriteLine(pierre);                        // Pierre Curie
var nobody = marie with { LastName = " " };       // throws ArgumentException

public record Person
{
    public required string FirstName { get; init; }
    public string? MiddleName { get; init; }
    public required string LastName { get; init; }

    private readonly string fullName;

    init
    {
        FirstName = FirstName.Trim();
        MiddleName = MiddleName?.Trim();
        LastName = LastName.Trim();

        if (FirstName is "" || LastName is "")
            throw new ArgumentException("Names must not be empty.");

        fullName = MiddleName is null
            ? $"{FirstName} {LastName}"
            : $"{FirstName} {MiddleName} {LastName}";
    }

    public override string ToString() => fullName;
}
```

## Motivation

Final initializers present a clear idiom which resolves a plethora of interrelated issues:

1. Inability to fully adopt object initializers over constructor arguments, due to requiring coordinated validation
2. Confusing gotchas where `with` expressions can unexpectedly bypass validation in constructors and member initializers, or preserve their stale computations
3. Clunky boilerplate in accessors, forcing people to choose whether to give up a valid-by-construction paradigm or to give up most of the syntax benefits of records
4. Inability to safely call virtual methods in constructors

These issues continue to hamper the adoption of recent features. Required members aimed to place object initializers on more of an even footing with constructors, but without final initializers, the language falls short of that aim. Records aimed to remove boilerplate especially from immutable objects, but they fail to do so in many cases. Indeed, getting `with` expressions for free on all records has made matters worse with non-obvious pitfalls for caching and validation. This is laid out well in [#4137 Thoughts on records, preserving invariants and verbosity](https://github.com/dotnet/csharplang/discussions/4137) and reported time and time again (see [Popular interest](#popular-interest)).

C# 9 moved the end of object construction. Before `init` accessors, an object was finished when its constructor returned. Since C# 9, object initializers and `with` expressions extend object construction as they continue assigning immutable state after the constructor returns. But a type still has no place to run code once its initialization actually ends.

Final initializers were designed alongside records to fill that gap. The [2020 design review](https://github.com/dotnet/csharplang/blob/main/meetings/2020/LDM-2020-05-04.md#final-initializers) called it "very complicated" to know when to use a constructors and when to use final initializers, so LDM [set them aside](https://github.com/dotnet/csharplang/blob/main/meetings/2020/LDM-2020-07-20.md#future-record-direction) until it had a better handle on the user scenarios. Six years of user requests show users repeatedly converging on the same request, showing that they understand the shape of the exact piece that is still missing.
### Required members without a constructor

[Required members](csharp-11.0/required-members.md) exist so that types can stop maintaining constructors that take every required and optional value. Callers set members by name, and the compiler makes sure they set the required ones. But a constructor was also where a type checked those values once and computed whatever it derives from them. That code has nowhere to go. Here's [a user's example](https://github.com/dotnet/csharplang/discussions/6591#discussioncomment-11963490):

```cs
public class Isotope
{
    public required string Element { get; init; }
    public required int N { get; init { field = value; UpdateDerived(); } }
    public required int Z { get; init { field = value; UpdateDerived(); } }
    public double HalfLifeSeconds { get; init { field = value; UpdateDerived(); } }

    public int A { get; private set; }                 // can't be init-only: UpdateDerived sets it
    public double TauSeconds { get; private set; }
    public double LambdaSeconds { get; private set; }

    private void UpdateDerived()                       // runs once per assignment, mostly on half-set state
    {
        A = Z + N;
        TauSeconds = HalfLifeSeconds / Math.Log(2);
        LambdaSeconds = Math.Log(2) / HalfLifeSeconds;
    }
}
```

The derived values are recomputed after every assignment, which is inefficient. The derived properties also lose their immutability. With a final initializer, the inputs go back to plain auto-properties and the computation runs once:

```cs
public class Isotope
{
    public required string Element { get; init; }
    public required int N { get; init; }
    public required int Z { get; init; }
    public double HalfLifeSeconds { get; init; }

    public int A { get; private init; }
    public double TauSeconds { get; private init; }
    public double LambdaSeconds { get; private init; }

    init
    {
        A = Z + N;
        TauSeconds = HalfLifeSeconds / Math.Log(2);
        LambdaSeconds = Math.Log(2) / HalfLifeSeconds;
    }
}
```

Validation across members has the same problem. Take a range whose minimum can't exceed its maximum. Without a constructor, the only place to check is each `init` accessor:

```cs
public record Range
{
    public required int Min
    {
        get;
        init => field = value <= Max ? value : throw new ArgumentException("Min exceeds Max.");
    }

    public required int Max
    {
        get;
        init => field = value >= Min ? value : throw new ArgumentException("Max is below Min.");
    }
}
```

Every one of these results is a valid range, but half of them throw, because each accessor checks against whatever value the other member holds at that moment:

```cs
var a = new Range { Min = 5, Max = 10 };     // throws: Max is still 0 when Min is checked
var b = new Range { Max = 10, Min = 5 };     // ok

var up1 = b with { Min = 20, Max = 30 };     // throws: Max is still 10 when Min is checked
var up2 = b with { Max = 30, Min = 20 };     // ok
var down1 = b with { Max = -5, Min = -10 };  // throws: Min is still 5 when Max is checked
var down2 = b with { Min = -10, Max = -5 };  // ok
```

No way of writing the accessors avoids this. In a `with` expression, both members already hold values, so whichever accessor runs first sees one new value and one old one. Moving the range up needs `Max` assigned first, and moving it down needs `Min` first, so every caller has to work out which member to update first. The alternatives are to skip the check, to add a `Validate()` method and hope callers remember it, or to give up object initializers for a constructor that `with` doesn't run. A final initializer checks the finished object once, in any order:

```cs
public record Range
{
    public required int Min { get; init; }
    public required int Max { get; init; }

    init
    {
        if (Min > Max) throw new ArgumentException("Min exceeds Max.");
    }
}
```

Validation isn't the only use. Libraries that implement change tracking, property-change notification, locking, or freezable objects [need the same hook](https://github.com/dotnet/csharplang/discussions/6591#discussioncomment-10181111), because changes made during initialization shouldn't be tracked, notified, or locked. Metalama, a metaprogramming framework, [shipped its own version](https://github.com/dotnet/csharplang/discussions/6591#discussioncomment-16540002) in 2026 by rewriting object creation and `with` expressions at compile time. That version works only in projects that use Metalama's compiler. They have communicated that they are eagerly awaiting the final initializers language feature.

### `with` expressions that keep invariants

A `with` expression clones the object and then assigns the listed members, without running any code the type declares. Anything the type checked or derived from those members during construction is now stale:

```cs
var roster = new Roster([new(7, "Avery"), new(13, "Blake")]);
var traded = roster with { Players = [new(9, "Casey")] };
Console.WriteLine(traded.ByNumber.ContainsKey(7)); // True: the index still describes the old roster

public record Player(int Number, string Name);

public sealed record Roster(ImmutableArray<Player> Players)
{
    public FrozenDictionary<int, Player> ByNumber { get; } = Players.ToFrozenDictionary(p => p.Number);
}
```

Nothing in the declaration hints at the problem, and even experienced users are surprised by it. Jon Skeet [hit it](https://codeblog.jonskeet.uk/2025/07/19/unexpected-inconsistency-in-records/) in production code in 2025. The fixes he found were to give up `with` on such records, to defer every derived member behind a `Lazy<T>`, or to [write analyzers](https://codeblog.jonskeet.uk/2025/07/29/records-and-the-with-operator-redux/) that flag record parameters used during initialization.

Validation fails the same way. [A popular article](https://enterprisecraftsmanship.com/posts/csharp-records-value-objects/) on using records as domain value objects shows `with` producing an instance whose invariant its constructor rejects. Users keep filing the same surprise ([#5652](https://github.com/dotnet/csharplang/discussions/5652), [dotnet/roslyn#81193](https://github.com/dotnet/roslyn/issues/81193)), and LDM noted in 2020 that "there is also no way to validate an object after a `with` expression."

A final initializer runs on the copy after the assignments, so the derived state follows the new members:

```cs
public sealed record Roster(ImmutableArray<Player> Players)
{
    public FrozenDictionary<int, Player> ByNumber { get; private init; }

    init => ByNumber = Players.ToFrozenDictionary(p => p.Number);
}
```

### Boilerplate and frequent cliff falls

Even when each property can be validated independently, supporting `with` means boilerplate. A `with` expression calls `init` accessors, so a type that wants its checks to survive `with` moves them into accessors. A record's primary constructor assigns the backing fields directly instead of calling the accessors ([dotnet/roslyn#81193](https://github.com/dotnet/roslyn/issues/81193)), so the type has to give up its primary constructor and write out what it generated:

```cs
public record Product
{
    public Product(string sku, decimal price) => (Sku, Price) = (sku, price);

    public string Sku
    {
        get;
        init
        {
            var sku = value.Trim();
            if (sku.Length == 0) throw new ArgumentException("SKU must not be empty.", nameof(Sku));
            if (sku.Length > 12) throw new ArgumentException("SKU must be at most 12 characters.", nameof(Sku));
            field = sku;
        }
    }

    public decimal Price
    {
        get;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Price));
    }

    public void Deconstruct(out string sku, out decimal price) => (sku, price) = (Sku, Price);
}
```

That works for `new Product("A1", 5)`, for `with`, and for deconstruction, but the one-line declaration has become a constructor, a full property per checked member, and a `Deconstruct` method. The checks have to stay in the accessors, because `with` skips the constructor. So as soon as a positional record needs any check, authors fall off a cliff from the one-line declaration.
Classes that aren't records hit the same cliff. A primary constructor's parameters can be checked only in initializer expressions, and optional `init` properties need redeclared accessors.

With final initializers, the top of the cliff becomes much more spacious for primary constructors. A type keeps its one-line declaration and puts its checks in one place, which runs after construction and after every `with` expression:

```cs
public record Product(string Sku, decimal Price)
{
    init
    {
        Sku = Sku.Trim();
        if (Sku.Length == 0) throw new ArgumentException("SKU must not be empty.", nameof(Sku));
        if (Sku.Length > 12) throw new ArgumentException("SKU must be at most 12 characters.", nameof(Sku));
        ArgumentOutOfRangeException.ThrowIfNegative(Price);
    }
}
```

### Popular interest

Users have asked for this in some form in eight of the last twelve years. Each request below was filed separately:

| Year | New requests | Upvotes on new requests | [#6591](https://github.com/dotnet/csharplang/discussions/6591) comments and replies | Upvotes on #6591 comments |
| --- | --- | ---: | ---: | ---: |
| 2015 | [dotnet/roslyn#2100](https://github.com/dotnet/roslyn/issues/2100) | 0 | n/a | n/a |
| 2019 | [#2722](https://github.com/dotnet/csharplang/discussions/2722) | 1 | n/a | n/a |
| 2020 | [#4002](https://github.com/dotnet/csharplang/discussions/4002), [#4137](https://github.com/dotnet/csharplang/discussions/4137) | 2 | n/a | n/a |
| 2021 | [#5320](https://github.com/dotnet/csharplang/discussions/5320), [#5574](https://github.com/dotnet/csharplang/discussions/5574) | 10 | n/a | n/a |
| 2022 | [#5652](https://github.com/dotnet/csharplang/discussions/5652), [#6187](https://github.com/dotnet/csharplang/discussions/6187), [#6416](https://github.com/dotnet/csharplang/discussions/6416) | 5 | 20 | 14 |
| 2023 | [#7075](https://github.com/dotnet/csharplang/discussions/7075) | 3 | 0 | 0 |
| 2024 | [#7885](https://github.com/dotnet/csharplang/discussions/7885) | 4 | 4 | 10 |
| 2025 | [dotnet/roslyn#81193](https://github.com/dotnet/roslyn/issues/81193) | 0 | 5 | 5 |
| 2026 | None | 0 | 83 | 39 |
| Total | 12 | 25 | 112 | 68 |

#6591 was opened in 2022. The discussion itself has 24 upvotes and 47 reactions, and 19 people commented on it in 2026 alone. The requests range from asking the compiler to call `ISupportInitialize.EndInit` after object initializers to a post-initialization block for classes with primary constructors and required members. Many users comment that the lack of a post-initialization block defeats the purpose of conciseness because primary constructors cannot be used and property accessors become multiline boilerplate-heavy affairs.

Various frameworks which set members after calling a constructor have each invented the same hook: [`ISupportInitialize`](https://learn.microsoft.com/dotnet/api/system.componentmodel.isupportinitialize) for designers, [`[OnDeserialized]`](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.ondeserializedattribute) and [`IDeserializationCallback`](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.ideserializationcallback) for serialization, and [`IJsonOnDeserialized`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.ijsonondeserialized) for System.Text.Json. Each only works when that framework does the creating.


### Other languages

Other languages with primary constructors, or constructors generated from field declarations, give a type a place to run its own code that every construction goes through:

- Kotlin: [`init` blocks](https://kotlinlang.org/docs/classes.html#initializer-blocks) run as part of the primary constructor.
- Scala: [statements in the class body](https://docs.scala-lang.org/scala3/book/domain-modeling-tools.html#classes) run as part of the primary constructor.
- Java: a record's [compact canonical constructor](https://openjdk.org/jeps/395) checks the components before they're assigned to fields.
- Dart: a [primary constructor body](https://dart.dev/language/primary-constructors#add-constructor-bodies), `this { ... }`, runs after the fields are initialized.
- F#: [`do` bindings](https://learn.microsoft.com/dotnet/fsharp/language-reference/members/do-bindings-in-classes) in a class run as part of the primary constructor.
- Python: a dataclass's [`__post_init__`](https://docs.python.org/3/library/dataclasses.html#post-init-processing) runs at the end of the generated `__init__`, after every field is assigned.

Several languages also run the type's code when their built-in syntax or standard library function copies an object with changes, so the type can check the new values:

- Kotlin: a data class's [`copy()`](https://kotlinlang.org/docs/data-classes.html#copying) calls the primary constructor.
- Scala: a case class's [`copy`](https://scala-lang.org/files/archive/spec/3.4/05-classes-and-objects.html#case-classes) is defined as a call to the primary constructor.
- Java: the derived record creation expression [`with`](https://openjdk.org/jeps/468#Description), a preview candidate, calls the canonical constructor at the end of its block.
- Groovy: a record's opt-in [`copyWith`](https://groovy-lang.org/objectorientation.html#_copying) calls the canonical constructor.
- Python: [`dataclasses.replace`](https://docs.python.org/3/library/dataclasses.html#dataclasses.replace) calls `__init__`, which calls `__post_init__`.
- Ruby: a `Data` object's [`with`](https://docs.ruby-lang.org/en/3.4/Data.html#method-i-with) calls `new`, which calls `initialize`, and a `Data` type can [override `initialize`](https://docs.ruby-lang.org/en/3.4/Data.html#method-c-new) to check the values.

A C# primary constructor has no body, and a `with` expression runs none of the type's code. C# also lets callers set immutable members after the constructor returns, through object initializers and `with`, so a C# type needs its hook at the end of initialization rather than in the constructor.

## Detailed design

A final initializer works like an `init` accessor for the whole object. It runs once every constructor and initializer has run, it can assign what an `init` accessor can, and a `with` expression runs it again on the copy.

A class, struct, record class, or record struct can declare a final initializer with the `init` keyword and a block or expression body:

```cs
class C
{
    init { ... }
}

struct S
{
    init => Validate();
    void Validate() { ... }
}
```

Like a finalizer, a final initializer has a fixed form, with no name or parameters. It takes no modifiers, but can have attributes. A type can declare at most one, across all of its partial declarations, so that order-dependent code that touches the same state isn't spread across files. Interfaces and static classes can't declare one. No existing code changes meaning, because `init` followed by `{` or `=>` isn't a valid member declaration today.

### Design principles

Five principles decide most of the design:

1. User code can't call a final initializer. This is because a final initializer can assign `readonly` and init-only state. That ability has to end when initialization ends.
2. Adding a final initializer to an existing type is a breaking change, like adding `IDisposable`. We accept that code compiled before the change doesn't run it. Making the change non-breaking would require 1) blocking all newly compiled types on every downlevel compiler and 2) a run-time check after every `new` expression for a type that comes from, or derives from, another assembly, in case it gains a final initializer later. See [Alternative: Make adding a final initializer non-breaking](#make-adding-a-final-initializer-non-breaking).
3. Final initializers are pay-for-play. Only types that have one get a method and get calls emitted for it. Whether a type has one is visible to compilers, serializers, and API authors. See [Alternative: Declare the final initializer on `System.Object`](#declare-the-final-initializer-on-systemobject).
4. Older compilers can't use a type that has a final initializer, because they'd create instances without running it. The runtime can't run it for them, because IL doesn't record where object creation ends (see [Appendix A](#appendix-a-final-initializer-calls-cant-be-inferred-by-the-runtime)).
5. Every object creation runs the final initializer, whether it's `new C()`, `new C { }`, or `new C { P = 1 }`. Types put important initialization code in their final initializers. Moving code between a constructor and a final initializer never changes *whether* it runs, only *when* it runs.

### When it runs

The final initializer runs at the end of each of these, on the object being created:

- An object creation expression, after the constructor and any object or collection initializer, as in `new C()`, `new C { P = 1 }`, `new() { P = 1 }`, and, for a struct with or without an explicitly declared default constructor, `new S()`.
- A `with` expression, on the copy, after the assignments. Whether a `with` expression with no assignments runs it is an [open question](#empty-with-expressions).
- A collection expression whose target type is created with a constructor and `Add` calls, after the elements are added.

A final initializer runs after every constructor and initializer, but it isn't the first code to see the object. An object initializer can call properties, indexers, `Add` methods, and extension members. Those can call any accessible member on the object before the final initializer runs. So code in other members can't assume the final initializer has run. A type that hands out `this` during initialization should think through what that code might observe.

Serializers, dependency injection containers, and other code that creates objects through reflection don't go through the compiler, so they have to call the final initializer themselves, once, after setting members.

### What the body can do

A final initializer's body is like the body of a `void` instance method with no parameters, plus the two things an `init` accessor can also do: assign `readonly` fields declared in the same type, and assign init-only properties on `this` or `base`. That includes the fields of a `readonly struct`, as it does for an `init` accessor. But also like an `init` accessor, it can't assign get-only auto-properties.

```cs
class C
{
    private readonly int f;
    public int P { get; init; }
    public int Q { get; private init; }
    public int R { get; }

    init
    {
        f = 1; // ok
        P = 2; // ok
        Q = 3; // ok
        R = 4; // error: read-only, as in an init accessor
    }
}
```

### Nullable analysis

Final initializers and constructors work in tandem to initialize the object's state. Thus, it is now permitted to leave a non-required, non-nullable member unassigned in any constructor and instead, to determine and assign its value in the final initializer. A non-nullable member that the final initializer assigns on every path isn't reported as uninitialized at the end of a constructor. In the [Summary](#summary) example, there is no compiler warning that `fullName` is possibly left null, because the final initializer assigns it.

At the start of the final initializer body, each required members has its declared nullability, because the object initializer has finished assigning members. Each other member has its declared nullability only if every constructor assigns it; otherwise, it starts as maybe-null.

### Inheritance

A derived type's final initializer runs its base type's final initializer first, before its own body, the same way a constructor without a constructor initializer implicitly calls `base()`. That repeats all the way up the hierarchy. A derived type that doesn't declare one still has its base's:

```cs
var d = new Derived();
// Output:
// Base constructor
// Derived constructor
// Base final initializer
// Derived final initializer

class Base
{
    public Base() => Console.WriteLine("Base constructor");
    init => Console.WriteLine("Base final initializer");
}

class Derived : Base
{
    public Derived() => Console.WriteLine("Derived constructor");
    init => Console.WriteLine("Derived final initializer");
}
```

### Safety restriction for derived records

A record class can declare a final initializer only if its base record class has one, or it has no base record class ([open question](#derived-records-whose-base-has-no-final-initializer)):

```cs
record A;
record B : A { init { } } // error: base record 'A' has no final initializer

record C { init { } }     // ok
record D : C { init { } } // ok
```

With this rule, a `with` expression whose receiver type has no final initializer can never produce an object that has one. So no `with` expression needs a run-time check for a derived type's final initializer, and older compilers and older binaries never skip one through `with` on a base type.

This rule could be loosened later, with less risk the longer that it has been since shipping final initializers.

### Generic creation

A type that has a final initializer can't be a type argument for a type parameter constrained to `new()`, `struct`, or `unmanaged`, because generic code compiled without knowledge of final initializers would create it with `new T()` and skip it ([open question](#type-parameters-constrained-to-new-struct-or-unmanaged)). Required members blocks `new()` the same way, but not `struct` or `unmanaged`.

```cs
static T Create<T>() where T : new() => new T();
static T Copy<T>(T value) where T : struct => value with { };
static T Make<T>() where T : unmanaged => new T();

Create<C>();     // error: 'C' has a final initializer
Copy(new S());   // error: 'S' has a final initializer
Make<S>();       // error: 'S' has a final initializer

class C { init { } }
struct S { init { } }
```

### Older compilers

Every type that declares or inherits a final initializer gets `[CompilerFeatureRequired("FinalInitializers")]`, plus `[Obsolete]` as an error for compilers that predate `CompilerFeatureRequired`, as required members does. A compiler that supports final initializers ignores that `Obsolete` attribute. The compiler marks the whole type instead of only its constructors, because an older compiler could otherwise pass it to a `new()`, `struct`, or `unmanaged` type parameter ([open question](#marking-for-older-compilers)).

Without this protection, it wouldn't be safe to declare even a brand-new type with a final initializer, because nothing would force its consumers to use a compiler that runs it.

### Versioning

Adding a final initializer to an existing type is a breaking change. Code compiled against the old version keeps creating instances without running it, older compilers can no longer use the type, and code that passes the type to a `new()`, `struct`, or `unmanaged` type parameter stops compiling. Removing a final initializer is also a binary breaking change, because compiled code calls it and derived types override it. Changing its body isn't breaking.

### Metadata and code generation

A final initializer is emitted as a public `void` instance method with no parameters and a well-known name, marked so that only compilers that support the feature can call or override it ([open question](#means-of-preventing-ordinary-calls)). In a class that can be derived from, it's virtual. A derived type's final initializer overrides it and calls the base one first. Only types that declare or inherit a final initializer have the method. This is the same approach records take for their clone method.

Object creation and `with` lower as follows (not C#; names are illustrative):

```
// new Person { FirstName = "Marie", LastName = "Curie" }
var __tmp = new Person();
__tmp.FirstName = "Marie";
__tmp.LastName = "Curie";
__tmp.<FinalInitializer>();

// marie with { FirstName = "Pierre" }
var __copy = marie.<Clone>$();
__copy.FirstName = "Pierre";
__copy.<FinalInitializer>();

// a derived final initializer
public override void <FinalInitializer>()
{
    base.<FinalInitializer>();
    ... // the declared body
}
```

With the recommended answers to two open questions, the compiler always knows statically whether to emit the call, so types without a final initializer pay nothing and nothing needs a runtime check:

- An object creation expression that names a type can see whether that type has a final initializer.
- `new T()` can't create a type that has one, as long as `new()`, `struct`, and `unmanaged` type parameters reject such types ([open question](#type-parameters-constrained-to-new-struct-or-unmanaged)). If a compilation or a type parameter could opt in instead, its `new T()` expressions would call a runtime helper.
- A `with` expression whose receiver type has no final initializer can't produce an object that has one, under the [safety restriction for derived records](#safety-restriction-for-derived-records). If derived records could add one anyway ([open question](#derived-records-whose-base-has-no-final-initializer)), every `with` expression on an unsealed record type without a final initializer would need a run-time check.

Future features could change that. An extension constructor whose return type is unsealed and has no final initializer, or a factory method used with an object initializer (`C.Create() { A = 42 }`, [#10207](https://github.com/dotnet/csharplang/discussions/10207)), could produce an object of a derived type that has one. Such features would need their own rule, or a runtime helper that runs the final initializer of the object's run-time type if it has one. An interface such as `System.Runtime.CompilerServices.IFinalInitializable` could make that helper faster; if we add one, users would need to be blocked from calling its method, and we'd need to decide what a constraint on it means.

## Specification

Throughout this section, ~~strikethrough~~ indicates text being removed from the existing specification, and **bold** indicates text being added.

The baseline is the [draft-v14](https://github.com/dotnet/csharpstandard/tree/draft-v14/standard) branch of the C# standard, except for collection expressions, whose baseline is the [C# 12 speclet](csharp-12.0/collection-expressions.md).

### Final initializers

A new subsection, §15.11.7, is added after [§15.11.6 Primary constructors](https://github.com/dotnet/csharpstandard/blob/draft-v14/standard/classes.md#15116-primary-constructors).

> #### 15.11.7 Final initializers
>
> A ***final initializer*** is a member that implements the actions to be performed on an instance at the end of its construction phase (§15.7.3.3). A final initializer is declared using a *final_initializer_declaration*:
>
> ```antlr
> final_initializer_declaration
>     : attributes? 'init' method_body
>     ;
> ```
>
> A class or struct declaration shall contain at most one *final_initializer_declaration*. For a partial type, this applies across all of its parts.
>
> The *method_body* of a final initializer shall be either a block body or an expression body (§15.6.1). It corresponds exactly to the *method_body* of an instance method with a `void` return type and no parameters (§15.6.11). A final initializer is an instance member.
>
> A class or struct ***has a final initializer*** if it declares one, or if it is a class whose direct base class has a final initializer. A type parameter has a final initializer if its effective base class has one.
>
> It is a compile-time error for a record class to declare a final initializer if its direct base class is not `object` and does not have a final initializer.
>
> Final initializers are invoked automatically and cannot be invoked explicitly. The final initializer of an instance is invoked at the end of an *object_creation_expression* that creates the instance (§12.8.17.2.1), at the end of a *with_expression* that produces the instance (§12.10), and after the elements of a collection expression have been added to the instance. An instance constructor does not invoke a final initializer, including when it is invoked through a *constructor_initializer*.
>
> Invoking the final initializer of an instance whose run-time type is `T` consists of the following steps:
>
> - If `T` is a class whose direct base class `B` has a final initializer, the final initializer of `B` is invoked on the instance.
> - If `T` declares a final initializer, its *method_body* is executed, with `this` referring to the instance.
>
> > *Note*: This matches an instance constructor that has no *constructor_initializer*, which implicitly invokes the direct base class's constructor before its own body (§15.11.2). *end note*
>
> The instance is in its construction phase (§15.7.3.3) while its final initializer executes.
>
> > *Note*: In addition to what an instance method can do, a final initializer can assign `readonly` fields declared in the same type (§15.5.3.1), including the fields of a `readonly struct`, and set init-only properties on `this` or `base` (§15.7.3.3). *end note*
>
> With regard to nullable reference type analysis (§8.9), at the beginning of a final initializer, a required member (§15.7.1) has its declared nullable state. Any other field or property has its declared nullable state if every instance constructor of the type leaves it in that state, and otherwise is maybe-null. A field or automatically implemented property that is definitely assigned on every path through the final initializer of its type need not be initialized to a valid nullable state when an instance constructor of that type returns.

### Class members

[§15.3.1 Class members](https://github.com/dotnet/csharpstandard/blob/draft-v14/standard/classes.md#1531-general) is updated as follows:

```diff
 class_member_declaration
     : constant_declaration
     | field_declaration
     | method_declaration
     | property_declaration
     | event_declaration
     | indexer_declaration
     | operator_declaration
     | constructor_declaration
     | finalizer_declaration
+    | final_initializer_declaration
     | static_constructor_declaration
     | type_declaration
     ;
```

> - Finalizers, which implement the actions to be performed before instances of the class are permanently discarded (§15.13).
> - **Final initializers, which implement the actions to be performed on an instance at the end of its construction phase (§15.11.7).**
> - Static constructors, which implement the actions required to initialize the class itself (§15.12).

### Struct members

[§16.3.1 Struct members](https://github.com/dotnet/csharpstandard/blob/draft-v14/standard/structs.md#1631-general) is updated as follows:

```diff
 struct_member_declaration
     : constant_declaration
     | struct_field_declaration
     | method_declaration
     | property_declaration
     | event_declaration
     | indexer_declaration
     | operator_declaration
     | constructor_declaration
+    | final_initializer_declaration
     | static_constructor_declaration
     | type_declaration
     | fixed_size_buffer_declaration   // unsafe code support
     ;
```

### Readonly fields

[§15.5.3.1 Readonly fields](https://github.com/dotnet/csharpstandard/blob/draft-v14/standard/classes.md#15531-general) is updated as follows:

> Direct assignments to readonly fields can only occur as part of that declaration or in an instance constructor**,** ~~or~~ static constructor**, or final initializer** in the same class. (A readonly field can be assigned to multiple times in these contexts.) Specifically, direct assignments to a readonly field are permitted only in the following contexts:
>
> - In the *variable_declarator* that introduces the field (by including a *variable_initializer* in the declaration).
> - For an instance field, in the instance constructors of the class that contains the field declaration, excluding local functions and anonymous functions, and only on the instance being constructed. For a static field, in the static constructor or a static field or property initializer in the class that contains the field declaration, excluding local functions and anonymous functions. These are also the only contexts in which it is valid to pass a readonly field as an output or reference parameter.
> - **For an instance field, in the final initializer (§15.11.7) of the class that contains the field declaration, excluding local functions and anonymous functions, and only on the instance being initialized.**

### Init accessors

[§15.7.3.3 Init accessors](https://github.com/dotnet/csharpstandard/blob/draft-v14/standard/classes.md#15733-init-accessors) is updated as follows:

> The ***construction phase of an object*** includes the following:
>
> - During execution of an *object_initializer* (§12.8.17.2.2)
> - During evaluation of a *with_expression*'s *member_initializer_list*
> - Inside an instance constructor of the containing or derived type, on `this` or `base`
> - **Inside the final initializer (§15.11.7) of the containing or derived type, on `this` or `base`**
> - Inside the *init_accessor_declaration* of any property, on `this` or `base`
> - Inside attribute usages with named parameters (§23.2.3)

### Simple names, `this` access, and `base` access

[§12.8.4 Simple names](https://github.com/dotnet/csharpstandard/blob/draft-v14/standard/expressions.md#1284-simple-names) is updated as follows:

> - Otherwise, if `T` is the instance type of the immediately enclosing class or struct type, if the lookup identifies an instance member, and if the reference occurs within the *block* of an instance constructor, an instance method, ~~or~~ an instance accessor (§12.2.1)**, or a final initializer (§15.11.7)**, the result is the same as a member access (§12.8.7) of the form `this.I`. This can only happen when `e` is zero.

[§12.8.14 This access](https://github.com/dotnet/csharpstandard/blob/draft-v14/standard/expressions.md#12814-this-access) is updated as follows:

> A *this_access* is permitted only in the *block* of an instance constructor, an instance method, an instance accessor (§12.2.1), ~~or~~ a finalizer**, or a final initializer (§15.11.7)**. It has one of the following meanings:
>
> […]
>
> - When `this` is used in a *primary_expression* within an instance method**,** ~~or~~ instance accessor**, or final initializer** of a class, it is classified as a value. The type of the value is the instance type (§15.3.2) of the class within which the usage occurs, and the value is a reference to the object for which the method or accessor was invoked **or whose final initializer is executing**.
>
> […]
>
> - When `this` is used in a *primary_expression* within an instance method**,** ~~or~~ instance accessor**, or final initializer** of a struct, it is classified as a variable. The type of the variable is the instance type (§15.3.2) of the struct within which the usage occurs.
>   - If the method or accessor is not an iterator (§15.15) or async function (§15.14), the `this` variable represents the struct for which the method or accessor was invoked **or whose final initializer is executing**.

[§12.8.15 Base access](https://github.com/dotnet/csharpstandard/blob/draft-v14/standard/expressions.md#12815-base-access) is updated as follows:

> A *base_access* is permitted only in the body of an instance constructor, an instance method, an instance accessor (§12.2.1), ~~or~~ a finalizer**, or a final initializer (§15.11.7)**.

### Object creation expressions

[§12.8.17.2.1 Object creation expressions](https://github.com/dotnet/csharpstandard/blob/draft-v14/standard/expressions.md#1281721-general) is updated as follows:

> Processing of an object creation expression that includes an object initializer or collection initializer consists of first processing the instance constructor and then processing the member or element initializations specified by the object initializer (§12.8.17.2.2) or collection initializer (§12.8.17.2.3).
>
> **If the type of the instance created by an *object_creation_expression* has a final initializer (§15.11.7), processing of the *object_creation_expression* ends by invoking that final initializer on the instance, after the instance constructor or default constructor invocation and any object initializer or collection initializer have been processed. This applies whether or not the *object_creation_expression* includes an object initializer or collection initializer.**

### With expressions

[§12.10 With expressions](https://github.com/dotnet/csharpstandard/blob/draft-v14/standard/expressions.md#1210-with-expressions) is updated as follows:

> This expression is evaluated as follows:
>
> - For a record class type, the receiver's clone method (§15.16.4.4) is invoked, and its result is converted to the receiver's type.
> - For a record struct or non-record struct type, the receiver is copied.
> - Each `member_initializer` is processed the same way as an assignment to a field or property access of the result of the conversion. Assignments are processed in lexical order. If *member_initializer_list* is omitted, no members are changed.
> - **If the receiver's type has a final initializer (§15.11.7), the final initializer of the result's run-time type is invoked on the result.**

### Collection expressions

The [Construction](csharp-12.0/collection-expressions.md#construction) section of the collection expressions speclet is updated as follows:

> If the target type is a *struct* or *class type* that implements `System.Collections.IEnumerable`, and the target type does not have a *create method*, the construction of the collection instance is as follows:
>
> […]
>
> * During the construction steps above, an applicable `EnsureCapacity` instance or extension method *may* be invoked one or more times on the *collection instance* with an `int` capacity argument.
> * **If the target type has a final initializer (§15.11.7), it is invoked on the *collection instance* after all elements have been added.**

### Constraints

[§8.4.5 Satisfying constraints](https://github.com/dotnet/csharpstandard/blob/draft-v14/standard/types.md#845-satisfying-constraints) is updated as follows:

> - If the constraint is the value type constraint (`struct`), the type `A` shall satisfy one of the following:
>   - `A` is a `struct` type or `enum` type, but not a nullable value type **and not a type that has a final initializer (§15.11.7)**.
>
> […]
>
> - If the constraint is the constructor constraint `new()`, the type `A` shall not be `abstract`, it shall not have a required member list (§15.7.1), **it shall not have a final initializer (§15.11.7),** and it shall have a public parameterless constructor.

[§15.2.5 Type parameter constraints](https://github.com/dotnet/csharpstandard/blob/draft-v14/standard/classes.md#1525-type-parameter-constraints) is updated as follows:

> The unmanaged type constraint specifies that a type argument used for the type parameter shall be a non-nullable unmanaged type (§8.8) **that does not have a final initializer (§15.11.7)**.

## Drawbacks

Final initializers are another mechanism related to object initialization, on top of constructors, field initializers, `init` accessors, required members, and primary constructors. That was exactly the 2020 design review's concern. Users will have to learn when to use a constructor and when to use a final initializer.

A type with a final initializer gives some things up. It can't be passed to `new()`, `struct`, or `unmanaged` type parameters. Older compilers can't use it, and neither can other .NET languages until they support the feature. Adding or removing a final initializer is a breaking change, so on existing public types the feature mostly suits cases where every consumer recompiles.

Serializers, dependency injection containers, and ORMs create objects through reflection. Until each one learns to call final initializers, the objects it creates skip them.

Under the recommended [safety restriction for derived records](#safety-restriction-for-derived-records), a derived record can add a final initializer only if its base record has one. A base record from a library that didn't plan for this can't be validated by its derived records.

## Alternatives

### Make adding a final initializer non-breaking

At the other end of an object's lifetime, the docs [warn](https://learn.microsoft.com/dotnet/api/system.idisposable) that adding `IDisposable` to an existing class is a breaking change, because existing consumers don't call `Dispose`. Adding a final initializer to an existing type has the same problem at the start of the lifetime, because code compiled before the change creates instances without calling it.

We could avoid that break, but only with two costly measures.

First, every older compiler and every compiler for another language would have to be blocked from compiling against .NET vNext at all, because any type in it might gain a final initializer later, and compiling would produce a binary which bypasses the final initializer. Moving a library to .NET vNext would make it consumable only by compilers that know about final initializers.

Second, `new` expressions would need to check at run time for a final initializer wherever another assembly could add one later. The narrowest version of that check, [suggested in the discussion](https://github.com/dotnet/csharplang/discussions/6591#discussioncomment-17478663), applies to a `new` expression whose type comes from another assembly or has a base class from another assembly, treating `System.Object` as a type that never gains one. A type whose whole hierarchy is in the current compilation needs no check, because adding a final initializer to it means recompiling the code that creates it.

The base-class part is what keeps the change non-breaking for unsealed types. Suppose assembly A declares `Base`, assembly B declares `Derived : Base`, and neither has a final initializer. Code in B that creates a `Derived` names a type from its own compilation, but `Derived` has a base class from A, so the check is emitted. When a new version of A adds a final initializer to `Base`, the check finds it, and a run-time helper or interface test is enough to run it, without a new method on `System.Object`.

Even this narrower check reaches most `new` expressions in application code, because most of them create framework types, from `new List<int>()` to `throw new ArgumentException(...)`. For scale, adding a call after every `new` expression in System.Private.CoreLib grows its IL code size by 6.5%. That's an upper bound, since it counts every `new` expression, but an application that mostly creates framework types could come close to it. The JIT might be able to elide some of the calls for types without a final initializer, but it's a blanket overhead nonetheless.

Accepting the break avoids all of this. Older compilers can keep targeting .NET vNext, and `new` expressions for types without a final initializer do not gain a blanket layer of cost.

### Declare the final initializer on `System.Object`

The original version of this proposal gave `System.Object` an empty virtual final initializer, compiled every declaration as an override with a base call, and called it after every object creation. Every type pays for that. Each type's vtable grows by one slot. Tools that list members show the method on every type, and if [preventing ordinary calls](#means-of-preventing-ordinary-calls) uses an unspeakable name, that name shows everywhere until tools learn to hide it. Every .NET language would need to call it after creating any object.

It also obscures whether a type has a final initializer, which compilers, serializers, and API authors need to know. If every type has one, finding out whether a type's final initializer does anything becomes subtle. So only types that declare or inherit a final initializer have the method. It's virtual if the type can be derived from, the same way records declare their clone method.

### Explicit base call

A derived type's final initializer always runs its base type's final initializer first. Users have suggested an explicit base call that the body can place anywhere.

Constructors have the same restriction, because a constructor's base call always runs before its body. Users have asked to [call the base constructor in the middle of the body](https://github.com/dotnet/csharplang/discussions/4827). If LDM takes that up, it should consider the same flexibility for final initializers at the same time, so the two keep matching.

We don't expect the automatic call to limit the feature. Of the 12 separate requests and 112 comments and replies on #6591, two participants suggested controlling the base call, and neither described a scenario that needs it.

## Answered questions

### Syntax

The original version of this proposal asked whether `init` is the best keyword, or whether the declaration should mirror finalizers with an empty parameter list. Users have suggested `init() { ... }`, other keywords, and concerns that `init` suggests something that runs early.

**Conclusion**: LDM [accepted `init { ... }`](https://github.com/dotnet/csharplang/blob/main/meetings/2020/LDM-2020-04-27.md#primary-constructor-bodies-and-validators). It mirrors `init` accessors and hints that the body can set init-only members as well as validate them, which is also why LDM renamed the feature from *validators*.

## Open questions

### Derived records whose base has no final initializer

A `with` expression on an unsealed record type can produce an object of a derived record type. If a derived record could declare a final initializer while its base record has none, a `with` expression whose receiver is typed as the base couldn't see statically whether to run one:

```cs
// Library A, compiled with C# 14:
public static class TagOperations
{
    public static Tag Rename(Tag b, string name) => b with { Name = name };
}

// Library B, compiled with final initializers:
public record Tag(string Name);

public record SpecialTag(string Name) : Tag(Name)
{
    init
    {
        if (Name.Contains(' ')) throw new ArgumentException("Special tags can't contain spaces.");
    }
}
```

That has two costs. First, a new compiler would have to emit a run-time check after every `with` expression whose receiver is an unsealed record type without a final initializer, in case the object's run-time type has one (not C#):

```
var __copy = b.<Clone>$();
__copy.Name = name;
if (__copy has a final initializer) __copy.<FinalInitializer>();
```

Record classes are unsealed unless declared `sealed`, so this would apply to most `with` expressions on record classes in every program, including types that never have a derived record with a final initializer. That gives up [pay-for-play](#design-principles) for `with`.

Second, code from an older compiler, or compiled before this feature, has no such check. `TagOperations.Rename(new SpecialTag("urgent"), "very urgent")` returns a `SpecialTag` with a space in its name. [Marking](#older-compilers) `SpecialTag` with `CompilerFeatureRequired` doesn't help. A compiler that recognizes `CompilerFeatureRequired` reports an error wherever code uses a marked type, even through `var` without naming it, but `Rename` uses only `Tag`, which has no final initializer. `new T()` doesn't have this problem, because the type argument names the derived type. Adding a final initializer to an existing type is a breaking change for code compiled before it, but here no existing type changed, and an older compiler keeps producing such code for as long as it's in use. Required members has no counterpart, because a copy keeps the values that satisfied the requirement, while a final initializer's guarantee concerns assignments made after the copy.

Shall a record class be able to declare a final initializer when its base record class has none, and if so, how do `with` expressions through the base handle it?

1. Make it an error for a record class to declare a final initializer if its base record class has none. A `with` expression through a type without a final initializer then always produces an object without one, so no `with` expression needs a runtime check, and older code has nothing to skip. Deriving a record that needs a final initializer requires a base record that has one. An author who owns the base can declare an empty `init { }` up front, but adding one to an existing base is a breaking change, and a base from another library may never have one. Moving to option 2 later breaks nothing.
2. Allow it, and check at run time. Any record class can declare a final initializer. Every `with` expression whose receiver is an unsealed record type without a final initializer gets a run-time check, and older code skips the derived type's final initializer. Moving to option 1 later would break code.
3. Give every unsealed record class an implicit empty final initializer. This isn't pay-for-play: every unsealed record gets the method, and every `with` expression on an unsealed record pays for a virtual call to it, whether or not any type in the hierarchy declares a final initializer. Bases compiled with the new compiler get marked, which closes the gap with older code for them. But recompiling any existing unsealed record adds a final initializer to it, which is a breaking change, and the marking blocks older compilers from all of them. They'd also be excluded from `new()`, `struct`, and `unmanaged` type parameters unless an implicit empty final initializer is exempt. A base compiled before this feature still has none, so its derived records still need option 1 or 2.
4. Check at run time as in option 2, and in record classes that have a final initializer, make the clone method that older code calls throw, while new compilers call a different clone method. Older code fails at run time instead of skipping the final initializer. That includes `b with { }`, which wouldn't need to fail if an [empty `with` expression](#empty-with-expressions) skips the final initializer. This is a workaround which leaves a historical stain on the ABI for clone methods.

**Recommendation:** Option 1. It's the only option where no `with` expression needs a run-time check and older code has nothing to skip, and it can be relaxed to option 2 later without breaking anyone.

### Type parameters constrained to `new()`, `struct`, or `unmanaged`

A type that has a final initializer can't be a type argument for a type parameter constrained to `new()`, `struct`, or `unmanaged`. Such a type parameter can be used in `new T()` or `new T { InterfaceProp = 1 }`, and in generic code compiled without knowledge of final initializers, that skips the final initializer. The compiler could call a runtime helper for these expressions, as it calls `Activator.CreateInstance<T>()` for the constructor itself, but existing binaries don't call that helper, and the runtime can't infer the call ([Appendix A](#appendix-a-final-initializer-calls-cant-be-inferred-by-the-runtime)). Required members blocks `new()` but not `struct` or `unmanaged`, and `new T()` on either kind of type parameter skips required members without a diagnostic.

```cs
static T Create<T>() where T : new() => new T();

Create<C>(); // 1: error; 2: ok if Create's assembly opts in; 3: ok if T is marked `allows init`

class C { init { } }
```

Shall we keep the restriction, let a compilation opt in, or let each type parameter opt in?

1. Keep the restriction. Either opt-in can be added later.
2. Let a compilation opt in. It then calls a runtime helper for every generic `new T` that runs a final initializer if there is one, and it gets an assembly-level attribute declaring that its `new()`, `struct`, and `unmanaged` type parameters are safe for types with final initializers. The opt-in is needed because the assembly can then no longer pass its own type parameters to `new()`, `struct`, or `unmanaged` type parameters of assemblies without the attribute. A user blocked by a library would ask its author to opt in and republish, and the author might in turn wait for upstream dependencies.
3. Let each type parameter opt in, for example with `where T : new(), allows init`. This would be unfortunate, since nearly everyone would want to add it and it would become boilerplate.

**Recommendation:** Option 1. Required members has the same limitation for `new()`, and either opt-in can come later.

### Marking for older compilers

Required members marks only the constructors that lack `SetsRequiredMembers`, with `CompilerFeatureRequired` and `Obsolete`. An older compiler can't call those constructors, but it can pass the type to a `new()` type parameter and create it without setting required members. The [proposed design](#older-compilers) marks the whole type instead:

```cs
// Compiled by an older compiler:
static T Create<T>() where T : new() => new T();

var c = Create<C>(); // 1: error; 2: ok, and C's final initializer doesn't run

class C { init { } } // compiled with final initializers, in another assembly
```

Shall we mark the type, or only its constructors?

1. Mark the type. Older compilers can't use it at all, including instances they receive from other code.
2. Mark only the constructors, as required members does. Older compilers can use instances they receive, but can pass the type to `new()`, `struct`, and `unmanaged` type parameters and skip its final initializer.

**Recommendation:** Option 1. Required members accepted the loophole, but closing it here costs older compilers only types they couldn't create correctly anyway.

### Means of preventing ordinary calls

C# syntax can't name a final initializer, but the [emitted method](#metadata-and-code-generation) is visible in metadata to other compilers and tools. How is it kept from being called?

1. Give it an unspeakable name, as records do for `<Clone>$`. Tools that list members show the odd name on every type that has a final initializer.
2. Give it a well-known name and put a modreq on its signature, so that only compilers that understand the modreq can call, override, or implement it, as `init` accessors do with `IsExternalInit`. `object.Finalize` is a precedent for a lifecycle method with an ordinary name that C# code can't call.

Either way, reflection can still find and call it, which serializers need, so its name should be documented.

**Recommendation:** Option 2. It follows the precedent of `init` accessors and keeps the member lists readable.

### Empty `with` expressions

`r with { }` makes a copy and assigns nothing. The original's final initializer ran when it was created. Its state may have changed since through `set` accessors, but that change didn't make the original uninitialized, so it's not clear the copy needs initializing again:

```cs
var copy = original with { }; // 1: the final initializer runs on copy; 2: it doesn't
```

Shall an empty `with` expression run the final initializer?

1. Run it. Every `with` expression runs it, just as every object creation does, with or without an object initializer. A final initializer that registers each instance or recomputes state from mutable members does so for the copy.
2. Skip it. The copy duplicates an object whose final initializer has run, so the call is saved. If older code's clone method throws for records with a final initializer, one of the answers to [derived records whose base has no final initializer](#derived-records-whose-base-has-no-final-initializer), `b with { }` wouldn't have to fail.

**Recommendation:** None. Running it treats every `with` expression alike, while skipping it treats the copy as the initialized object it duplicates, and we don't see a reason to prefer either.

## Appendix A: Final initializer calls can't be inferred by the runtime

For any `new` expression that runs a final initializer, or might, the compiler has to emit an explicit call in IL. Tying the feature to a new runtime that inserts the calls itself can't avoid that overhead, because IL doesn't record where an object creation expression ends:

```cs
class C
{
    public int A { set => Console.WriteLine("set A"); }
    public int B { set => Console.WriteLine("set B"); }
    init => Console.WriteLine("Final initializer");
}
```

These two samples behave differently:

```cs
var c = new C { A = 1 };
c.B = 2;
// Output:
// set A
// Final initializer
// set B
```

```cs
var c = new C { A = 1, B = 2 };
// Output:
// set A
// set B
// Final initializer
```

Only the explicit call to the final initializer distinguishes them. Without it, release builds produce identical IL for both samples, so the runtime can't infer where the call belongs.
