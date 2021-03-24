# Recursive Pattern Matching

## Summary
[summary]: #summary

Pattern matching extensions for C# enable many of the benefits of algebraic data types and pattern matching from functional languages, but in a way that smoothly integrates with the feel of the underlying language. Elements of this approach are inspired by related features in the programming languages [F#](http://www.msr-waypoint.net/pubs/79947/p29-syme.pdf "Extensible Pattern Matching Via a Lightweight Language") and [Scala](https://link.springer.com/content/pdf/10.1007%2F978-3-540-73589-2.pdf "Matching Objects With Patterns, page 273").

## Detailed design
[design]: #detailed-design

### Is Expression

The `is` operator is extended to test an expression against a *pattern*.

```antlr
relational_expression
    : is_pattern_expression
    ;
is_pattern_expression
    : relational_expression 'is' pattern
    ;
```

This form of *relational_expression* is in addition to the existing forms in the C# specification. It is a compile-time error if the *relational_expression* to the left of the `is` token does not designate a value or does not have a type.

Every *identifier* of the pattern introduces a new local variable that is *definitely assigned* after the `is` operator is `true` (i.e. *definitely assigned when true*).

> Note: There is technically an ambiguity between *type* in an `is-expression` and *constant_pattern*, either of which might be a valid parse of a qualified identifier. We try to bind it as a type for compatibility with previous versions of the language; only if that fails do we resolve it as we do an expression in other contexts, to the first thing found (which must be either a constant or a type). This ambiguity is only present on the right-hand-side of an `is` expression.

### Patterns

Patterns are used in the *is_pattern* operator, in a *switch_statement*, and in a *switch_expression* to express the shape of data against which incoming data  (which we call the input value) is to be compared. Patterns may be recursive so that parts of the data may be matched against sub-patterns.

```antlr
pattern
    : declaration_pattern
    | constant_pattern
    | var_pattern
    | positional_pattern
    | property_pattern
    | discard_pattern
    ;
declaration_pattern
    : type simple_designation
    ;
constant_pattern
    : constant_expression
    ;
var_pattern
    : 'var' designation
    ;
positional_pattern
    : type? '(' subpatterns? ')' property_subpattern? simple_designation?
    ;
subpatterns
    : subpattern
    | subpattern ',' subpatterns
    ;
subpattern
    : pattern
    | identifier ':' pattern
    ;
property_subpattern
    : '{' '}'
    | '{' subpatterns ','? '}'
    ;
property_pattern
    : type? property_subpattern simple_designation?
    ;
simple_designation
    : single_variable_designation
    | discard_designation
    ;
discard_pattern
    : '_'
    ;
```

#### Declaration Pattern

```antlr
declaration_pattern
    : type simple_designation
    ;
```

The *declaration_pattern* both tests that an expression is of a given type and casts it to that type if the test succeeds. This may introduce a local variable of the given type named by the given identifier, if the designation is a *single_variable_designation*. That local variable is *definitely assigned* when the result of the pattern-matching operation is `true`.

The runtime semantic of this expression is that it tests the runtime type of the left-hand *relational_expression* operand against the *type* in the pattern.  If it is of that runtime type (or some subtype) and not `null`, the result of the `is operator` is `true`.

Certain combinations of static type of the left-hand-side and the given type are considered incompatible and result in compile-time error. A value of static type `E` is said to be *pattern-compatible* with a type `T` if there exists an identity conversion, an implicit reference conversion, a boxing conversion, an explicit reference conversion, or an unboxing conversion from `E` to `T`, or if one of those types is an open type. It is a compile-time error if an input of type `E` is not *pattern-compatible* with the *type* in a type pattern that it is matched with.

The type pattern is useful for performing run-time type tests of reference types, and replaces the idiom

```csharp
var v = expr as Type;
if (v != null) { // code using v
```

With the slightly more concise

```csharp
if (expr is Type v) { // code using v
```

It is an error if *type* is a nullable value type.

The type pattern can be used to test values of nullable types: a value of type `Nullable<T>` (or a boxed `T`) matches a type pattern `T2 id` if the value is non-null and the type of `T2` is `T`, or some base type or interface of `T`. For example, in the code fragment

```csharp
int? x = 3;
if (x is int v) { // code using v
```

The condition of the `if` statement is `true` at runtime and the variable `v` holds the value `3` of type `int` inside the block. After the block the variable `v` is in scope but not definitely assigned.

#### Constant Pattern

```antlr
constant_pattern
    : constant_expression
    ;
```

A constant pattern tests the value of an expression against a constant value. The constant may be any constant expression, such as a literal, the name of a declared `const` variable, or an enumeration constant. When the input value is not an open type, the constant expression is implicitly converted to the type of the matched expression; if the type of the input value is not *pattern-compatible* with the type of the constant expression, the pattern-matching operation is an error.

The pattern *c* is considered matching the converted input value *e* if `object.Equals(c, e)` would return `true`.

