# Record structs

The syntax for a record struct is as follows:

```antlr
record_struct_declaration
    : attributes? struct_modifier* 'partial'? 'record' 'struct' identifier type_parameter_list?
      parameter_list? struct_interfaces? type_parameter_constraints_clause* record_struct_body
    ;

record_struct_body
    : struct_body
    | ';'
    ;
```

Record struct types are value types, like other struct types. They implicitly inherit from the class `System.ValueType`.
The modifiers and members of a record struct are subject to the same restrictions as those of structs
(accessibility on type, modifiers on members, `base(...)` instance constructor initializers,
definite assignment for `this` in constructor, destructors, ...).
Record structs will also follow the same rules as structs for parameterless instance constructors and field initializers,
but this document assumes that we will lift those restrictions for structs generally.

See https://github.com/dotnet/csharplang/blob/master/spec/structs.md

Record structs cannot use `ref` modifier.

At most one partial type declaration of a partial record struct may provide a `parameter_list`.
The `parameter_list` may not be empty.

Record struct parameters cannot use `ref`, `out` or `this` modifiers (but `in` and `params` are allowed).

## Members of a record struct

In addition to the members declared in the record struct body, a record struct type has additional synthesized members.
Members are synthesized unless a member with a "matching" signature is declared in the record struct body or
an accessible concrete non-virtual member with a "matching" signature is inherited.
Two members are considered matching if they have the same
signature or would be considered "hiding" in an inheritance scenario.
See https://github.com/dotnet/csharplang/blob/master/spec/basic-concepts.md#signatures-and-overloading

It is an error for a member of a record struct to be named "Clone".

It is an error for an instance field of a record struct to have an unsafe type.

A record struct is not permitted to declare a destructor.

The synthesized members are as follows:

### Equality members

The synthesized equality members are similar as in a record class (`Equals` for this type, `Equals` for `object` type, `==` and `!=` operators for this type),\
except for the lack of `EqualityContract`, null checks or inheritance.

The record struct implements `System.IEquatable<R>` and includes a synthesized strongly-typed overload of `Equals(R other)` where `R` is the record struct.
The method is `public`.
The method can be declared explicitly. It is an error if the explicit declaration does not match the expected signature or accessibility.

If `Equals(R other)` is user-defined (not synthesized) but `GetHashCode` is not, a warning is produced.

```C#
public bool Equals(R other);
```

The synthesized `Equals(R)` returns `true` if and only if for each instance field `fieldN` in the record struct
the value of `System.Collections.Generic.EqualityComparer<TN>.Default.Equals(fieldN, other.fieldN)` where `TN` is the field type is `true`.

The record struct includes synthesized `==` and `!=` operators equivalent to operators declared as follows:
```C#
public static bool operator==(R r1, R r2)
    => r1.Equals(r2);
public static bool operator!=(R r1, R r2)
    => !(r1 == r2);
```
The `Equals` method called by the `==` operator is the `Equals(R other)` method specified above. The `!=` operator delegates to the `==` operator. It is an error if the operators are declared explicitly.

The record struct includes a synthesized override equivalent to a method declared as follows:
```C#
public override bool Equals(object? obj);
```
It is an error if the override is declared explicitly. 
The synthesized override returns `other is R temp && Equals(temp)` where `R` is the record struct.

The record struct includes a synthesized override equivalent to a method declared as follows:
```C#
public override int GetHashCode();
```
The method can be declared explicitly.

A warning is reported if one of `Equals(R)` and `GetHashCode()` is explicitly declared but the other method is not explicit.

The synthesized override of `GetHashCode()` returns an `int` result of combining the values of `System.Collections.Generic.EqualityComparer<TN>.Default.GetHashCode(fieldN)` for each instance field `fieldN` with `TN` being the type of `fieldN`.

For example, consider the following record struct:
```C#
record struct R1(T1 P1, T2 P2);
```

