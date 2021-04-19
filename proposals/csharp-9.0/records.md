
# Records

This proposal tracks the specification for the C# 9 records feature, as agreed to by the C#
language design team.

The syntax for a record is as follows:

```antlr
record_declaration
    : attributes? class_modifier* 'partial'? 'record' identifier type_parameter_list?
      parameter_list? record_base? type_parameter_constraints_clause* record_body
    ;

record_base
    : ':' class_type argument_list?
    | ':' interface_type_list
    | ':' class_type argument_list? ',' interface_type_list
    ;

record_body
    : '{' class_member_declaration* '}' ';'?
    | ';'
    ;
```

Record types are reference types, similar to a class declaration. It is an error for a record to provide
a `record_base` `argument_list` if the `record_declaration` does not contain a `parameter_list`.
At most one partial type declaration of a partial record may provide a `parameter_list`.

Record parameters cannot use `ref`, `out` or `this` modifiers (but `in` and `params` are allowed).

## Inheritance

Records cannot inherit from classes, unless the class is `object`, and classes cannot inherit from records. Records can inherit from other records.

## Members of a record type

In addition to the members declared in the record body, a record type has additional synthesized members.
Members are synthesized unless a member with a "matching" signature is declared in the record body or
an accessible concrete non-virtual member with a "matching" signature is inherited.
Two members are considered matching if they have the same
signature or would be considered "hiding" in an inheritance scenario.
It is an error for a member of a record to be named "Clone".
It is an error for an instance field of a record to have an unsafe type.

The synthesized members are as follows:

### Equality members

If the record is derived from `object`, the record type includes a synthesized readonly property equivalent to a property declared as follows:
```C#
Type EqualityContract { get; };
```
The property is `private` if the record type is `sealed`. Otherwise, the property is `virtual` and `protected`.
The property can be declared explicitly. It is an error if the explicit declaration does not match the expected signature or accessibility, or if the explicit declaration doesn't allow overriding it in a derived type and the record type is not `sealed`.

If the record type is derived from a base record type `Base`, the record type includes a synthesized readonly property equivalent to a property declared as follows:
```C#
protected override Type EqualityContract { get; };
```

The property can be declared explicitly. It is an error if the explicit declaration does not match the expected signature or accessibility, or if the explicit declaration doesn't allow overriding it in a derived type and the record type is not `sealed`. It is an error if either synthesized, or explicitly declared property doesn't override a property with this signature in the record type `Base` (for example, if the property is missing in the `Base`, or sealed, or not virtual, etc.).
The synthesized property returns `typeof(R)` where `R` is the record type.

The record type implements `System.IEquatable<R>` and includes a synthesized strongly-typed overload of `Equals(R? other)` where `R` is the record type.
The method is `public`, and the method is `virtual` unless the record type is `sealed`.
The method can be declared explicitly. It is an error if the explicit declaration does not match the expected signature or accessibility, or the explicit declaration doesn't allow overriding it in a derived type and the record type is not `sealed`.

If `Equals(R? other)` is user-defined (not synthesized) but `GetHashCode` is not, a warning is produced.

```C#
public virtual bool Equals(R? other);
```
The synthesized `Equals(R?)` returns `true` if and only if each of the following are `true`:
- `other` is not `null`, and
- For each instance field `fieldN` in the record type that is not inherited, the value of
`System.Collections.Generic.EqualityComparer<TN>.Default.Equals(fieldN, other.fieldN)` where `TN` is the field type, and
- If there is a base record type, the value of `base.Equals(other)` (a non-virtual call to `public virtual bool Equals(Base? other)`); otherwise
the value of `EqualityContract == other.EqualityContract`.

The record type includes synthesized `==` and `!=` operators equivalent to operators declared as follows:
```C#
public static bool operator==(R? left, R? right)
    => (object)left == right || (left?.Equals(right) ?? false);
public static bool operator!=(R? left, R? right)
    => !(left == right);
```
The `Equals` method called by the `==` operator is the `Equals(R? other)` method specified above. The `!=` operator delegates to the `==` operator. It is an error if the operators are declared explicitly.
    