We expect to see `e is null` as the most common way to test for `null` in newly written code, as it cannot invoke a user-defined `operator==`.

#### Var Pattern

```antlr
var_pattern
    : 'var' designation
    ;
designation
    : simple_designation
    | tuple_designation
    ;
simple_designation
    : single_variable_designation
    | discard_designation
    ;
single_variable_designation
    : identifier
    ;
discard_designation
    : _
    ;
tuple_designation
    : '(' designations? ')'
    ;
designations
    : designation
    | designations ',' designation
    ;
```

If the *designation* is a *simple_designation*, an expression *e* matches the pattern. In other words, a match to a *var pattern* always succeeds with a *simple_designation*. If the *simple_designation* is a *single_variable_designation*, the value of *e* is bounds to a newly introduced local variable. The type of the local variable is the static type of *e*.

If the *designation* is a *tuple_designation*, then the pattern is equivalent to a *positional_pattern* of the form `(var` *designation*, ... `)` where the *designation*s are those found within the *tuple_designation*.  For example, the pattern `var (x, (y, z))` is equivalent to `(var x, (var y, var z))`.

It is an error if the name `var` binds to a type.

#### Discard Pattern

```antlr
discard_pattern
    : '_'
    ;
```

An expression *e* matches the pattern `_` always. In other words, every expression matches the discard pattern.

A discard pattern may not be used as the pattern of an *is_pattern_expression*.

#### Positional Pattern

A positional pattern checks that the input value is not `null`, invokes an appropriate `Deconstruct` method, and performs further pattern matching on the resulting values.  It also supports a tuple-like pattern syntax (without the type being provided) when the type of the input value is the same as the type containing `Deconstruct`, or if the type of the input value is a tuple type, or if the type of the input value is `object` or `ITuple` and the runtime type of the expression implements `ITuple`.

```antlr
positional_pattern
    : type? '(' subpatterns? ')' property_subpattern? simple_designation?
    ;
subpatterns
    : subpattern
    | subpattern ',' subpatterns
    ;
subpattern
    : pattern
    | identifier ':' pattern
    ;
```

If the *type* is omitted, we take it to be the static type of the input value.

Given a match of an input value to the pattern *type* `(` *subpattern_list* `)`, a method is selected by searching in *type* for accessible declarations of `Deconstruct` and selecting one among them using the same rules as for the deconstruction declaration.

It is an error if a *positional_pattern* omits the type, has a single *subpattern* without an *identifier*, has no *property_subpattern* and has no *simple_designation*. This disambiguates between a *constant_pattern* that is parenthesized and a *positional_pattern*.

In order to extract the values to match against the patterns in the list,
- If *type* was omitted and the input value's type is a tuple type, then the number of subpatterns is required to be the same as the cardinality of the tuple. Each tuple element is matched against the corresponding *subpattern*, and the match succeeds if all of these succeed. If any *subpattern* has an *identifier*, then that must name a tuple element at the corresponding position in the tuple type.
- Otherwise, if a suitable `Deconstruct` exists as a member of *type*, it is a compile-time error if the type of the input value is not *pattern-compatible* with *type*. At runtime the input value is tested against *type*. If this fails then the positional pattern match fails. If it succeeds,  the input value is converted to this type and `Deconstruct` is invoked with fresh compiler-generated variables to receive the `out` parameters. Each value that was received is matched against the corresponding *subpattern*, and the match succeeds if all of these succeed. If any *subpattern* has an *identifier*, then that must name a parameter at the corresponding position of `Deconstruct`.
- Otherwise if *type* was omitted, and the input value is of type `object` or `ITuple` or some type that can be converted to `ITuple` by an implicit reference conversion, and no *identifier* appears among the subpatterns, then we match using `ITuple`.
- Otherwise the pattern is a compile-time error.

The order in which subpatterns are matched at runtime is unspecified, and a failed match may not attempt to match all subpatterns.

##### Example

This example uses many of the features described in this specification

``` c#
    var newState = (GetState(), action, hasKey) switch {
        (DoorState.Closed, Action.Open, _) => DoorState.Opened,
        (DoorState.Opened, Action.Close, _) => DoorState.Closed,
        (DoorState.Closed, Action.Lock, true) => DoorState.Locked,
        (DoorState.Locked, Action.Unlock, true) => DoorState.Closed,
        (var state, _, _) => state };
```

#### Property Pattern

A property pattern checks that the input value is not `null` and recursively matches values extracted by the use of accessible properties or fields.

```antlr
property_pattern
    : type? property_subpattern simple_designation?
    ;
property_subpattern
    : '{' '}'
    | '{' subpatterns ','? '}'
    ;
```

It is an error if any _subpattern_ of a _property_pattern_ does not contain an _identifier_ (it must be of the second form, which has an _identifier_).  A trailing comma after the last subpattern is optional.

Note that a null-checking pattern falls out of a trivial property pattern. To check if the string `s` is non-null, you can write any of the following forms

