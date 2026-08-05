# Nullable Reference Types Specification

***This is a work in progress - several parts are missing or incomplete. An updated version of this document can be found in the C# 9 folder. ***

## Syntax

### Nullable reference types

Nullable reference types have the same syntax `T?` as the short form of nullable value types, but do not have a corresponding long form.

For the purposes of the specification, the current `nullable_type` production is renamed to `nullable_value_type`, and a `nullable_reference_type` production is added:

```antlr
reference_type
    : ...
    | nullable_reference_type
    ;
    
nullable_reference_type
    : non_nullable_reference_type '?'
    ;
    
non_nullable_reference_type
    : type
    ;
```

The `non_nullable_reference_type` in a `nullable_reference_type` must be a non-nullable reference type (class, interface, delegate or array), or a type parameter that is constrained to be a non-nullable reference type (through the `class` constraint, or a class other than `object`).

Nullable reference types cannot occur in the following positions:

- as a base class or interface
- as the receiver of a `member_access`
- as the `type` in an `object_creation_expression`
- as the `delegate_type` in a `delegate_creation_expression`
- as the `type` in an `is_expression`, a `catch_clause` or a `type_pattern`
- as the `interface` in a fully qualified interface member name

A warning is given on a `nullable_reference_type` where the nullable annotation context is disabled.

### Nullable class constraint

The `class` constraint has a nullable counterpart `class?`:

```antlr
primary_constraint
    : ...
    | 'class' '?'
    ;
```

### The null-forgiving operator

The post-fix `!` operator is called the null-forgiving operator.

```antlr
primary_expression
    : ...
    | null_forgiving_expression
    ;
    
null_forgiving_expression
    : primary_expression '!'
    ;
```

The `primary_expression` must be of a reference type.  

The postfix `!` operator has no runtime effect - it evaluates to the result of the underlying expression. Its only role is to change the null state of the expression, and to limit warnings given on its use.

### nullable implicitly typed local variables

`var` infers an annotated type for reference types.
For instance, in `var s = "";` the `var` is inferred as `string?`.

### Nullable compiler directives

`#nullable` directives control the nullable annotation and warning contexts.

```antlr
pp_directive
    : ...
    | pp_nullable
    ;
    
pp_nullable
    : whitespace? '#' whitespace? 'nullable' whitespace nullable_action pp_new_line
    ;
    
nullable_action
    : 'disable'
    | 'enable'
    | 'restore'
    ;
```

`#pragma warning` directives are expanded to allow changing the nullable warning context, and to allow individual warnings to be enabled on even when they're disabled by default:

```antlr
pragma_warning_body
    : ...
    | 'warning' whitespace nullable_action whitespace 'nullable'
    ;

warning_action
    : ...
    | 'enable'
    ;
```

Note that the new form of `pragma_warning_body` uses `nullable_action`, not `warning_action`.

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

A given type can have one of four nullabilities: *Oblivious*, *nonnullable*, *nullable* and *unknown*. 

*Nonnullable* and *unknown* types may cause warnings if a potential `null` value is assigned to them. *Oblivious* and *nullable* types, however, are "*null-assignable*" and can have `null` values assigned to them without warnings. 

*Oblivious* and *nonnullable* types can be dereferenced or assigned without warnings. Values of *nullable* and *unknown* types, however, are "*null-yielding*" and may cause warnings when dereferenced or assigned without proper null checking. 

The *default null state* of a null-yielding type is "maybe null". The default null state of a non-null-yielding type is "not null".

The kind of type and the nullable annotation context it occurs in determine its nullability:

- A nonnullable value type `S` is always *nonnullable*
- A nullable value type `S?` is always *nullable*
- An unannotated reference type `C` in a *disabled* annotation context is *oblivious*
- An unannotated reference type `C` in an *enabled* annotation context is *nonnullable*
- A nullable reference type `C?` is *nullable* (but a warning may be yielded in a *disabled* annotation context)

Type parameters additionally take their constraints into account:

- A type parameter `T` where all constraints (if any) are either null-yielding types (*nullable* and *unknown*) or the `class?` constraint is *unknown*
- A type parameter `T` where at least one constraint is either *oblivious* or *nonnullable* or one of the `struct` or `class` constraints is
    - *oblivious* in a *disabled* annotation context
    - *nonnullable* in an *enabled* annotation context
- A nullable type parameter `T?` where at least one of `T`'s constraints is *oblivious* or *nonnullable* or one of the `struct` or `class` constraints, is
    - *nullable* in a *disabled* annotation context (but a warning is yielded)
    - *nullable* in an *enabled* annotation context

For a type parameter `T`, `T?` is only allowed if `T` is known to be a value type or known to be a reference type.

### Nested functions

Nested functions (lambdas and local functions) are treated like methods, except in regards to their captured variables.
The default state of a captured variable inside a lambda or local function is the intersection of the nullable state
of the variable at all the "uses" of that nested function. A use of a function is either a call to that function, or
where it is converted to a delegate.

### Oblivious vs nonnullable

A `type` is deemed to occur in a given annotation context when the last token of the type is within that context.

Whether a given reference type `C` in source code is interpreted as oblivious or nonnullable depends on the annotation context of that source code. But once established, it is considered part of that type, and "travels with it" e.g. during substitution of generic type arguments. It is as if there is an annotation like `?` on the type, but invisible.

## Constraints

