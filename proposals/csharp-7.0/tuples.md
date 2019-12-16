# Tuples 

This proposal specifies the changes required to the [C# 6.0 (draft) Language specification](../../spec/introduction.md) to support *Tuples* as a new [value type](../../spec/types.md#value-types).

## Changes to [Lexical structure](../../spec/lexical-structure.md)

> The grammar for [Literals](../../spec/lexical-structure.md#literals) is extended to include `tuple_literal`:

```antlr
literal
    : boolean_literal
    | integer_literal
    | real_literal
    | character_literal
    | string_literal
    | null_literal
    | tuple_literal // new
    ;

tuple_literal
    : '(' tuple_literal_element_list ')'
    ;

tuple_literal_element_list
    : tuple_literal_element ',' tuple_literal_element
    | tuple_literal_element_list ',' tuple_literal_element
    ;

tuple_literal_element
    : ( identifier ':' )? expression
    ;
```

Note that because it is not a constant expression, a tuple literal cannot be used as default value for an optional parameter.

## Changes to [Types](../../spec/types.md)

> The first paragraph of [Value types](../../spec/types.md#value-types) is replaced with the following text:

A value type is either a `struct` type, an enumeration type, or a tuple type. C# provides a set of predefined struct types called the ***simple types***. The simple types are identified through reserved words.

> The `value_type` grammar is updated to include `tuple_type`:

```antlr
value_type
    : struct_type
    | enum_type
    | tuple_type // new
    ;

tuple_type
    : '(' tuple_type_element_list ')'
    ;
    
tuple_type_element_list
    : tuple_type_element ',' tuple_type_element
    | tuple_type_element_list ',' tuple_type_element
    ;
    
tuple_type_element
    : type identifier?
    ;
```

## Additions to [Types](../../spec/types.md)

> The following sections should be added after [Nullable types](../../spec/types.md#nullable-types) (at the end of the current *Value types* section.)

### Tuple types

Tuple types are declared with the following syntax:

```csharp
public (int sum, int count) Tally(IEnumerable<int> values) { ... }

var t = Tally(myValues);
Console.WriteLine($"Sum: {t.sum}, count: {t.count}");
 ```

The syntax `(int sum, int count)` indicates an anonymous data structure with public fields of the given names and types, referred to as *tuple*.

Tuple values can be created using *tuple literals*:

```csharp
var t1 = (sum: 0, count: 1);
var t2 = (0, 1);     // field names are optional
```

Tuples cannot be created with the `new` operator. The `new` operator can be used to initialize arrays of tuples, or nullable tuples:

```csharp
var array = new (int x, int y)[10];
var nullable = new (int x, int y)?();
```

Tuples can be created for a known target type:

```csharp
public (int sum, int count) Tally(IEnumerable<int> values)
{
    var s = 0; var c = 0;
    foreach (var value in values) { s += value; c++; }
    return (s, c); // target typed to (int sum, int count)
}
```

Specifying field names is optional. Duplicate names are disallowed.

```csharp
var t1 = (sum: 0, count: 1); // OK, all fields are named
var t2 = (sum: 0, 1);       // Ok, implies (int sum, int);
var t3 = (sum: 0, sum: 1);  // error! duplicate names.
```

### Tuple literals

```csharp
var t1 = (0, 2);              // infer tuple type from values
var t2 = (sum: 0, count: 1);  // infer tuple type from names and values
```

A tuple literal is "target typed" whenever possible. The tuple literal has a "conversion from expression" to any tuple type, as long as the element expressions of the tuple literal have an implicit conversion to the element types of the tuple type.

```csharp
(string name, byte age) t = (null, 5); // Ok: the expressions null and 5 convert to string and byte
```

In cases where the tuple literal is not part of a conversion, it acquires its "natural type", which means a tuple type where the element types are the types of the constituent expressions. Since not all expressions have types, not all tuple literals have a natural type either:

```csharp
var t = ("John", 5); // Ok: the type of t is (string, int)
var t = (null, 5); //   Error: null doesn't have a type
```

A tuple literal may include names, in which case they become part of the natural type:

```csharp
var t = (name: "John", age: 5); // The type of t is (string name, int age)
```

Tuple literals may be deconstructed directly:

```csharp
(string x, byte y, var z) = (null, 1, 2);
(string x, byte y) t = (null, 1);
```

Or for deconstructing assignment:

```csharp
string x;
byte y;

(x, y) = (null, 1);
(x, y) = (y, x); // swap!
```

The evaluation order of deconstruction assignment expressions is "breadth first":

1. Evaluate the LHS: Evaluate each of the expressions inside of it one by one, left to right, to yield side effects and establish a storage location for each.
1. Evaluate the RHS: Evaluate each of the expressions inside of it one by one, left to right to yield side effects
1. Convert each of the RHS expressions to the LHS types expected, one by one, left to right.
1. Assign each of the conversion results from 3 to the storage locations found in 

> **Note to reviewers**: I found this in the LDM notes for July 13-16, 2016. I don't think it is still accurate:

A deconstructing assignment is a *statement-expression* whose type could be `void`.

### Duality with underlying type

Tuples map to underlying types of particular names.

```csharp
System.ValueTuple<T1, T2>
System.ValueTuple<T1, T2, T3>
...
System.ValueTuple<T1, T2, T3,..., T7, TRest>
```

Tuple types behave exactly like underlying types. The only additional enhancement is the more expressive field names given by the programmer.

```csharp
var t = (sum: 0, count: 1);
t.sum   = 1;        // sum   is the name for the field #1
t.Item1 = 1;        // Item1 is the name of underlying field #1 and is also available

var t1 = (0, 1); // tuple omits the field names.
t.Item1 = 1;     // underlying field name is still available

t.ToString();     // ToString on the underlying tuple type is called.

System.ValueTuple<int, int> vt = t; // identity conversion
(int moo, int boo) t2 = vt;         // identity conversion

```

Because of the dual nature of tuples, it is not allowed to assign field names that overlap with preexisting member names of the underlying type. The only exception is the use of predefined `Item1`, `Item2`,...`ItemN` at corresponding position N, since that would not be ambiguous.

```csharp
var t =  (ToString: 0, ObjectEquals: 1);  // error: names match underlying member names
var t1 = (Item1: 0, Item2: 1);            // valid
var t2 = (misc: 0, Item1: 1);             // error: "Item1" was used in a wrong position
```

If the tuple is bigger than the limit of 7, the implementation will nest the "tail" as a tuple into the eighth element recursively. This nesting is visible by accessing the `Rest` field of a tuple, but that field my be hidden from e.g. auto-completion, just as the `ItemX` field names may be hidden but allowed when a tuple has named elements.

A well formed "big tuple" will have names `Item1` etc. all the way up to the number of tuple elements, even though the underlying type doesn't physically have those fields directly defined. The same goes for the tuple returned from the `Rest` field, only with the numbers "shifted" appropriately.

For tuple element names occurring in partial type declarations, the names must be the same.

```csharp
partial class C : IEnumerable<(string name, int age)> { ... }
partial class C : IEnumerable<(string fullname, int)> { ... } // error: names must be specified and the same
```

When tuple elements names are used in overridden signatures, or implementations of interface methods, tuple element names in parameter and return types must be preserved. It is an error for the same generic interface to be inherited or implemented twice with identity convertible type arguments that have conflicting tuple element names

> note:
>
> For the purpose of overloading, overriding and hiding, tuples of the same types and lengths as well as their underlying ValueTuple types are considered equivalent. All other differences are immaterial. When overriding a member it is permitted to use tuple types with same or different field names than in the base member.
>
> A situation where same field names are used for non-matching fields between base and derived member signatures, a warning is reported by the compiler.  

```csharp
class Base
{
    virtual void M1(ValueTuple<int, int> arg){...}
}
class Derived : Base
{
    override void M1((int c, int d) arg){...} // valid override, signatures are equivalent
}
class Derived2 : Derived 
{
    override void M1((int c1, int c) arg){...} // also valid, warning on possible misuse of name 'c' 
}

class InvalidOverloading 
{
    virtual void M1((int c, int d) arg){...}
    virtual void M1((int x, int y) arg){...}        // invalid overload, signatures are eqivalent
    virtual void M1(ValueTuple<int, int> arg){...}  // also invalid
}
```

> endnote.

### Tuple field name erasure at runtime

Tuple field names aren't part of the runtime representation of tuples, but are tracked only by the compiler. As a result, the field names will not be available to a 3rd party observer of a tuple instance - such as reflection or dynamic code.

In alignment with the identity conversions, a boxed tuple does not retain the names of the fields and will unbox to any tuple type that has the same element types in the same order.

```csharp
object o = (a: 1, b: 2);           // boxing conversion
var t = ((int moo, int boo))o;     // unboxing conversion
```

## Additions to [Variables](../../spec/variables.md)

> The following text should be added at the end of the [Variables](../../spec/variables.md) section.

### "Discards"

The `_` can be used as a *discard* under these rules:

- A standalone `_` is a discard when no `_` is defined in scope.
- A "designator" `var _` or `T _` in deconstruction, pattern matching and out vars is a discard.
- Discards are like unassigned variables, and do not have a value. They can only occur in contexts where they are assigned to.

Examples:

```csharp
M(out _, out var _, out int _); // three out variable discards
(_, var _, int _) = GetCoordinates(); // deconstruction into discards
if (x is var _ && y is int _) { ... } // discards in patterns
```

## Changes to [Conversions](../spec/conversions.md)

> The following text should be added to [Identity conversions](../../spec/conversions.md#identity-conversions), after the bullet point on `object` and `dynamic`:

* Element names are immaterial to tuple conversions. Tuples with the same types in the same order are identity convertible to each other or to and from corresponding underlying `ValueTuple` types, regardless of the names.

```csharp
var t = (sum: 0, count: 1);

System.ValueTuple<int, int> vt = t;  // identity conversion
(int moo, int boo) t2 = vt;          // identity conversion

t2.moo = 1;
```

> note:
>
> An element name at one position on one side of a conversion, and the same name at another position on the other side almost certainly have bug in the code:

```csharp
(string first, string last) GetNames() { ... }
(string last, string first) names = GetNames(); // Oops!
```

> Compilers should issue a warning for the preceding code.
>
> endnote.

### Boxing conversions

> The following text should be added to [Boxing conversions](../../spec/conversions.md#boxing-conversions) after the first paragraph:

Tuples, like all value types, have a boxing conversion. Importantly, the names aren't part of the runtime representation of tuples, but are tracked only by the compiler. Thus, once you've "cast away" the names, you cannot recover them. In alignment with the identity conversions, a boxed tuple will unbox to any tuple type that has the same element types in the same order.

### Tuple conversions

> This section should be added after [Implicit enumeration conversions](../../spec/conversions.md#implicit-enumeration-conversions)

Tuple types and expressions support a variety of conversions by "lifting" conversions of the elements into overall *tuple conversion*.
For the classification purpose, all element conversions are considered recursively. For example: To have an implicit conversion, all element expressions/types must have implicit conversions to the corresponding element types.

Tuple conversions are *Standard Conversions* and therefore can stack with user-defined operators to form user-defined conversions.

An implicit tuple conversion is a standard conversion. It applies between two tuple types of equal arity when there is any implicit conversion between each corresponding pair of types.

An explicit tuple conversion is a standard conversion. It applies between two tuple types of equal arity when there is any explicit conversion between each corresponding pair of types.

A tuple conversion can be classified as a valid instance conversion or an extension method invocation as long as all element conversions are applicable as instance conversions.

On top of the member-wise conversions implied by target typing, implicit conversions between tuple types themselves are allowed.

### Target typing

> This section should be added after [Anonymous function conversions and method group conversions](../../spec.md#Anonymous-function-conversions-and-method-group-conversions)

A tuple literal is "target typed" when used in a context specifying a tuple type. The tuple literal has a "conversion from expression" to any tuple type, as long as the element expressions of the tuple literal have an implicit conversion to the element types of the tuple type.

```csharp
(string name, byte age) t = (null, 5); // Ok: the expressions null and 5 convert to string and byte
```

A successful conversion from tuple expression to tuple type is classified as an *ImplicitTuple* conversion, unless the tuple's natural type matches the target type exactly, in such case it is an *Identity* conversion.

```csharp
void M1((int x, int y) arg){...};
void M1((object x, object y) arg){...};

M1((1, 2));            // first overload is used. Identity conversion is better than implicit conversion.
M1(("hi", "hello"));   // second overload is used. Implicit tuple conversion is better than no conversion.
```

Target typing will "see through" nullable target types. A successful conversion from tuple expression to a nullable tuple type is classified as *ImplicitNullable* conversion.

```csharp
((int x, int y, int z)?, int t)? SpaceTime()
{
    return ((1,2,3), 7);  // valid, implicit nullable conversion
}
```

## Additions to [Expressions](../../spec/expressions.md)

### Overload resolution and tuples with no natural types

> The following text is added after after the bullet list in [Exactly matching expressions](../../spec/expressions.md#Exactly-matching-expressions):

The exact match rule for tuple expressions is based on the natural types of the constituent tuple arguments. The rule is mutually recursive with respect to other containing or contained expressions not in a possession of a natural type.

### Deconstruction expressions

> This section should be added at the end of the [Expressions](../../spec/expressions.md) chapter.

Tuple deconstruction expressions receive and "split out" a tuple:

```csharp
(var sum, var count) = Tally(myValues); // deconstruct result
Console.WriteLine($"Sum: {sum}, count: {count}");  
```

The `_` wildcard indicates that the one or more of the tuple fields are discarded:

```csharp
(var sum, _) = Tally(myValues); // deconstruct result
Console.WriteLine($"Sum: {sum}, count was ignored");  
```

Any object may be deconstructed by providing an accessible `Deconstruct` method, either as a member or as an extension method. A `Deconstruct` method converts an object to a set of discrete values. The Deconstruct method "returns" the component values by use of individual `out` parameters. Deconstruct is overloadable.

The deconstructor pattern could be implemented as a member method, or an extension method:

```csharp
class Name
{
    public void Deconstruct(out string first, out string last) { first = First; last = Last; }
    ...
}
// or
static class Extensions
{
    public static void Deconstruct(this Name name, out string first, out string last) { first = name.First; last = name.Last; }
}
```

Overload resolution for `Deconstruct` methods considers only the arity of the `Deconstruct` method. If multiple `Deconstruct` methods of the same arity are accessible, the expression is ambiguous and a binding time error occurs.

If necessary to satisfy implicit conversions of the tuple member types, the compiler passes temporary variables to the `Deconstruct` method, instead of the ones declared in the deconstruction. For example, if `p` has

```csharp
void Deconstruct(out byte x, out byte y) ...;
```

The compiler translates

```csharp
(int x, int y) = p;
```

equivalently to:

```csharp
p.Deconstruct(out byte __x, out byte __y);
(int x, int y) = (__x, __y);
```

### Additions to [Classes](../../spec/classes.md)

> The following note should be added to the end of the section on [extension methods](../../spec/classes.md#extension-methods):

> note: 
>Extension methods on a tuple type apply to tuples with different element names:

```csharp
static void M(this (int x, int y) t) { ... }

(int a, int b) t = ...;
t.M(); // Sure
```

> endnote.