# Nullable Reference Types Specification

***This is a work in progress - several parts are missing or incomplete.***

This feature adds two new kinds of nullable types (nullable reference types and nullable generic types) to the existing nullable value types, and introduces a static flow analysis for purpose of null-safety.

## Syntax

### Nullable reference types and nullable type parameters

Nullable reference types and nullable type parameters have the same syntax `T?` as the short form of nullable value types, but do not have a corresponding long form.

For the purposes of the specification, the current `nullable_type` production is renamed to `nullable_value_type`, and `nullable_reference_type` and `nullable_type_parameter` productions are added:

```antlr
type
    : value_type
    | reference_type
    | nullable_type_parameter
    | type_parameter
    | type_unsafe
    ;

reference_type
    : ...
    | nullable_reference_type
    ;

nullable_reference_type
    : non_nullable_reference_type '?'
    ;

non_nullable_reference_type
    : reference_type
    ;

nullable_type_parameter
    : non_nullable_non_value_type_parameter '?'
    ;

non_nullable_non_value_type_parameter
    : type_parameter
    ;
```

The `non_nullable_reference_type` in a `nullable_reference_type` must be a nonnullable reference type (class, interface, delegate or array).

The `non_nullable_non_value_type_parameter` in `nullable_type_parameter` must be a type parameter that isn't constrained to be a value type.

Nullable reference types and nullable type parameters cannot occur in the following positions:

- as a base class or interface
- as the receiver of a `member_access`
- as the `type` in an `object_creation_expression`
- as the `delegate_type` in a `delegate_creation_expression`
- as the `type` in an `is_expression`, a `catch_clause` or a `type_pattern`
- as the `interface` in a fully qualified interface member name

A warning is given on a `nullable_reference_type` and `nullable_type_parameter` in a *disabled* nullable annotation context.

### `class` and `class?` constraint

The `class` constraint has a nullable counterpart `class?`:

```antlr
primary_constraint
    : ...
    | 'class' '?'
    ;
```

A type parameter constrained with `class` (in an *enabled* annotation context) must be instantiated with a nonnullable reference type.

A type parameter constrained with `class?` (or `class` in a *disabled* annotation context) may either be instantiated with a nullable or nonnullable reference type.

A warning is given on a `class?` constraint in a *disabled* annotation context.

### `notnull` constraint

A type parameter constrained with `notnull` may not be a nullable type (nullable value type, nullable reference type or nullable type parameter).

```antlr
primary_constraint
    : ...
    | 'notnull'
    ;
```

### `default` constraint

The `default` constraint can be used on a method override or explicit implementation to disambiguate `T?` meaning "nullable type parameter" from "nullable value type" (`Nullable<T>`). Lacking the `default` constraint a `T?` syntax in an override or explicit implementation will be interpreted as `Nullable<T>`

See https://github.com/dotnet/csharplang/blob/master/proposals/csharp-9.0/unconstrained-type-parameter-annotations.md#default-constraint

### The null-forgiving operator

The post-fix `!` operator is called the null-forgiving operator. It can be applied on a *primary_expression* or within a *null_conditional_expression*:

```antlr
primary_expression
    : ...
    | null_forgiving_expression
    ;

null_forgiving_expression
    : primary_expression '!'
    ;

null_conditional_expression
    : primary_expression null_conditional_operations_no_suppression suppression?
    ;

null_conditional_operations_no_suppression
    : null_conditional_operations? '?' '.' identifier type_argument_list?
    | null_conditional_operations? '?' '[' argument_list ']'
    | null_conditional_operations '.' identifier type_argument_list?
    | null_conditional_operations '[' argument_list ']'
    | null_conditional_operations '(' argument_list? ')'
    ;

null_conditional_operations
    : null_conditional_operations_no_suppression suppression?
    ;

suppression
    : '!'
    ;
```

For example:

```csharp
var v = expr!;
expr!.M();
_ = a?.b!.c;
```

The `primary_expression` and `null_conditional_operations_no_suppression` must be of a nullable type.

The postfix `!` operator has no runtime effect - it evaluates to the result of the underlying expression. Its only role is to change the null state of the expression to "not null", and to limit warnings given on its use.

### Nullable compiler directives

`#nullable` directives control the nullable annotation and warning contexts.

```antlr
pp_directive
    : ...
    | pp_nullable
    ;

pp_nullable
    : whitespace? '#' whitespace? 'nullable' whitespace nullable_action (whitespace nullable_target)? pp_new_line
    ;

nullable_action
    : 'disable'
    | 'enable'
    | 'restore'
    ;

nullable_target
    : 'warnings'
    | 'annotations'
    ;
```