Nullable reference types can be used as generic constraints. Furthermore `object` is now valid as an explicit constraint. Absence of a constraint is now equivalent to an `object?` constraint (instead of `object`), but (unlike `object` before) `object?` is not prohibited as an explicit constraint.

`class?` is a new constraint denoting "possibly nullable reference type", whereas `class` denotes "nonnullable reference type".

The nullability of a type argument or of a constraint does not impact whether the type satisfies the constraint, except where that is already the case today (nullable value types do not satisfy the `struct` constraint). However, if the type argument does not satisfy the nullability requirements of the constraint, a warning may be given.

## Null state and null tracking

Every expression in a given source location has a *null state*, which indicated whether it is believed to potentially evaluate to null. The null state is either "not null" or "maybe null". The null state is used to determine whether a warning should be given about null-unsafe conversions and dereferences.

### Null tracking for variables

For certain expressions denoting variables or properties, the null state is tracked between occurrences, based on assignments to them, tests performed on them and the control flow between them. This is similar to how definite assignment is tracked for variables. The tracked expressions are the ones of the following form:

```antlr
tracked_expression
    : simple_name
    | this
    | base
    | tracked_expression '.' identifier
    ;
```

Where the identifiers denote fields or properties.

***Describe null state transitions similar to definite assignment***

### Null state for expressions

The null state of an expression is derived from its form and type, and from the null state of variables involved in it.

### Literals

The null state of a `null` literal is "maybe null". The null state of a `default` literal that is being converted to a type that is known not to be a nonnullable value type is "maybe null". The null state of any other literal is "not null".

### Simple names

If a `simple_name` is not classified as a value, its null state is "not null". Otherwise it is a tracked expression, and its null state is its tracked null state at this source location.

### Member access

If a `member_access` is not classified as a value, its null state is "not null". Otherwise, if it is a tracked expression, its null state is its tracked null state at this source location. Otherwise its null state is the default null state of its type.

### Invocation expressions

If an `invocation_expression` invokes a member that is declared with one or more attributes for special null behavior, the null state is determined by those attributes. Otherwise the null state of the expression is the default null state of its type.

### Element access

If an `element_access` invokes an indexer that is declared with one or more attributes for special null behavior, the null state is determined by those attributes. Otherwise the null state of the expression is the default null state of its type.

### Base access

If `B` denotes the base type of the enclosing type, `base.I` has the same null state as `((B)this).I` and `base[E]` has the same null state as `((B)this)[E]`.

### Default expressions

`default(T)` has the null state "non-null" if `T` is known to be a nonnullable value type. Otherwise it has the null state "maybe null".

### Null-conditional expressions

A `null_conditional_expression` has the null state "maybe null".

### Cast expressions

If a cast expression `(T)E` invokes a user-defined conversion, then the null state of the expression is the default null state for its type. Otherwise, if `T` is null-yielding (*nullable* or *unknown*) then the null state is "maybe null". Otherwise the null state is the same as the null state of `E`.

### Await expressions

The null state of `await E` is the default null state of its type.

### The `as` operator

An `as` expression has the null state "maybe null".

### The null-coalescing operator

`E1 ?? E2` has the same null state as `E2`

### The conditional operator

The null state of `E1 ? E2 : E3` is "not null" if the null state of both `E2` and `E3` are "not null". Otherwise it is "maybe null".

### Query expressions

The null state of a query expression is the default null state of its type.

### Assignment operators

`E1 = E2` and `E1 op= E2` have the same null state as `E2` after any implicit conversions have been applied.

### Unary and binary operators

If a unary or binary operator invokes an user-defined operator that is declared with one or more attributes for special null behavior, the null state is determined by those attributes. Otherwise the null state of the expression is the default null state of its type.

***Something special to do for binary `+` over strings and delegates?***

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

## Type inference

### Type inference for `var`

The type inferred for local variables declared with `var` is informed by the null state of the initializing expression.

```csharp
var x = E;
```

If the type of `E` is a nullable reference type `C?` and the null state of `E` is "not null" then the type inferred for `x` is `C`. Otherwise, the inferred type is the type of `E`.

The nullability of the type inferred for `x` is determined as described above, based on the annotation context of the `var`, just as if the type had been given explicitly in that position.

### Type inference for `var?`

The type inferred for local variables declared with `var?` is independent of the null state of the initializing expression.

```csharp
var? x = E;
```

If the type `T` of `E` is a nullable value type or a nullable reference type then the type inferred for `x` is `T`. Otherwise, if `T` is a nonnullable value type `S` the type inferred is `S?`. Otherwise, if `T` is a nonnullable reference type `C` the type inferred is `C?`. Otherwise, the declaration is illegal.

The nullability of the type inferred for `x` is always *nullable*.

### Generic type inference

Generic type inference is enhanced to help decide whether inferred reference types should be nullable or not. This is a best effort, and does not in and of itself yield warnings, but may lead to nullable warnings when the inferred types of the selected overload are applied to the arguments.

The type inference does not rely on the annotation context of incoming types. Instead a `type` is inferred which acquires its own annotation context from where it "would have been" if it had been expressed explicitly. This underscores the role of type inference as a convenience for what you could have written yourself.

More precisely, the annotation context for an inferred type argument is the context of the token that would have been followed by the `<...>` type parameter list, had there been one; i.e. the name of the generic method being called. For query expressions that translate to such calls, the context is taken from the initial contextual keyword of the query clause from which the call is generated.

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

## Attributes for special null behavior


