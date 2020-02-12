# Tuples

This proposal specifies the changes required to the [C# 6.0 (draft) Language specification](../../spec/introduction.md) to support *Tuples* as a new [value type](../../spec/types.md#value-types).

## Changes to [Lexical structure](../../spec/lexical-structure.md)

### Literals

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
```

> Add the following section after [The null literal](../../spec/lexical-structure.md#The-null-literal):

#### Tuple literals

A tuple literal consists of two or more tuple literal elements, each of which is optionally named.

```antlr
tuple_literal
    : '(' ( tuple_literal_element ',' )+ tuple_literal_element ')'
    ;

tuple_literal_element
    : ( identifier ':' )? expression
    ;
```

A tuple literal is implicitly typed; that is, its type is determined by the context in which it is used, referred to as the *target*. Each element *expression* in a tuple literal shall have a value that can be converted implicitly to its corresponding target element type.

\[Example:
```csharp
var t1 = (0, 2);              // infer tuple type (int, int) from values
var t2 = (sum: 0, count: 1);  // infer tuple type (int sum, int count) from names and values
(int, double) t3 = (0, 2);    // infer tuple type (int, double) from values; can implicitly convert int to double
(int, double) t4 = (0.0, 2);  // Error: can't implicitly convert double to int
```
end example\]

A tuple literal has a "conversion from expression" to any tuple type of the same arity, as long as each of the element expressions of the tuple literal has an implicit conversion to the type of the corresponding element of the tuple type.

\[Example:
```csharp
(string name, byte age) t = (null, 5); // OK: the expressions null and 5 convert to string and byte, respectively
```
end example\]

In cases where a tuple literal is not part of a conversion, the literal's type is its [natural type](XXX), if one exists.

\[Example:
```csharp
var t = ("John", 5); // OK: the natural type is (string, int)
var t = (null, 5);   // Error: null doesn't have a type
var t = (name: "John", age: 5); // OK: The natural type is (string name, int age)
```
end example\]

A tuple literal is *not* a [constant expression](../../spec/expressions.md#Constant-expressions). 

For a discussion of tuple literals as tuple initializers, see [Tuple types](XXX).

## Additions to [Types](../../spec/types.md)

> Add the following sections after [Nullable types](../../spec/types.md#nullable-types) (at the end of the current *Value types* section.)

### Tuple types

#### General

A tuple is declared using the following syntax:

```antlr
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

A ***tuple*** is an anonymous data structure type that contains an ordered sequence of two or more ***elements***, which are optionally named. Each element is public. If a tuple is mutable, its element values are also mutable?

A tuple's ***natural type*** is the combination of its element types, in lexical order, and element names, if they exist.

A tuple's ***arity*** is the combination of its element types, in lexical order; element names are *not* included.
 Each unique tuple arity designates a distinct tuple type.

Two tuple values are equal if they have the same arity, and the values of the elements in each corresponding element pair are equal.

An element in a tuple is accessed using the [member-access operator `.`](../../spec/expressions.md#Member-access).

Given the following,

```csharp
(int code, string message) pair1 = (3, "hello");
System.Console.WriteLine("first = {0}, second = {1}", pair1.code, pair1.message);
```

the syntax `(int code, string message)` declares a tuple type having two elements, each with the given type and name.

As shown, a tuple can be initialized using a [tuple literal](XXX).

An element need not have a name. An element without a name is unnamed.

If a tuple declarator contains the type of all the tuple's elements, that set of types cannot be changed or augmented based on the context in which it is used; otherwise, element type information shall be inferred from the usage context. Likewise for element names.

A tuple's type can be declared explicitly. Consider the following declarations:

```csharp
(int, string) pair2 = (2, "Goodbye"); 
(int code, string message) pair3 = (2, "Goodbye");
(int code, string) pair4 = (2, "Goodbye"); 
(int, string message) pair5 = (2, "Goodbye"); 
(int code, string) pair6 = (2, message: "Goodbye");	// Warning: can't give a name to the second element
(int code, string) pair7 = (newName: 2, "Goodbye");	// Warning: can't change the name of element code
```

The type of `pair2` is `(int, string)` with unknown element names. Similarly, the type of `pair3` is `(int, string)` but this time with the element names `code` and `message`, respectively. For `pair4` and `pair5`, one element is named, the other not.

In the case of `pair6`, the second element is declared as being unanmed, and any attempt to provide a name in an initializing context shall be ignored. Likewise for any attempt to change an element's name, as in the case of `pair7`.

A tuple's type can be wholely inferred from the context in which it is used. Consider the following declarations:

```csharp
var pair10 = (1, "Hello"); 
var pair11 = (code: 1, message: "Hello");
var pair12 = (code: 1, "Hello");
var pair13 = (1, message: "Hello");
```

The type of `pair10` is inferred from the initializer's tuple literal, as `(int, string)` with unknown element names. Similarly, the type of `pair11` is inferred from the initialer's tuple literal, as `(int, string)` but this time with the element names `code` and `message`, respectively. For `pair12` and `pair13`, the element types are inferred, and one element is named, the other not.

Element names within a tuple type shall be distinct.

\[Example:
```csharp
(int e1, float e1) t = (10, 1.2);					// Error: both elements have the same name
(int e1, (int e1, int e2) e2) t = (10, (20, 30));	// OK: element names in each tuple type are distinct
```
end example\]

The name of any element in a partial type declaration shall be the same for an element in the same position in any other partial declaration for that type.

\[Example:
```csharp
partial class C : IEnumerable<(string name, int age)> { ... }
partial class C : IEnumerable<(string fullname, int)> { ... } // Error: names must be specified and the same
```
end example\]

A tuple cannot be created with the `new` operator. However, the `new` operator can be used to create and initialize an array of tuple or a nullable tuple.

#### A tuple's underlying type

Each tuple type maps to an underlying type. Specifically, a tuple having two elements maps to `System.ValueTuple<T1, T2>`, one with three elements maps to `System.ValueTuple<T1, T2, T3>`, and so on, up to seven elements. Tuple types having eight or more elements map to `System.ValueTuple<T1, T2, T3,..., T7, TRest>`. The first element in an underlying type has the public name `Item1`, the second `Item2`, and so on through `Item7`. Any elements beyond seven can be accesed as a group by the public name `Rest`, whose type is "tuple of the remaining elements". Alternatively, those elements can be accessed individually using the names `Item8` through `Item`*N*, where *N* is the total number of elements, even though the underlying type has no such names defined.

A tuple type shall behave exactly like its underlying type. The only additional enhancement in the tuple type case is the ability to provide a more expressive name for each element.

\[Example:
```csharp
var t1 = (sum: 0, 1);
t1.sum   = 1;		// access the first element by its declared name
t1.Item1 = 1;		// access the first element by its underlying name
t1.Item2 = 3;		// access the second element by its underlying name

System.ValueTuple<int, int> vt = t1;	// identity conversion

 var t2 = (1, 2, 3, 4, 5, 6, 7, 8, 9);	// t2 is a System.ValueTuple<T1, T2, T3,..., T7, TRest>
 var t3 = t4.Rest;						// t3 is a (int, int); that is, a System.ValueTuple<T1, T2>
 System.Console.WriteLine("Item9 = {0}", t1.Item9);	// outputs 9 even though no such name Item9 exists!
```
end example\]

\[Example:
```csharp
var t =  (ToString: 0, GetHashCode: 1);	// Error: names match underlying member names
var t1 = (Item1: 0, Item2: 1);			// OK
var t2 = (misc: 0, Item1: 1);			// Error: "Item1" used in a wrong position
```
end example\]

#### Element names and overloading, overriding, and hiding

When tuple element names are used in overridden signatures or implementations of interface methods, tuple element names in parameter and return types shall be preserved. It is an error for the same generic interface to be inherited or implemented twice with identity-convertible type arguments that have conflicting tuple element names.

For the purpose of overloading, overriding and hiding, tuples of the same arity, as well as their underlying ValueTuple types, shall be considered equivalent. All other differences are immaterial. When overriding a member it shall be permitted to use tuple types with the same element names or element names different than in the base member.

If the same element name is used for non-matching elements in base and derived member signatures, the implementation shall issue a warning.  

```csharp
public class Base
{
    public virtual void M1(ValueTuple<int, int> arg){...}
}
public class Derived : Base
{
    public override void M1((int c, int d) arg){...}	// valid override, signatures are equivalent
}
public class Derived2 : Derived 
{
    public override void M1((int c1, int c) arg){...}	// also valid, warning on possible misuse of name 'c' 
}

public class InvalidOverloading 
{
    public virtual void M1((int c, int d) arg){...}
    public virtual void M1((int x, int y) arg){...}		// invalid overload, signatures are eqivalent
    public virtual void M1(ValueTuple<int, int> arg){...}	// also invalid
}
```

### Tuple element name erasure at runtime

A tuple element name is not part of the runtime representation of a tuple of that type; an element's name is tracked only by the compiler. [*Note*: As a result, element names are not available to a third-party observer of a tuple instance (such as with reflection or dynamic code). *end note*]

In alignment with the identity conversions, a boxed tuple shall not retain the names of the elements, and shall unbox to any tuple type that has the same element types in the same order.

\[Example:
```csharp
object o = (a: 1, b: 2);           // boxing conversion
var t = ((int moo, int boo))o;     // unboxing conversion
```
end example\]

## Additions to [Variables](../../spec/variables.md)

> Add the following text at the end of the [Variables](../../spec/variables.md) section.

## Discards

The identifier `_` can be used as a *discard* in the following circumstances:

- When no identifier `_` is defined in the current scope.
- A "designator" `var _` or `T _` in [deconstruction](XXX), [pattern matching](XXX) and [out vars](XXX).

Like unassigned variables, discards do not have a value. A discard may only occur in contexts where it is assigned to.

\[Example:
```csharp
M(out _, out var _, out int _);			// three out variable discards
(_, var _, int _) = GetCoordinates();	// deconstruction into discards
if (x is var _ && y is int _) { ... }	// discards in patterns
```
end example\]

## Changes to [Conversions](../spec/conversions.md)

### Identity conversion

> Add the following text to [Identity conversion](../../spec/conversions.md#identity-conversion), after the bullet point on `object` and `dynamic`:

* Element names are immaterial to tuple conversions. Tuples with the same arity are identity-convertible to each other or to and from corresponding underlying `ValueTuple` types, regardless of their element names.

\[Example:
```csharp
var t = (sum: 0, count: 1);

System.ValueTuple<int, int> vt = t;  // identity conversion
(int moo, int boo) t2 = vt;          // identity conversion

t2.moo = 1;
```
end example\]

In the case in which an element name at one position on one side of a conversion, and the same name at a different position on the other side, the compiler shall issue a warning.

\[Example:
```csharp
(string first, string last) GetNames() { ... }
(string last, string first) names = GetNames(); // Oops!
```
end example\]

### Boxing conversions

> Add the following text to [Boxing conversions](../../spec/conversions.md#boxing-conversions) after the first paragraph:

Tuples have a boxing conversion. Importantly, the element names aren't part of the runtime representation of tuples, but are tracked only by the compiler. Thus, once element names have been "cast away", they cannot be recovered. In alignment with identity conversion, a boxed tuple unboxes to any tuple type that has the same arity.

### Tuple conversions

> Add this section after [Implicit enumeration conversions](../../spec/conversions.md#implicit-enumeration-conversions)

Tuple types and expressions support a variety of conversions by "lifting" conversions of the elements into overall *tuple conversion*. For the classification purpose, all element conversions are considered recursively. For example, to have an implicit conversion, all element expressions/types shall have implicit conversions to the corresponding element types.

Tuple conversions are *Standard Conversions*.

An implicit tuple conversion is a standard conversion. It applies from one tuple type to another of equal arity when  here is any implicit conversion from each element in the source tuple to the corresponding element in the destination tuple.

An explicit tuple conversion is a standard conversion. It applies between two tuple types of equal arity when there is any explicit conversion between each corresponding pair of element types.

A tuple conversion can be classified as a valid instance conversion or an extension method invocation as long as all element conversions are applicable as instance conversions.

On top of the member-wise conversions implied by implicit typing, implicit conversions between tuple types themselves are allowed.

### Tuple Literal Conversion

> Add this section after [Anonymous function conversions and method group conversions](../../spec.md#Anonymous-function-conversions-and-method-group-conversions)

A tuple literal is implicitly typed when used in a context specifying a tuple type. The tuple literal has a "conversion from expression" to any tuple type of the same arity, as long as the element expressions of the tuple literal have an implicit conversion to the corresponding element types of the tuple type.

\[Example:
```csharp
(string name, byte age) t = (null, 5); // Ok: the expressions null and 5 convert to string and byte
```
end example\]

A successful conversion from tuple expression to tuple type is classified as an *ImplicitTuple* conversion, unless the tuple's [natural type](XXX) matches the target type exactly, in such case it is an *Identity* conversion.

```csharp
void M1((int x, int y) arg){...};
void M1((object x, object y) arg){...};

M1((1, 2));            // first overload is used. Identity conversion is better than implicit conversion.
M1(("hi", "hello"));   // second overload is used. Implicit tuple conversion is better than no conversion.
```

A successful conversion from tuple expression to a nullable tuple type is classified as *ImplicitNullable* conversion.

```csharp
((int x, int y, int z)?, int t)? SpaceTime()
{
    return ((1,2,3), 7);  // valid, implicit nullable conversion
}
```

## Additions to [Expressions](../../spec/expressions.md)

### Overload resolution and tuples with no natural types

> Add the following text after the bullet list in [Exactly matching expressions](../../spec/expressions.md#Exactly-matching-expressions):

The exact-match rule for tuple expressions is based on the [natural types](XXX) of the constituent tuple elements. The rule is mutually recursive with respect to other containing or contained expressions not in a possession of a natural type.

### Deconstruction expressions

> Add this section at the end of the [Expressions](../../spec/expressions.md) chapter.

A tuple-deconstruction expression copies from a source tuple zero or more of its element values to corresponding destinations.

```antlr
tuple_deconstruction_expression
    : '(' destination_list ')'
    ;
    
destination_list
    : destination ',' destination
    | destination_list ',' destination
    ;
    
destination
    : type identifier
    ;
```

Element values are copied from the source tuple to the destination(s). Each element's position is inferred from the destination position within *destination_list*. A destination with identifier `_` indicates that the corresponding element is discarded rather than being copied. The destination list shall account for every element in the tuple.

\[Example:
```csharp
int code;
string message;

(code, message) = (10, "hello");				// copy both element values to existing variables
(code, _) = (11, "Go!");						// copy element 1 to code and discard element 2
(_, _) = (12, "Stop!");							// discard both element values
(int code2, string message2) = (20, "left");	// copy both element values to newly created variables
(code, string message3) = (21, "right");		// Error: can't mix existing and new variables
(code, _) = (30, 2.5, (10, 20));			    // Error: can't deconstruct tuple of 3 elements into 2 values
(code, _, _) = (30, 2.5, (10, 20));				// OK: deconstructing 3 elements into 3 values
```
end example\]

Any object may be deconstructed by providing an accessible `Deconstruct` method, either as an instance member or as an extension method. A `Deconstruct` method converts an object to a set of discrete values. The Deconstruct method "returns" the component values by use of individual `out` parameters. `Deconstruct` is overloadable. Consider the following:

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

Overload resolution for `Deconstruct` methods considers only the arity of the `Deconstruct` method. If multiple `Deconstruct` methods of the same arity are accessible, the expression is ambiguous and a binding-time error shall occur.

If necessary to satisfy implicit conversions of the tuple member types, the compiler passes temporary variables to the `Deconstruct` method, instead of the ones declared in the deconstruction. For example, if object `p` has the following method:

```csharp
void Deconstruct(out byte x, out byte y) ...;
```

the compiler translates

```csharp
(int x, int y) = p;
```

to:

```csharp
p.Deconstruct(out byte __x, out byte __y);
(int x, int y) = (__x, __y);
```

The evaluation order of deconstruction assignment expressions is "breadth first":

1. Evaluate the LHS: Evaluate each of the expressions inside of it one by one, left to right, to yield side effects and establish a storage location for each.
1. Evaluate the RHS: Evaluate each of the expressions inside of it one by one, left to right to yield side effects
1. Convert each of the RHS expressions to the LHS types expected, one by one, left to right.
1. Assign each of the conversion results from Step 3 to the storage locations found in (???)

\[Example:
```csharp
string x;
byte y;

(x, y) = (y, x); // swap!
```
end example\]

A deconstructing assignment is a *statement-expression* whose type could be `void`.

### Additions to [Classes](../../spec/classes.md)

### Extension methods

> Add the following note to the end of the section on [extension methods](../../spec/classes.md#extension-methods):

[*Note*: Extension methods on a tuple type apply to tuples with different element names:

```csharp
static void M(this (int x, int y) t) { ... }

(int a, int b) t = ...;
t.M();  // OK
```

The extension method `M` is a candidate method, even though the tuple `t` has different element names (`a` and `b`) than the formal parameter of `M` (`x` and `y`).
*endnote*].

### Additions to [Annex C: Standard Library](XXX)

> The published standard contains an Annex C, which states, "A conforming C# implementation shall provide a minimum set of types having specific semantics. These types and their members are listed here, in alphabetical order by namespace and type. For a formal definition of these types and their members, refer to ISO/IEC 23271:2012 Common Language Infrastructure (CLI), Partition IV; Base Class Library (BCL), Extended Numerics Library, and Extended Array Library, which are included by reference in this specification." 

> The GitHub-based spec does *not* appear to have this annex.

> Add the following section to either of "C.2 Standard Library Types defined in ISO/IEC 23271" or "C.3 Standard Library Types not defined in ISO/IEC 23271:2012", as appropriate:

```csharp
namespace System
{
    public struct ValueTuple<[NullableAttribute(2)] T1, [NullableAttribute(2)] T2> : IStructuralComparable, IStructuralEquatable, IComparable, IComparable<(T1, T2)>, IEquatable<(T1, T2)>, ITuple
    {
        [NullableAttribute(1)]
        public T1 Item1;
        [NullableAttribute(1)]
        public T2 Item2;
        [NullableContextAttribute(1)]
        public ValueTuple(T1 item1, T2 item2);
        public int CompareTo([NullableAttribute(new[] { 0, 1, 1 })] (T1, T2) other);
        [NullableContextAttribute(2)]
        public override bool Equals(object? obj);
        public bool Equals([NullableAttribute(new[] { 0, 1, 1 })] (T1, T2) other);
        public override int GetHashCode();
        [NullableContextAttribute(1)]
        public override string ToString();
    }
}

namespace System
{
    public struct ValueTuple<[NullableAttribute(2)] T1, [NullableAttribute(2)] T2, [NullableAttribute(2)] T3> : IStructuralComparable, IStructuralEquatable, IComparable, IComparable<(T1, T2, T3)>, IEquatable<(T1, T2, T3)>, ITuple
    {
        [NullableAttribute(1)]
        public T1 Item1;
        [NullableAttribute(1)]
        public T2 Item2;
        [NullableAttribute(1)]
        public T3 Item3;
        [NullableContextAttribute(1)]
        public ValueTuple(T1 item1, T2 item2, T3 item3);
        public int CompareTo([NullableAttribute(new[] { 0, 1, 1, 1 })] (T1, T2, T3) other);
        [NullableContextAttribute(2)]
        public override bool Equals(object? obj);
        public bool Equals([NullableAttribute(new[] { 0, 1, 1, 1 })] (T1, T2, T3) other);
        public override int GetHashCode();
        [NullableContextAttribute(1)]
        public override string ToString();
    }
}

namespace System
{
    [NullableAttribute(0)]
    [NullableContextAttribute(1)]
    public struct ValueTuple<[NullableAttribute(2)] T1, [NullableAttribute(2)] T2, [NullableAttribute(2)] T3, [NullableAttribute(2)] T4> : IStructuralComparable, IStructuralEquatable, IComparable, IComparable<(T1, T2, T3, T4)>, IEquatable<(T1, T2, T3, T4)>, ITuple
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public T4 Item4;
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4);
        public int CompareTo([NullableAttribute(new[] { 0, 1, 1, 1, 1 })] (T1, T2, T3, T4) other);
        [NullableContextAttribute(2)]
        public override bool Equals(object? obj);
        public bool Equals([NullableAttribute(new[] { 0, 1, 1, 1, 1 })] (T1, T2, T3, T4) other);
        public override int GetHashCode();
        public override string ToString();
    }
}

namespace System
{
    [NullableAttribute(0)]
    [NullableContextAttribute(1)]
    public struct ValueTuple<[NullableAttribute(2)] T1, [NullableAttribute(2)] T2, [NullableAttribute(2)] T3, [NullableAttribute(2)] T4, [NullableAttribute(2)] T5> : IStructuralComparable, IStructuralEquatable, IComparable, IComparable<(T1, T2, T3, T4, T5)>, IEquatable<(T1, T2, T3, T4, T5)>, ITuple
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public T4 Item4;
        public T5 Item5;
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5);
        public int CompareTo([NullableAttribute(new[] { 0, 1, 1, 1, 1, 1 })] (T1, T2, T3, T4, T5) other);
        [NullableContextAttribute(2)]
        public override bool Equals(object? obj);
        public bool Equals([NullableAttribute(new[] { 0, 1, 1, 1, 1, 1 })] (T1, T2, T3, T4, T5) other);
        public override int GetHashCode();
        public override string ToString();
    }
}

namespace System
{
    [NullableAttribute(0)]
    [NullableContextAttribute(1)]
    public struct ValueTuple<[NullableAttribute(2)] T1, [NullableAttribute(2)] T2, [NullableAttribute(2)] T3, [NullableAttribute(2)] T4, [NullableAttribute(2)] T5, [NullableAttribute(2)] T6> : IStructuralComparable, IStructuralEquatable, IComparable, IComparable<(T1, T2, T3, T4, T5, T6)>, IEquatable<(T1, T2, T3, T4, T5, T6)>, ITuple
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public T4 Item4;
        public T5 Item5;
        public T6 Item6;
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6);
        public int CompareTo([NullableAttribute(new[] { 0, 1, 1, 1, 1, 1, 1 })] (T1, T2, T3, T4, T5, T6) other);
        [NullableContextAttribute(2)]
        public override bool Equals(object? obj);
        public bool Equals([NullableAttribute(new[] { 0, 1, 1, 1, 1, 1, 1 })] (T1, T2, T3, T4, T5, T6) other);
        public override int GetHashCode();
        public override string ToString();
    }
}

namespace System
{
    [NullableAttribute(0)]
    [NullableContextAttribute(1)]
    public struct ValueTuple<[NullableAttribute(2)] T1, [NullableAttribute(2)] T2, [NullableAttribute(2)] T3, [NullableAttribute(2)] T4, [NullableAttribute(2)] T5, [NullableAttribute(2)] T6, [NullableAttribute(2)] T7> : IStructuralComparable, IStructuralEquatable, IComparable, IComparable<(T1, T2, T3, T4, T5, T6, T7)>, IEquatable<(T1, T2, T3, T4, T5, T6, T7)>, ITuple
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public T4 Item4;
        public T5 Item5;
        public T6 Item6;
        public T7 Item7;
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7);
        public int CompareTo([NullableAttribute(new[] { 0, 1, 1, 1, 1, 1, 1, 1 })] (T1, T2, T3, T4, T5, T6, T7) other);
        [NullableContextAttribute(2)]
        public override bool Equals(object? obj);
        public bool Equals([NullableAttribute(new[] { 0, 1, 1, 1, 1, 1, 1, 1 })] (T1, T2, T3, T4, T5, T6, T7) other);
        public override int GetHashCode();
        public override string ToString();
    }
}

namespace System
{
    [NullableAttribute(0)]
    [NullableContextAttribute(1)]
    public struct ValueTuple<[NullableAttribute(2)] T1, [NullableAttribute(2)] T2, [NullableAttribute(2)] T3, [NullableAttribute(2)] T4, [NullableAttribute(2)] T5, [NullableAttribute(2)] T6, [NullableAttribute(2)] T7, [NullableAttribute(0)] TRest> : IStructuralComparable, IStructuralEquatable, IComparable, IComparable<ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>>, IEquatable<ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>>, ITuple where TRest : struct
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public T4 Item4;
        public T5 Item5;
        public T6 Item6;
        public T7 Item7;
        [NullableAttribute(0)]
        public TRest Rest;
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, [NullableAttribute(0)] TRest rest);
        public int CompareTo([NullableAttribute(new[] { 0, 1, 1, 1, 1, 1, 1, 1, 0 })] ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> other);
        [NullableContextAttribute(2)]
        public override bool Equals(object? obj);
        public bool Equals([NullableAttribute(new[] { 0, 1, 1, 1, 1, 1, 1, 1, 0 })] ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> other);
        public override int GetHashCode();
        public override string ToString();
    }
}
```
