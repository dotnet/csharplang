# Partial type inference

## Summary
[summary]: #summary

Partial type inference introduces a syntax skipping inferrable type arguments in the argument list of

1. *invocation_expression*
2. *object_creation_expresssion*

and allowing to specify just ambiguous ones.

An example of the partial type inference in *invocation_expression* can be seen below where the first type argument of the `M` method call is inferred by the compiler and the second type parameter is given in the type argument list. 

```csharp
void M<T1, T2>(T1 t1) { ... }
...
M<_, int>("text"); // Inferred as void M<string, int>(string)
```

An example of partial type inference in *object_creation_expression* can be seen below where the first type argument of the `C<T1, T2>` object is inferred by the compiler and the second type parameter is given in the type argument list.


```csharp
class C<T1, T2> 
{
    public C(T1 p1) { ... }
}
...
new C<_, int>("text"); // Inferred as C<string, int>.ctor(string)
```

## Motivation

Skipped since it was given in the previous document.

## Detailed design

### Grammar

We modify [Identifiers](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/lexical-structure.md#643-identifiers) as follows:

- The semantics of an identifier named `_` depends on the context in which it appears:
  - It can denote a named program element, such as a variable, class, or method, or
  - It can denote a discard (§9.2.9.1)**, or**
  - **It can denote a type to be inferred (See [Types/Inferred Type] section).**

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

### Types

We change the [8.1. General](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/types.md#81-general) section in the following way.

```diff
type
    : reference_type
    | value_type
    | type_parameter
    | pointer_type                  // unsafe code support
+   | inferred_type_placeholder
    ;

+inferred_type_placeholder
+    : '_'
+    ;
```

*inferred_type_placeholder* can be only used in the *constructor type inference* and the *method type inference*. (See the [Type Inference] section)

We change the [8.2.1 General](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/types.md#821-general) section in the following way.

```diff
non_array_type
    : value_type
    | class_type
    | interface_type
    | delegate_type
    | 'dynamic'
    | type_parameter
    | pointer_type      // unsafe code support
+   | inferred_type_placeholder 
    ;
```

We add the following new section to the [Types](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/types.md#8-types) section.

#### Inferred types

Inferred types represent a user explicitly written types, which have to be resolved during type inference.

Currently, inferred types can be used only in:
* a type argument of *invocation_expression* during method type inference 
* a type of *object_creation_expression* during constructor type inference. This type can be only a generic type which is *partial_inferred_type*. 

```csharp
F<_, int>( ... ); // _ represents an inferred type.
new C<_, int>( ... ); // C<_, int> represents an inferred type.
F<C<_>, int>( ... ); // C<_> represents an inferred type.
new C<C<_>, int>( ... ); // C<C<_>, int> represents an inferred type.
C<_> temp = ...; // _ nor C<_> doesn't represent an inferred type.
new _( ... ); // _ doesn't represent an inferred type.
new _[ ... ]; // _[ ... ] doesn't represent an inferred type.
new _?( ... ); // _? doesn't represent an inferred type.

// Container<_> doesn't represent an inferred type. 
// (Containing type's or method's type won't be inferred)
Container<_>.Method<_>(arg);
new Container<_>.Type<_>(arg);
```

* *inferred_type_placeholder* represents an unknown type denoted by the `_` identifier, which will be resolved during type inference.

* *partial_inferred_type* represents a type which contains *inferred_type_placeholder* in its syntax but it is not *inferred_type_placeholder*.
The contained uknown type(s) are inferred during type inference assembling the closed type. 

```csharp
_ // inferred_type_placeholder, but not partial_inferred_type
_? // partial_inferred_type, but not inferred_type
List<_> // partial_inferred_type, but not inferred_type
_[] //partial_inferred_type, but not inferred_type
```

* We can use a question mark `?` to say that the inferred type should be a nullable type (e.g. `F<_?>(...)`).

* A method group is said to be *partial_inferred* if it contains at least one *inferred_type_placeholder* or *partial_inferred_type* in its type argument list. 

* A type is said to be *partial_inferred* if it is *inferred_type_placeholder* or *partial_inferred_type*. 

* When a type with the `_` identifier is presented in the scope where *inferred_type_placeholder* is used, a warning should appear since the *inferred_type_placeholder* hides the type's name causing a breaking change. 
There are two possible resolutions of this warning. 
If the `_` identifier should represent *inferred_type_placeholder*, the user should suppress the warning or should rename the type or alias declaration.
If the `_` identifier should represent a type, the user should use `@_` to explicitly reference a typename.

* When there is a type or alias declaration with the `_` identifier, a warning should appear since it is a contextual keyword.
A possible resolution would be to rename the declaration or suppress the warning when the declaration can't be renamed.     

### Method invocations

The binding-time processing of a [method invocation](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12892-method-invocations) of the form `M(A)`, where `M` is a method group (possibly including a *type_argument_list*), and `A` is an optional *argument_list* is changed in the following way.

The initial set of candidate methods for is changed by adding new condition.

- If `F` is non-generic, `F` is a candidate when:
  - `M` has no type argument list, and
  - `F` is applicable with respect to `A` (§12.6.4.2).
- If `F` is generic and `M` has no type argument list, `F` is a candidate when:
  - Type inference (§12.6.3) succeeds, inferring a list of type arguments for the call, and
  - Once the inferred type arguments are substituted for the corresponding method type parameters, all constructed types in the parameter list of `F` satisfy their constraints ([§8.4.5](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/types.md#845-satisfying-constraints)), and the parameter list of `F` is applicable with respect to `A` (§12.6.4.2)
- **If `F` is generic and `M` is *partial_inferred*, `F` is a candidate when:**
  - **Method type inference (See the [Type Inference] section) succeeds, inferring the type arguments list for the call, and**
  - **Once the inferred type arguments are substituted for the corresponding method type parameters, all constructed types in the parameter list of `F` satisfy their constraints ([§8.4.5](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/types.md#845-satisfying-constraints)), and the parameter list of `F` is applicable with respect to `A` ([§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12642-applicable-function-member))**
- If `F` is generic and `M` includes a type argument list, `F` is a candidate when:
  - `F` has the same number of method type parameters as were supplied in the type argument list, and
  - Once the type arguments are substituted for the corresponding method type parameters, all constructed types in the parameter list of `F` satisfy their constraints (§8.4.5), and the parameter list of `F` is applicable with respect to `A` (§12.6.4.2).

### Object creation expressions

The binding-time processing of an [*object_creation_expression*](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#128162-object-creation-expressions) of the form new `T(A)`, where `T` is a *class_type*, or a *value_type*, and `A` is an optional *argument_list*, is changed in the following way.

The binding-time processing of an *object_creation_expression* of the form new `T(A)`, where `T` is a *class_type*, or a *value_type*, and `A` is an optional *argument_list*, consists of the following steps:

- if `T` is a *type_parameter* and `A` is not present:
  - If no value type constraint or constructor constraint (§15.2.5) has been specified for `T`, a binding-time error occurs.
  - The result of the *object_creation_expression* is a value of the run-time type that the type parameter has been bound to, namely the result of invoking the default constructor of that type. The run-time type may be a reference type or a value type.
- Otherwise, if `T` is a *class_type* or a *struct_type*:
  - If `T` is an abstract or static *class_type*, a compile-time error occurs.
  - **The instance constructor to invoke is determined using the overload resolution rules of [§12.6.4](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1264-overload-resolution). The set of candidate instance constructors is determined as follows:**
    - **`T` is not inferrred (*partial_inferred*), the constructor is accessible in `T`, and is applicable with respect to `A` ([§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12642-applicable-function-member)).**
    - **If `T` is *partial_inferred* and the constructor is accessible in `T`, constructor type inference of the constructor is performed. Once the type arguments are inferred and substituted for the corresponding type parameters, all constructed types in the parameter list of the constructor satisfy their constraints, and the parameter list of the constructor is applicable with respect to `A` ([§12.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12642-applicable-function-member)).**
  - A binding-time error occurs when:
    - The set of candidate instance constructors is empty, or if a single best instance constructor cannot be identified.
  - The result of the *object_creation_expression* is a value of type `T`, namely the value produced by invoking the instance constructor determined in the two steps above.
  - Otherwise, the *object_creation_expression* is invalid, and a binding-time error occurs.

### Type inference

We replace the [type inference/general](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12631-general) section with the following section.

* Type inference for generic method invocation is performed when the invocation:
  * Doesn't have a *type_argument_list*.
  * The method group is *partial_inferred*.

  ```csharp
  M( ... ); // Type inference is invoked.
  M<_, string>( ... ); // Type inference is invoked.
  M<List<_>, string>( ... ); // Type inference is invoked.
  ```

* Type inference is applied to each generic method in the method group. 

* Type inference for constructors is performed when the generic type of *object_creation_expression* is *partially_inferred*.
  
  ```csharp
  new C<_, string>( ... ); // Type inference is invoked.
  new C<List<_>, string>( ... ); // Type inference is invoked.
  ```

* Type inference is applied to each constructor which is contained in the type. 

* When one of the cases appears, a ***type inference*** process attempts to infer type arguments for the call or type. 
  The presence of type inference allows a more convenient syntax to be used for calling a generic method or creating an object of a generic type, and allows the programmer to avoid specifying redundant type information.

* In the case of *method type inference*, we infer method type parameters. 
  In the case of *constructor type inference*, we infer type parameters of a type defining the constructors. 
  The previous sentence prohibits inferring type parameters of an outside type that contains the inferred type. (e.g. inference of `new Containing<_>.Nested<_>(42)` is not allowed)

* Type inference occurs as part of the binding-time processing of a [method invocation](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#12892-method-invocations) or an [object_creation_expression](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#128162-object-creation-expressions) and takes place before the overload resolution step of the invocation.
   
* If type inference succeeds, then the inferred type arguments are used to determine the types of arguments for subsequent overload resolution. 
  If overload resolution chooses a generic method or constructor as the one to invoke, then the inferred type arguments are used as the type arguments for the invocation or for the type containing the constructor.
  If type inference for a particular method or constructor fails, that method or constructor does not participate in overload resolution. 
  The failure of type inference, in and of itself, does not cause a binding-time error. However, it often leads to a binding-time error when overload resolution then fails to find any applicable methods or constructors.

* If each supplied argument does not correspond to exactly one parameter in the method or constructor [corresponding-parameters](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12622-corresponding-parameters), or there is a non-optional parameter with no corresponding argument, then inference immediately fails. 
  Otherwise, assume that the generic method has the following signature:

  `Tₑ M<X₁...Xᵥ>(T₁ p₁ ... Tₓ pₓ)`

  With a method call of the form `M<...>(E₁ ...Eₓ)` the task of type inference is to find unique type arguments `S₁...Sᵥ` for each of the type parameters `X₁...Xᵥ` so that the call `M<S₁...Sᵥ>(E₁...Eₓ)` becomes valid.

  In case of construtor, assume the following signature:
  
  `M<X₁...Xᵥ>..ctor(T₁ p₁ ... Tₓ pₓ)` 

  With a constructor call of the form `new M<...>(E₁ ...Eₓ)` the task of type inference is to find unique type arguments `S₁...Sᵥ` for each of the type parameters `X₁...Xᵥ` so that the call `new M<S₁...Sᵥ>(E₁...Eₓ)` becomes valid.

* The process of type inference is described below as an algorithm. A conformant compiler may be implemented using an alternative approach, provided it reaches the same result in all cases.

* During the process of inference each type variable `Xᵢ` is either *fixed* to a particular type `Sᵢ` or *unfixed* with an associated set of *bounds.* Each of the bounds is some type `T`. Initially each type variable `Xᵢ` is unfixed with an empty set of bounds.

* Type inference takes place in phases. Each phase will try to infer types for more type variables based on the findings of the previous phase. The first phase makes some initial inferences of bounds, whereas the second phase fixes type variables to specific types and infers further bounds. The second phase may have to be repeated a number of times.

* Additional changes of method type inference algorithm are made as follows:
  * If the inferred method group contains a nonempty *type_argument_list*.
    * We replace each *inferred_type_placeholder* with a new type variable `X`.
    * We perform *shape inference* from each type argument to the corresponding type parameter.

* Additional changes of constructor type inference algorithm are made as follows:
  * If the inferred type contains a nonempty *type_argument_list*.
    * We replace each *inferred_type_placeholder* with a new type variable `X`.
    * We perform *shape inference* from each type argument to the corresponding type parameter.

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

### Compile-time checking of dynamic member invocation

We change the [compile-time checking](https://github.com/dotnet/csharpstandard/blob/draft-v7/standard/expressions.md#1265-compile-time-checking-of-dynamic-member-invocation).

- First, if `F` is a generic method and type arguments were provided, then if the method group is *partial_inferred*, an binding error occurs since it is not supported in the runtime binding.

We add the following
- If an object creation expression is inferred and the argument list contains a dynamic value, an binding error appears since constructor type inference is not supported by runtime.

## Drawbacks

Skipped since it was given in the previous document.

## Alternatives

Skipped since it was given in the previous document.

## Unresolved questions

Skipped since it was given in the previous document.