For this record struct, the synthesized equality members would be something like:
```C#
struct R1 : IEquatable<R1>
{
    public T1 P1 { get; set; }
    public T2 P2 { get; set; }
    public override bool Equals(object? obj) => obj is R1 temp && Equals(temp);
    public bool Equals(R1 other)
    {
        return
            EqualityComparer<T1>.Default.Equals(P1, other.P1) &&
            EqualityComparer<T2>.Default.Equals(P2, other.P2);
    }
    public static bool operator==(R1 r1, R1 r2)
        => r1.Equals(r2);
    public static bool operator!=(R1 r1, R1 r2)
        => !(r1 == r2);    
    public override int GetHashCode()
    {
        return Combine(
            EqualityComparer<T1>.Default.GetHashCode(P1),
            EqualityComparer<T2>.Default.GetHashCode(P2));
    }
}
```

### Printing members: PrintMembers and ToString methods

The record struct includes a synthesized method equivalent to a method declared as follows:
```C#
private bool PrintMembers(System.Text.StringBuilder builder);
```

The method does the following:
1. for each of the record struct's printable members (non-static public field and readable property members), appends that member's name followed by " = " followed by the member's value separated with ", ",
2. return true if the record struct has printable members.

For a member that has a value type, we will convert its value to a string representation using the most efficient method available to the target platform. At present that means calling `ToString` before passing to `StringBuilder.Append`.

The `PrintMembers` method can be declared explicitly.
It is an error if the explicit declaration does not match the expected signature or accessibility.

The record struct includes a synthesized method equivalent to a method declared as follows:
```C#
public override string ToString();
```

The method can be declared explicitly. It is an error if the explicit declaration does not match the expected signature or accessibility.

The synthesized method:
1. creates a `StringBuilder` instance,
2. appends the record struct name to the builder, followed by " { ",
3. invokes the record struct's `PrintMembers` method giving it the builder, followed by " " if it returned true,
4. appends "}",
5. returns the builder's contents with `builder.ToString()`.

For example, consider the following record struct:

``` csharp
record struct R1(T1 P1, T2 P2);
```

For this record struct, the synthesized printing members would be something like:

```C#
struct R1 : IEquatable<R1>
{
    public T1 P1 { get; set; }
    public T2 P2 { get; set; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(nameof(P1));
        builder.Append(" = ");
        builder.Append(this.P1); // or builder.Append(this.P1.ToString()); if P1 has a value type
        builder.Append(", ");

        builder.Append(nameof(P2));
        builder.Append(" = ");
        builder.Append(this.P2); // or builder.Append(this.P2.ToString()); if P2 has a value type

        return true;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(R1));
        builder.Append(" { ");

        if (PrintMembers(builder))
            builder.Append(" ");

        builder.Append("}");
        return builder.ToString();
    }
}
```

## Positional record struct members

In addition to the above members, record structs with a parameter list ("positional records") synthesize
additional members with the same conditions as the members above.

### Primary Constructor

A record struct has a public constructor whose signature corresponds to the value parameters of the
type declaration. This is called the primary constructor for the type. It is an error to have a primary
constructor and a constructor with the same signature already present in the struct.
A record struct is not permitted to declare a parameterless primary constructor.

Instance field declarations for a record struct are permitted to include variable initializers.
If there is no primary constructor, the instance initializers execute as part of the parameterless constructor.
Otherwise, at runtime the primary constructor executes the instance initializers appearing in the record-struct-body.

If a record struct has a primary constructor, any user-defined constructor must have an
explicit `this` constructor initializer.

Parameters of the primary constructor as well as members of the record struct are in scope within initializers of instance fields or properties. 
Instance members would be an error in these locations (similar to how instance members are in scope in regular constructor initializers
today, but an error to use), but the parameters of the primary constructor would be in scope and useable and
would shadow members. Static members would also be useable.

A warning is produced if a parameter of the primary constructor is not read.

The definite assigment rules for struct instance constructors apply to the primary constructor of record structs. For instance, the following
is an error:

```csharp
record struct Pos(int X) // definite assignment error in primary constructor
{
    private int x;
    public int X { get { return x; } set { x = value; } } = X;
}
```

### Properties

For each record struct parameter of a record struct declaration there is a corresponding public property
member whose name and type are taken from the value parameter declaration.