```csharp
if (s is object o) ... // o is of type object
if (s is string x) ... // x is of type string
if (s is {} x) ... // x is of type string
if (s is {}) ...
```

Given a match of an expression *e* to the pattern *type* `{` *property_pattern_list* `}`, it is a compile-time error if the expression *e* is not *pattern-compatible* with the type *T* designated by *type*. If the type is absent, we take it to be the static type of *e*. If the *identifier* is present, it declares a pattern variable of type *type*. Each of the identifiers appearing on the left-hand-side of its *property_pattern_list* must designate an accessible readable property or field of *T*. If the *simple_designation* of the *property_pattern* is present, it defines a pattern variable of type *T*.

At runtime, the expression is tested against *T*. If this fails then the property pattern match fails and the result is `false`. If it succeeds, then each *property_subpattern* field or property is read and its value matched against its corresponding pattern. The result of the whole match is `false` only if the result of any of these is `false`. The order in which subpatterns are matched is not specified, and a failed match may not match all subpatterns at runtime. If the match succeeds and the *simple_designation* of the *property_pattern* is a *single_variable_designation*, it defines a variable of type *T* that is assigned the matched value.

> Note: The property pattern can be used to pattern-match with anonymous types.

##### Example

```csharp
if (o is string { Length: 5 } s)
```

### Switch Expression

A *switch_expression* is added to support `switch`-like semantics for an expression context.

The C# language syntax is augmented with the following syntactic productions:

```antlr
multiplicative_expression
    : switch_expression
    | multiplicative_expression '*' switch_expression
    | multiplicative_expression '/' switch_expression
    | multiplicative_expression '%' switch_expression
    ;
switch_expression
    : range_expression 'switch' '{' '}'
    | range_expression 'switch' '{' switch_expression_arms ','? '}'
    ;
switch_expression_arms
    : switch_expression_arm
    | switch_expression_arms ',' switch_expression_arm
    ;
switch_expression_arm
    : pattern case_guard? '=>' expression
    ;
case_guard
    : 'when' null_coalescing_expression
    ;
```

The *switch_expression* is not permitted as an *expression_statement*.

> We are looking at relaxing this in a future revision.

The type of the *switch_expression* is the [*best common type*](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#finding-the-best-common-type-of-a-set-of-expressions) of the expressions appearing to the right of the `=>` tokens of the *switch_expression_arm*s if such a type exists and the expression in every arm of the switch expression can be implicitly converted to that type.  In addition, we add a new *switch expression conversion*, which is a predefined implicit conversion from a switch expression to every type `T` for which there exists an implicit conversion from each arm's expression to `T`.

It is an error if some *switch_expression_arm*'s pattern cannot affect the result because some previous pattern and guard will always match.

A switch expression is said to be *exhaustive* if some arm of the switch expression handles every value of its input.  The compiler shall produce a warning if a switch expression is not *exhaustive*.

At runtime, the result of the *switch_expression* is the value of the *expression* of the first *switch_expression_arm* for which the expression on the left-hand-side of the *switch_expression* matches the *switch_expression_arm*'s pattern, and for which the *case_guard* of the *switch_expression_arm*, if present, evaluates to `true`. If there is no such *switch_expression_arm*, the *switch_expression* throws an instance of the exception `System.Runtime.CompilerServices.SwitchExpressionException`.

### Optional parens when switching on a tuple literal

In order to switch on a tuple literal using the *switch_statement*, you have to write what appear to be redundant parens

```csharp
switch ((a, b))
{
```

To permit

```csharp
switch (a, b)
{
```

the parentheses of the switch statement are optional when the expression being switched on is a tuple literal.

### Order of evaluation in pattern-matching

Giving the compiler flexibility in reordering the operations executed during pattern-matching can permit flexibility that can be used to improve the efficiency of pattern-matching. The (unenforced) requirement would be that properties accessed in a pattern, and the Deconstruct methods, are required to be "pure" (side-effect free, idempotent, etc). That doesn't mean that we would add purity as a language concept, only that we would allow the compiler flexibility in reordering operations.

**Resolution 2018-04-04 LDM**: confirmed: the compiler is permitted to reorder calls to `Deconstruct`, property accesses, and invocations of methods in `ITuple`, and may assume that returned values are the same from multiple calls. The compiler should not invoke functions that cannot affect the result, and we will be very careful before making any changes to the compiler-generated order of evaluation in the future.

### Some Possible Optimizations

The compilation of pattern matching can take advantage of common parts of patterns. For example, if the top-level type test of two successive patterns in a *switch_statement* is the same type, the generated code can skip the type test for the second pattern.

When some of the patterns are integers or strings, the compiler can generate the same kind of code it generates for a switch-statement in earlier versions of the language.

For more on these kinds of optimizations, see [[Scott and Ramsey (2000)]](https://www.cs.tufts.edu/~nr/cs257/archive/norman-ramsey/match.pdf "When Do Match-Compilation Heuristics Matter?").
