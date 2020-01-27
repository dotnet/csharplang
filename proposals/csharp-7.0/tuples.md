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

A tuple literal is *target typed* whenever possible; that is, its type is determined by the context in which it is used.

**ISSUE:** If the term target-typed is specific to this context, we probably need a complete definition somewhere.

\[Example:
```csharp
var t1 = (0, 2);              // infer tuple type from values
var t2 = (sum: 0, count: 1);  // infer tuple type from names and values
```
end example\]

A tuple literal has a "conversion from expression" to any tuple type having the same number of elements, as long as each of the element expressions of the tuple literal has an implicit conversion to the type of the corresponding element of the tuple type.

\[Example:
```csharp
(string name, byte age) t = (null, 5); // OK: the expressions null and 5 convert to string and byte, respectively
```
end example\]

In cases where a tuple literal is not part of a conversion, it acquires its *natural type*, which means a tuple type where the element types are the types of the constituent expressions, in lexical order. Since not all expressions have types, not all tuple literals have a natural type either:

**ISSUE:** Is natural type specific to tuples? Need we say more about this term?

\[Example:
```csharp
var t = ("John", 5); // OK: the type of t is (string, int)
var t = (null, 5);   // Error: null doesn't have a type
```
end example\]

If a tuple literal includes element names, those names become part of the natural type: 

\[Example:
```csharp
var t = (name: "John", age: 5); // The type of t is (string name, int age)
```
end example\]