If the record type is derived from a base record type `Base`, the record type includes a synthesized override equivalent to a method declared as follows:
```C#
public sealed override bool Equals(Base? other);
```
It is an error if the override is declared explicitly. It is an error if the method doesn't override a method with same signature in record type `Base` (for example, if the method is missing in the `Base`, or sealed, or not virtual, etc.).
The synthesized override returns `Equals((object?)other)`.

The record type includes a synthesized override equivalent to a method declared as follows:
```C#
public override bool Equals(object? obj);
```
It is an error if the override is declared explicitly. It is an error if the method doesn't override `object.Equals(object? obj)` (for example, due to shadowing in intermediate base types, etc.).
The synthesized override returns `Equals(other as R)` where `R` is the record type.

The record type includes a synthesized override equivalent to a method declared as follows:
```C#
public override int GetHashCode();
```
The method can be declared explicitly.
It is an error if the explicit declaration doesn't allow overriding it in a derived type and the record type is not `sealed`. It is an error if either synthesized, or explicitly declared method doesn't override `object.GetHashCode()` (for example, due to shadowing in intermediate base types, etc.).
 
A warning is reported if one of `Equals(R?)` and `GetHashCode()` is explicitly declared but the other method is not explicit.

The synthesized override of `GetHashCode()` returns an `int` result of combining the following values:
- For each instance field `fieldN` in the record type that is not inherited, the value of
`System.Collections.Generic.EqualityComparer<TN>.Default.GetHashCode(fieldN)` where `TN` is the field type, and
- If there is a base record type, the value of `base.GetHashCode()`; otherwise
the value of `System.Collections.Generic.EqualityComparer<System.Type>.Default.GetHashCode(EqualityContract)`.

For example, consider the following record types:
```C#
record R1(T1 P1);
record R2(T1 P1, T2 P2) : R1(P1);
record R3(T1 P1, T2 P2, T3 P3) : R2(P1, P2);
```

For those record types, the synthesized equality members would be something like:
```C#
class R1 : IEquatable<R1>
{
    public T1 P1 { get; init; }
    protected virtual Type EqualityContract => typeof(R1);
    public override bool Equals(object? obj) => Equals(obj as R1);
    public virtual bool Equals(R1? other)
    {
        return !(other is null) &&
            EqualityContract == other.EqualityContract &&
            EqualityComparer<T1>.Default.Equals(P1, other.P1);
    }
    public static bool operator==(R1? left, R1? right)
        => (object)left == right || (left?.Equals(right) ?? false);
    public static bool operator!=(R1? left, R1? right)
        => !(left == right);
    public override int GetHashCode()
    {
        return Combine(EqualityComparer<Type>.Default.GetHashCode(EqualityContract),
            EqualityComparer<T1>.Default.GetHashCode(P1));
    }
}

class R2 : R1, IEquatable<R2>
{
    public T2 P2 { get; init; }
    protected override Type EqualityContract => typeof(R2);
    public override bool Equals(object? obj) => Equals(obj as R2);
    public sealed override bool Equals(R1? other) => Equals((object?)other);
    public virtual bool Equals(R2? other)
    {
        return base.Equals((R1?)other) &&
            EqualityComparer<T2>.Default.Equals(P2, other.P2);
    }
    public static bool operator==(R2? left, R2? right)
        => (object)left == right || (left?.Equals(right) ?? false);
    public static bool operator!=(R2? left, R2? right)
        => !(left == right);
    public override int GetHashCode()
    {
        return Combine(base.GetHashCode(),
            EqualityComparer<T2>.Default.GetHashCode(P2));
    }
}

class R3 : R2, IEquatable<R3>
{
    public T3 P3 { get; init; }
    protected override Type EqualityContract => typeof(R3);
    public override bool Equals(object? obj) => Equals(obj as R3);
    public sealed override bool Equals(R2? other) => Equals((object?)other);
    public virtual bool Equals(R3? other)
    {
        return base.Equals((R2?)other) &&
            EqualityComparer<T3>.Default.Equals(P3, other.P3);
    }
    public static bool operator==(R3? left, R3? right)
        => (object)left == right || (left?.Equals(right) ?? false);
    public static bool operator!=(R3? left, R3? right)
        => !(left == right);
    public override int GetHashCode()
    {
        return Combine(base.GetHashCode(),
            EqualityComparer<T3>.Default.GetHashCode(P3));
    }
}
```

