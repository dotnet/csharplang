# Pattern match `Span<char>` on a constant string

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary
[summary]: #summary

Permit pattern matching a `Span<char>` and a `ReadOnlySpan<char>` on a constant string.

## Motivation
[motivation]: #motivation

For perfomance, usage of `Span<char>` and `ReadOnlySpan<char>` is preferred over string in many scenarios. The framework has added many new APIs to allow you to use `ReadOnlySpan<char>` in place of a `string`.

A common operation on strings is to use a switch to test if it is a particular value, and the compiler optimizes such a switch. However there is currently no way to do the same on a `ReadOnlySpan<char>` efficiently, other than implementing the switch and the optimization manually.

In order to encourage adoption of `ReadOnlySpan<char>` we allow pattern matching a `ReadOnlySpan<char>`, on a constant `string`, thus also allowing it to be used in a switch.

```csharp
static bool Is123(ReadOnlySpan<char> s)
{
    return s is "123";
}

static bool IsABC(Span<char> s)
{
    return s switch { "ABC" => true, _ => false };
}
```

## Detailed design
[design]: #detailed-design

We alter the [spec](../csharp-7.0/pattern-matching.md#constant-pattern) for constant patterns as follows (the proposed addition is shown in bold):

> A constant pattern tests the value of an expression against a constant value. The constant may be any constant expression, such as a literal, the name of a declared `const` variable, or an enumeration constant, or a `typeof` expression etc.
>
> If both *e* and *c* are of integral types, the pattern is considered matched if the result of the expression `e == c` is `true`.
>
> **If *e* is of type `System.Span<char>` or `System.ReadOnlySpan<char>`, and *c* is a constant string, and *c* does not have a constant value of `null`, then the pattern is considered matching if `System.MemoryExtensions.SequenceEqual<char>(e, System.MemoryExtensions.AsSpan(c))` returns `true`.**
> 
> Otherwise the pattern is considered matching if `object.Equals(e, c)` returns `true`. In this case it is a compile-time error if the static type of *e* is not *pattern compatible* with the type of the constant.

### Well-known members
`System.Span<T>` and `System.ReadOnlySpan<T>` are matched by name, must be `ref struct`s, and can be defined outside corlib.

`System.MemoryExtensions` is matched by name and can be defined outside corlib.

The signature of `System.MemoryExtensions.SequenceEqual` overloads must match:
- `public static bool SequenceEqual<T>(System.Span<T>, System.ReadOnlySpan<T>)`
- `public static bool SequenceEqual<T>(System.ReadOnlySpan<T>, System.ReadOnlySpan<T>)`

The signature of `System.MemoryExtensions.AsSpan` must match:
- `public static System.ReadOnlySpan<char> AsSpan(string)`

Methods with optional parameters are excluded from consideration.

## Drawbacks
[drawbacks]: #drawbacks

None

## Alternatives
[alternatives]: #alternatives

None

## Unresolved questions
[unresolved]: #unresolved-questions

1. Should matching be defined independently from `MemoryExtensions.SequenceEqual()` etc.?

    > ... the pattern is considered matching if `e.Length == c.Length` and `e[i] == c[i]` for all characters in `e`.

    _Recommendation: Define in terms of `MemoryExtensions.SequenceEqual()` for performance. If `MemoryExtensions` is missing, report compile error._

2. Should matching against `(string)null` be allowed?

    If so, should `(string)null` subsume `""` since `MemoryExtensions.AsSpan(null) == MemoryExtensions.AsSpan("")`?
    ```csharp
    static bool IsEmpty(ReadOnlySpan<char> span)
    {
        return span switch
        {
            (string)null => true, // ok?
            "" => true,           // error: unreachable?
            _ => false,
        };
    }
    ```

    _Recommendation: Constant pattern `(string)null` should be reported as an error._

3. Should the constant pattern match include a runtime type test of the expression value for `Span<char>` or `ReadOnlySpan<char>`?
    ```csharp
    static bool Is123<T>(Span<T> s)
    {
        return s is "123"; // test for Span<char>?
    }

    static bool IsABC<T>(Span<T> s)
    {
        return s is Span<char> and "ABC"; // ok?
    }

    static bool IsEmptyString<T>(T t) where T : ref struct
    {
        return t is ""; // test for ReadOnlySpan<char>, Span<char>, string?
    }
    ```

    _Recommendation: No implicit runtime type test for constant pattern. (`IsABC<T>()` example is allowed because the type test is explicit.)_

4. Should subsumption consider constant string patterns, list patterns, and `Length` property pattern?
    ```csharp
    static int ToNum(ReadOnlySpan<char> s)
    {
        return s switch
        {
            { Length: 0 } => 0,
            "" => 1,        // error: unreachable?
            ['A',..] => 2,
            "ABC" => 3,     // error: unreachable?
            _ => 4,
        };
    }
    ```

    _Recommendation: Same subsumption behavior as used when the expression value is `string`. (Does that mean no subsumption between constant strings, list patterns, and `Length`, other than treating `[..]` as matching any?)_

## Design meetings

https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-10-07.md#readonlyspanchar-patterns