A tuple literal is *not* a [constant expression](../../spec/expressions.md#Constant-expressions). As such, a tuple literal cannot be used as the default value for an optional parameter.

**ISSUE:** Consider removing the second sentence above, as it sounds like just one example of usage prohibition (rather than a spec requirement); there likely are others. If that is true, either completely omit that sentence, or delete it from here and add an example showing that such a usage fails.

For a discussion of tuple literals as tuple initializers, see [Tuple types](../../spec/types.md#Tuple-types).

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
```

## Additions to [Types](../../spec/types.md)

> Add the following sections after [Nullable types](../../spec/types.md#nullable-types) (at the end of the current *Value types* section.)

### Tuple types

#### General

A tuple type is declared using the following syntax:

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

A ***tuple*** is an anonymous data structure type that contains an ordered sequence of two or more ***elements***. Each element is public. Each unique, ordered combination of element types designates a distinct tuple type.

An element in a tuple is accessed using the [member-access operator `.`](../../spec/expressions.md#Member-access).

Given the following,

```csharp
(int code, string message) pair1 = (3, "hello");
System.Console.WriteLine("first = {0}, second = {1}", pair1.code, pair1.message);
```

the syntax `(int code, string message)` declares a tuple type having two elements, each with the given type and name.

As shown, a tuple can be initialized using a [tuple literal](../../spec/lexical-structure.md#Tuple-literals).

An element need not have a name.

The type of a tuple is the ordered set of types of its elements along with their names. An element without a name is unnamed. If a tuple declarator contains the type of all the tuple's elements, that set of types cannot be changed or augmented based on the context in which it is used; otherwise, element type information shall be inferred from the usage context. Likewise for element names.

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

Elements within a tuple type shall have distinct names or be unnamed.

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

**ISSUE:** Where will the type System.Value be defined (and its public names listed)?

The name given explicitly to any element shall not be the same as any name in the underlying type, except that an explicit name may have the form `Item`*N* provided it corresponds position-wise with an element of the same name in the underlying type.

\[Example:
```csharp
var t =  (ToString: 0, GetHashCode: 1);	// Error: names match underlying member names
var t1 = (Item1: 0, Item2: 1);			// OK
var t2 = (misc: 0, Item1: 1);			// Error: "Item1" used in a wrong position
```
end example\]

#### Element names and overloading, overriding, and hiding

When tuple element names are used in overridden signatures or implementations of interface methods, tuple element names in parameter and return types shall be preserved. It is an error for the same generic interface to be inherited or implemented twice with identity-convertible type arguments that have conflicting tuple element names.

For the purpose of overloading, overriding and hiding, tuples of the same types and lengths as well as their underlying ValueTuple types shall be considered equivalent. All other differences are immaterial. When overriding a member it is permitted to use tuple types with the same names or names different than in the base member.

**ISSUE:** Re the mention of "tuple length", which is not mentioned elsewhere; it seems to me that a tuple type includes an implied length based on the number of elements.

If the same element name is used for non-matching elements in base and derived member signatures, the implementation shall issue a warning.  

```csharp
class Base
{
    virtual void M1(ValueTuple<int, int> arg){...}
}
class Derived : Base
{
    override void M1((int c, int d) arg){...}	// valid override, signatures are equivalent
}
class Derived2 : Derived 
{
    override void M1((int c1, int c) arg){...}	// also valid, warning on possible misuse of name 'c' 
}

class InvalidOverloading 
{
    virtual void M1((int c, int d) arg){...}
    virtual void M1((int x, int y) arg){...}		// invalid overload, signatures are eqivalent
    virtual void M1(ValueTuple<int, int> arg){...}	// also invalid
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

**ISSUE:** see my commetns later w.r.t deconstruction, assignment, patterns, and section organization

## Discards

**ISSUE:** We need a definition for "discard" 

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

* Element names are immaterial to tuple conversions. Tuples with the same element type set in the same order are identity-convertible to each other or to and from corresponding underlying `ValueTuple` types, regardless of the names.

\[Example:
```csharp
var t = (sum: 0, count: 1);

System.ValueTuple<int, int> vt = t;  // identity conversion
(int moo, int boo) t2 = vt;          // identity conversion

t2.moo = 1;
```
end example\]

In teh case in which an element name at one position on one side of a conversion, and the same name at a different position on the other side, the copmpiler shall issue a warning.

\[Example:
```csharp
(string first, string last) GetNames() { ... }
(string last, string first) names = GetNames(); // Oops!
```
end example\]

### Boxing conversions

> Add the following text to [Boxing conversions](../../spec/conversions.md#boxing-conversions) after the first paragraph:

Tuples have a boxing conversion. Importantly, the element names aren't part of the runtime representation of tuples, but are tracked only by the compiler. Thus, once element names have been "cast away", they cannot be recovered. In alignment with identity conversion, a boxed tuple unboxes to any tuple type that has the same element types in the same order.

### Tuple conversions

> Add this section after [Implicit enumeration conversions](../../spec/conversions.md#implicit-enumeration-conversions)

Tuple types and expressions support a variety of conversions by "lifting" conversions of the elements into overall *tuple conversion*. For the classification purpose, all element conversions are considered recursively. For example, to have an implicit conversion, all element expressions/types shall have implicit conversions to the corresponding element types.

Tuple conversions are *Standard Conversions* and therefore can stack with user-defined operators to form user-defined conversions.

**ISSUE:** Is 'stack' a defined term in this context?

An implicit tuple conversion is a standard conversion. It applies between two tuple types of equal arity when there is any implicit conversion between each corresponding pair of types.

**ISSUE:** Re 'arity' does this mean number of elements, with their types and names? In any event, perhaps we should define this in terms of tuples and consistently use it throughout, as in, "The arity of a tuple is ...".

An explicit tuple conversion is a standard conversion. It applies between two tuple types of equal arity when there is any explicit conversion between each corresponding pair of types.

A tuple conversion can be classified as a valid instance conversion or an extension method invocation as long as all element conversions are applicable as instance conversions.

On top of the member-wise conversions implied by target typing, implicit conversions between tuple types themselves are allowed.

### Target typing

> Add this section after [Anonymous function conversions and method group conversions](../../spec.md#Anonymous-function-conversions-and-method-group-conversions)

A tuple literal is "target typed" when used in a context specifying a tuple type. The tuple literal has a "conversion from expression" to any tuple type, as long as the element expressions of the tuple literal have an implicit conversion to the element types of the tuple type.

\[Example:
```csharp
(string name, byte age) t = (null, 5); // Ok: the expressions null and 5 convert to string and byte
```
end example\]

A successful conversion from tuple expression to tuple type is classified as an *ImplicitTuple* conversion, unless the tuple's natural type matches the target type exactly, in such case it is an *Identity* conversion.

```csharp
void M1((int x, int y) arg){...};
void M1((object x, object y) arg){...};

M1((1, 2));            // first overload is used. Identity conversion is better than implicit conversion.
M1(("hi", "hello"));   // second overload is used. Implicit tuple conversion is better than no conversion.
```

Target typing will "see through" nullable target types. A successful conversion from tuple expression to a nullable tuple type is classified as *ImplicitNullable* conversion.

**ISSUE:** Replace "see through" with something not quoted.

```csharp
((int x, int y, int z)?, int t)? SpaceTime()
{
    return ((1,2,3), 7);  // valid, implicit nullable conversion
}
```

## Additions to [Expressions](../../spec/expressions.md)

### Overload resolution and tuples with no natural types

> Add the following text after the bullet list in [Exactly matching expressions](../../spec/expressions.md#Exactly-matching-expressions):

The exact-match rule for tuple expressions is based on the natural types of the constituent tuple arguments. The rule is mutually recursive with respect to other containing or contained expressions not in a possession of a natural type.

**ISSUE:** The use of "argument" here doesn't seem right; arguments are passed to methods! Should it be "elements" instead?

### Deconstruction expressions

> Add this section at the end of the [Expressions](../../spec/expressions.md) chapter.

A tuple-deconstruction expression copies from a source tuple zero or more of its element values to corresponding destinations.

**ISSUE:** I whipped up the following grammar; it needs to be made correct/complete. I'm guessing we can leverage on existing productions. Also, destination can be a declaration of a new local variable (explicit or var), or it can be the name of an existing one. My tests show that destination_list can't contain a combination of the two, however.

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

**ISSUE:** Is this expression constrained to being only on the LHS of simple (compound?) assignment, where the RHS is a tuple having at least as many elements as positions indicated by the LHS? See also my issue later w.r.t "deconstruction assignment expressions".

Element values are copied from the source tuple to the destinations. Each element's position is inferred from the destination position within *destination_list*. A destination with identifier `_` indicates that the corresponding element is discarded rather than being copied. The destination list shall accouint for every element in the tuple.

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

**ISSUE:** As this is an operator, what is the result type? `void`?
**ISSUE:** Presumably the scope of any newly created variable is from the point of declaration on to the end of the block, or does this fall-out of saying its a local variable?

Any object may be deconstructed by providing an accessible `Deconstruct` method, either as a member or as an extension method. A `Deconstruct` method converts an object to a set of discrete values. The Deconstruct method "returns" the component values by use of individual `out` parameters. `Deconstruct` is overloadable. Consider the following:

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

**ISSUE:** I think we need to say more about this code (which is not an example, but is intended to show/say just what is going on/needed).

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

**ISSUE:** This is the first place I've seen deconstruction mentioned in the same breath as assignment. If deconstruction can only occur on the LHS of a (simple?, compound?) assignment, should deconstruction be covered under assignment rather than in its own section? If the two are in spearate sections, the expression grammar needs to have the new production, tuple_deconstruction_expression, plugged into it. From my experience with tuples, patterns, and _ wildcards in other languages, I'm thinking that the V7.0 support for tuple deconstruction is the beginning of a more general pattern support mechanism to come later. If that is the case, perhaps we should admit that and start using pattern-related terminology and organization. If so, that suggests tuple deconstruction should be separate from the assignment operator, in a (pre-)patterns section that will be expanded over future spec versions.

1. Evaluate the LHS: Evaluate each of the expressions inside of it one by one, left to right, to yield side effects and establish a storage location for each.
1. Evaluate the RHS: Evaluate each of the expressions inside of it one by one, left to right to yield side effects
1. Convert each of the RHS expressions to the LHS types expected, one by one, left to right.
1. Assign each of the conversion results from Step 3 to the storage locations found in (???)

> **Note to reviewers**: I found this in the LDM notes for July 13-16, 2016. I don't think it is still accurate:

**ISSUE:** flesh out the following example

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
t.M(); // Sure
```
*endnote*].

**ISSUE:** Explain what the comment "Sure" means.