### Copy and Clone members

A record type contains two copying members:

* A constructor taking a single argument of the record type. It is referred to as a "copy constructor".
* A synthesized public parameterless instance "clone" method with a compiler-reserved name

The purpose of the copy constructor is to copy the state from the parameter to the new instance being
created. This constructor doesn't run any instance field/property initializers present in the record
declaration. If the constructor is not explicitly declared, a constructor will be synthesized
by the compiler. If the record is sealed, the constructor will be private, otherwise it will be protected.
An explicitly declared copy constructor must be either public or protected, unless the
record is sealed.
The first thing the constructor must do, is to call a copy constructor of the base, or a parameter-less
object constructor if the record inherits from object. An error is reported if a user-defined copy
constructor uses an implicit or explicit constructor initializer that doesn't fulfill this requirement.
After a base copy constructor is invoked, a synthesized copy constructor copies values for all instance
fields implicitly or explicitly declared within the record type. 
The sole presence of a copy constructor, whether explicit or implicit, doesn't prevent an automatic
addition of a default instance constructor.

If a virtual "clone" method is present in the base record, the synthesized "clone" method overrides it and
the return type of the method is the current containing type if the "covariant returns" feature is supported
and the override return type otherwise. An error is produced if the base record clone method is sealed.
If a virtual "clone" method is not present in the base record, the return type of the clone method
is the containing type and the method is virtual, unless the record is sealed or abstract.
If the containing record is abstract, the synthesized clone method is also abstract.
If the "clone" method is not abstract, it returns the result of a call to a copy constructor. 


### Printing members: PrintMembers and ToString methods

If the record is derived from `object`, the record includes a synthesized method equivalent to a method declared as follows:
```C#
bool PrintMembers(System.Text.StringBuilder builder);
```
The method is `private` if the record type is `sealed`. Otherwise, the method is `virtual` and `protected`.

The method:
1. for each of the record's printable members (non-static public field and readable property members), appends that member's name followed by " = " followed by the member's value separated with ", ",
2. return true if the record has printable members.

For a member that has a value type, we will convert its value to a string representation using the most efficient method available to the target platform. At present that means calling `ToString` before passing to `StringBuilder.Append`.

If the record type is derived from a base record `Base`, the record includes a synthesized override equivalent to a method declared as follows:
```C#
protected override bool PrintMembers(StringBuilder builder);
```

If the record has no printable members, the method calls the base `PrintMembers` method with one argument (its `builder` parameter) and returns the result.

Otherwise, the method:
1. calls the base `PrintMembers` method with one argument (its `builder` parameter),
2. if the `PrintMembers` method returned true, append ", " to the builder,
3. for each of the record's printable members, appends that member's name followed by " = " followed by the member's value: `this.member` (or `this.member.ToString()` for value types), separated with ", ",
4. return true.

The `PrintMembers` method can be declared explicitly.
It is an error if the explicit declaration does not match the expected signature or accessibility, or if the explicit declaration doesn't allow overriding it in a derived type and the record type is not `sealed`.

The record includes a synthesized method equivalent to a method declared as follows:
```C#
public override string ToString();
```

The method can be declared explicitly. It is an error if the explicit declaration does not match the expected signature or accessibility, or if the explicit declaration doesn't allow overriding it in a derived type and the record type is not `sealed`. It is an error if either synthesized, or explicitly declared method doesn't override `object.ToString()` (for example, due to shadowing in intermediate base types, etc.).

The synthesized method:
1. creates a `StringBuilder` instance,
2. appends the record name to the builder, followed by " { ",
3. invokes the record's `PrintMembers` method giving it the builder, followed by " " if it returned true,
4. appends "}",
3. returns the builder's contents with `builder.ToString()`.

For example, consider the following record types:

``` csharp
record R1(T1 P1);
record R2(T1 P1, T2 P2, T3 P3) : R1(P1);
```

For those record types, the synthesized printing members would be something like:

