# Null-conditional assignment

## Summary
[summary]: #summary

Permits assignment to occur conditionally within a `a?.b` or `a?[b]` expression.

```cs
using System;

class C
{
    public object obj;
}

void M(C? c)
{
    c?.obj = new object();
}
```

```cs
using System;

class C
{
    public event Action E;
}

void M(C? c)
{
    c?.E += () => { Console.WriteLine("handled event E"); };
}
```

```cs
void M(object[]? arr)
{
    arr?[42] = new object();
}

```
## Motivation
[motivation]: #motivation

A variety of motivating use cases can be found in the championed issue. Major motivations include:
1. Parity between properties and `Set()` methods.
2. Attaching event handlers in UI code.

## Detailed design
[design]: #detailed-design

- The right side of the assignment is only evaluated when the receiver of the conditional access is non-null.
```cs
// M() is only executed if 'a' is non-null.
// note: the value of 'a.b' doesn't affect whether things are evaluated here.
a?.b = M();
```

- All forms of compound assignment are allowed.
```cs
a?.b -= M(); // ok
a?.b += M(); // ok
// etc.
```

- If the result of the expression is used, the expression's type must be known to be of a value type or a reference type. This is consistent with existing behaviors on conditional accesses.
```cs
class C<T>
{
    public T? field;
}

void M1<T>(C<T>? c, T t)
{
    (c?.field = t).ToString(); // error: 'T' cannot be made nullable.
    c?.field = t; // ok
}
```

- Conditional access expressions are still not lvalues, and it's still not allowed to e.g. take a `ref` to them.
```cs
M(ref a?.b); // error
```

- It is not allowed to ref-assign to a conditional access. The main reason for this is that the only way you would conditionally access a ref variable is a ref field, and ref structs are forbidden from being used in nullable value types. If a valid scenario for a conditional ref-assignment came up in the future, we could add support at that time.
```cs
ref struct RS
{
    public ref int b;
}

void M(RS a, ref int x)
{
  a?.b = ref x; // error: Operator '?' can't be applied to operand of type 'C'.
}
```

- It's not possible to e.g. assign to conditional accesses through deconstruction assignment. We anticipate it will be rare for people to want to do this, and not a significant drawback to need to do it over multiple separate assignment expressions instead.
```cs
(a?.b, c?.d) = (x, y); // error
```

### Specification
The *null conditional assignment* grammar is defined as follows:

```antlr
null_conditional_assignment
    : null_conditional_member_access assignment_operator expression
    : null_conditional_element_access assignment_operator expression
```
See [§11.7.7](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1177-null-conditional-member-access) and [§11.7.11](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11711-null-conditional-element-access) for reference.

When the *null conditional assignment* appears in an expression-statement, its semantics are as follows:
- `P?.A = B` is equivalent to `if (P is not null) P.A = B;`, except that `P` is only evaluated once.
- `P?[A] = B` is equivalent to `if (P is not null) P[A] = B`, except that `P` is only evaluated once.

Otherwise, its semantics are as follows:
- `P?.A = B` is equivalent to `(P is null) ? (T?)null : (P.A = B)`, where `T` is the result type of `P.A = B`, except that `P` is only evaluated once.
- `P?[A] = B` is equivalent to `(P is null) ? (T?)null : (P[A] = B)`, where `T` is the result type of `P[A] = B`, except that `P` is only evaluated once.

### Implementation
The grammar in the standard currently doesn't correspond strongly to the syntax design used in the implementation. We expect that to remain the case after this feature is implemented. The syntax design in the [implementation](https://github.com/dotnet/roslyn/blob/09408ab8a29e03caddfb11f29328c05169ac7cde/src/Compilers/CSharp/Portable/Syntax/Syntax.xml#L583-L607) isn't expected to actually change--only the way it is used will change. For example:

```mermaid
graph TD;
subgraph ConditionalAccessExpression
  whole[a?.b = c]
end
subgraph  
  direction LR;
  subgraph Expression
    whole-->a;
  end
  subgraph OperatorToken
    whole-->?;
  end
  subgraph WhenNotNull
    whole-->whenNotNull[.b = c];
    whenNotNull-->.b;
    whenNotNull-->=;
    whenNotNull-->c;
  end
end
```

### Complex examples
```cs
class C
{
    ref int M() => /*...*/;
}

void M1(C? c)
{
    c?.M() = 42; // equivalent to:
    if (c is not null)
        c.M() = 42;
}

int? M2(C? c)
{
    return c?.M() = 42; // equivalent to:
    return c is null ? (int?)null : c.M() = 42;
}
```

```cs
M(a?.b?.c = d); // equivalent to:
M(a is null
    ? null
    : (a.b is null
        ? null
        : (a.b.c = d)));
```

```cs
return a?.b = c?.d = e?.f; // equivalent to:
return a?.b = (c?.d = e?.f); // equivalent to:
return a is null
    ? null
    : (a.b = c is null
        ? null
        : (c.d = e is null
            ? null
            : e.f));
}
```

```cs
a?.b ??= c; // equivalent to:
if (a is not null)
{
    if (a.b is null)
    {
        a.b = c;
    }
}

return a?.b ??= c; // equivalent to:
return a is null
    ? null
    : a.b is null
        ? a.b = c
        : a.b;
```

## Drawbacks
[drawbacks]: #drawbacks

The choice to keep the assignment within the conditional access introduces some additional work for the IDE, which has many code paths which need to work backwards from an assignment to identifying the thing being assigned.

## Alternatives
[alternatives]: #alternatives

We could instead make the `?.` syntactically a child of the `=`. This makes it so any handling of `=` expressions needs to become aware of the conditionality of the right side in the presence of `?.` on the left. It also makes it so the structure of the syntax doesn't correspond as strongly to the semantics.

## Unresolved questions
[unresolved]: #unresolved-questions

## Design meetings

* https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-04-27.md#null-conditional-assignment
* https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-08-31.md#null-conditional-assignment
* https://github.com/dotnet/csharplang/blob/main/meetings/2022/LDM-2022-10-26.md#null-conditional-assignment
