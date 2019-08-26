# Pattern Matching for C# 7

Pattern matching extensions for C# enable many of the benefits of algebraic data types and pattern matching from functional languages, but in a way that smoothly integrates with the feel of the underlying language. The basic features are: [record types](https://github.com/dotnet/csharplang/proposals/records.md), which are types whose semantic meaning is described by the shape of the data; and pattern matching, which is a new expression form that enables extremely concise multilevel decomposition of these data types. Elements of this approach are inspired by related features in the programming languages [F#](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/p29-syme.pdf "Extensible Pattern Matching Via a Lightweight Language") and [Scala](https://infoscience.epfl.ch/record/98468/files/MatchingObjectsWithPatterns-TR.pdf "Matching Objects With Patterns").

## Is expression

The `is` operator is extended to test an expression against a *pattern*.

```antlr
relational_expression
    : relational_expression 'is' pattern
    ;
```

This form of *relational_expression* is in addition to the existing forms in the C# specification. It is a compile-time error if the *relational_expression* to the left of the `is` token does not designate a value or does not have a type.

Every *identifier* of the pattern introduces a new local variable that is *definitely assigned* after the `is` operator is `true` (i.e. *definitely assigned when true*).

> Note: There is technically an ambiguity between *type* in an `is-expression` and *constant_pattern*, either of which might be a valid parse of a qualified identifier. We try to bind it as a type for compatibility with previous versions of the language; only if that fails do we resolve it as we do in other contexts, to the first thing found (which must be either a constant or a type). This ambiguity is only present on the right-hand-side of an `is` expression.

## Patterns

Patterns are used in the `is` operator and in a *switch_statement* to express the shape of data against which incoming data is to be compared. Patterns may be recursive so that parts of the data may be matched against sub-patterns.

```antlr
pattern
    : declaration_pattern
    | constant_pattern
    | var_pattern
    ;

declaration_pattern
    : type simple_designation
    ;

constant_pattern
    : shift_expression
    ;

var_pattern
    : 'var' simple_designation
    ;
```

> Note: There is technically an ambiguity between *type* in an `is-expression` and *constant_pattern*, either of which might be a valid parse of a qualified identifier. We try to bind it as a type for compatibility with previous versions of the language; only if that fails do we resolve it as we do in other contexts, to the first thing found (which must be either a constant or a type). This ambiguity is only present on the right-hand-side of an `is` expression.

### Declaration pattern

The *declaration_pattern* both tests that an expression is of a given type and casts it to that type if the test succeeds. If the *simple_designation* is an identifier, it introduces a local variable of the given type named by the given identifier. That local variable is *definitely assigned* when the result of the pattern-matching operation is true.

```antlr
declaration_pattern
    : type simple_designation
    ;
```

The runtime semantic of this expression is that it tests the runtime type of the left-hand *relational_expression* operand against the *type* in the pattern. If it is of that runtime type (or some subtype), the result of the `is operator` is `true`. It declares a new local variable named by the *identifier* that is assigned the value of the left-hand operand when the result is `true`.

Certain combinations of static type of the left-hand-side and the given type are considered incompatible and result in compile-time error. A value of static type `E` is said to be *pattern compatible* with the type `T` if there exists an identity conversion, an implicit reference conversion, a boxing conversion, an explicit reference conversion, or an unboxing conversion from `E` to `T`. It is a compile-time error if an expression of type `E` is not pattern compatible with the type in a type pattern that it is matched with.

