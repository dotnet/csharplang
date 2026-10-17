# Discriminated Unions

This directory explores two alternative designs for bringing the traditional programming concept of discriminated unions to C#. One builds on C# unions, the other on enums.

## Start Here

Start with either of the two main proposals. Each introduces its syntax with a complete example before describing the design:

- [Discriminated unions](discriminated-unions.md) adds `case` declarations to unions.

  ```csharp
  union Option<T>
  {
      case struct None = default;
      case struct Some(T Value);
  }
  ```

- [Data-carrying enums](data-carrying-enums.md) adds cases with associated data through `enum class` and `enum struct`.

  ```csharp
  closed enum struct Option<T>
  {
      None = default,
      Some(T Value)
  }
  ```

Readers can follow either proposal without reading the other first. Most supporting documents are shared between the two approaches.

The [design comparison](comparison.md) examines the advantages and tradeoffs of each approach.

## Supporting Documents

The main proposals link to shared details as needed:

- [Common design](union-enum-common.md) covers contextual construction and inference, equality and hashing, nullability, metadata, and other shared behavior and open questions.
- [Non-boxed storage](union-enum-non-boxed-layout.md) explores a shared internal layout for unions and `enum struct` that avoids boxing during construction and extraction.
- [Generated layouts](union-case-types-and-layout.md) explores selecting storage through attributes and source generators, including native layout and Swift interop.
- [Ref-like types](union-enum-ref-like-types.md) explores an optional extension for ref-like payloads and its open questions. Neither main design depends on it.
- [Switch emission](union-enum-switch-emit.md) discusses implementation strategies and possible compiler and JIT optimizations for both designs.

[Enum members](enum-members.md) is specific to enums. It proposes allowing user-declared members in ordinary enums and supports those members in the data-carrying enum design.
