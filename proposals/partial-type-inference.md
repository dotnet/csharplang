# Partial type inference

Note: This proposal was created because of championed [Partial type inference](https://github.com/dotnet/csharplang/issues/1349). It is a continuation of the proposed second version published in [csharplang/discussions/7467](https://github.com/dotnet/csharplang/discussions/7467) and the first version published in [csharplang/discussions/7286](https://github.com/dotnet/csharplang/discussions/7286)

## Summary
[summary]: #summary

Partial type inference introduces a syntax skipping inferrable type arguments in the argument list of

1. *invocation_expression*
2. *object_creation_expresssion*

and allowing to specify just ambiguous ones.

```csharp
M<_, object>(42, null); 
void M<T1, T2>(T1 t1, T2 t2) { ... }
```

It also improves the type inference in the case of *object_creation_expression* by leveraging type bounds obtained from the target and *type_parameter_constraints_clauses*. 

```csharp
using System.Collections.Generic;
IList<int> temp = new List<_>();
```

Besides the changes described above, the proposal mentions further interactions and possibilities to extend the partial type inference.

## Motivation
[motivation]: #motivation

* The current method type inference has two possible areas for improvement.
  The first improvement regards the strength of the method type inference, which uses only arguments' types to deduce the method's type arguments.
  Concretely, we can see the weakness in cases where type arguments only depend on target type or type parameter restrictions.

  ```csharp
  object data = database.fetch();
  // Error below, method type inference can't infer the return type. We have to specify the type argument.
  int count = data.Field("count"); 
   
  public static class Extensions
  {
    public static TReturn Field<TReturn>(this object inst, string fieldName) { ... }
  }
  ```

  ```csharp
  // Error below, method type inference can't infer T. We have to specify all type arguments.
  test(new MyData()); 
  
  public void test<T, U>(U data) where T : TestCaseDefault<U> { ... }
  ```

  The second improvement regards the "all or nothing" principle, where the method type inference infers either all of the type arguments or nothing.

  ```csharp
  // Error below, impossible to specify just one type argument. We have to specify all of them. 
  log<, object>(new Message( ... ), null); 
  
  public void log<T, U>(T message, U appendix) { ... }
  ```

  The first improvement, which would improve the method type inference algorithm, has a significant disadvantage of introducing a breaking change.
  On the other hand, the second improvement, which would enable specifying some of the method's type arguments, does not influence old code, solves problems regarding the "all or nothing" principle, and reduces the first weakness.
  
  ```csharp
  // We can use _ to mark type arguments which should be inferred by the compiler.
  test<TestCaseDefault<_>, _>(new MyData());
  
  public void test<T, U>(U data) where T : TestCaseDefault<U> { ... }
  ```
  
  ```csharp
  // We can use _ to mark type arguments which should be inferred by the compiler.
  log<_, object>(new Message( ... ), null); 
  
  public void log<T, U>(T message, U appendix) { ... }
  ```

* The next motivation is constructor type inference. 
  Method type inference is not defined on *object_creation_expression*, prohibiting taking advantage of type inference.
  We divide use cases into the following categories, where type inference would help the programmer. 

  1. Cases where the method type inference would succeed.
   
  
  ```csharp
  public static Wrapper<T> Create<T>(T item) { return new Wrapper<T>(item); }
  
  class Wrapper<T> { public Wrapper(T item) { ... } }
  ```
  
  2. Cases where the method type inference would be weak. (Using type info from target type, or type arguments' constraints)
  
  ```csharp
  // Method type inference can't infer TLogger because it doesn't use type constraints specified by `where` clauses
  var alg = Create(new MyData()); 
   
  public static Algorithm<TData, TLogger> Create<TData, TLogger>(TData data) where TLogger : Logger<TData> 
  { 
    return new Algorithm<TData, TLogger>(data); 
  } 
  
  class Algorithm<TData, TLogger> where TLogger : Logger<TData> 
  { 
    public Algorithm(TData data) { ... }
  }
  ```

  An existing solution can be seen in `Create()` method wrappers of constructors enabling a type inference through method type inference as you can see in the examples above.
  However, we can't use it with *object_or_collection_initializer*; we are limited by method type inference strength, and it adds unnecessary boiler code.

  Adding constructor type inference as we will describe in the following section would solve above mentioned examples.
  
  ```csharp
  var wrappedData = new Wrapper<_>(new MyData());
  class Wrapper<T> { public Wrapper(T item) { ... } }
  ```
   
  ```csharp
  var alg = new Algorithm<_, _>(new MyData());
  var algWithSpecialLogger = new Algorithm<_ , SpecialLogger<_>>(new MyData());
   
  class Algorithm<TData, TLogger> where TLogger : Logger<TData> { public Algorithm(TData data) { ... }}
  ```

No matter how the partial type inference would work, we should be careful about the following things.

- **Convenience** - We want an easy and intuitive syntax that we can skip the inferrable type arguments.
- **Performance** - Type inference is a complicated problem when we introduce subtyping and overloading in a type system.
Although it can be done, the computation can take exponential time which we don't want.
So it has to be restricted to cases, where the problem can be solved effectively but it still has practical usage.
- **IDE** - Improvement of the type inference can complicate IDE hints during coding. 
We should give the user clear and not overwhelming errors when there will be an error and try to provide info that helps him to fix it.
- **Extensions** - We don't want to make this change blocker for another potential feature in the future. 
So we will want to look ahead to other potential directions, which can be done after this feature.

## Detailed design
[design]: #detailed-design

### Grammar

We modify [Identifiers](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/lexical-structure.md#643-identifiers) as follows:

- The semantics of an identifier named `_` depends on the context in which it appears:
  - It can denote a named program element, such as a variable, class, or method, or
  - It can denote a discard (§9.2.9.1).
  - **It will denote a type argument to be inferred.**

We modify [Keywords](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/lexical-structure.md#644-keywords) as follows:

* A ***contextual keyword*** is an identifier-like sequence of characters that has special meaning in certain contexts, but is not reserved, and can be used as an identifier outside of those contexts as well as when prefaced by the `@` character.

  ```diff
  contextual_keyword
      : 'add'    | 'alias'      | 'ascending' | 'async'     | 'await'
      | 'by'     | 'descending' | 'dynamic'   | 'equals'    | 'from'
      | 'get'    | 'global'     | 'group'     | 'into'      | 'join'
      | 'let'    | 'nameof'     | 'on'        | 'orderby'   | 'partial'
      | 'remove' | 'select'     | 'set'       | 'unmanaged' | 'value'
  +   | 'var'    | 'when'       | 'where'     | 'yield'     | '_'
  -   | 'var'    | 'when'       | 'where'     | 'yield'
    ;
  ```

### Type arguments

We change [type arguments](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/types.md#842-type-arguments) section as follows:

* ***inferred_type_argument*** represents an unknown type, which will be resolved during type inference. 

* Each argument in a type argument list is ~~simply a type~~ **a type or an inferred type**.

```diff
type_argument_list
  : '<' type_arguments '>'
  ;
  
type_arguments
-  : type_argument (',' type_argument)*
+  : type_argument (',' type_arguments)
+  | inferred_type_argument (',' type_arguments)
  ;   

type_argument
  : type
  ;

+inferred_type_argument
+  : '_'
+  ;
```

* When a type with `_` identifier is presented in the scope where **inferred_type_argument** is used, a warning should appear since the **inferred_type_argument** hides the type's name causing a breaking change. 
  There are two possible resolutions of this warning. 
  If the `_` identifier should represent **inferred_type_argument**, the user should suppress the warning or should rename the type or alias declaration.
  If the `_` identifier should represent a type, the user should use `@_` to explicitly reference a typename.

* When there is a type or alias declaration with the `_` identifier, a warning should appear since it is a contextual keyword.
  A possible resolution would be to rename the declaration or suppress the warning when the declaration can't be renamed.     

* `_` identifier is considered to represent *inferred_type_argument* when:
  * It occurs in *type_argument_list* of a method group during method invocation.
  * It occurs in *type_argument_list* of a type in *object_creation_expression*.
  * It occurs as an arbitrary nested identifier in the expressions mentioned above.
  
  ```csharp
  F<_, int>( ... ); // _ represents an inferred type argument.
  new C<_, int>( ... ); // _ represents an inferred type argument.
  F<C<_>, int>( ... ); // _ represents an inferred type argument.
  new C<C<_>, int>( ... ); // _ represents an inferred type argument.
  C<_> temp = ...; // _ doesn't represent an inferred type argument.
  new _( ... ) // _ doesn't represent an inferred type argument.
  
  // _ of Container<_> doesn't represent an inferred type argument. 
  // (Containing type's type argument won't be inferred)
  Container<_>.Method<_>(arg); 
  ```   

* We can use a question mark `?` to say that the inferred type argument should be a nullable type (e.g. `F<_?>(...)`).

* A method group and type are said to be *partial_inferred* if it contains at least one *inferred_type_argument*. 

### Method invocations

The binding-time processing of a [method invocation](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12892-method-invocations) of the form `M(A)`, where `M` is a method group (possibly including a *type_argument_list*), and `A` is an optional *argument_list* is changed in the following way.

The initial set of candidate methods for is changed by adding new condition.

- If `F` is non-generic, `F` is a candidate when:
  - `M` has no type argument list, and
  - `F` is applicable with respect to `A` (§12.6.4.2).
- If `F` is generic and `M` has no type argument list, `F` is a candidate when:
  - Type inference (§12.6.3) succeeds, inferring a list of type arguments for the call, and
  - Once the inferred type arguments are substituted for the corresponding method type parameters, all constructed types in the parameter list of `F` satisfy their constraints ([§8.4.5](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/types.md#845-satisfying-constraints)), and the parameter list of `F` is applicable with respect to `A` (§12.6.4.2)
- **If `F` is generic and `M` has type argument list containing at least one *inferred_type_argument*, `F` is a candidate when:**
  - **Type inference ([§12.6.3](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1263-type-inference)) succeeds, inferring a list of *inferred_type_arguments* for the call, and**
  - **Once the *inferred_type_arguments* are inferred and together with remaining type arguments are substituted for the corresponding method type parameters, all constructed types in the parameter list of `F` satisfy their constraints ([§8.4.5](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/types.md#845-satisfying-constraints)), and the parameter list of `F` is applicable with respect to `A` ([§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12642-applicable-function-member))**
- If `F` is generic and `M` includes a type argument list, `F` is a candidate when:
  - `F` has the same number of method type parameters as were supplied in the type argument list, and
  - Once the type arguments are substituted for the corresponding method type parameters, all constructed types in the parameter list of `F` satisfy their constraints (§8.4.5), and the parameter list of `F` is applicable with respect to `A` (§12.6.4.2).

### Object creation expressions

The binding-time processing of an [*object_creation_expression*](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#128162-object-creation-expressions) of the form new `T(A)`, where `T` is a *class_type*, or a *value_type*, and `A` is an optional *argument_list*, is changed in the following way.

Note: Type inference of constructor is described later in the type inference section.

The binding-time processing of an *object_creation_expression* of the form new `T(A)`, where `T` is a *class_type*, or a *value_type*, and `A` is an optional *argument_list*, consists of the following steps:

- If `T` is a *value_type* and `A` is not present:
  - **The *object_creation_expression* is a default constructor invocation.**
    - **If the type is *partially_inferred*, type inference of the default constructor occurs to determine the type arguments. If it succeeded, construct the type using inferred type arguments. If it failed and there is no chance to get the target type now or later, the binding-time error occurs. Otherwise, repeat the binding when the target type will be determined and add it to the inputs of type inference.**
    - **If the type inference above succeeded or the type is not inferred, the result of the *object_creation_expression* is a value of (constructed) type `T`, namely the default value for `T` as defined in [§8.3.3](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/types.md#833-default-constructors).**
- Otherwise, if `T` is a *type_parameter* and `A` is not present:
  - If no value type constraint or constructor constraint (§15.2.5) has been specified for `T`, a binding-time error occurs.
  - The result of the *object_creation_expression* is a value of the run-time type that the type parameter has been bound to, namely the result of invoking the default constructor of that type. The run-time type may be a reference type or a value type.
- Otherwise, if `T` is a *class_type* or a *struct_type*:
  - If `T` is an abstract or static *class_type*, a compile-time error occurs.
  - **The instance constructor to invoke is determined using the overload resolution rules of [§12.6.4](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1264-overload-resolution). The set of candidate instance constructors is determined as follows:**
    - **`T` is not inferrred (*partially_inferred*), the constructor is accessible in `T`, and is applicable with respect to `A` ([§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12642-applicable-function-member)).**
    - **If `T` is *partially_constructed* and the constructor is accessible in `T`, type inference of the constructor is performed. Once the *inferred_type_arguments* are inferred and together with the remaining type arguments are substituted for the corresponding type parameters, all constructed types in the parameter list of the constructor satisfy their constraints, and the parameter list of the constructor is applicable with respect to `A` ([§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12642-applicable-function-member)).**
  - **A binding-time error occurs when:**
    - **The set of candidate instance constructors is empty, or if a single best instance constructor cannot be identified, and there is no chance to know the target type now or later.**
  - **If the set of candidate instance constructors is still empty, or if a single best instance constructor cannot be identified, repeat the binding of the *object_creation_expression* to the time, when target type will be known and add it to inputs of type inference.**
  - The result of the *object_creation_expression* is a value of type `T`, namely the value produced by invoking the instance constructor determined in the two steps above.
  - Otherwise, the *object_creation_expression* is invalid, and a binding-time error occurs.

### Type inference

We replace the [type inference/general](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12631-general) section with the following section.

* Type inference for generic method invocation is performed when the invocation:
  * Doesn't have a *type_argument_list*.
  * The type argument list contains at least one *inferred_type_argument*.

  ```csharp
  M( ... ); // Type inference is invoked.
  M<_, string>( ... ); // Type inference is invoked.
  M<List<_>, string>( ... ); // Type inference is invoked.
  ```

* Type inference is applied to each generic method in the method group. 

* Type inference for constructors is performed when the generic type of *object_creation_expression*:
  * Its *type_argument_list* contains at least one *inferred_type_argument*.
  
  ```csharp
  new C<_, string>( ... ); // Type inference is invoked.
  new C<List<_>, string>( ... ); // Type inference is invoked.
  ```

* Type inference is applied to each constructor which is contained in the type. 

* When one of the cases appears, a ***type inference*** process attempts to infer type arguments for the call. 
  The presence of type inference allows a more convenient syntax to be used for calling a generic method or creating an object of a generic type, and allows the programmer to avoid specifying redundant type information.

* In the case of *method type inference*, we infer method type parameters. 
  In the case of *constructor type inference*, we infer type parameters of a type defining the constructors. 
  The previous sentence prohibits inferring type parameters of an outside type that contains the inferred type. (e.g. inference of `new Containing<_>.Nested<_>(42)` is not allowed)

* Type inference occurs as part of the binding-time processing of a [method invocation](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12892-method-invocations) or an [object_creation_expression](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#128162-object-creation-expressions) and takes place before the overload resolution step of the invocation.
   
* If type inference succeeds, then the inferred type arguments are used to determine the types of arguments for subsequent overload resolution. 
  If overload resolution chooses a generic method or constructor as the one to invoke, then the inferred type arguments are used as the type arguments for the invocation or for the type containing the constructor.
  If type inference for a particular method or constructor fails, that method or constructor does not participate in overload resolution. 
  The failure of type inference, in and of itself, does not cause a binding-time error. However, it often leads to a binding-time error when overload resolution then fails to find any applicable methods or constructors.

* Arguments binding
  * It can happen that an argument of an expression will be *object_creation_expression*, which needs a target type to be successful binded. 
  * In these situations, we behave like the type of the argument is unknown and bind it when we will know the target type.
  * We treat it in the same manner as an unconverted *new()* operator.

* If each supplied argument does not correspond to exactly one parameter in the method or constructor [corresponding-parameters](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12622-corresponding-parameters), or there is a non-optional parameter with no corresponding argument, then inference immediately fails. 
  Otherwise, assume that the generic method has the following signature:

  `Tₑ M<X₁...Xᵥ>(T₁ p₁ ... Tₓ pₓ)`

  With a method call of the form `M<...>(E₁ ...Eₓ)` the task of type inference is to find unique type arguments `S₁...Sᵥ` for each of the type parameters `X₁...Xᵥ` so that the call `M<S₁...Sᵥ>(E₁...Eₓ)` becomes valid.

  In case of construtor, assume the following signature:
  
  `M<X₁...Xᵥ>..ctor(T₁ p₁ ... Tₓ pₓ)` 

  With a constructor call of the form `new M<...>(E₁ ...Eₓ)` the task of type inference is to find unique type arguments `S₁...Sᵥ` for each of the type parameters `X₁...Xᵥ` so that the call `new M<S₁...Sᵥ>(E₁...Eₓ)` becomes valid.

* The process of type inference is described below as an algorithm. A conformant compiler may be implemented using an alternative approach, provided it reaches the same result in all cases.

* During the process of inference each type variable `Xᵢ` is either *fixed* to a particular type `Sᵢ` or *unfixed* with an associated set of *bounds.* Each of the bounds is some type `T`. Initially each type variable `Xᵢ` is unfixed with an empty set of bounds.

* Type inference takes place in phases. Each phase will try to infer type arguments for more type variables based on the findings of the previous phase. The first phase makes some initial inferences of bounds, whereas the second phase fixes type variables to specific types and infers further bounds. The second phase may have to be repeated a number of times.

* Additional changes of method type inference algorithm are made as follows:
  * If the inferred method group contains a nonempty *type_argument_list*.
    * We replace each `_` identifier with a new type variable `X`.
    * We perform *shape inference* from each type argument to the corresponding type parameter.

* Additional changes of constructor type inference algorithm are made as follows:
  * If the inferred type contains a nonempty *type_argument_list*.
    * We replace each `_` identifier with a new type variable `X`.
    * We perform *shape inference* from each type argument to the corresponding type parameter.
  * If the target type should be used based on the expression binding, perform *upper-bound inference* from it to the type containing the constructor
  * If the expression contains *where* clauses defining type constraints of type parameters of the type containing constructor, for each constraint not representing *constructor* constraint, *reference type constraint*, *value type constraint* and *unmanaged type constraint* perform *lower-bound inference* from the constraint to the corresponding type parameter.

#### Type inference algorithm change

We change the type inference algorithm contained in the [type inference](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1263-type-inference) section as follows and continue with an explanation of the changes at the end. 

* Shape dependence
  * An *unfixed* type variable `Xᵢ` *shape-depends directly on* an *unfixed* type variable `Xₑ` if `Xₑ` represents *inferred_type_argument* and it is contained in *shape bound* of the type variable `Xᵢ`.
  * `Xₑ` *shape-depends on* `Xᵢ` if `Xₑ` *shape-depends directly on* `Xᵢ` or if `Xᵢ` *shape-depends directly on* `Xᵥ` and `Xᵥ` *shape-depends on* `Xₑ`. Thus “*shape-depends on*” is the transitive but not reflexive closure of “*shape-depends directly on*”.

* Type dependence
  * An *unfixed* type variable `Xᵢ` *type-depends directly on* an *unfixed* type variable `Xₑ` if `Xₑ` occurs in any bound of type variable `Xᵢ`.
  * `Xₑ` *type-depends on* `Xᵢ` if `Xₑ` *type-depends directly on* `Xᵢ` or if `Xᵢ` *type-depends directly on* `Xᵥ` and `Xᵥ` *type-depends on* `Xₑ`. Thus “*type-depends on*” is the transitive but not reflexive closure of “*type-depends directly on*”.

* Shape inference
  * A *shape* inference from a type `U` to a type `V` is made as follows:
    * If `V` is one of the *unfixed* `Xᵢ` then `U` is a shape bound of `V`.
    * When a shape bound `U` of `V` is set:
      * We perform *upper-bound* inference from `U` to all lower-bounds of `V`, which contains an unfixed type variable
      * We perform *exact* inference from `U` to all exact-bounds of `V`, which contains an unfixed type variable.
      * We perform *lower-bound* inference from `U` to all upper-bounds of `V`, which contains an unfixed type variable.
      *  We perform *lower-bound* inference from all lower-bounds of `V` to `U` if `U` contains an unfixed type variable.
      *  We perform *exact* inference from all exact-bounds of `V` to `U` if `U` contains unfixed type variable.
      *  We perform *upper-type* inference from all upper-bounds of `V` to `U` if `U` contains an unfixed type variable.
    * Otherwise, no inferences are made

* Lower-bound inference
  * When a new bound `U` is added to the set of lower-bounds of `V`:
    * We perform *lower-bound* inference from `U` to the shape of `V`, if it has any and the shape contains an unfixed type variable.
    * We perform *upper-bound* inference from the shape of `V` to `U`, if `V` has a shape and `U` contains an unfixed type variable.
    * We perform *exact* inference from `U` to all lower-bounds of `V`, which contains an unfixed type variable.
    * We perform *lower-bound* inference from `U` to all exact-bounds and upper-bounds of `V`, which contains an unfixed type variable.
    * We perform *exact* inference from all lower-bounds of `V` to `U` if `U` contains an unfixed type variable.
    * We perform *upper-bound* type inference from all exact-bounds and upper-bounds of `V` to `U` if `U` contains unfixed type variable.

* Upper-bound inference
  * When new bound `U` is added to the set of upper-bounds of `V`:
    * We perform *upper-bound* inference from `U` to the shape of `V` , if it has any and the shape contains an unfixed type variable.
    * We perform *lower-bound* inference from the shape of `V` to `U`, if `V` has a a shape and `U` contains an unfixed type variable.
    * We perform *exact* inference from `U` to all upper-bounds of `V`, which contains an unfixed type variable.
    * We perform *upper-bound* inference from `U` to all exact-bounds and lower-bounds of `V`, which contains an unfixed type variable.
    * We perform *exact* inference from all upper-bounds of `V` to `U` if `U` contains an unfixed type variable.
    * We perform *lower-bound* type inference from all exact-bounds and lower-bounds of `V` to `U` if `U` contains unfixed type variable.

* Exact inference
  * When new bound `U` is added to the set of lower-bounds of `V`:
    * We perform *exact-bound* inference from `U` to the shape of `V`, if has any and the shape contains an unfixed type variable.
    * We perform *exact* inference from the shape of `V` to `U`, if `V` has a shape and `U` contains an unfixed type variable.
    * We perform *exact* inference from `U` to all exact-bounds of `V`, which contains an unfixed type variable.
    * We perform *lower-bound* inference from `U` to all lower-bounds of `V`, which contains an unfixed type variable.
    * We perform *upper-bound* inference from `U` to all upper-bounds of `V`, which contains an unfixed type variable.
    * We perform *exact* inference from all exact-bounds of `V` to `U`, which contains an unfixed type variable.
    * We perform *upper-bound* inference from all lower-bounds of `V` to `U`, which contains an unfixed type variable.
    * We perform *lower-bound* inference from all upper-bounds of `V` to `U`, which contains an unfixed type variable.
  
* Second phase
  * **Firstly, all *unfixed* type variables `Xᵢ` which do not *depend on* ([§12.6.3.6](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12636-dependence)), *shape-depend on*, and *type-depend on* any `Xₑ` are fixed ([§12.6.3.12](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#126312-fixing)).**
  * **If no such type variables exist, all *unfixed* type variables `Xᵢ` are *fixed* for which all of the following hold:**
    * **There is at least one type variable `Xₑ` that *depends on*, *shape-depends on*, or *type-depends on* `Xᵢ`**
    * **There is no type variable `Xₑ` on which `Xᵢ` *shape-depends on*.**
    * **`Xᵢ` has a non-empty set of bounds and has at least on bound which doesn't contain any *unfixed* type variable.**
  * If no such type variables exist and there are still unfixed type variables, type inference fails.
  * [...]

* First phase

  * For each of the method **/constructor** arguments `Eᵢ`:
  * [...]
    
* Fixing
  * **An *unfixed* type variable `Xᵢ` with a set of bounds is *fixed* as follows:**
    * **If the type variable has a shape bound, check the type has no conflicts with other bounds of that type variable in the same way as the standard says. It it has no conflicts, the type variable is *fixed* to that type. Otherwise type inference failed.**
    * Otherwise, fix it as the standard says. 

* Explanation of inference improvements
  * Now, the inferred bounds can contain other unfixed type variables.
    So we have to propagate the type info also through these bounds.
 
    ```csharp
    void F<T1> (T1 p1) { ... }
    ...
    F<IList<_>>(new List<int>());
    ```

  * Consider an example above. 
    We have now two type variables `T1` and `_`. From the first bound, we get that `IList<_>` is a shape bound of `T1`(Ignore now the type of bound, it would be the same in other types of bound).
    When we investigate the second bound `List<int>`, we will figure out that it would be a lower bound of `T1`.
    But now, we have to somehow propagate the int type to the `_` type variable, because it relates to it.
    That means, in the process of adding new bounds, we have to also propagate this info through bounds, which contain unfixed type variables.
    In this case, we do additional inference of `IList<_>` and `List<int>` yielding exact bound `int` of `_`.

* Explanation of type-dependence
  * Type-dependence is required because, till this time when a type variable had any bounds, it didn't contain any unfixed type variable.
    It was important because we could do the parameter fixation, where we work with exact types(not unfixed type variables).
    However now, the type variable can contain bounds containing unfixed type variables.
    We have to ensure that we will not start fixing the type variable till these unfixed type variables are unfixed(In some cases, we can be in a situation, where this dependency will form a cycle. In this case, we will allow the fixation earlier).
  
  * We use the previous example.
    After the first phase. `T1` has bounds `IList<_>` and `List<int>`. `_` has bound `int`.
    In this situation, we can't start to fix `T1` because `_` is not fixed yet.
    `T1` is type-dependent on `_`.
    So, we will first fix `_`, which becomes `int`.
    Then, `T1` is not type-dependent anymore, because all bounds don't contain any unfixed type variables.
    `IList<_>` is now `IList<int>` after the `_` fixation.
    We can fix `T1` now.

* Explanation of shape-dependence
  * A similar thing is for shape-dependence.
    Although it cares about bounds received from the type argument list.
    We want a shape bound to be exact (not containing any unfixed type variables) because it is later important for the fixation.
    An intention is to keep the exact form of the given hint(`IList<_>`).

  * Example: Given `IList<_>` as a type argument, when we treat nullability, we want the hinted type parameter to be non-nullable(not `IList<_>?`).
    It can happen, other bounds would infer the nullable version, and although `IList<_>` can be converted to `IList<_>?`, it is not the user's intention.

* Explanation of inference restriction during constructor type inference
  * Because performing type inference can even take exponential time when a type system contains overloading, the restriction was made above to avoid it. 
    It regards binding arguments before the overload resolution when we bind all *object_creation_expressions* without target info and then in case of overload resolution success and some of these arguments failed in the binding, we try to bind it again with already known target type information.

### Compile-time checking of dynamic member invocation

We change the [compile-time checking](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1265-compile-time-checking-of-dynamic-member-invocation) in order to be useful during partial type inferece.

- First, if `F` is a generic method and type arguments were provided, then those ***, that aren't *inferred_type_argument*** are substituted for the type parameters in the parameter list. However, if type arguments were not provided, no such substitution happens.
- Then, any parameter whose type is open (i.e., contains a type parameter; see §8.4.3) is elided, along with its corresponding parameter(s).
- **If the method group contains inferred type arguments, a warning should appear since partial type inference is not supported by runtime.** 

We add the following
- If an object creation expression is inferred and the argument list contains a dynamic value, an error should appear since constructor type inference is not supported by runtime.  

## Drawbacks
[drawbacks]: #drawbacks

Difference between constructor type inference and method type inference could confuse the programmer. 
However, a generic type doesn't usually have constructors containing parameters which types contain all type parameters of the generic type. 
So original method type inference would be too weak to infer the type arguments of the generic type.

## Alternatives
[alternatives]: #alternatives

What other designs have been considered? What is the impact of not doing this?

* Alternative: Strong type inference as we can see in Haskell or Rust without restrictions mentioned above.

  Reason not to do that: It would introduce breaking changes. It would take an exponential time to deal with overloading. 


* Alternative: Default type parameters values([link](https://github.com/dotnet/csharplang/discussions/280)):

  Reason not to do that: It would need to change old code to take advantage of it 

* Alternative: Named type arguments([link](https://github.com/dotnet/csharplang/discussions/280)):

  Reason not to do that: We would like to have less type annotations in the code.

* Alternative: Allow type inference in object creation with floating arity(`new C<>(...)`):
  
  Reason not to do that: Maybe much controversial ?

  <details>

  This change was initially a part of the proposal. To support this feature, following sections of this proposal would be changed.

  ### Type arguments

  ...

  * A type is said to be *generic_inferred* when all the following hold:
  * It has an empty *type_argument_list*.
  * It occurs as a *type* of *object_creation_expression*.
  
  ```csharp
  new C<>(...) // Valid code, C is generic_inferred.
  new C<G<>>(...) // Invalid code, C nor G are generic_inferred.
  F<>(...) // Invalid code, F isn't generic_inferred.
  ```

  ### Namespace and type names

  Determining the [meaning](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/basic-concepts.md#781-general) of a *namespace_or_type_name* is changed as follows.

  * If a type is a *generic_inferred*, then we resolve the identifier in the same manner except ignoring the arity of type parameters (Types of arity 0 is ignored). 
  If there is an ambiguity in the current scope, a compilation-time error occurs.

    ```csharp
    class P1
    {
      void M() 
      {
        // generic_inferred type C1<> refers to generic type C<T> where 
        // the type argument will be inferred from inspecting candidate constructors of C<T> 
        new C1<>( ... );  
        // generic_inferred type C1<> refers to generic type C<T1, T2> where 
        // the type arguments will be inferred from inspecting candidate constructors of C<T1, T2> 
        new C2<>( ... ); 
      }
      class C1<T> { ... }
      class C2<T1, T2> { ... }
    }
    class P2
    {
      void M() 
      {
        new C1<>( ... ); // Compile-time error occurs because of ambiguity between C1<T> and C1<T1, T2>
      }
      class C1<T> { ... }
     class C1<T1, T2> { ... }
    }
    ``` 

  ### Object creation expressions

  ...

  - If the type is ***generic_inferred* or** *partially_inferred*, type inference of the default constructor occurs to determine the type arguments. If it succeeded, construct the type using inferred type arguments. If it failed and there is no chance to get the target type now or later, the binding-time error occurs. Otherwise, repeat the binding when the target type will be determined and add it to the inputs of type inference.
  
  ...
  
  - The instance constructor to invoke is determined using the overload resolution rules of [§12.6.4](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1264-overload-resolution). The set of candidate instance constructors is determined as follows:
    - `T` is not inferrred (***generic_inferred* or** *partially_inferred*), the constructor is accessible in `T`, and is applicable with respect to `A` ([§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12642-applicable-function-member)).
    - If `T` is ***generic_constructed* or** *partially_constructed* and the constructor is accessible in `T`, type inference of the constructor is performed. Once the *inferred_type_arguments* are inferred and together with the remaining type arguments are substituted for the corresponding type parameters, all constructed types in the parameter list of the constructor satisfy their constraints, and the parameter list of the constructor is applicable with respect to `A` ([§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12642-applicable-function-member)).
    - ...

  ### Type inference
  
  ...

  * Type inference for constructors is performed when the generic type of *object_creation_expression*:
  * **Has a diamond operator.**
  * Its *type_argument_list* contains at least one *inferred_type_argument*.
  
  ```diff
  +new C<>( ... ); // Type inference is invoked.
  new C<_, string>( ... ); // Type inference is invoked.
  new C<List<_>, string>( ... ); // Type inference is invoked.
  ```

  </details>

* Alternative: Allowing object initializers contribute to the inference in object creation: 

  Example of inferring type arguments of type from the initializer list:

  ```csharp
  using System.Collections.Generic;
  var statistics = new Dictionary<,>(){["Joe"] = 20}; // Inferred: Dictionary<string, int>
  ```
   
  Reason not to do that: Maybe much controversial ?

  <details>

  This change was initially a part of the proposal. To support this feature, following sections of this proposal would be changed.

  ### Type inference

  ...

  * Additional changes of constructor type inference algorithm are made as follows:
    * ...
    * If the expression contains an *object_initializer_list*, for each *initializer_element* of the list perform *lower-bound inference* from the type of the element to the type of *initializer_target*. If the binding of the element fails, skip it.
    * If the expression contains a *collection_initializer_list* and the type doesn't have overloads of the `Add` method, for each *initializer_element* of the list perform *lower-bound inference* from the types of the elements contained in the *initializer_element* to the types of the method's parameters. If the binding of any element fails, skip it.
    * If the expression contains a *collection_initializer_list* using an indexer, use the indexer defined in the type and perform *lower_bound_inference* from the types in *initializer_element* to types of matching parameters of the indexer.

  </details>

## Unresolved questions
[unresolved]: #unresolved-questions

* Would it be confusing to have the constructor type inference to work differently in subtle ways than the method call type inference ?
  
  Potential resolution: We could restrict the power of constructor type inference to use just parameters.

* Type inference for arrays

  In a similar way as we propose partial type inference in method type inference. 
  It can be used in *array_creation_expression* as well (e.g. `new C<_>[]{...}`). 
  However, It has the following complication.
  To avoid a breaking change, the type inference has to be as powerful as in method type inference. There is a question if it is still as valuable as in cases with methods.

* Type inference for delegates

  We can do the same thing for `delegate_creation_expression`. However, these expressions seems to be used rarely, so is it valuable to add the type inference for them as well ?

* Type inference for local variables

  Sometimes `var` keyword as a variable declaration is not sufficient.
  We would like to be able to specify more the type information about variable but still have some implementation details hidden.
  With the `_` placeholder we would be able to specify more the shape of the variable avoiding unnecessary specification of type arguments.

  ```csharp
  // I get an wrapper, which I'm interested in, but I don't care about the type arguments, 
  // because I don't need them in my code.
  Wrapper<_> wrapper = ... 
  wrapper.DoSomething( ... );
  ```

* Type inference for casting

  This can be useful with combination with already preparing collection literals.

  ```csharp
  var temp = (Span<_>)[1,2,3];
  ```

* Should we allow type inference of nested types like this `new Containing<_>.Nested<_>(42)` ?

    Potentional resolution: In my opinion, it looks weird and I wouldn't allow it, because then it would be coherent with method type inference and I don't think it would be common usage.
    I use nested type as a helper which contains logic of some part of the outside type.
    That means it does not usually use all type parameters defined in the outside type.
    Hence, there can be a lack of type info to infer type parameters of outside type when we try to use the constructor of nested type.
    From the theoretical point of view, it can be done.

* Is there a better choice for choosing the placeholder for inferred type argument ?

    Potentional resolution: My choice contained in the [detailed design](#detailed-design) is based on the following.

<details>

We base our choice on the usages specified below.

1. Type argument list of generic method call (e.g. `Foo<T1, T2>(...)`)
2. Type argument list of type creation (e.g. `new Bar<T1, T2>(...)`)
3. Type argument list of local variable (e.g. `Bar<T1, T2> temp = ...`)
4. Expressing array type (e.g. `T1[]`)
5. Expressing inferred type alone `T1` in local variable

**Diamond operator**

1. In the case of generic method calls it doesn't much make sense since method type inference is enabled by default without using angle brackets.

```csharp
Foo<>(arg1, arg2, arg3); // Doesn't bring us any additional info
```

2. There is an advantage. It can turn on the type inference. However, it would complicate overload resolution because we would have to search for every generic type of the same name no matter what arity. But could make a restriction. Usually, there is not more than one generic type with the same name. So when there will be just one type of that name, we can turn the inference on.

```csharp
new Bar<>(...); // Many constructors which we have to investigate for applicability
new Baz<>(...); // Its OK, we know what set of constructors to investigate.

class Bar { ... }
class Bar<T1> { ... }
class Bar<T1, T2> { ... }

class Baz<T1,T2> { ... }
```

3. It could make sense to specify just a wrapper of some type that gives us general API that doesn't involve its type arguments. It would say that the part of the code just cares about the wrapper. However, we think that it doesn't give us much freedom because type arguments usually appear in public API and only a few of them are for internal use. 

```csharp
Wrapper<> temp = ...
```

4. It doesn't seem very well.

```csharp
<>[] temp = ...
```

5. It clashes with `var` and looks wierd.

```csharp
<> temp = ... // equivalent to `var temp = ...`
```

**Whitespace seperated by commas**

1. It is able to specify the arity of the generic method. However, it seems to be messy when it is used in generic methods with many generic type parameters. Also, it already has its own meaning of expressing open generic type.

```csharp
Foo<, string, List<>, >(arg1, arg2, arg3);
```

1. The same reasoning as above. However, it could be use as an option to determining an arity of inferred type. `new Klass<,,,>(...)` determines exact class, which type arguments we want to infer. We wouldn't permit it with hinting the type arguments (e.g. `new Klass<,,int>(...)` would be invalid code). 

```csharp
new Bar<, string, List<>, >(arg1, arg2) { arg3 };
```

1. It doesn't work with array type.

```csharp
Bar<, string, List<>, > temp = ...
```

4. It doesn't seems very well.

```csharp
[] temp = ...
Foo<, [], >(arg1, arg2)
```

5. It looks like CSharp would not be a statically-typed language, clashed with `var` and probably introduce many implementation problems in the parser.

```csharp
temp = ...
```

**_ seperated by commas**

1. It specifies the arity of the generic method. It explicitly says that we want to infer this type argument. It seems to be less messy.

```csharp
Foo<_, string, List<_>, _>(arg1, arg2, arg3);
```

2. The same reasons as above.

```csharp
new Bar<_, string, List<_>, _>(arg1, arg2, arg3);
```

3. The same reasons as above.

```csharp
Bar<_, string, List<_>, _>(arg1, arg2);
```

4. Looks quite OK.

```csharp
_[] temp = ...
```

5. Clashes with `var` and seems to be wierd.

```csharp
_ temp = ...
```

**var seperated by commas**

1. More keystrokes. It starts to raise the question if it brings the advantage of saving keystrokes.

```csharp
Foo<var, string, List<var>, var>(arg1, arg2, arg3);
```

2. The same reasons as above

```csharp
new Bar<var, string, List<var>, var>(arg1, arg2, arg3);
```

3. The same reasons as above.

```csharp
Bar<var, string, List<var>, var>(arg1, arg2);
```

1. Looks OK.

```csharp
var[] temp = ...
```

5. State of the art.
   
```csharp
var temp = ...
```

**Something else seperated by commas**

Doesn't make a lot of sense because it needs to assign new meaning to that character in comparison with `_`, `var`, `<>`, `<,,,>`. 
Asterisk `*` can be considered, however, it can remind a pointer.  

**Conslusion**

I prefer `_` character. 
Additionally to that, I would prohibit using `_` in the same places as `var`.

</details>

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.
