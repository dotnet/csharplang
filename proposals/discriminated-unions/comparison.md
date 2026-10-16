# Comparing the Discriminated Union Designs

The [union-based design](discriminated-unions.md) and [data-carrying enum design](data-carrying-enums.md) offer two ways to bring discriminated unions to C#. The union approach extends one feature to cover both existing types and newly declared cases. The enum approach extends familiar named alternatives and, with `enum class`, represents each value directly as a case record.

LDM [previously chose the enum-centric direction](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-09-29.md#enums-vs-unions) and anticipated translation through unions. These drafts revisit the choice with differences in both declaration syntax and runtime type models.

## Declaration and Language Design

### One Union Feature or an Extension to Enums

Using `union` directly matches the term "discriminated union." It also gives modern C# a coherent family of type unions and discriminated unions, with common conversions, matching, and tooling. Learning one form helps explain the other, and future improvements to unions can benefit both.

Enums offer a different familiarity: they have represented a choice among named alternatives since C# began. Adding data to those alternatives is a small conceptual step. However, the name can invite expectations about integral values, `Enum.GetValues`, or `where T : Enum`. These new declarations produce records and structs, so those ordinary numeric-enum facilities do not apply.

### Declaration Syntax

An enum case extends the familiar `Name` syntax by adding a primary constructor parameter list:

```csharp
closed enum struct Result
{
    Success(string Value),
    Failure(int Code);

    public bool Succeeded => this is Success;
}
```

This keeps the case list compact. The cost is a longer declaration header and additional rules for separating cases from members. Cases must precede members, and the transition uses a semicolon in place of the comma between cases.

The union form has a lighter header and gives each case its own declaration:

```csharp
union struct Result
{
    case Success(string Value);
    case Failure(int Code);

    public bool Succeeded => this is Success;
}
```

The repeated `case` keyword adds tokens, but makes cases easy to recognize among ordinary members. Each declaration ends with a semicolon, without a separate boundary between cases and members. The grammar also permits cases and other members to be interspersed. For reference-type cases, the short form `union Result` avoids both `closed` and `class` in the header, whereas the enum form is `closed enum class Result`.

### Exhaustiveness and Library Evolution

Unions are exhaustive over their declared case types by default. This matches the usual expectation of a discriminated union and avoids requiring a `closed` modifier for the common fixed set of alternatives.

Data-carrying enums instead adopt the [open/closed enum policy](../closed-enums.md). An author can omit `closed` when future versions may add cases, requiring consumers to provide a fallback, or promise a fixed set with `closed`. This already provides a choice for evolving public APIs. An equivalent indication for [non-exhaustive unions](discriminated-unions.md#open-unions) remains an open question. These defaults could be aligned independently of the underlying type model.

### Members and Interfaces

Unions already support members and interface implementations. The union-based proposal can reuse their rules and concentrate on declaring cases. Data-carrying enums introduce those capabilities into another declaration form and build on [Members in Enum Declarations](enum-members.md) for user-declared functionality. That is additional language design, but it also makes ordinary enums more useful independently of discriminated unions.

### Generic Case Reuse

Both shorthand designs nest their cases, so a case inherits every enclosing type argument. An error case cannot be passed directly between `Result<int, E>` and `Result<string, E>`, even though its error payload has the same type. The [common design](union-enum-common.md#generic-case-identity) explains this restriction. The underlying union feature provides an alternative: separately declared case records can use only the type parameters their payloads need and be shared between union instantiations.

## Object Model and Existing C# Code

### Direct Case Objects

An `enum class` value is the case record itself. A `union` without `struct` is a struct containing a reference to that record. The direct model makes `enum class` fit more naturally into existing code that relies on inheritance or works through `object` and interfaces.

Given `enum class Result` or `union Result` with the cases above:

```csharp
Result.Success success = Result.Success("Complete");
Result result = success;
object value = result;
```

For `enum class`, `value` is still the `Result.Success` object. An ordinary case type test succeeds. For `union`, the last assignment boxes the enclosing `Result`, so code receiving that `object` must unwrap the union to inspect its case. Ordinary typed construction of the union does not allocate a second heap object: the struct stores the existing case reference.

Inheritance also means a concrete enum case retains methods and interfaces declared on its enclosing enum. A union case does not inherit its wrapper's functionality. Narrowing a union to its case can therefore require converting back to the union to use those members.

### Generic Libraries and Tooling

Given an `IEnumerable<Result> results` whose elements represent `Success` cases:

```csharp
IEnumerable<Result.Success> successes = results.OfType<Result.Success>();
```

For `enum class`, this finds the case objects. For `union`, `union struct`, and `enum struct`, enumeration exposes boxed enclosing structs, so `OfType` finds no case instances. A lambda such as `results.Where(result => result is Result.Success)` can use union- or enum-aware matching because the input is statically known to be `Result`. Improved inference does not change ordinary runtime type tests inside generic library code.

With `enum class`, `IEnumerable<Result.Success>` can also convert covariantly to `IEnumerable<Result>`. A union requires element-wise conversion because its cases are not derived from the enclosing type.

Reflection and serialization expose the same representation boundary. An inspection contract such as `IUnion` lets tools opt into unwrapping a union, but existing libraries do not automatically use it. Case discovery and construction remain [metadata design work](union-enum-common.md#metadata-encoding).

### Class and Struct Semantics

`enum class` and `enum struct` describe the actual kind of the enclosing type, consistent with `record class` and `record struct`. Authors can choose a reference type when APIs or generic constraints require one.

Both `union` and `union struct` declare structs. The `struct` keyword instead changes the storage strategy and makes declared cases value types. This offers a short default declaration, but requires explaining that `struct` does not change the enclosing type from a class to a struct. A proposed [`union class` synonym](discriminated-unions.md#explicit-union-class-spelling) would make the class-case spelling explicit while potentially reinforcing that confusion.

### Matching and Equality

`enum class` uses ordinary reference type patterns and record equality. Its value equality, hashing, and equality operators arise from the record hierarchy.

The union approach reuses [union matching](../csharp-15.0/unions.md#union-matching), saving new design work, but those rules add distinctions for users to learn. Typed patterns inspect the contained case, while untyped property patterns inspect the wrapper. Consequently, the enclosing union implementing an interface does not make its cases match that interface pattern.

A union also needs to lift efficient typed equality and equality operators from its cases to the enclosing struct. The [common design](union-enum-common.md#equality-and-hashing) supplies those members, so this is additional synthesis and specification work rather than a loss of value equality for callers. `enum struct` requires the same attention to equality over its stored case.

### Integration with Type Unions

The union design participates directly in the existing union system, including the [`IUnion` inspection interface](../csharp-15.0/unions.md#union-access-interface). Libraries and tools can use one contract to inspect both type unions and discriminated unions. Data-enum integration is [optional and exploratory](data-carrying-enums.md#possible-connections-to-type-unions), so adopting that design alone does not provide the same contract.

That contract also has costs. The current union declaration reserves the name `Value` for the contained case, which can compete with a domain meaning such as an option's payload. For `union struct`, reading `Value` boxes a case. Data enums need not expose an object-valued view at all. Both struct designs can use the [shared non-boxed layout](union-enum-non-boxed-layout.md) for ordinary construction and typed extraction without boxing.

## Differences in the Current Drafts

The following differences affect usability or design effort, but could be changed without choosing a different declaration family.

| Area | Advantage and tradeoff |
| --- | --- |
| [Inference from a known case](union-enum-common.md#inference-from-a-known-case-type) | `enum class` can use ordinary inference through its case's base type. Both union forms depend on proposed union-case inference. `enum struct` needs a further extension, otherwise callers need explicit type arguments or a value already typed as the enum. This comparison concerns known case types, not leading-dot shorthand generally. |
| [Case extraction APIs](data-carrying-enums.md#generated-apis-and-conversions) | `enum struct` provides predicates such as `IsSome` and explicit casts back to case types. These offer convenient direct access at the cost of more generated names. Unions currently rely on matching and their established access APIs. |
| [User-declared constructors](discriminated-unions.md#user-declared-constructors) | Unions already have rules for convenient custom initialization. Enum constructors remain an open question, though static factories can provide equivalent functionality. Whether unions should also restrict direct construction of the enclosing type remains unresolved. Enums already require callers to construct cases. |

## Optional Ref-Like Extension

The [ref-like extension](union-enum-ref-like-types.md) has fewer object-view conflicts on the enum route. Both designs need ref-safety and storage rules, but `ref union struct` must additionally resolve the union pattern's requirements for an object-valued `Value` and case types convertible to `object`. Ref-like cases cannot be boxed. Data enums have no mandatory union pattern to revise.
