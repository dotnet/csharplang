
# Ad hoc structural equality

One of the component features proposed as a part of records is structural equality defined on certain members. This seems like a feature which could be orthogonal and useful for regular classes. The following proposal details how we could mark specific members as "keys" to a generated structural equality.

Much like the `Key` modifier on anonymous properties in VB, a `key` modifier will be permitted on C# fields or properties.

If there are any `key` modifiers present on any members in a class or struct declaration,
equality will be generated for the type based on the annotated members as follows.

If no `key` members are present, the type uses inherited equality, like today.

## Synthesized members

An example of a complex hierarchy with multiple types with `key` modifiers:

```C#
class A
{
    public key int P1 { get; }
    public int P2 { get; }
}
class B : A
{
    public key int P3 { get; }
    public int P4 { get; }
}
```

produces

```C#
class A : IEquatable<A>
{
    public key int P1 { get; }
    public int P2 { get; }

    public override bool Equals(object o)
        => o is A a && Equals(a);

    public override int GetHashCode()
        => P1;

    public bool Equals(A a)
        => EqualityContractOrigin == a.EqualityContractOrigin
        && KeyEquals(a);

    protected virtual Type EqualityContractOrigin
        => typeof(A);

    protected bool KeyEquals(A a)
        => P1 == a.P1;

    public static bool operator==(A left, A right) => left.Equals(right);
    public static bool operator!=(A left, A right) => !(left == right);
}
class B : A, IEquatable<B>
{
    public key int P3 { get; }
    public int P4 { get; }

    public override bool Equals(object o)
        => o is B b && Equals(b);

    public override int GetHashCode()
        => Hash.Combine(base.GetHashCode(), P3);

    public bool Equals(B b)
        => EqualityContractOrigin == b.EqualityContractOrigin
        && KeyEquals(b);

    protected override Type EqualityContractOrigin
        => typeof(B);

    protected bool KeyEquals(B b)
        => base.KeyEquals(b) && P3 == a.P3;

    public static bool operator==(B left, B right) => left.Equals(right);
    public static bool operator!=(B left, B right) => !(left == right);
}
```


Three synthesized members will be added to the type, `T`:

1. override of `object.Equals(object)`. It is an error if a base class has sealed this member.

2. override of `object.GetHashCode()`. It is an error if a base class has sealed this member.

3. `public bool Equals(T)`. This member is also an implicit implementation of `IEquatable<T>`

4. `protected virtual Type EqualityContractOrigin { get; }`. If a virtual property with
the same name exists in a base class, the synthesized member is an override.

5. `protected bool KeyEquals(T)`. If there is a matching member in a base class, the
synthesized method hides that method.

5. `public static bool operator==(T, T)`

6. `public static bool operator!=(T, T)`

The implementations are defined as follows:

### `object.Equals`

Tests if the other object is of type `T` and, if so, passes it to the synthesized
member `bool Equals(T)`.

### `object.GetHashCode`

If there is not a visible `KeyEquals` method in the base class, the implementation
is equivalent to the GetHashCode on a tuple of each of the `key` members.

If the base class contains a `KeyEquals` method, the result of the above is combined
with `base.GetHashCode`, using a standard hash code combining implementation.

### `Equals(T other)`

Equivalent to `KeyEquals(other) && EqualityContractOrigin == other.EqualityContractOrigin`.

### `EqualityContractOrigin`

Returns `typeof(T)`, overriding a virtual member of the same name if one exists.

### `KeyEquals(T other)`

Equivalent to comparing equality between a tuple constructed of the `this` `key` members and the
`other` `key` members. If present, the result of `base.KeyEquals(other)` is `&&` with this
result.

### `operator==`, `operator!=`

Equivalent to the implementations for `System.ValueTuple<T>`