```C#
class R1 : IEquatable<R1>
{
    public T1 P1 { get; init; }
    
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append(nameof(P1));
        builder.Append(" = ");
        builder.Append(this.P1); // or builder.Append(this.P1.ToString()); if P1 has a value type
        
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

class R2 : R1, IEquatable<R2>
{
    public T2 P2 { get; init; }
    public T3 P3 { get; init; }
    
    protected override bool PrintMembers(StringBuilder builder)
    {
        if (base.PrintMembers(builder))
            builder.Append(", ");
            
        builder.Append(nameof(P2));
        builder.Append(" = ");
        builder.Append(this.P2); // or builder.Append(this.P2); if P2 has a value type
        
        builder.Append(", ");
        
        builder.Append(nameof(P3));
        builder.Append(" = ");
        builder.Append(this.P3); // or builder.Append(this.P3); if P3 has a value type
        
        return true;
    }
    
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(R2));
        builder.Append(" { ");

        if (PrintMembers(builder))
            builder.Append(" ");

        builder.Append("}");
        return builder.ToString();
    }
}
```

## Positional record members

In addition to the above members, records with a parameter list ("positional records") synthesize
additional members with the same conditions as the members above.

### Primary Constructor

A record type has a public constructor whose signature corresponds to the value parameters of the
type declaration. This is called the primary constructor for the type, and causes the implicitly
declared default class constructor, if present, to be suppressed. It is an error to have a primary
constructor and a constructor with the same signature already present in the class.

At runtime the primary constructor

1. executes the instance initializers appearing in the class-body

1. invokes the base class constructor with the arguments provided in the `record_base` clause, if present

If a record has a primary constructor, any user-defined constructor, except "copy constructor" must have an
explicit `this` constructor initializer. 

Parameters of the primary constructor as well as members of the record are in scope within the `argument_list`
of the `record_base` clause and within initializers of instance fields or properties. Instance members would
be an error in these locations (similar to how instance members are in scope in regular constructor initializers
today, but an error to use), but the parameters of the primary constructor would be in scope and useable and
would shadow members. Static members would also be useable, similar to how base calls and initializers work in
ordinary constructors today.

A warning is produced if a parameter of the primary constructor is not read.

Expression variables declared in the `argument_list` are in scope within the `argument_list`. The same shadowing
rules as within an argument list of a regular constructor initializer apply.

### Properties

For each record parameter of a record type declaration there is a corresponding public property
member whose name and type are taken from the value parameter declaration.

For a record:

* A public `get` and `init` auto-property is created (see separate `init` accessor specification).
  An inherited `abstract` property with matching type is overridden.
  It is an error if the inherited property does not have `public` overridable `get` and `init` accessors.
  It is an error if the inherited property is hidden.  
  The auto-property is initialized to the value of the corresponding primary constructor parameter.
  Attributes can be applied to the synthesized auto-property and its backing field by using `property:` or `field:`
  targets for attributes syntactically applied to the corresponding record parameter.  

### Deconstruct

A positional record with at least one parameter synthesizes a public void-returning instance method called Deconstruct with an out
parameter declaration for each parameter of the primary constructor declaration. Each parameter
of the Deconstruct method has the same type as the corresponding parameter of the primary
constructor declaration. The body of the method assigns each parameter of the Deconstruct method
to the value from an instance member access to a member of the same name.
The method can be declared explicitly. It is an error if the explicit declaration does not match
the expected signature or accessibility, or is static.

## `with` expression

A `with` expression is a new expression using the following syntax.

```antlr
with_expression
    : switch_expression
    | switch_expression 'with' '{' member_initializer_list? '}'
    ;

member_initializer_list
    : member_initializer (',' member_initializer)*
    ;

member_initializer
    : identifier '=' expression
    ;
```
A `with` expression is not permitted as a statement.

A `with` expression allows for "non-destructive mutation", designed to
produce a copy of the receiver expression with modifications in assignments
in the `member_initializer_list`.

A valid `with` expression has a receiver with a non-void type. The receiver type must be a record.

On the right hand side of the `with` expression is a `member_initializer_list` with a sequence
of assignments to *identifier*, which must be an accessible instance field or property of the receiver's
type.

First, receiver's "clone" method (specified above) is invoked and its result is converted to the
receiver's type. Then, each `member_initializer` is processed the same way as an assignment to
a field or property access of the result of the conversion. Assignments are processed in lexical order.