> Note: [In C# 7.1 we extend this](../csharp-7.1/generics-pattern-match.md) to permit a pattern-matching operation if either the input type or the type `T` is an open type. This paragraph is replaced by the following:
> 
> Certain combinations of static type of the left-hand-side and the given type are considered incompatible and result in compile-time error. A value of static type `E` is said to be *pattern compatible* with the type `T` if there exists an identity conversion, an implicit reference conversion, a boxing conversion, an explicit reference conversion, or an unboxing conversion from `E` to `T`, **or if either `E` or `T` is an open type**. It is a compile-time error if an expression of type `E` is not pattern compatible with the type in a type pattern that it is matched with.

The declaration pattern is useful for performing run-time type tests of reference types, and replaces the idiom

```csharp
var v = expr as Type;
if (v != null) { // code using v }
```

With the slightly more concise

```csharp
if (expr is Type v) { // code using v }
```

It is an error if *type* is a nullable value type.

The declaration pattern can be used to test values of nullable types: a value of type `Nullable<T>` (or a boxed `T`) matches a type pattern `T2 id` if the value is non-null and the type of `T2` is `T`, or some base type or interface of `T`. For example, in the code fragment

```csharp
int? x = 3;
if (x is int v) { // code using v }
```

The condition of the `if` statement is `true` at runtime and the variable `v` holds the value `3` of type `int` inside the block.

### Constant pattern

```antlr
constant_pattern
    : shift_expression
    ;
```

A constant pattern tests the value of an expression against a constant value. The constant may be any constant expression, such as a literal, the name of a declared `const` variable, or an enumeration constant, or a `typeof` expression.

If both *e* and *c* are of integral types, the pattern is considered matched if the result of the expression `e == c` is `true`.

Otherwise the pattern is considered matching if `object.Equals(e, c)` returns `true`. In this case it is a compile-time error if the static type of *e* is not *pattern compatible* with the type of the constant.

### Var pattern

```antlr
var_pattern
    : 'var' simple_designation
    ;
```

An expression *e* matches a *var_pattern* always. In other words, a match to a *var pattern* always succeeds. If the *simple_designation* is an identifier, then at runtime the value of *e* is bound to a newly introduced local variable. The type of the local variable is the static type of *e*.

It is an error if the name `var` binds to a type.

## Switch statement

The `switch` statement is extended to select for execution the first block having an associated pattern that matches the *switch expression*.

```antlr
switch_label
    : 'case' complex_pattern case_guard? ':'
    | 'case' constant_expression case_guard? ':'
    | 'default' ':'
    ;

case_guard
    : 'when' expression
    ;
```

The order in which patterns are matched is not defined. A compiler is permitted to match patterns out of order, and to reuse the results of already matched patterns to compute the result of matching of other patterns.

If a *case-guard* is present, its expression is of type `bool`. It is evaluated as an additional condition that must be satisfied for the case to be considered satisfied.

It is an error if a *switch_label* can have no effect at runtime because its pattern is subsumed by previous cases. [TODO: We should be more precise about the techniques the compiler is required to use to reach this judgment.]

A pattern variable declared in a *switch_label* is definitely assigned in its case block if and only if that case block contains precisely one *switch_label*.

[TODO: We should specify when a *switch block* is reachable.]

### Scope of pattern variables

The scope of a variable declared in a pattern is as follows:

- If the pattern is a case label, then the scope of the variable is the *case block*.

Otherwise the variable is declared in an *is_pattern* expression, and its scope is based on the construct immediately enclosing the expression containing the *is_pattern* expression as follows:

- If the expression is in an expression-bodied lambda, its scope is the body of the lambda.
- If the expression is in an expression-bodied method or property, its scope is the body of the method or property.
- If the expression is in a `when` clause of a `catch` clause, its scope is that `catch` clause.
- If the expression is in an *iteration_statement*, its scope is just that statement.
- Otherwise if the expression is in some other statement form, its scope is the scope containing the statement.

For the purpose of determining the scope, an *embedded_statement* is considered to be in its own scope. For example, the grammar for an *if_statement* is

``` antlr
if_statement
    : 'if' '(' boolean_expression ')' embedded_statement
    | 'if' '(' boolean_expression ')' embedded_statement 'else' embedded_statement
    ;
```

So if the controlled statement of an *if_statement* declares a pattern variable, its scope is restricted to that *embedded_statement*:

```csharp
if (x) M(y is var z);
```

In this case the scope of `z` is the embedded statement `M(y is var z);`.

Other cases are errors for other reasons (e.g. in a parameter's default value or an attribute, both of which are an error because those contexts require a constant expression).

> [In C# 7.3 we added the following contexts](../csharp-7.3/expression-variables-in-initializers.md) in which a pattern variable may be declared:
> - If the expression is in a *constructor initializer*, its scope is the *constructor initializer* and the constructor's body.
> - If the expression is in a field initializer, its scope is the *equals_value_clause* in which it appears.
> - If the expression is in a query clause that is specified to be translated into the body of a lambda, its scope is just that expression.

## Changes to syntactic disambiguation

There are situations involving generics where the C# grammar is ambiguous, and the language spec says how to resolve those ambiguities:

> #### 7.6.5.2 Grammar ambiguities
> The productions for *simple-name* (§7.6.3) and *member-access* (§7.6.5) can give rise to ambiguities in the grammar for expressions. For example, the statement:
> ```csharp
> F(G<A,B>(7));
> ```
> could be interpreted as a call to `F` with two arguments, `G < A` and `B > (7)`. Alternatively, it could be interpreted as a call to `F` with one argument, which is a call to a generic method `G` with two type arguments and one regular argument.

> If a sequence of tokens can be parsed (in context) as a *simple-name* (§7.6.3), *member-access* (§7.6.5), or *pointer-member-access* (§18.5.2) ending with a *type-argument-list* (§4.4.1), the token immediately following the closing `>` token is examined. If it is one of
> ```none
> (  )  ]  }  :  ;  ,  .  ?  ==  !=  |  ^
> ```
> then the *type-argument-list* is retained as part of the *simple-name*, *member-access* or *pointer-member-access* and any other possible parse of the sequence of tokens is discarded. Otherwise, the *type-argument-list* is not considered to be part of the *simple-name*, *member-access* or > *pointer-member-access*, even if there is no other possible parse of the sequence of tokens. Note that these rules are not applied when parsing a *type-argument-list* in a *namespace-or-type-name* (§3.8). The statement
> ```csharp
> F(G<A,B>(7));
> ```
> will, according to this rule, be interpreted as a call to `F` with one argument, which is a call to a generic method `G` with two type arguments and one regular argument. The statements
> ```csharp
> F(G < A, B > 7);
> F(G < A, B >> 7);
> ```
> will each be interpreted as a call to `F` with two arguments. The statement
> ```csharp
> x = F < A > +y;
> ```
> will be interpreted as a less than operator, greater than operator, and unary plus operator, as if the statement had been written `x = (F < A) > (+y)`, instead of as a *simple-name* with a *type-argument-list* followed by a binary plus operator. In the statement
> ```csharp
> x = y is C<T> + z;
> ```
> the tokens `C<T>` are interpreted as a *namespace-or-type-name* with a *type-argument-list*.

There are a number of changes being introduced in C# 7 that make these disambiguation rules no longer sufficient to handle the complexity of the language.

### Out variable declarations

It is now possible to declare a variable in an out argument:

```csharp
M(out Type name);
```

However, the type may be generic: 

```csharp
M(out A<B> name);
```

Since the language grammar for the argument uses *expression*, this context is subject to the disambiguation rule. In this case the closing `>` is followed by an *identifier*, which is not one of the tokens that permits it to be treated as a *type-argument-list*. I therefore propose to **add *identifier* to the set of tokens that triggers the disambiguation to a *type-argument-list*.**

### Tuples and deconstruction declarations

A tuple literal runs into exactly the same issue. Consider the tuple expression

```csharp
(A < B, C > D, E < F, G > H)
```

Under the old C# 6 rules for parsing an argument list, this would parse as a tuple with four elements, starting with `A < B` as the first. However, when this appears on the left of a deconstruction, we want the disambiguation triggered by the *identifier* token as described above:

```csharp
(A<B,C> D, E<F,G> H) = e;
```

This is a deconstruction declaration which declares two variables, the first of which is of type `A<B,C>` and named `D`. In other words, the tuple literal contains two expressions, each of which is a declaration expression.

For simplicity of the specification and compiler, I propose that this tuple literal be parsed as a two-element tuple wherever it appears (whether or not it appears on the left-hand-side of an assignment). That would be a natural result of the disambiguation described in the previous section.

### Pattern-matching

Pattern matching introduces a new context where the expression-type ambiguity arises. Previously the right-hand-side of an `is` operator was a type. Now it can be a type or expression, and if it is a type it may be followed by an identifier. This can, technically, change the meaning of existing code:

```csharp
var x = e is T < A > B;
```

This could be parsed under C#6 rules as

```csharp
var x = ((e is T) < A) > B;
```

but under under C#7 rules (with the disambiguation proposed above) would be parsed as

```csharp
var x = e is T<A> B;
```

which declares a variable `B` of type `T<A>`. Fortunately, the native and Roslyn compilers have a bug whereby they give a syntax error on the C#6 code. Therefore this particular breaking change is not a concern.

Pattern-matching introduces additional tokens that should drive the ambiguity resolution toward selecting a type. The following examples of existing valid C#6 code would be broken without additional disambiguation rules:

```csharp
var x = e is A<B> && f;            // &&
var x = e is A<B> || f;            // ||
var x = e is A<B> & f;             // &
var x = e is A<B>[];               // [
```

### Proposed change to the disambiguation rule

I propose to revise the specification to change the list of disambiguating tokens from

>
```none
(  )  ]  }  :  ;  ,  .  ?  ==  !=  |  ^
```

to

>
```none
(  )  ]  }  :  ;  ,  .  ?  ==  !=  |  ^  &&  ||  &  [
```

And, in certain contexts, we treat *identifier* as a disambiguating token. Those contexts are where the sequence of tokens being disambiguated is immediately preceded by one of the keywords `is`, `case`, or `out`, or arises while parsing the first element of a tuple literal (in which case the tokens are preceded by `(` or `:` and the identifier is followed by a `,`) or a subsequent element of a tuple literal.

### Modified disambiguation rule

The revised disambiguation rule would be something like this

> If a sequence of tokens can be parsed (in context) as a *simple-name* (§7.6.3), *member-access* (§7.6.5), or *pointer-member-access* (§18.5.2) ending with a *type-argument-list* (§4.4.1), the token immediately following the closing `>` token is examined, to see if it is
> - One of `(  )  ]  }  :  ;  ,  .  ?  ==  !=  |  ^  &&  ||  &  [`; or
> - One of the relational operators `<  >  <=  >=  is as`; or
> - A contextual query keyword appearing inside a query expression; or
> - In certain contexts, we treat *identifier* as a disambiguating token. Those contexts are where the sequence of tokens being disambiguated is immediately preceded by one of the keywords `is`, `case` or `out`, or arises while parsing the first element of a tuple literal (in which case the tokens are preceded by `(` or `:` and the identifier is followed by a `,`) or a subsequent element of a tuple literal.
> 
> If the following token is among this list, or an identifier in such a context, then the *type-argument-list* is retained as part of the *simple-name*, *member-access* or  *pointer-member-access* and any other possible parse of the sequence of tokens is discarded.  Otherwise, the *type-argument-list* is not considered to be part of the *simple-name*, *member-access* or *pointer-member-access*, even if there is no other possible parse of the sequence of tokens. Note that these rules are not applied when parsing a *type-argument-list* in a *namespace-or-type-name* (§3.8).

### Breaking changes due to this proposal

No breaking changes are known due to this proposed disambiguation rule.

### Interesting examples

Here are some interesting results of these disambiguation rules:

The expression `(A < B, C > D)` is a tuple with two elements, each a comparison.

The expression `(A<B,C> D, E)` is a tuple with two elements, the first of which is a declaration expression.

The invocation `M(A < B, C > D, E)` has three arguments.

The invocation `M(out A<B,C> D, E)` has two arguments, the first of which is an `out` declaration.

The expression `e is A<B> C` uses a declaration expression.

The case label `case A<B> C:` uses a declaration expression.

## Some examples of pattern matching

### Is-As

We can replace the idiom

```csharp
var v = expr as Type;	
if (v != null) {
    // code using v
}
```

With the slightly more concise and direct

```csharp
if (expr is Type v) {
    // code using v
}
```

### Testing nullable

We can replace the idiom

```csharp
Type? v = x?.y?.z;
if (v.HasValue) {
    var value = v.GetValueOrDefault();
    // code using value
}
```

With the slightly more concise and direct

```csharp
if (x?.y?.z is Type value) {
    // code using value
}
```

### Arithmetic simplification

Suppose we define a set of recursive types to represent expressions (per a separate proposal):

```csharp
abstract class Expr;
class X() : Expr;
class Const(double Value) : Expr;
class Add(Expr Left, Expr Right) : Expr;
class Mult(Expr Left, Expr Right) : Expr;
class Neg(Expr Value) : Expr;
```

Now we can define a function to compute the (unreduced) derivative of an expression:

```csharp
Expr Deriv(Expr e)
{
  switch (e) {
    case X(): return Const(1);
    case Const(*): return Const(0);
    case Add(var Left, var Right):
      return Add(Deriv(Left), Deriv(Right));
    case Mult(var Left, var Right):
      return Add(Mult(Deriv(Left), Right), Mult(Left, Deriv(Right)));
    case Neg(var Value):
      return Neg(Deriv(Value));
  }
}
```

An expression simplifier demonstrates positional patterns:

```csharp
Expr Simplify(Expr e)
{
  switch (e) {
    case Mult(Const(0), *): return Const(0);
    case Mult(*, Const(0)): return Const(0);
    case Mult(Const(1), var x): return Simplify(x);
    case Mult(var x, Const(1)): return Simplify(x);
    case Mult(Const(var l), Const(var r)): return Const(l*r);
    case Add(Const(0), var x): return Simplify(x);
    case Add(var x, Const(0)): return Simplify(x);
    case Add(Const(var l), Const(var r)): return Const(l+r);
    case Neg(Const(var k)): return Const(-k);
    default: return e;
  }
}
```