For a record struct:

* A public `get` and `init` auto-property is created if the record struct has `readonly` modifier, `get` and `set` otherwise.
  Both kinds of set accessors (`set` and `init`) are considered "matching". So the user may declare an init-only property
  in place of a synthesized mutable one.
  An inherited `abstract` property with matching type is overridden.
  No auto-property is created if the record struct has an instance field with expected name and type.
  It is an error if the inherited property does not have `public` `get` and `set`/`init` accessors.
  It is an error if the inherited property or field is hidden.  
  The auto-property is initialized to the value of the corresponding primary constructor parameter.
  Attributes can be applied to the synthesized auto-property and its backing field by using `property:` or `field:`
  targets for attributes syntactically applied to the corresponding record struct parameter.  

### Deconstruct

A positional record struct with at least one parameter synthesizes a public void-returning instance method called `Deconstruct` with an out
parameter declaration for each parameter of the primary constructor declaration. Each parameter
of the Deconstruct method has the same type as the corresponding parameter of the primary
constructor declaration. The body of the method assigns each parameter of the Deconstruct method
to the value from an instance member access to a member of the same name.
The method can be declared explicitly. It is an error if the explicit declaration does not match
the expected signature or accessibility, or is static.

# Allow `with` expression on structs

It is now valid for the receiver in a `with` expression to have a struct type.

On the right hand side of the `with` expression is a `member_initializer_list` with a sequence
of assignments to *identifier*, which must be an accessible instance field or property of the receiver's
type.

For a receiver with struct type, the receiver is first copied, then each `member_initializer` is processed 
the same way as an assignment to a field or property access of the result of the conversion. 
Assignments are processed in lexical order.

# Improvements on records

## Allow `record class`

The existing syntax for record types allows `record class` with the same meaning as `record`:

```antlr
record_declaration
    : attributes? class_modifier* 'partial'? 'record' 'class'? identifier type_parameter_list?
      parameter_list? record_base? type_parameter_constraints_clause* record_body
    ;
```

## Allow user-defined positional members to be fields

See https://github.com/dotnet/csharplang/blob/master/meetings/2020/LDM-2020-10-05.md#changing-the-member-type-of-a-primary-constructor-parameter

No auto-property is created if the record has or inherits an instance field with expected name and type.

# Allow parameterless constructors and member initializers in structs

We are going to support both parameterless constructors and member initializers in structs.
This will be specified in more details.

Raw notes:  
Allow parameterless ctors on structs and also field initializers (no runtime detection)  
We will enumerate scenarios where initializers aren't evaluated: arrays, generics, default, ...  
Consider diagnostics for using struct with parameterless ctor in some of those cases?  

# Open questions

- should we disallow a user-defined constructor with a copy constructor signature?
- confirm that we want to disallow members named "Clone".
- `with` on generics? (may affect the design for record structs)
- double-check that synthesized `Equals` logic is functionally equivalent to runtime implementation (e.g. float.NaN)
- how to recognize record structs in metadata? (we don't have an unspeakable clone method to leverage...)
- should `GetHashCode` include a hash of the type itself, to get different values between `record struct S1;` and `record struct S2;`?
- could field- or property-targeting attributes be placed in the positional parameter list?
- how to place attributes on the properties of a record struct?  IDE has serialization types that would work nicely as record structs, but which need attributes on the members. Supporting `[property: DataMember(Order = 1)]` would solve this.

## Answered

- confirm that we want to keep PrintMembers design (separate method returning `bool`) (answer: yes)
- confirm we won't allow `record ref struct` (issue with `IEquatable<RefStruct>` and ref fields) (answer: yes)
- confirm implementation of equality members. Alternative is that synthesized `bool Equals(R other)`, `bool Equals(object? other)` and operators all just delegate to `ValueType.Equals`. (answer: yes)
- confirm that we want to allow field initializers when there is a primary constructor. Do we also want to allow parameterless struct constructors while we're at it (the Activator issue was apparently fixed)? (answer: yes, updated spec should be reviewed in LDM)
- how much do we want to say about `Combine` method? (answer: as little as possible)
