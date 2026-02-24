# Unions

Champion issue: https://github.com/dotnet/csharplang/issues/9662

## Summary
[summary]: #summary

*Unions* is a set of interlinked features, that combine to provide C# support for union types:

- *Union types*: Structs and classes that have a `[Union]` attribute are recognized as *union types*, and support the *union behaviors*.
- *Case types*: Union types have a set of *case types*, which is given by parameters to constructors and factory methods.
- *Union behaviors*: Union types support the following *union behaviors*:
    - *Union conversions*: There are implicit *union conversions* from each case type to a union type.
    - *Union matching*: Pattern matching against union values implicitly "unwraps" their contents, applying the pattern to the underlying value instead.
    - *Union exhaustiveness*: Switch expressions over union values are exhaustive when all case types have been matched, without need for a fallback case.
    - *Union nullability*: Nullability analysis has enhanced tracking of the null state of a union's contents.
- *Union patterns*: All union types follow a basic *union pattern*, but there are additional optional patterns for specific scenarios.
- *Union declarations*: A shorthand syntax allows declaration of union types directly. The implementation is "opinionated" - a struct declaration that follows the basic union pattern and stores the contents as a single reference field.
- *Union interfaces*: A few interfaces are known by the language and used in its implementation of union declarations. 

## Motivation
[motivation]: #motivation

Unions are a long-requested C# feature, which allows expressing values from a closed set of types in a way that pattern matching can trust to be exhaustive.

The separation between union *types* and union *declarations* allows C# to have a succinct union declaration syntax with opinionated semantics, while also allowing existing types or types with other implementation choices to opt into union behaviors.

The proposed unions in C# are unions of *types* and not "discriminated" or "tagged". "Discriminated unions" can be expressed in terms of "type unions" by using fresh type declarations as case types. Alternatively they can be implemented as a [closed hierarchy](https://github.com/dotnet/csharplang/blob/main/proposals/closed-hierarchies.md), which is another, related, upcoming C# feature focused on exhaustiveness.

## Detailed design
[design]: #detailed-design

### Union types

Any class or struct type with a `System.Runtime.CompilerServices.UnionAttribute` attribute is considered a *union type*:

```csharp
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(Class | Struct, AllowMultiple = false)]
    public class UnionAttribute : Attribute;
}
```

A union type must follow a certain pattern of public *union members*, which must either be declared on the union type itself or delegated to a "union member provider".

Some union members are mandatory, and others are optional.

A union type has a set of *case types* which are established based on the signatures of certain union members.