`#pragma warning` directives are expanded to allow changing the nullable warning context:

```antlr
pragma_warning_body
    : ...
    | 'warning' whitespace warning_action whitespace 'nullable'
    ;
```

For example:

```csharp
#pragma warning disable nullable
```

## Nullable contexts

Every line of source code has a *nullable annotation context* and a *nullable warning context*. These control whether nullable annotations have effect, and whether nullability warnings are given. The annotation context of a given line is either *disabled* or *enabled*. The warning context of a given line is either *disabled* or *enabled*.

Both contexts can be specified at the project level (outside of C# source code), or anywhere within a source file via `#nullable` pre-processor directives. If no project level settings are provided the default is for both contexts to be *disabled*.

The `#nullable` directive controls the annotation and warning contexts within the source text, and take precedence over the project-level settings.

A directive sets the context(s) it controls for subsequent lines of code, until another directive overrides it, or until the end of the source file.

The effect of the directives is as follows:

- `#nullable disable`: Sets the nullable annotation and warning contexts to *disabled*
- `#nullable enable`: Sets the nullable annotation and warning contexts to *enabled*
- `#nullable restore`: Restores the nullable annotation and warning contexts to project settings
- `#nullable disable annotations`: Sets the nullable annotation context to *disabled*
- `#nullable enable annotations`: Sets the nullable annotation context to *enabled*
- `#nullable restore annotations`: Restores the nullable annotation context to project settings
- `#nullable disable warnings`: Sets the nullable warning context to *disabled*
- `#nullable enable warnings`: Sets the nullable warning context to *enabled*
- `#nullable restore warnings`: Restores the nullable warning context to project settings

## Nullability of types

A given type can have one of three nullabilities: *oblivious*, *nonnullable*, and *nullable*.

*Nonnullable* types may cause warnings if a potential `null` value is assigned to them. *Oblivious* and *nullable* types, however, are "*null-assignable*" and can have `null` values assigned to them without warnings.

Values of *oblivious* and *nonnullable* types can be dereferenced or assigned without warnings. Values of *nullable* types, however, are "*null-yielding*" and may cause warnings when dereferenced or assigned without proper null checking.

The *default null state* of a null-yielding type is "maybe null" or "maybe default". The default null state of a non-null-yielding type is "not null".

The kind of type and the nullable annotation context it occurs in determine its nullability:

- A nonnullable value type `S` is always *nonnullable*
- A nullable value type `S?` is always *nullable*
- An unannotated reference type `C` in a *disabled* annotation context is *oblivious*
- An unannotated reference type `C` in an *enabled* annotation context is *nonnullable*
- A nullable reference type `C?` is *nullable* (but a warning may be yielded in a *disabled* annotation context)

Type parameters additionally take their constraints into account:

- A type parameter `T` where all constraints (if any) are either nullable types or the `class?` constraint is *nullable*
- A type parameter `T` where at least one constraint is either *oblivious* or *nonnullable* or one of the `struct` or `class` or `notnull` constraints is
    - *oblivious* in a *disabled* annotation context
    - *nonnullable* in an *enabled* annotation context
- A nullable type parameter `T?` is *nullable*, but a warning is yielded in a *disabled* annotation context if `T` isn't a value type

### Oblivious vs nonnullable

A `type` is deemed to occur in a given annotation context when the last token of the type is within that context.

Whether a given reference type `C` in source code is interpreted as oblivious or nonnullable depends on the annotation context of that source code. But once established, it is considered part of that type, and "travels with it" e.g. during substitution of generic type arguments. It is as if there is an annotation like `?` on the type, but invisible.

## Constraints

Nullable reference types can be used as generic constraints.

`class?` is a new constraint denoting "possibly nullable reference type", whereas `class` in an *enabled* annotation context denotes "nonnullable reference type".

`default` is a new constraint denoting a type parameter that isn't known to be a reference or value type. It can only be used on overridden and explicitly implemented methods. With this constraint, `T?` means a nullable type parameter, as opposed to being a shorthand for `Nullable<T>`.

`notnull` is a new constraint denoting a type parameter that is nonnullable.

The nullability of a type argument or of a constraint does not impact whether the type satisfies the constraint, except where that is already the case today (nullable value types do not satisfy the `struct` constraint). However, if the type argument does not satisfy the nullability requirements of the constraint, a warning may be given.

## Null state and null tracking

Every expression in a given source location has a *null state*, which indicated whether it is believed to potentially evaluate to null. The null state is either "not null", "maybe null", or "maybe default". The null state is used to determine whether a warning should be given about null-unsafe conversions and dereferences.

The distinction between "maybe null" and "maybe default" is subtle and applies to type parameters. The distinction is that a type parameter `T` which has the state "maybe null" means the value is in the domain of legal values for `T` however that legal value may include `null`. Where as a "maybe default" means that the value may be outside the legal domain of values for `T`. 

Example: 

```c#
// The value `t` here has the state "maybe null". It's possible for `T` to be instantiated
// with `string?` in which case `null` would be within the domain of legal values here. The 
// assumption though is the value provided here is within the legal values of `T`. Hence 
// if `T` is `string` then `null` will not be a value, just as we assume that `null` is not
// provided for a normal `string` parameter
void M<T>(T t)
{
    // There is no guarantee that default(T) is within the legal values for T hence the 
    // state *must* be "maybe-default" and hence `local` must be `T?`
    T? local = default(T);
}
```

### Null tracking for variables

For certain expressions denoting variables, fields or properties, the null state is tracked between occurrences, based on assignments to them, tests performed on them and the control flow between them. This is similar to how definite assignment is tracked for variables. The tracked expressions are the ones of the following form:

```antlr
tracked_expression
    : simple_name
    | this
    | base
    | tracked_expression '.' identifier
    ;
```

Where the identifiers denote fields or properties.

The null state for tracked variables is "not null" in unreachable code. This follows other decisions around unreachable code like considering all locals to be definitely assigned.

***Describe null state transitions similar to definite assignment***

### Null state for expressions

The null state of an expression is derived from its form and type, and from the null state of variables involved in it.

### Literals

The null state of a `null` literal depends on the target type of the expression. If the target type is a type parameter constrained to a reference type then it's "maybe default". Otherwise it is "maybe null".

The null state of a `default` literal depends on the target type of the `default` literal. A `default` literal with target type `T` has the same null state as the `default(T)` expression.

The null state of any other literal is "not null".

### Simple names

If a `simple_name` is not classified as a value, its null state is "not null". Otherwise it is a tracked expression, and its null state is its tracked null state at this source location.

### Member access

If a `member_access` is not classified as a value, its null state is "not null". Otherwise, if it is a tracked expression, its null state is its tracked null state at this source location. Otherwise its null state is the default null state of its type.

```c#
var person = new Person();

// The receiver is a tracked expression hence the member_access of the property 
// is tracked as well 
if (person.FirstName is not null)
{
    Use(person.FirstName);
}

// The return of an invocation is not a tracked expression hence the member_access
// of the return is also not tracked
if (GetAnonymous().FirstName is not null)
{
    // Warning: Cannot convert null literal to non-nullable reference type.
    Use(GetAnonymous().FirstName);
}

void Use(string s) 
{ 
    // ...
}

public class Person
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    private static Person s_anonymous = new Person();
    public static Person GetAnonymous() => s_anonymous;
}
```

### Invocation expressions

If an `invocation_expression` invokes a member that is declared with one or more attributes for special null behavior, the null state is determined by those attributes. Otherwise the null state of the expression is the default null state of its type.

The null state of an `invocation_expression` is not tracked by the compiler.

```c#

// The result of an invocation_expression is not tracked
if (GetText() is not null)
{
    // Warning: Converting null literal or possible null value to non-nullable type.
    string s = GetText();
    // Warning: Dereference of a possibly null reference.
    Use(s);
}

// Nullable friendly pattern
if (GetText() is string s)
{
    Use(s);
}

string? GetText() => ... 
Use(string s) {  }
```

### Element access

If an `element_access` invokes an indexer that is declared with one or more attributes for special null behavior, the null state is determined by those attributes. Otherwise the null state of the expression is the default null state of its type.

```c#
object?[] array = ...;
if (array[0] != null)
{
    // Warning: Converting null literal or possible null value to non-nullable type.
    object o = array[0];
    // Warning: Dereference of a possibly null reference.
    Console.WriteLine(o.ToString());
}

// Nullable friendly pattern
if (array[0] is {} o)
{
    Console.WriteLine(o.ToString());
}
```

### Base access

If `B` denotes the base type of the enclosing type, `base.I` has the same null state as `((B)this).I` and `base[E]` has the same null state as `((B)this)[E]`.

### Default expressions

`default(T)` has the null state based on the properties of the type `T`:

- If the type is a *nonnullable* type then it has the null state "not null"
- Else if the type is a type parameter then it has the null state "maybe default"
- Else it has the null state "maybe null"

### Null-conditional expressions ?.

A `null_conditional_expression` has the null state based on the expression type. Note that this refers to the type of the `null_conditional_expression`, not the original type of the member being invoked:

- If the type is a *nullable* value type then it has the null state "maybe null"
- Else if the type is a *nullable* type parameter then it has the null state "maybe default"
- Else it has the null state "maybe null"

### Cast expressions

If a cast expression `(T)E` invokes a user-defined conversion, then the null state of the expression is the default null state for the type of the user-defined conversion. Otherwise:

- If `T` is a *nonnullable* value type then `T` has the null state "not null"
- Else if `T` is a *nullable* value type then `T` has the null state "maybe null"
- Else if `T` is a *nullable* type in the form `U?` where `U` is a type parameter then `T` has the null state "maybe default"
- Else if `T` is a *nullable* type, and `E` has null state "maybe null" or "maybe default", then `T` has the null state "maybe null"
- Else if `T` is a type parameter, and `E` has null state "maybe null" or "maybe default", then `T` has the null state "maybe default"
- Else `T` has the same null state as `E`

### Unary and binary operators

If a unary or binary operator invokes an user-defined operator then the null state of the expression is the default null state for the type of the user-defined operator. Otherwise it is the null state of the expression.

***Something special to do for binary `+` over strings and delegates?***

### Await expressions

The null state of `await E` is the default null state of its type.

### The `as` operator

The null state of an `E as T` expression depends first on properties of the type `T`. If the type of `T` is *nonnullable* then the null state is "not null". Otherwise the null state depends on the conversion from the type of `E` to type `T`:

- If the conversion is an identity, boxing, implicit reference, or implicit nullable conversion, then the null state is the null state of `E`
- Else if `T` is a type parameter then it has the null state "maybe default"
- Else it has the null state "maybe null"

### The null-coalescing operator

The null state of `E1 ?? E2` is the null state of `E2`

### The conditional operator

The null state of `E1 ? E2 : E3` is based on the null state of `E2` and `E3`:

- If both are "not null" then the null state is "not null"
- Else if either is "maybe default" then the null state is "maybe default"
- Else the null state is "not null"

### Query expressions

The null state of a query expression is the default null state of its type.

*Additional work needed here*

### Assignment operators

`E1 = E2` and `E1 op= E2` have the same null state as `E2` after any implicit conversions have been applied.

### Expressions that propagate null state

`(E)`, `checked(E)` and `unchecked(E)` all have the same null state as `E`.

### Expressions that are never null

The null state of the following expression forms is always "not null":

- `this` access
- interpolated strings
- `new` expressions (object, delegate, anonymous object and array creation expressions)
- `typeof` expressions
- `nameof` expressions
- anonymous functions (anonymous methods and lambda expressions)
- null-forgiving expressions
- `is` expressions

### Nested functions

Nested functions (lambdas and local functions) are treated like methods, except in regards to their captured variables.
The initial state of a captured variable inside a lambda or local function is the intersection of the nullable state
of the variable at all the "uses" of that nested function or lambda. A use of a local function is either a call to that 
function, or where it is converted to a delegate. A use of a lambda is the point at which it is defined in source.

## Type inference

### nullable implicitly typed local variables

`var` infers an annotated type for reference types, and type parameters that aren't constrained to be a value type.
For instance:
- in `var s = "";` the `var` is inferred as `string?`.
- in `var t = new T();` with an unconstrained `T` the `var` is inferred as `T?`.

### Generic type inference

Generic type inference is enhanced to help decide whether inferred reference types should be nullable or not. This is a best effort. It may yield warnings regarding nullability constraints, and may lead to nullable warnings when the inferred types of the selected overload are applied to the arguments.

### The first phase

Nullable reference types flow into the bounds from the initial expressions, as described below. In addition, two new kinds of bounds, namely `null` and `default` are introduced. Their purpose is to carry through occurrences of `null` or `default` in the input expressions, which may cause an inferred type to be nullable, even when it otherwise wouldn't. This works even for nullable *value* types, which are enhanced to pick up "nullness" in the inference process.

The determination of what bounds to add in the first phase are enhanced as follows:

If an argument `Ei` has a reference type, the type `U` used for inference depends on the null state of `Ei` as well as its declared type:
- If the declared type is a nonnullable reference type `U0` or a nullable reference type `U0?` then
    - if the null state of `Ei` is "not null" then `U` is `U0`
    - if the null state of `Ei` is "maybe null" then `U` is `U0?`
- Otherwise if `Ei` has a declared type, `U` is that type
- Otherwise if `Ei` is `null` then `U` is the special bound `null`
- Otherwise if `Ei` is `default` then `U` is the special bound `default`
- Otherwise no inference is made.

### Exact, upper-bound and lower-bound inferences

In inferences *from* the type `U` *to* the type `V`, if `V` is a nullable reference type `V0?`, then `V0` is used instead of `V` in the following clauses.
- If `V` is one of the unfixed type variables, `U` is added as an exact, upper or lower bound as before
- Otherwise, if `U` is `null` or `default`, no inference is made
- Otherwise, if `U` is a nullable reference type `U0?`, then `U0` is used instead of `U` in the subsequent clauses.

The essence is that nullability that pertains directly to one of the unfixed type variables is preserved into its bounds. For the inferences that recurse further into the source and target types, on the other hand, nullability is ignored. It may or may not match, but if it doesn't, a warning will be issued later if the overload is chosen and applied.

### Fixing

The spec currently does not do a good job of describing what happens when multiple bounds are identity convertible to each other, but are different. This may happen between `object` and `dynamic`, between tuple types that differ only in element names, between types constructed thereof and now also between `C` and `C?` for reference types.

In addition we need to propagate "nullness" from the input expressions to the result type.

To handle these we add more phases to fixing, which is now:

1. Gather all the types in all the bounds as candidates, removing `?` from all that are nullable reference types
2. Eliminate candidates based on requirements of exact, lower and upper bounds (keeping `null` and `default` bounds)
3. Eliminate candidates that do not have an implicit conversion to all the other candidates
4. If the remaining candidates do not all have identity conversions to one another, then type inference fails
5. *Merge* the remaining candidates as described below
6. If the resulting candidate is a reference type or a nonnullable value type and *all* of the exact bounds or *any* of the lower bounds are nullable value types, nullable reference types, `null` or `default`, then `?` is added to the resulting candidate, making it a nullable value type or reference type.

*Merging* is described between two candidate types. It is transitive and commutative, so the candidates can be merged in any order with the same ultimate result. It is undefined if the two candidate types are not identity convertible to each other.

The *Merge* function takes two candidate types and a direction (*+* or *-*):

- *Merge*(`T`, `T`, *d*) = T
- *Merge*(`S`, `T?`, *+*) = *Merge*(`S?`, `T`, *+*) = *Merge*(`S`, `T`, *+*)`?`
- *Merge*(`S`, `T?`, *-*) = *Merge*(`S?`, `T`, *-*) = *Merge*(`S`, `T`, *-*)
- *Merge*(`C<S1,...,Sn>`, `C<T1,...,Tn>`, *+*) = `C<`*Merge*(`S1`, `T1`, *d1*)`,...,`*Merge*(`Sn`, `Tn`, *dn*)`>`, *where*
    - `di` = *+* if the `i`'th type parameter of `C<...>` is covariant
    - `di` = *-* if the `i`'th type parameter of `C<...>` is contra- or invariant
- *Merge*(`C<S1,...,Sn>`, `C<T1,...,Tn>`, *-*) = `C<`*Merge*(`S1`, `T1`, *d1*)`,...,`*Merge*(`Sn`, `Tn`, *dn*)`>`, *where*
    - `di` = *-* if the `i`'th type parameter of `C<...>` is covariant
    - `di` = *+* if the `i`'th type parameter of `C<...>` is contra- or invariant
- *Merge*(`(S1 s1,..., Sn sn)`, `(T1 t1,..., Tn tn)`, *d*) = `(`*Merge*(`S1`, `T1`, *d*)`n1,...,`*Merge*(`Sn`, `Tn`, *d*) `nn)`, *where*
    - `ni` is absent if `si` and `ti` differ, or if both are absent
    - `ni` is `si` if `si` and `ti` are the same
- *Merge*(`object`, `dynamic`) = *Merge*(`dynamic`, `object`) = `dynamic`

## Warnings

### Potential null assignment

### Potential null dereference

### Constraint nullability mismatch

### Nullable types in disabled annotation context

### Override and implementation nullability mismatch

## Attributes for special null behavior

