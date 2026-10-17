# Ref-Like Unions and Enums

This proposal explores allowing unions and data-carrying enums to hold ref-like values such as `Span<T>`. It is an optional extension to the [union-based discriminated-union proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/discriminated-unions.md) and the [data-carrying enum proposal](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/data-carrying-enums.md). Either design can proceed without it if the use cases do not justify the additional complexity.

## Motivation

A parser may want to return either a slice of its input or an error without allocating or copying the slice. `ReadOnlySpan<char>` cannot be boxed or stored in an ordinary struct, so the existing representations cannot carry that result. Ref-like unions and enums would support such payloads under the existing ref-safety rules. The union-based form also needs changes to its [access pattern](#union-pattern-for-ref-like-cases).

## Design

The proposed `ref` modifier applies to `enum struct` and `union`. A ref-like union is restricted to `case struct` declarations. Both forms make the enclosing type and each case type a `ref struct`, including data-free cases. Payloads may therefore be ref-like types or type parameters declared with `allows ref struct`.

The enum-based declaration is:

```csharp
public ref enum struct RefOption<T> where T : allows ref struct
{
    None = default,
    Some(T Value)
}
```

The corresponding union-based syntax is:

```csharp
public ref union RefOption<T> where T : allows ref struct
{
    case struct None = default;
    case struct Some(T Value);
}
```

Both declarations have the following conceptual translation:

```csharp
public ref struct RefOption<T> where T : allows ref struct
{
    public ref struct None { }

    public ref struct Some
    {
        public T Value { get; set; }

        public Some(T Value) => this.Value = Value;

        public readonly void Deconstruct(out T Value) => Value = this.Value;
    }

    private byte _discriminator;
    private T _value;

    // Generated construction, case-access APIs, and metadata omitted.
}
```

Either form can hold a span:

```csharp
Span<int> buffer = stackalloc int[16];
RefOption<Span<int>> option = RefOption<Span<int>>.Some(buffer);
```

The existing [ref-safety and anti-constraint rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/ref-struct-interfaces.md#ref-struct-generic-parameters) apply to case construction, conversions, pattern extraction, and `TryGetValue`. Ref-like payloads are stored in fields of their declared types under the [shared non-boxed layout](https://github.com/dotnet/csharplang/blob/main/proposals/discriminated-unions/union-enum-non-boxed-layout.md).

## Equality and Hashing

Ref-like enums, unions, and their case types do not receive synthesized record equality or hashing. By default, the compiler emits `Equals(object)` and `GetHashCode()` overrides that throw `NotSupportedException`, following [`Span<T>`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Span.cs) and [`ReadOnlySpan<T>`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/ReadOnlySpan.cs). Authors provide any custom equality and hashing on the enclosing enum or union, rather than on individual case types. For example, they may declare `==`/`!=`.

## Open Questions

### Union Pattern for Ref-Like Cases

For a ref-like union, the object-valued `Value` requirement is unresolved because these case values cannot be boxed. The current union pattern also requires case types to be implicitly convertible to `object`. This form needs changes to the union pattern, potentially allowing construction directly from a case value and access through `HasValue`/`TryGetValue` without an object-valued `Value`. Construction, conversions, and matching depend on resolving these requirements.