The contents of a union value can be accessed through a `Value` property. The language assumes that `Value` only ever contains a value of one of the case types, or null (see [Well-formedness](#well-formedness)).

#### Union member providers

By default, union members are found on the union type itself. However, if the union type *directly contains* a declaration of an interface called `IUnionMembers` then the interface acts as a *union member provider*. In that case, union members are *only* found on the union member provider, not on the union type itself.

A union member provider interface must be public, and the union type itself must implement it as an interface.

We use the term *union-defining type* for the type where the union members are found: The union member provider if it exists, and the union type itself otherwise.

#### Union members

Union members are looked up by name and signature on the union-defining type. They do not have to be declared directly on the union-defining type, but can be inherited. 

It is an error for any union member not to be public.

The creation members and the `Value` property are mandatory, and are collectively referred to as the *basic union pattern*.

The `HasValue` and `TryGetValue` members are collectively referred to as the *non-boxing union access pattern*.

The different union members are described in the following.

#### Union creation members

Union creation members are used to create new union values from a case type value.

If the union-defining type is the union type itself, each constructor with a single parameter is a *union constructor*.
The case types of the union are identified as the set of types built from parameter types of these constructors in the following way:
 - If the parameter type is a nullable type (whether a value or a reference), the case type is the underlying type
 - Otherwise, the case type is the parameter type.

```csharp
// Union constructor making `Dog` a case type
public Pet(Dog value) { ... }
```
```csharp
// Union constructor making `int` a case type
public Union(int? value) { ... }
```
```csharp
// Union constructor making `string` a case type
public Union(string? value) { ... }
```

If the union-defining type is a union member provider, each static `Create` method with a single parameter and a return type 
that is identity-convertible to the union type itself is a *union factory method*. 
The case types of the union are identified as the set of types built from parameter types of these factory methods in the following way:
 - If the parameter type is a nullable type (whether a value or a reference), the case type is the underlying type
 - Otherwise, the case type is the parameter type.

```csharp
// Union factory method making `Cat` a case type
public static Pet Create(Cat value) { ... }
```
```csharp
// Union factory method making `int` a case type
public static Union Create(int? value) { ... }
```
```csharp
// Union factory method making `string` a case type
public static Union Create(string? value) { ... }
```

Union constructors and union factory methods are referred to collectively as *union creation members*.

The single parameter of a union creation member must be a by-value or `in` parameter.

A union type must have at least one union creation member, and therefore at least one case type.

#### Value property

The `Value` property allows access to the value contained in a union, regardless of its case type.

Every union-defining type must declare a `Value` property of type `object?` or `object`. The property must have a `get` accessor and may optionally have an `init` or `set` accessor, which can be of any accessibility and is not used by the compiler.

```csharp
// Union 'Value' property
public object? Value { get; }
```

#### Non-boxing access members

A union type can choose to additionally implement the *non-boxing union access pattern*, which allows strongly typed conditional access to each case type, as well as a way to check for null.

This allows the compiler to implement pattern matching more efficiently when case types are value types and stored as such within the union.

The non-boxing access members are:

- A `HasValue` property of type `bool` with a public `get` accessor. It may optionally have an `init` or `set` accessor, which can be of any accessibility and is not used by the compiler. 
- A `TryGetValue` method for each case type. The method returns `bool` and takes a single out-parameter of a type that is identity-convertible to the case type.
    
```csharp
// Non-boxing access members
public bool HasValue { get { ... } }
public bool TryGetValue(out Dog value) { ... }
```

`HasValue` is expected to return true if and only if the union's `Value` is not null.

`TryGetValue` is expected to return true if and only if the union's `Value` is of the given case type, and if so, deliver that value in the method's out parameter.

#### Well-formedness

The language and compiler make a number of behavioral assumptions about union types. If a type qualifies as a union type but does not satisfy those assumptions, then union behaviors may not work as expected.

* *Soundness*: The `Value` property always evaluates to null or to a value of a case type. That is true even for the default value of the union type.
* *Stability*: If a union value is created from a case type, the `Value` property will match that case type or null. If a union value is created from a `null` value, the `Value` property will be `null`.
* *Creation equivalence*: If a value is implicitly convertible to two different case types then the creation member for either of those case types has the same observable behavior when called with that value.
* *Access pattern consistency*: The behavior of the `HasValue` and `TryGetValue` non-boxing access members, if present, is observably equivalent to that of checking against the `Value` property directly.

#### Examples of union types

`Pet` implements the basic union pattern on the union type itself:

```csharp
[Union] public record struct Pet
{
    // Creation members = case types are 'Dog' and 'Cat'
    public Pet(Dog value) => Value = value;
    public Pet(Cat value) => Value = value;

    // 'Value' property
    public object? Value { get; }
}
```

`IntOrBool` implements the non-boxing access pattern on the union type itself:

```csharp
public record struct IntOrBool
{
    private bool _isBool;
    private int _value;

    public IntOrBool(int value) => (_isBool, _value) = (false, value);
    public IntOrBool(bool value) => (_isBool, _value) = (true, value ? 1 : 0);

    public object Value => _isBool ? _value is 1 : _value;

    public bool HasValue => true;
    public bool TryGetValue(out int value)
    {
        value = _value;
        return !_isBool;
    }
    public bool TryGetValue(out bool value)
    {
        value = _isBool && _value is 1;
        return _isBool;
    }
}
```

*Note:* This is just an example of how the non-boxing access pattern might be implemented. The user code can store the content any way it likes. In particular, it does not prevent the implementation from boxing! The `non-boxing` in its name refers to allowing the compiler's pattern matching implementation to access each case type in a strongly typed way, as opposed to the `object?`-typed `Value` property.

`Result<T>` implements the basic pattern via a union member provider:

```csharp
public record class Result<T> : Result<T>.IUnionMembers
{
    object? _value;

    public interface IUnionMembers
    {
        public static Result<T> Create(T value) => new() { _value = value };
        public static Result<T> Create(Exception value) => new() { _value = value };

        public object? Value { get; }
    }

    object? IUnionMembers.Value => _value;
}
```

### Union behaviors

The union behaviors are generally implemented by means of the basic union pattern. If the union offers the non-boxing access pattern, union pattern matching will preferentially make use of it.

#### Union conversions

A *union conversion* implicitly converts to a union type from each of its case types. Specifically, there's a union conversion to a union type `U` from a type or expression `E` if there's a standard implicit conversion from `E` to a type `C` and `C` is a parameter type of a *union creation member* of `U`.
If union type `U` is a struct, there's a union conversion to type `U?` from a type or expression `E` if there's a standard implicit conversion from `E` to a type `C` and `C` is a parameter type of a *union creation member* of `U`.

A union conversion is not itself a standard implicit conversion. It may therefore not participate in a user-defined implicit conversion or another union conversion.

There are no explicit union conversions beyond the implicit union conversions. Thus, even if there is an explicit conversion from `E` to a union's case type `C`, that doesn't mean there is an explicit conversion from `E` to that union type.

A union conversion is executed by calling the union's creation member:

``` c#
Pet pet = dog;
// becomes
Pet pet = new Pet(dog);
// and
Result<string> result = "Hello"
//becomes
Result<string> result = Result<string>.IUnionMembers.Create("Hello");
```

It is an error if overload resolution does not find a single best candidate member, or if that member is not one of the union type's union members.

Union conversion is just another "form" of an implicit user-defined conversion. An applicable
user-defined conversion operator "shadows" union conversion.

The rationale behind this decision:
> If someone written a user-defined operator, it should get priority.
> In other words, if the user actually wrote their own operator, they want us to call it.
> Existing types with conversion operators transformed into union types continue to work
> the same way with respect to existing code utilizing the operators today.

In the following example an implicit user-defined conversion takes priority over a union conversion. 
``` c#
struct S1 : System.Runtime.CompilerServices.IUnion
{
    public S1(int x) => ...
    public S1(string x) => ...
    object System.Runtime.CompilerServices.IUnion.Value => ...
    public static implicit operator S1(int x) => ...
}

class Program
{
    static S1 Test1() => 10; // implicit operator S1(int x) is used
    static S1 Test2() => (S1)20; // implicit operator S1(int x) is used
}
```

In the following example, when explicit cast is used in code, an explicit user-defined conversion
takes priority over a union conversion. But, when there is no explicit cast in code, a union conversion
is used because explicit user-defined conversion is not applicable.
``` c#
struct S2 : System.Runtime.CompilerServices.IUnion
{
    public S2(int x) => ...
    public S2(string x) => ...
    object System.Runtime.CompilerServices.IUnion.Value => ...
    public static explicit operator S2(int x) => ...
}

class Program
{
    static S2 Test3() => 10; // Union conversion S2.S2(int) is used
    static S2 Test4() => (S2)20; // explicit operator S2(int x)
}
```

#### Union matching

When the incoming value of a pattern is of a union type or of a nullable of a union type, 
the nullable value and the underlying union value's contents may be "unwrapped", depending on the pattern.

For the unconditional `_` and `var` patterns, the pattern is applied to the incoming value itself. For example:

```csharp
if (GetPet() is var pet) { ... } // 'pet' is the union value returned from `GetPet`
```

However, all other patterns get implicitly applied to the underlying union's `Value` property:

``` c#
if (GetPet() is Dog dog) { ... }   // 'Dog dog' is applied to 'GetPet().Value'
if (GetPet() is null) { ... }      // 'null' is applied to 'GetPet().Value'
if (GetPet() is { } value) { ... } // '{ } value' is applied to 'GetPet().Value'
```

For logical patterns, this rule is applied individually to the branches, bearing in mind that the left branch of an `and` pattern can affect the incoming type of the right branch:

``` c#
GetPet() switch
{
    var pet and not null   => ... // 'var pet' applies to the incoming 'Pet' and 'not null' to its 'Value'
    not null and var value => ... // 'not null' applies to the 'Value' as does 'var value' because of the 
                                  // left branch changing the incoming type to `object?`.
}
```

*Note:* This rule means that `GetPet() is Pet pet` will likely not succeed, as `Pet` is applied to the _contents_, not to the `Pet` union itself.

*Note:* The reason for the different treatment of unconditional `var` pattern (as well as `_`, which is essentially a shorthand for `var _`) is an assumption that their use is qualitatively different from other patterns. `var` patterns are used simply to name the value being matched against, oftentimes in nested patterns, such as `PetOwner{ Pet: var pet }`. Here, the helpful semantics is for `pet` to retain the union type `Pet`, instead of the `Value` property being dereferenced to a useless `object?` type.

If the incoming value is a class type, then the `null` pattern will succeed regardless of whether the union value itself is `null` or its contained value is `null`:

```csharp
if (result is null) { ... } // if (result == null || result.Value == null)
```

Other union matching patterns will succeed only when the union value itself is not `null`.
```csharp
if (result is 1) { ... } // if (result != null && result.Value is 1)
```

Similarly, if the incoming value is a nullable values type (wrapping a struct union type), then the `null` pattern will succeed 
regardless of whether the incoming value itself is `null` or its contained value is `null`:

```csharp
if (result is null) { ... } // if (result.HasValue == false || result.GetValueOrDefault().Value == null)
```

Other union matching patterns will succeed only when the the incoming value itself is not `null`.
```csharp
if (result is 1) { ... } // if (result.HasValue && result.GetValueOrDefault().Value is 1)
```


The compiler will prefer implementing pattern behavior by means of members prescribed by the non-boxing access pattern. While it is free to do any optimization within the bounds of the well-formedness rules, the following are the minimum set guaranteed to be applied:

* For a pattern that implies checking for a specific type `T`, if a `TryGetValue(S value)` method is available, and there is an identity, or implicit reference/boxing conversion from `T` to `S`, then that method is used to obtain the value. The pattern is then applied to that value. If there is more than one such method, then any where the conversion from `T` to `S` is not a boxing conversion is preferred if available. If there is still more than one method, one is chosen in an implementation-defined manner.
* Otherwise, for a pattern that implies checking for `null`, if a `HasValue` property is available, that property is used to check if the union value is null.
* Otherwise, the pattern is applied to the result of accessing the `IUnion.Value` property on the incoming union.

[The is-type operator](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1214121-the-is-type-operator) applied to a union type
has the same meaning as a type pattern applied to the union type.

#### Union exhaustiveness

A union type is assumed to be "exhausted" by its case types. This means that a `switch` expression is exhaustive if it handles all of a union's case types:


``` c#
var name = pet switch
{
    Dog dog => ...,
    Cat cat => ...,
    // No warning about non-exhaustive switch
};
```

#### Nullability

The null state of a union's `Value` property is tracked like any other property, with these modifications: 

- When a union creation member is called (explicitly or through a union conversion), the new union's `Value` gets the null state of the incoming value.
- When the non-boxing access pattern's `HasValue` or `TryGetValue(...)` are used to query the contents of a union type (explicitly or via pattern matching), it impacts `Value`'s nullability state in the same way as if `Value` had been checked directly: The null state of `Value` becomes "not null" on the `true` branch.

Even when a union switch is otherwise exhaustive, if the null state of the incoming union's `Value` property is "maybe null", a warning will be given on unhandled null.

``` c#
Pet pet = GetNullableDog(); // 'pet.Value' is "maybe null"
var value = pet switch
{
    Dog dog => ...,
    Cat cat => ...,
    // Warning: 'null' not handled
}
```

### Union interfaces

The following interfaces are used by the language in its implementation of union features.

#### Union access interface

The `IUnion` interface marks a type as a union type at compile time and provides a way to access union contents at runtime.

```csharp
public interface IUnion
{
    // The value of the union or null
    object? Value { get; }
}
```

Unions generated by the compiler implement this interface.

Example use:

```csharp
if (value is IUnion { Value: null }) { ... }
```

### Union declarations

Union declarations are a succinct and opinionated way of declaring union types in C#. They declare a struct which uses a single object reference for storing its `Value`, which means:

* *Boxing*: Any value types among their case types will be boxed on entry.
* *Compactness*: Union values only contain a single field.

The intent is for union declarations to cover the vast majority of use cases quite nicely. The two main reasons for hand coding specific union types rather than use union declarations are expected to be:

* Adapting existing types to the union patterns to gain union behaviors.
* Implementing a different storage strategy for e.g. efficiency or interop reasons.

#### Syntax

A union declaration has a name and a list of *union constructors* types.

``` antlr
union_declaration
    : attributes? struct_modifier* 'partial'? 'union' identifier type_parameter_list?
      '(' type (',' type)* ')'  struct_interfaces? type_parameter_constraints_clause* 
      (`{` struct_member_declaration* `}` | ';')
    ;
```

In addition to the restrictions on struct members ([§16.3](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/structs.md#163-struct-members)), the following applies to union members:

* Instance fields, auto-properties or field-like events are not permitted.
* Explicitly declared public constructors with a single parameter are not permitted.
* Explicitly declared constructors must use a `this(...)` initializer to (directly or indirectly) delegate to one of the generated constructors.

The *union constructors* types can be any type that converts to `object`, e.g., interfaces, type parameters, nullable types and other unions. It is fine for resulting cases to overlap, and for unions to nest or be null.

Examples:

```csharp
// Union of existing types
public union Pet(Cat, Dog, Bird);

// Union with function member
public union OneOrMore<T>(T, IEnumerable<T>)
{
    public IEnumerable<T> AsEnumerable() => Value switch
    {
        IEnumerable<T> list => list,
        T value => [value],
    }
}

// "Discriminated" union with freshly declared case types
public record class None();
public record class Some<T>(T value);
public union Option<T>(None, Some<T>);

#### Lowering

A union declaration is lowered to a struct declaration with

* the same attributes, modifiers, name, type parameters and constraints,
* implicit implementations of `IUnion`,
* a `public object? Value { get; }` auto-property,
* a public constructor for each *union constructor* type,
* any members in the union declaration's body.

It is an error for user-declared members to conflict with generated members.

Example:

``` c#
public union Pet(Cat, Dog){ ... }
```

Is lowered to:

``` c#
[Union] public struct Pet : IUnion
{
    public Pet(Cat value) => Value = value;
    public Pet(Dog value) => Value = value;
    
    public object? Value { get; }
    
    ... // original body
}
```

## Open questions
[open]: #open-questions

### [Resolved] Is union declaration a record?

> A union declaration is lowered to a record struct

I think this default behavior is unnecessary and, given that it is not configurable, going to significantly
limit usage scenarios. Records generate a lot of code that is either unused or doesn't match specific requirements.
For example, records are pretty much forbidden in compiler's code base because of that code bloat. I think that it would be better
to change the default:
 - By default, a union declaration declares a regular struct with just union-specific members.
 - A user can declare a record union: ``` record union U(E1, ...) ... ``` 

**Resolution:** A union declaration is a plain struct, not record struct. The ```record union ...``` isn't supported

### [Resolved] Union declaration syntax

It looks like the proposed syntax is incomplete or unnecessarily limiting. For example, it looks like
base clause is not permitted. However, I can easily imagine a need to implement an interface, for example.
I think that apart from the element-types-list the syntax should match regular `struct`/`record struct`
declaration where the `struct` keyword is replaced with `union` keyword.

**Resolution:** The restriction is removed.

### [Resolved] Union declaration members

> Instance fields, auto-properties or field-like events are not permitted.

This feels arbitrary and absolutely unnecessary.

**Resolution:** The restriction is kept.

### [Resolved] Nullable value types as Union case types

> The case types of the union are identified as the set of parameter types from these constructors.
> The case types of the union are identified as the set of parameter types from these factory methods.

At the same time:

> A `TryGetValue` method for each case type. The method returns `bool` and takes a single out-parameter of a type that corresponds to the given case type in the following way:
>    - If the case type is a nullable value type, the type of the parameter should be identity-convertible to the underlying type
>    - Otherwise, the type should be identity-convertible to the case type.

Is there an advantage to have a nullable value type among the case types especially that a type pattern cannot use
nullable value type as the target type? It feels like we could simply say that, if constructor's/factory's parameter
type is a nullable value type, then corresponding case type is the underlying type. Then we wouldn't need that extra clause
for the `TryGetValue` method, all out parameters are case types.

**Resolution:** The suggestion is approved

### [Resolved] Default nullable state of `Value` property

> For union types where none of the case types are nullable, the default state for `Value` is "not null" rather than "maybe null". 

With the new design, where `Value` property is not defined in some general interface, but 
is an API that specifically belongs to the declared type, the rule quoted above feels like
over-engineering. Moreover, the rule likely will force consumers to use nullable types in situations where
otherwise nullable types wouldn't be used.

For example, consider the following union declaration:
``` c#
union U1(int, bool, DateTime);
```

According to the quoted rule, the default state for `Value` is "not null". But that doesn't match behavior of the
type, `default(U1).Value` is `null`. In order to realign the behavior, consumer is forced to make at least one 
case type nullable. Something like: 
``` c#
union U1(int?, bool, DateTime);
```

But that is likely undesirable, consumer might not want to allow explicit creation with `int?` value.

Proposal: Remove the quoted rule, nullable analysis should use annotations from the `Value` property
          to infer its default nullability.

**Resolution:** The proposal is approved

### [Resolved] Union matching for Nullable of a union value type

> When the incoming value of a pattern is of a union type, the union value's contents may be "unwrapped", depending on the pattern.

Should we expand this rule to scenarios when incoming value of a pattern is of a `Nullable<union type>`?

Consider the following scenario:
``` C#
    static bool Test1(StructUnion? u)
    {
        return u is 1;
    }   

    static bool Test2(ClassUnion? u)
    {
        return u is 1;
    }   
```

The meaning of ```u is 1``` in Test1 and Test2 are very different. In Test1 it is not a union matching, in Test2 it is.
Perhaps "union matching" should "dig" through `Nullable<T>` as pattern matching usually does in other situations.

If we go with that, then the union matching `null` pattern against `Nullable<union type>` should work as against classes.
I.e. the pattern is true when ```(!nullableValue.HasValue || nullableValue.Value.Value is null)```.

**Resolution:** The proposal is approved.

### What to do about "bad" APIs?

What should compiler do about union matching APIs that look like a match, but otherwise "bad"?
For example, compiler finds TryGetValue/HasValue with matching signature, but it is "bad" because
a required custom modifier or it requires an unknown feature, etc. Should compiler silently ignore the API or
report an error?
Similar, the API might be marked as Obsolete/Experimental. Should compiler report any diagnostics, silently use the API
or silently not use the API?

### What if types for union declaration are missing

What happens if `UnionAttribute`, `IUnion` or `IUnion<TUnion>` are missing? Error? Synthesize? Something else?

### [Resolved] Design of generic IUnion interface

Arguments have been made that `IUnion<TUnion>` should not inherit from `IUnion` or constrain its type parameter to `IUnion<TUnion>`. We should revisit.

**Resolution:** The `IUnion<TUnion>` interface is removed for now.

### [Resolved] Nullable value types as case types and their interaction with `TryGetValue`

The rules above state that if a case type is a nullable value type, the parameter type used in a corresponding `TryGetValue` method should be the *underlying* type. 
This is motivated by the fact that a `null` value would never be yielded through this method. On the consumption side, a nullable value type is not allowed as a type pattern, whereas a match against the underlying type should be able to map to a call of this method.

We should confirm that we agree with this unwrapping.

**Resolution:** Agreed/confirmed

### *The non-boxing union access pattern*

Need to specify precise rules for finding suitable `HasValue` and `TryGetValue` APIs.
Is inheritance involved? Is read/write `HasValue` an acceptable match? Etc.

### [Resolved] `TryGetValue` matching conversions

The Union Matching section says:
> For a pattern that implies checking for a specific type `T`, if a `TryGetValue(S value)`
> method is available, and there is an implicit conversion from `T` to `S`,
> then that method is used to obtain the value.

Is the set of implicit conversions restricted in any way? For example, are user-defined conversions allowed?
What about tuple conversions and other not so trivial conversions? Some of those are even standard conversions. 

Is the set of `TryGetValue` methods restricted in any other way? For example, Union Patterns section implies
that only methods with a parameter type matching a case type are considered:
> a `public bool TryGetValue(out T value)` method for each case type `T`.

It would be good to have an explicit answer. 

**Resolution:** Only implicit identity, or reference, or boxing conversions are considered 

### `TryGetValue` and nullable analysis

> When the non-boxing access pattern's `HasValue` or `TryGetValue(...)`
> are used to query the contents of a union type (explicitly or via pattern matching),
> it impacts `Value`'s nullability state in the same way as if `Value` had been
> checked directly: The null state of `Value` becomes "not null" on the `true` branch.

Is the set of `TryGetValue` methods restricted in any way? For example, Union Patterns section implies
that only methods with a parameter type matching a case type are considered:
> a `public bool TryGetValue(out T value)` method for each case type `T`.

It would be good to have an explicit answer. 

### Clarify rules around `default` values of struct union types

*Note*: The default nullability rule mentioned below has been removed.

*Note*: The "default" well-formedness rules mentioned below have been removed. We should confirm that this is what we want.

[Nullability](#Nullability) section says:
> For union types where none of the case types are nullable, the default state for `Value` is "not null" rather than "maybe null". 

Given that, for the example below, current implementation considers `Value` of `s2` as "not null":
``` c#
S2 s2 = default;

struct S2 : System.Runtime.CompilerServices.IUnion
{
    public S2(int x) => throw null!;
    public S2(bool x) => throw null!;
    object? System.Runtime.CompilerServices.IUnion.Value => throw null!;
}
```

At the same time, [Well-formedness](#Well-formedness) section says:
>* *Default value*: If a union type is a value type, it's default value has `null` as its `Value`.
>* *Default constructor*: If a union type has a nullary (no-argument) constructor, the resulting union has `null` as its `Value`.

An implementation like that will be in contradiction with nullable analysis behavior for the example above.

Should the [Well-formedness](#Well-formedness) rules be adjusted, or should state of `Value` of `default` be "maybe null"?
If the latter, should initialization ```S2 s2 = default;``` produce a nullability warning?

### Confirm that a type parameter is never a union type, even when constrained to one.

``` C#
class C1 : System.Runtime.CompilerServices.IUnion
{
    private readonly object _value;
    public C1(int x) { _value = x; }
    public C1(string x) { _value = x; }
    object System.Runtime.CompilerServices.IUnion.Value => _value;
}

class Program
{
    static bool Test1<T>(T u) where T : C1
    {
        return u is int; // Not a union matching
    }   

    static bool Test2<T>(T u) where T : C1
    {
        return u is string; // Not a union matching
    }   
}
```

### Should post-condition attributes affect default nullability of a Union instance?

*Note*: The default nullability rule mentioned below has been removed. And we no longer infer default nullability 
of `Value` property from union creation methods. Therefore, the question is obsolete/no longer applicable to the current design. 
> For union types where none of the case types are nullable, the default state for `Value` is "not null" rather than "maybe null". 

Is the warning expected in the following scenario
``` c#
#nullable enable

struct S1 : System.Runtime.CompilerServices.IUnion
{
    public S1(int x) => throw null!;
    public S1([System.Diagnostics.CodeAnalysis.NotNull] bool? x) => throw null!;
    object? System.Runtime.CompilerServices.IUnion.Value => throw null!;
}
class Program
{
    static void Test2(S1 s)
    {
       // warning CS8655: The switch expression does not handle some null inputs (it is not exhaustive).
       //                 For example, the pattern 'null' is not covered.
        _ = s switch { int => 1, bool => 3 }; // 
    } 
}
```

### Union conversions

#### [Resolved] Where do they belong among other conversions priority-wise?

Union conversions feel like another form of a user-defined conversion. Therefore, current implementation
classifies them right after a failed attempt to classify an implicit user-defined conversion, and, in case
of existence is treated as just another form of a user-defined conversion. This has the
following consequences:
- An implicit user-defined conversion takes priority over a union conversion
- When explicit cast is used in code, an explicit user-defined conversion takes priority over a union conversion 
- When there is no explicit cast in code, a union conversion takes priority over an explicit user-defined conversion 

``` c#
struct S1 : System.Runtime.CompilerServices.IUnion
{
    public S1(int x) => ...
    public S1(string x) => ...
    object System.Runtime.CompilerServices.IUnion.Value => ...
    public static implicit operator S1(int x) => ...
}

struct S2 : System.Runtime.CompilerServices.IUnion
{
    public S2(int x) => ...
    public S2(string x) => ...
    object System.Runtime.CompilerServices.IUnion.Value => ...
    public static explicit operator S2(int x) => ...
}

class Program
{
    static S1 Test1() => 10; // implicit operator S1(int x) is used
    static S1 Test2() => (S1)20; // implicit operator S1(int x) is used
    static S2 Test3() => 10; // Union conversion S2.S2(int) is used
    static S2 Test4() => (S2)20; // explicit operator S2(int x)
}
```

Need to confirm this is the behavior that we like. Otherwise the conversion rules should be clarified.

**Resolution:**

Approved by the working group.

#### [Resolved] Ref-ness of constructor's parameter

Currently language allows only by-value and `in` parameters for user-defined conversion operators.
It feels like reasons for this restriction are also applicable to constructors suitable for union
conversions. 

**Proposal:**

Adjust definition of a `case type constructor` in `Union types` section above:
``` diff
-For each public constructor with exactly one parameter, the type of that parameter is considered a *case type* of the union type.
+For each public constructor with exactly one **by-value or `in`** parameter, the type of that parameter is considered a *case type* of the union type.
```

**Resolution:**

Approved by the working group for now. However, we might consider "splitting" the set of case type constructors
and the set of constructors suitable for union type conversions.

#### [Resolved] Nullable Conversions

[Nullable Conversions](https://github.com/dotnet/csharpstandard/blob/09d5f56455cab8868ee9798de8807a2e91fb431f/standard/conversions.md#1061-nullable-conversions) section explicitly lists conversions that can be used as underlying. Current specification doesn't propose
any adjustments to that list. This result in an error for the following scenario:
``` c#
struct S1 : System.Runtime.CompilerServices.IUnion
{
    public S1(int x) => throw null;
    public S1(string x) => throw null;
    object System.Runtime.CompilerServices.IUnion.Value => throw null;
}

class Program
{
    static S1? Test1(int x)
    {
        return x; // error CS0029: Cannot implicitly convert type 'int' to 'S1?'
    }   
}
```

**Proposal:**

Adjust the specification to support an implicit nullable conversion from `S` to `T?` backed by a union conversion.
Specifically, assuming `T` is a union type there's an implicit conversion to a type `T?` from a type or
expression `E` if there's a union conversion from `E` to a type `C` and `C` is a case type of `T`.
Note, there is no requirement for type of `E` to be a non-nullable value type.
The conversion is evaluated as the underlying union conversion from `S` to `T` followed by a wrapping from `T` to `T?`

**Resolution:**

Approved.

#### [Resolved] Lifted conversions

Do we want to adjust [Lifted conversions](https://github.com/dotnet/csharpstandard/blob/09d5f56455cab8868ee9798de8807a2e91fb431f/standard/conversions.md#1062-lifted-conversions)
section to support lifted union conversions? Currently they are not allowed:

``` c#
struct S1 : System.Runtime.CompilerServices.IUnion
{
    public S1(int x) => throw null;
    public S1(string x) => throw null;
    object System.Runtime.CompilerServices.IUnion.Value => throw null;
}

class Program
{
    static S1 Test1(int? x)
    {
        return x; // error CS0029: Cannot implicitly convert type 'int?' to 'S1'
    }   

    static S1? Test2(int? y)
    {
        return y; // error CS0029: Cannot implicitly convert type 'int?' to 'S1?'
    }   
}
```

**Resolution:**

No lifted union conversions for now.
Some notes from the discussion:
> The analogy to the user defined conversions breaks down a little here.
> In general unions are able to contain a null value that comes in.
> It is not clear whether lifting should create an instance of a union type with `null`
> value stored in it, or whether it should create a `null` value of `Nullable<Union>`.

#### [Resolved] Block union conversion from an instance of a base type?

One might find the current behavior confusing:
``` c#
struct S1 : System.Runtime.CompilerServices.IUnion
{
    public S1(System.ValueType x)
    {
    }
    public S1(string x) => throw null;
    object System.Runtime.CompilerServices.IUnion.Value => throw null;
}

class Program
{
    static S1 Test1(System.ValueType x)
    {
        return x; // Union conversion
    }   

    static S1 Test2(System.ValueType y)
    {
        return (S1)y; // Unboxing conversion
    }   
}
```

Note, language explicitly disallows declaring user-defined conversions from a base type. Therefore, it might make sence to not
allow union conversions like that.

**Resolution:**

Do nothing special for now. Generic scenarios cannot be fully protected anyway.

#### [Resolved] Block union conversion from an instance of an interface type?

One might find the current behavior confusing:
``` c#
struct S1 : I1, System.Runtime.CompilerServices.IUnion
{
    public S1(I1 x) => throw null;
    public S1(string x) => throw null;
    object System.Runtime.CompilerServices.IUnion.Value => throw null;
}

interface I1 { }

struct S2 : System.Runtime.CompilerServices.IUnion
{
    public S2(I1 x) => throw null;
    public S2(string x) => throw null;
    object System.Runtime.CompilerServices.IUnion.Value => throw null;
}

class C3 : System.Runtime.CompilerServices.IUnion
{
    public C3(I1 x) => throw null;
    public C3(string x) => throw null;
    object System.Runtime.CompilerServices.IUnion.Value => throw null;
}

class Program
{
    static S1 Test1(I1 x)
    {
        return x; // Union conversion
    }   

    static S1 Test2(I1 x)
    {
        return (S1)x; // Unboxing
    }   

    static S2 Test3(I1 x)
    {
        return x; // Union conversion
    }   

    static S2 Test4(I1 x)
    {
        return (S2)x; // Union conversion
    }   

    static C3 Test3(I1 x)
    {
        return x; // Union conversion
    }   

    static C3 Test4(I1 x)
    {
        return (C3)x; // Reference conversion
    }   
}
```

Note, language explicitly disallows declaring user-defined conversions from a base type. Therefore, it might make sence to not
allow union conversions like that.

**Resolution:**

Do nothing special for now. Generic scenarios cannot be fully protected anyway.

### Namespace of IUnion interface

Containing namespace for `IUnion` interface remains unspecified. If the intent is to keep it in a `global` namespace,
Let’s state that explicitly. 

**Proposal**: If this is something simply overlooked,  we could use `System.Runtime.CompilerServices` namespace.

### Classes as `Union` types

#### [Resolved] Checking instance itself for `null`

If a union type is a class type, it's value might itself be null. What about null checks then?
The `null` pattern has been co-opted to check the `Value` property, so how do you check that the union itself isn't null?

For example:
-	When `S` is a `Union` struct, ```s is null``` for a value of `S?`is `true`only when `s` itself is `null`. 
When `C` is a `Union` class, ```c is null``` for a value of `C?`is `false`when `c` itself is `null`,
but it is `true` when `c` itself is not `null`and `c.Value` is `null`.

Another example:
``` c#
class C1 : IUnion
{
    private readonly object? _value;

    public C1(){}
    public C1(int x) { _value = x; }
    public C1(string x) { _value = x; }
    object? IUnion.Value => _value;
}

class Program
{
    static int Test1(C1? u)
    {
        // warning CS8655: The switch expression does not handle some null inputs (it is not exhaustive).
        //                 For example, the pattern 'null' is not covered.
        // This is very confusing, the switch expression is indeed not exhaustive (u itself is not
        // checked for null), but there is a case 'null => 3' in the switch expression. 
        // It looks like the only way to shut off the warning is to use 'case _'. Adding it removes
        // all benefits of exhaustiveness checking, any union case could be missing and there would
        // be no diagnostic about that.  
        return u switch { int => 1, string => 2, null => 3 };
    }
}
```

This part of the design is clearly optimized around the expectation that a union type is a struct.
Some options:
 - Too bad. Use `==` for your null check instead of a pattern match.
 - Let the `null` pattern (and implicit null check in other patterns) apply to both the union value and its `Value` property: `u is null ==> u == null || u.Value == null`.
 - Disallow classes from being union types!

#### [Resolved] Deriving from a `Union` class

When a class uses a `Union`class as its base class, according to the current specification,
it becomes a `Union`class itself. This happens because it automatically “inherits” implementation
of `IUnion` interface, it is not required to re-implement it. At the same time, constructors of the
derived type define the set of types in this new `Union`. It is very easy to get to very strange language
behavior around the two classes:

``` c#
class C1 : IUnion
{
    private readonly object _value;
    public C1(long x) { _value = x; }
    public C1(string x) { _value = x; }
    object IUnion.Value => _value;
}

class C2(int x) : C1(x);

class Program
{
    static int Test1(C1 u)
    {
        // Good
        return u switch { long => 1, string => 2, null => 3 };
    } 

    static int Test2(C2 u)
    {
        // error CS8121: An expression of type 'C2' cannot be handled by a pattern of type 'long'.
        // error CS8121: An expression of type 'C2' cannot be handled by a pattern of type 'string'.
        return u switch { long => 1, string => 2, null => 3 };
    } 
}
```

Some options:
 - Change when a class type is a `Union` type. For example, a class is a `Union` type when all true:
   * It is `sealed` because derived types won't be considered as `Union`types, allowing which is confusing.
   * None of its bases implement `IUnion`
     
   This is still not perfect. The rules are too subtle. It is easy to make a mistake. There is no diagnostic on
   the declaration, but `Union` matching doesn’t work.    
 - Disallow classes from being union types.

### [Resolved] The is-type operator

[The is-type operator](https://github.com/dotnet/csharpstandard/blob/draft-v8/standard/expressions.md#1214121-the-is-type-operator)
is specified as a runtime type check. Syntactically it looks very much like a type pattern, but it isn’t. Therefore, the special `Union`matching
won’t be used, which could lead to a user confusion.  

``` c#
struct S1 : IUnion
{
    private readonly object _value;
    public S1(int x) { _value = x; }
    public S1(string x) { _value = x; }
    object IUnion.Value => _value;
}

class Program
{
    static bool Test1(S1 u)
    {
        return u is int; // warning CS0184: The given expression is never of the provided ('int') type
    }   

    static bool Test2(S1 u)
    {
        return u is string and ['1', .., '2']; // Good
    }   
}
```

In case of a recursive union, the type pattern might give no warning, but it still won’t do what user might think it would do.

**Resolution:**
Should work as a type pattern.

### List pattern

List pattern always fails with `Union` matching:
``` c#
struct S1 : IUnion
{
    private readonly object _value;
    public S1(int[] x) { _value = x; }
    public S1(string[] x) { _value = x; }
    object IUnion.Value => _value;
}

class Program
{
    static bool Test1(S1 u)
    {
        // error CS8985: List patterns may not be used for a value of type 'object'. No suitable 'Length' or 'Count' property was found.
        // error CS0021: Cannot apply indexing with [] to an expression of type 'object'
        return u is [10];
    }   
}

static class Extensions
{
    extension(object o)
    {
        public int Length => 0;
    }
}
```

### Other questions
* Both the use of constructors in union conversions and the use of `TryGetValue(...)` in union pattern matching are specified to be lenient when multiple ones apply: They'll just pick one. This should not matter per the well-formedness rules, but are we comfortable with it?
* The specification subtly relies on the implementation of the `IUnion.Value` property rather than any `Value` property found on the union type itself. This is meant to give greater flexibility for existing types (which may have their own `Value` property for other uses) to implement the pattern. But it is awkward, and inconsistent with how other members are found and used directly on the union type. Should we make a change? Some other options:
    * Require union types to expose a public `Value` property.
    * Prefer a public `Value` property if it exists, but fall back to the `IUnion.Value` implementation if not (similar to `GetEnumerator` rules).
* The proposed union declaration syntax isn't universally loved, particularly when it comes to expressing the case types. Alternatives so far also meet with criticism, but it's possible we will end up making a change. Some top concerns voiced about the current one:
    * Commas as separators between case types may seem to imply that order matters.
    * Parenthesized lists look too much like primary constructors (despite not having parameter names).
    * Too different from enums, which have their "cases" in curly braces.
* While union declarations generate structs with a single reference field, they are still somewhat susceptible to unexpected behavior when used in a concurrent context. For instance, if a user-defined function member dereferences `this` more than once, the containing variable may have been reassigned as a whole by another thread in between the two accesses. The compiler could generate code to copy `this` to a local when necessary. Should it? In general, what degree of concurrency resiliency is desirable and reasonably attainable?
