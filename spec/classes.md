# Classes

## General

A `class` is a data structure that may contain data members (`constants` and `fields`), function members (`methods`, `properties`, `events`, `indexers`, `operators`, `instance` constructors, `finalizers`, and `static` constructors), and nested types. Class types support inheritance, a mechanism whereby a ***derived class*** can extend and specialize a ***base class***.

## Class declarations

### General

A *class-declaration* is a *type-declaration* (§14.7) that declares a new class.

```ANTLR
class-declaration:
  attributes? class-modifiers partial? class identifier type-parameter-list?
  class-base? type-parameter-constraints-clauses class-body ;?
```

A *class-declaration* consists of an optional set of *attributes* (§22), followed by an optional set of *class-modifiers* (§15.2.2), followed by an optional `partial` modifier (§15.2.7), followed by the keyword `class` and an *identifier* that names the class, followed by an optional *type-parameter-list* (§15.2.3), followed by an optional *class-base* specification (§15.2.4), followed by an optional set of *type-parameter-constraints-clauses* (§15.2.5), followed by a *class-body* (§15.2.6), optionally followed by a semicolon.

A `class` declaration shall not supply a *type-parameter-constraints-clauses* unless it also supplies a *type-parameter-list*.

A `class` declaration that supplies a *type-parameter-list* is a generic `class` declaration. Additionally, any `class` nested inside a generic `class` declaration or a generic `struct` declaration is itself a generic `class` declaration, since type arguments for the containing type shall be supplied to create a constructed type.

### Class modifiers

#### General

A *class-declaration* may optionally include a sequence of `class` modifiers:
```ANTLR
class-modifiers:
    class-modifier
    class-modifiers class-modifier

class-modifier:
    new
    public
    protected
    internal
    private
    abstract
    sealed
    static
```
It is a compile-time error for the same modifier to appear multiple times in a `class` declaration.

The `new` modifier is permitted on nested classes. It specifies that the `class` hides an inherited member by the same name, as described in §15.3.5. It is a compile-time error for the `new` modifier to appear on a `class` declaration that is not a nested `class` declaration.

The `public`, `protected`, `internal`, and `private` modifiers control the accessibility of the class. Depending on the context in which the `class` declaration occurs, some of these modifiers might not be permitted (§8.5.2).

When a partial type declaration (§15.2.7) includes an accessibility specification (via the `public`, `protected`, `internal`, and `private` modifiers), that specification shall agree with all other parts that include an accessibility specification. If no part of a partial type includes an accessibility specification, the type is given the appropriate default accessibility (§8.5.2).

The `abstract`, `sealed`, and `static` modifiers are discussed in the following subclauses.

#### Abstract classes

The `abstract` modifier is used to indicate that a `class` is incomplete and that it is intended to be used only as a base class. An ***abstract class*** differs from a ***non-abstract class*** in the following ways:

-  An `abstract` class cannot be instantiated directly, and it is a compile-time error to use the `new` operator on an abstract class. While it is possible to have variables and values whose compile-time types are abstract, such variables and values will necessarily either be `null` or contain references to instances of non-abstract classes derived from the abstract types.
-  An `abstract` class is permitted (but not required) to contain `abstract` members.
-  An `abstract` class cannot be sealed.

When a non-abstract class is derived from an `abstract` class, the non-abstract class shall include actual implementations of all inherited `abstract` members, thereby overriding those `abstract` members. [Example: In the following code

```csharp
abstract class A
{
    public abstract void F();
}

abstract class B: A
{
    public void G() {}
}

class C: B
{
    public override void F() {
        // actual implementation of F
    }
}
```

the abstract class `A` introduces an abstract method `F`. Class `B` introduces an additional method `G`, but since it doesn’t provide an implementation of `F`, `B` shall also be declared abstract. Class `C` overrides `F` and provides an actual implementation. Since there are no `abstract` members in `C`, `C` is permitted (but not required) to be non-abstract. end example]

If one or more parts of a partial type declaration (§15.2.7) of a `class` include the `abstract` modifier, the `class` is abstract. Otherwise, the `class` is non-abstract.

#### Sealed classes

The `sealed` modifier is used to prevent derivation from a class. A compile-time error occurs if a sealed `class` is specified as the base class of another class.

A sealed `class` cannot also be an abstract class.

> [!NOTE] 
> The `sealed` modifier is primarily used to prevent unintended derivation, but it also enables certain run-time optimizations. In particular, because a sealed `class` is known to never have any derived classes, it is possible to transform `virtual` function member invocations on sealed `class` instances into non-virtual invocations. 

If one or more parts of a partial type declaration (§15.2.7) of a `class` include the `sealed` modifier, the `class` is sealed. Otherwise, the `class` is unsealed.

#### Static Classes

##### General

The `static` modifier is used to mark the `class` being declared as a ***`static class`***. A `static class` shall not be instantiated, shall not be used as a type and shall contain only `static` members. Only a `static` class can contain declarations of extension methods (§15.6.10).

A `static class` declaration is subject to the following restrictions:

-  A `static` class shall not include a `sealed` or `abstract` modifier. (However, since a `static` class cannot be instantiated or derived from, it behaves as if it was both `sealed` and `abstract`.)
-  A `static` class shall not include a *class-base* specification (§15.2.4) and cannot explicitly specify a base class or a list of implemented interfaces. A `static` class implicitly inherits from type `object`.
-  A `static` class shall only contain `static` members (§15.3.8). 

> [!NOTE] 
> All constants and nested types are classified as `static` members. 

-  A `static` class shall not have members with `protected` or `protected internal` declared accessibility.

It is a compile-time error to violate any of these restrictions.

A `static class` has no instance constructors. It is not possible to declare an instance constructor in a `static class`, and no default instance constructor (§15.11.5) is provided for a `static class`.

The members of a `static class` are not automatically static, and the member declarations shall explicitly include a `static` modifier (except for constants and nested types). When a `class` is nested within a static outer class, the nested `class` is not a `static class` unless it explicitly includes a `static` modifier.

If one or more parts of a partial type declaration (§15.2.7) of a `class` include the `static` modifier, the `class` is static. Otherwise, the `class` is not `static`.

##### Referencing `static class` types

A *namespace-or-type-name* (§8.8) is permitted to reference a `static class` if

-  The *namespace-or-type-name* is the `T` in a *namespace-or-type-name* of the form `T.I`, or
-  The *namespace-or-type-name* is the `T` in a *typeof-expression* (§12.7.12) of the form `typeof(T)`.
A *primary-expression* (§12.7) is permitted to reference a `static class` if
-  The *primary-expression* is the `E` in a *member-access* (§12.7.5) of the form `E.I`.

In any other context, it is a compile-time error to reference a `static` class.

> [!NOTE] 
> For example, it is an error for a `static class` to be used as a base class, a constituent type (§15.3.7) of a member, a generic type argument, or a type parameter constraint. Likewise, a `static class` cannot be used in an array type, a pointer type, a new expression, a cast expression, an is expression, an as expression, a sizeof expression, or a default value expression. 

### Type parameters

A type parameter is a simple identifier that denotes a placeholder for a type argument supplied to create a constructed type. By constrast, a type argument (§9.4.2) is the type that is substituted for the type parameter when a constructed type is created.

[](#Grammar_type_parameter_list)
```ANTLR
type-parameter-list:
  < type-parameters >
```

[](#Grammar_type_parameters)
```ANTLR
type-parameters:
  attributes~opt~ type-parameter
  type-parameters , attributes~opt~ type-parameter
```

*type-parameter* is defined in §9.5.

Each type parameter in a `class` declaration defines a name in the declaration space (§8.3) of that class. Thus, it cannot have the same name as another type parameter of that `class` or a member declared in that class. A type parameter cannot have the same name as the type itself.

Two partial generic type declarations (in the same program) contribute to the same unbound generic type if they have the same fully qualified name (which includes a *generic-dimension-specifier* (§12.7.12) for the number of type parameters) (§8.8.3). Two such partial type declarations shall specify the same name for each type parameter, in order.

### Class base specification

#### General

A `class` declaration may include a *class-base* specification, which defines the direct base class of the `class` and the interfaces (§18) directly implemented by the class.

[](#Grammar_class_base)
```ANTLR
class-base:
  : class-type
  : interface-type-list
  : class-type , interface-type-list

interface-type-list:
  interface-type
  interface-type-list , interface-type
```

#### Base classes

When a *class-type* is included in the *class-base*, it specifies the direct base class of the `class` being declared. If a non-partial class declaration has no *class-base*, or if the *class-base* lists only interface types, the direct base class is assumed to be `object`. When a partial class declaration includes a base class specification, that base class specification shall reference the same type as all other parts of that partial type that include a base class specification. If no part of a partial class includes a base class specification, the base class is object. A `class` inherits members from its direct base class, as described in §15.3.4.

[Example: In the following code

```csharp
class A {}

class B: A {}
```

Class `A` is said to be the direct base class of `B`, and `B` is said to be derived from `A`. Since `A` does not explicitly specify a direct base class, its direct base class is implicitly object. end example]

For a constructed `class` type, including a nested type declared within a generic type declaration (§16.3.9.7), if a base class is specified in the generic `class` declaration, the base class of the constructed type is obtained by substituting, for each *type-parameter* in the base class declaration, the corresponding *type-argument* of the constructed type. 

[Example: Given the generic `class` declarations

```csharp
class B<U,V> {…}

class G<T>: B<string,T[]> {…}
```

the base class of the constructed type `G<int>` would be `B<string,int[]>`. end example]

The base class specified in a `class` declaration can be a constructed `class` type (§9.4). A base class cannot be a type parameter on its own (§9.5), though it can involve the type parameters that are in scope. [Example:

```csharp
class Base<T> {}

class Extend : Base<int> // Valid, non-constructed class with
// constructed base class

class Extend<V>: V {} // Error, type parameter used as base class

class Extend<V> : Base<V> {} // Valid, type parameter used as type
// argument for base class
```

end example]

The direct base class of a `class` type shall be at least as accessible as the `class` type itself (§8.5.5). For example, it is a compile-time error for a public class to derive from a private or internal class.

The direct base class of a `class` type shall not be any of the following types: `System.Array`, `System.Delegate`, `System.Enum`, or `System.ValueType`. Furthermore, a generic `class` declaration shall not use `System.Attribute` as a direct or indirect base class (§22.2.1).

In determining the meaning of the direct base class specification `A` of a class `B`, the direct base class of `B` is temporarily assumed to be `object`, which ensures that the meaning of a base class specification cannot recursively depend on itself. [Example: The following

```csharp
class X<T> {

    public class Y{}

}

class Z : X<Z.Y> {}
```

Is in error since in the base class specification `X<Z.Y>` the direct base class of `Z` is considered to be object, and hence (by the rules of §8.8) `Z` is not considered to have a member `Y`. end example]

The base classes of a `class` are the direct base class and its base classes. In other words, the set of base classes is the transitive closure of the direct base class relationship. [Example: In the following:

```csharp
class A {…}

class B<T>: A {…}

class C<T>: B<IComparable<T>> {…}

class D<T>: C<T[]> {…}
```

the base classes of `D<int>` are `C<int[]>`, `B<IComparable<int[]>>`, `A`, and `object`.

end example]

Except for `class` `object`, every `class` has exactly one direct base class. The `object` class has no direct base class and is the ultimate base class of all other classes.

It is a compile-time error for a `class` to depend on itself. For the purpose of this rule, a `class` ***directly depends on*** its direct base class (if any) and *directly depends on* the nearest enclosing `class` within which it is nested (if any). Given this definition, the complete set of classes upon which a `class` depends is the transitive closure of the *directly depends on* relationship.

[Example: The example

```csharp
class A: A {}
```

Is erroneous because the `class` depends on itself. Likewise, the example

```csharp
class A: B {}

class B: C {}

class C: A {}
```

is in error because the classes circularly depend on themselves. Finally, the example

```csharp
class A: B.C {}

class B: A
{
    public class C {}
}
```

results in a compile-time error because A depends on `B.C` (its direct base class), which depends on `B` (its immediately enclosing class), which circularly depends on `A`. end example]

A `class` does not depend on the classes that are nested within it. [Example: In the following code

```csharp
class A
{
class B: A {}
}
```

`B` depends on `A` (because `A` is both its direct base class and its immediately enclosing class), but `A` does not depend on `B` (since `B` is neither a base class nor an enclosing `class` of `A`). Thus, the example is valid. end example]

It is not possible to derive from a `sealed` class. [Example: In the following code

```csharp
sealed class A {}

class B: A {} // Error, cannot derive from a sealed class
```

class `B` is in error because it attempts to derive from the `sealed` class `A`. end example]

#### Interface implementations

A *class-base* specification may include a list of interface types, in which case the `class` is said to implement the given interface types. For a constructed `class` type, including a nested type declared within a generic type declaration (§15.3.9.7), each implemented interface type is obtained by substituting, for each *type-parameter* in the given interface, the corresponding *type-argument* of the constructed type.

The set of interfaces for a type declared in multiple parts (§15.2.7) is the union of the interfaces specified on each part. A particular interface can only be named once on each part, but multiple parts can name the same base interface(s). There shall only be one implementation of each member of any given interface. [Example: In the following:

```csharp
partial class C: IA, IB {…}

partial class C: IC {…}

partial class C: IA, IB {…}
```

the set of base interfaces for class `C` is `IA`, `IB`, and `IC`. end example]

Typically, each part provides an implementation of the interface(s) declared on that part; however, this is not a requirement. A part can provide the implementation for an interface declared on a different part. [Example:

```csharp
partial class X
{
    int IComparable.CompareTo(object o) {…}
}

partial class X: IComparable
{
    …
}
```

end example]

The base interfaces specified in a `class` declaration can be constructed interface types (§9.4, §18.2). A base interface cannot be a type parameter on its own, though it can involve the type parameters that are in scope. [Example: The following code illustrates how a `class` can implement and extend constructed types:

```csharp
class C<U, V> {}

interface I1<V> {}

class D: C<string, int>, I1<string> {}

class E<T>: C<int, T>, I1<T> {}
```

end example]

Interface implementations are discussed further in §18.6.

### Type parameter constraints

Generic type and method declarations can optionally specify type parameter constraints by including *type-parameter-constraints-clause*s.

```ANTLR
type-parameter-constraints-clauses:
    type-parameter-constraints-clause
    type-parameter-constraints-clauses type-parameter-constraints-clause
    
type-parameter-constraints-clause:
    'where' type-parameter ':' type-parameter-constraints

type-parameter-constraints:
    primary-constraint
    secondary-constraints
    constructor-constraint
    primary-constraint ',' secondary-constraints
    primary-constraint ',' constructor-constraint
    secondary-constraints ',' constructor-constraint
    primary-constraint ',' secondary-constraints ',' constructor-constraint

primary-constraint:
    class-type
    'class'
    'struct'

secondary-constraints:
    interface-type
    type-parameter
    secondary-constraints ',' interface-type
    secondary-constraints ',' type-parameter

constructor-constraint:
    'new' '(' ')'
```

Each *type-parameter-constraints-clause* consists of the token `where`, followed by the name of a type parameter, followed by a colon and the list of constraints for that type parameter. There can be at most one `where` clause for each type parameter, and the `where` clauses can be listed in any order. Like the `get` and `set` tokens in a property accessor, the `where` token is not a keyword.

The list of constraints given in a `where` clause can include any of the following components, in this order: a single primary constraint, one or more secondary constraints, and the constructor constraint, `new()`.

A primary constraint can be a `class` type or the ***reference type constraint*** `class` or the ***value type constraint*** `struct`. A secondary constraint can be a *type-parameter* or *interface-type*.

The reference type constraint specifies that a type argument used for the type parameter shall be a reference type. All `class` types, `interface` types, `delegate` types, `array` types, and type parameters known to be a reference type (as defined below) satisfy this constraint.

The `value` type constraint specifies that a type argument used for the type parameter shall be a non-nullable `value` type. All non-nullable `struct` types, `enum` types, and type parameters having the `value` type constraint satisfy this constraint. Note that although classified as a `value` type, a nullable value type (§9.3.11) does not satisfy the `value` type constraint. A type parameter having the `value` type constraint shall not also have the *constructor-constraint*, although it may be used as a type argument for another type parameter with a *constructor-constraint*. 

> [!NOTE] 
> The `System.Nullable<T>` type specifies the non-nullable value type constraint for `T`. Thus, recursively constructed types of the forms `T??` and `Nullable<Nullable<T>>` are prohibited. 

`Pointer` types are never allowed to be `type` arguments and are not considered to satisfy either the `reference` type or `value` type constraints.

If a constraint is a `class` type, an `interface` type, or a type parameter, that type specifies a minimal “base type” that every type argument used for that type parameter shall support. Whenever a constructed `type` or generic `method` is used, the `type` argument is checked against the constraints on the type parameter at compile-time. The `type` argument supplied shall satisfy the conditions described in §9.4.5.

A *class-type* constraint shall satisfy the following rules:

-  The type shall be a `class` type.
-  The type shall not be `sealed`.
-  The type shall not be one of the following types: `System.Array`, `System.Delegate`, `System.Enum`, or `System.ValueType`.
-  The type shall not be `object`.
-  At most one constraint for a given type parameter may be a `class` type.

A type specified as an *interface-type* constraint shall satisfy the following rules:

-  The type shall be an `interface` type.
-  A type shall not be specified more than once in a given where clause.

In either case, the constraint may involve any of the type parameters of the associated type or `method` declaration as part of a `constructed` type, and may involve the type being declared.

Any `class` or `interface` type specified as a type parameter constraint shall be at least as accessible (§8.5.5) as the `generic` type or method being declared.

A type specified as a *type-parameter* constraint shall satisfy the following rules:

-  The type shall be a type parameter.
-  A type shall not be specified more than once in a given `where` clause.

In addition there shall be no cycles in the dependency graph of type parameters, where dependency is a transitive relation defined by:

-  If a type parameter `T` is used as a constraint for type parameter `S` then `S` ***depends on*** `T`.
-  If a type parameter `S` depends on a type parameter `T` and `T` depends on a type parameter `U` then `S` ***depends on*** `U`.

Given this relation, it is a compile-time error for a type parameter to depend on itself (directly or indirectly).

Any constraints shall be consistent among dependent type parameters. If type parameter `S` depends on type parameter `T` then:

-  `T` shall not have the `value` type constraint. Otherwise, `T` is effectively sealed so `S` would be forced to be the same type as `T`, eliminating the need for two type parameters.
-  If `S` has the value type constraint then `T` shall not have a *class-type* constraint.
-  If `S` has a *class-type* constraint `A` and `T` has a *class-type* constraint `B` then there shall be an identity conversion or implicit reference conversion from `A` to `B` or an implicit reference conversion from `B` to `A`.
-  If `S` also depends on type parameter `U` and `U` has a *class-type* constraint `A` and `T` has a *class-type* constraint `B` then there shall be an identity conversion or implicit reference conversion from `A` to `B` or an implicit reference conversion from `B` to `A`.

It is valid for `S` to have the value type constraint and `T` to have the reference type constraint. Effectively this limits `T` to the types `System.Object`, `System.ValueType`, `System.Enum`, and any interface type.

If the `where` clause for a type parameter includes a constructor constraint (which has the form `new()`), it is possible to use the `new` operator to create instances of the type (§12.7.11.2). Any type argument used for a type parameter with a constructor constraint shall be a `value` type, a non-abstract class having a public parameterless constructor, or a type parameter having the `value` type constraint or constructor constraint.

[Example: The following are examples of constraints:

```csharp
interface IPrintable
{
    void Print();
}

interface IComparable<T>
{
    int CompareTo(T value);
}

interface IKeyProvider<T>
{

    T GetKey();
}

class Printer<T> where T: IPrintable {…}

class SortedList<T> where T: IComparable<T> {…}

class Dictionary<K,V>
    where K: IComparable<K>
    where V: IPrintable, IKeyProvider<K>, new()
{
…
}
```

The following example is in error because it causes a circularity in the dependency graph of the type parameters:

```csharp
class Circular<S,T>
    where S: T
    where T: S // Error, circularity in dependency graph
{
…
}
```

The following examples illustrate additional invalid situations:

```csharp
class Sealed<S,T>
    where S: T
    where T: struct // Error, `T` is sealed
{
…
}

class A {…}

class B {…}

class Incompat<S,T>
    where S: A, T
    where T: B // Error, incompatible class-type constraints
{
…
}

class StructWithClass<S,T,U>
    where S: struct, T
    where T: U
    where U: A // Error, A incompatible with struct
{
…
}
```

end example]

The ***dynamic erasure*** of a type `C` is type C<sub>o</sub> constructed as follows: 

-  If `C` is a *nested type* `Outer.Inner` then C<sub>o</sub> is a nested type `Outer`<sub>o</sub>.Inner<sub>o</sub>.
-  If `C` is a *constructed type* G<A<sup>1</sup>, …, A<sup>n</sup>> with type arguments A<sup>1</sup>, …, A<sup>n</sup> then C<sub>o</sub> is the constructed type G<A<sup>1</sup><sub>o</sub>, …, A<sup>n</sup><sub>o</sub>>.
-  If `C` is an *array type* `E[]` then C<sub>o</sub> is the array type E<sub>o</sub>[].
-  If `C` is a *pointer type* `E\*` then C<sub>o</sub> is the pointer type E<sub>o</sub>\*.
-  If `C` is dynamic then C<sub>o</sub> is object.
-  Otherwise, C<sub>o</sub> is `C`.

The ***effective base class*** of a type parameter `T` is defined as follows:

Let `R` be a set of types such that:

-  For each constraint of `T` that is a *type-parameter*, `R` contains its effective base class.
-  For each constraint of `T` that is a *struct-type*, `R` contains `System.ValueType`.
-  For each constraint of `T` that is an *enumeration type*, `R` contains `System.Enum`.
-  For each constraint of `T` that is a *delegate type*, `R` contains its dynamic erasure.
-  For each constraint of `T` that is an *array type*, `R` contains `System.Array`.
-  For each constraint of `T` that is a *class-type*, `R` contains its dynamic erasure.

Then

-  If `T` has the value type constraint, its *effective base class* is `System.ValueType`.
-  Otherwise, if `R` is empty then the *effective base class* is object.
-  Otherwise, the *effective base class* of `T` is the most-encompassed type (§11.5.3) of set `R`. If the set has no encompassed type, the *effective base class* of `T` is object. The consistency rules ensure that the most-encompassed type exists.

If the type parameter is a `method` type parameter whose constraints are inherited from the base method the *effective base class* is calculated after type substitution.

These rules ensure that the effective base class is always a *class-type*.

The ***effective interface set*** of a type parameter `T` is defined as follows:

-  If `T` has no *secondary-constraints*, its effective interface set is empty.
-  If `T` has *interface-type* constraints but no *type-parameter* constraints, its effective interface set is the set of dynamic erasures of its *interface-type* constraints.
-  If `T` has no *interface-type* constraints but has *type-parameter* constraints, its effective interface set is the union of the effective interface sets of its *type-parameter* constraints.
-  If `T` has both *interface-type* constraints and *type-parameter* constraints, its effective interface set is the union of the set of dynamic erasures of its *interface-type* constraints and the effective interface sets of its *type-parameter* constraints.

A type parameter is ***known to be a reference type*** if it has the reference type constraint or its effective base class is not `object` or `System.ValueType`.

Values of a constrained type parameter type can be used to access the instance members implied by the constraints. [Example: In the following:

```csharp
interface IPrintable
{
    void Print();
}

class Printer<T> where T: IPrintable
{
void PrintOne(T x) {
    x.Print();
    }
}
```

the methods of `IPrintable` can be invoked directly on `x` because `T` is constrained to always implement `IPrintable`. end example]

When a partial generic type declaration includes constraints, the constraints shall agree with all other parts that include constraints. Specifically, each part that includes constraints shall have constraints for the same set of type parameters, and for each type parameter, the sets of primary, secondary, and constructor constraints shall be equivalent. Two sets of constraints are equivalent if they contain the same members. If no part of a partial generic type specifies type parameter constraints, the type parameters are considered unconstrained. [Example:

```csharp
partial class Map<K,V>
    where K: IComparable<K>
    where V: IKeyProvider<K>, new()
{
…
}

partial class Map<K,V>
    where V: IKeyProvider<K>, new()
    where K: IComparable<K>
{
…
}

partial class Map<K,V>
{
…
}
```

is correct because those parts that include constraints (the first two) effectively specify the same set of primary, secondary, and constructor constraints for the same set of type parameters, respectively. end example]

### Class body

The *class-body* of a `class` defines the members of that class.

```ANTLR
class-body:
  { class-member-declarations<sub>opt</sub> }
```

### Partial declarations

The modifier `partial` is used when defining a `class`, `struct`, or `interface` type in multiple parts. The `partial` modifier is a contextual keyword (§7.4.4) and only has special meaning immediately before one of the keywords `class`, `struct`, or `interface`.

Each part of a ***partial type*** declaration shall include a `partial` modifier and shall be declared in the same namespace or containing type as the other parts. The `partial` modifier indicates that additional parts of the type declaration might exist elsewhere, but the existence of such additional parts is not a requirement; it is valid for the only declaration of a type to include the `partial` modifier.

All parts of a partial type shall be compiled together such that the parts can be merged at compile-time. Partial types specifically do not allow already compiled types to be extended.

Nested types can be declared in multiple parts by using the `partial` modifier. Typically, the containing type is declared using `partial` as well, and each part of the nested type is declared in a different part of the containing type.

[Example: The following `partial` class is implemented in two parts, which reside in different source files. The first part is machine generated by a database-mapping tool while the second part is manually authored:

```csharp
public partial class Customer
{
    private int id;
    private string name;
    private string address;
    private List<Order> orders;
    
    public Customer() {
    …
    }
}
    
public partial class Customer
{
    public void SubmitOrder(Order orderSubmitted) {
    orders.Add(orderSubmitted);
    }
    
    public bool HasOutstandingOrders() {
    return orders.Count > 0;
    }
}
```

When the two parts above are compiled together, the resulting code behaves as if the `class` had been written as a single unit, as follows:

```csharp
public class Customer
{
    private int id;
    private string name;
    private string address;
    private List<Order> orders;
    
    public Customer() {
    …
    }
    
    public void SubmitOrder(Order orderSubmitted) {
    orders.Add(orderSubmitted);
    }
    
    public bool HasOutstandingOrders() {
    return orders.Count > 0;
    }
}
```

end example]

The handling of attributes specified on the type or type parameters of different parts of a partial declaration is discussed in §22.3.

## Class members

### General

The members of a `class` consist of the members introduced by its *class-member-declaration*s and the members inherited from the direct base class.

```ANTLR
class-member-declarations:
    class-member-declaration
    class-member-declarations class-member-declaration

class-member-declaration:
    constant-declaration
    field-declaration
    method-declaration
    property-declaration
    event-declaration
    indexer-declaration
    operator-declaration
    constructor-declaration
    finalizer-declaration
    static-constructor-declaration
    type-declaration
```

The members of a `class` are divided into the following categories:

-  `Constants`, which represent constant values associated with the `class` (§15.4).
-  `Fields`, which are the variables of the `class` (§15.5).
-  `Methods`, which implement the computations and actions that can be performed by the `class` (§15.6).
-  `Properties`, which define named characteristics and the actions associated with reading and writing those characteristics (§15.7).
-  `Events`, which define notifications that can be generated by the `class` (§15.8).
-  `Indexers`, which permit instances of the `class` to be indexed in the same way (syntactically) as arrays (§15.9).
-  `Operators`, which define the expression operators that can be applied to instances of the `class` (§15.10).
-  `Instance` constructors, which implement the actions required to initialize instances of the `class` (§15.11)
-  `Finalizers`, which implement the actions to be performed before instances of the `class` are permanently discarded (§15.13).
-  `Static` constructors, which implement the actions required to initialize the `class` itself (§15.12).
-  `Types`, which represent the types that are local to the `class` (§14.7).

Members that can contain executable code are collectively known as the *function members* of the class. The function members of a `class` are the `methods`, `properties`, `events`, `indexers`, `operators`, `instance` constructors, `finalizers`, and `static` constructors of that class.

A *class-declaration* creates a new declaration space (§8.3), and the *type-parameter*sand the *class-member-declarations* immediately contained by the *class-declaration* introduce new members into this declaration space. The following rules apply to *class-member-declaration*s:

-  `Instance` constructors, `finalizers`, and `static` constructors shall have the same name as the immediately enclosing class. All other members shall have names that differ from the name of the immediately enclosing class.

-  The name of a type parameter in the *type-parameter-list* of a `class` declaration shall differ from the names of all other type parameters in the same *type-parameter-list* and shall differ from the name of the `class` and the names of all members of the class.

-  The name of a type shall differ from the names of all non-type members declared in the same class. If two or more type declarations share the same fully qualified name, the declarations shall have the `partial` modifier (§15.2.7) and these declarations combine to define a single type. 

> [!NOTE] 
> Since the fully qualified name of a type declaration encodes the number of type parameters, two distinct types may share the same name as long as they have different number of type parameters. 

-  The name of a `constant`, `field`, `property`, or `event` shall differ from the names of all other members declared in the same class.

-  The name of a `method` shall differ from the names of all other non-methods declared in the same class. In addition, the signature (§8.6) of a `method` shall differ from the signatures of all other methods declared in the same class, and two methods declared in the same `class` shall not have signatures that differ solely by `ref` and `out`.

-  The signature of an instance constructor shall differ from the signatures of all other instance constructors declared in the same class, and two constructors declared in the same `class` shall not have signatures that differ solely by ref and out.

-  The signature of an indexer shall differ from the signatures of all other indexers declared in the same `class`.

-  The signature of an operator shall differ from the signatures of all other operators declared in the same `class`.

The inherited members of a `class` (§15.3.4) are not part of the declaration space of a `class`. 

> [!NOTE] 
> Thus, a derived `class` is allowed to declare a member with the same name or signature as an inherited member (which in effect hides the inherited member). 

The set of members of a type declared in multiple parts (§15.2.7) is the union of the members declared in each part. The bodies of all parts of the type declaration share the same declaration space (§8.3), and the scope of each member (§8.7) extends to the bodies of all the parts. The accessibility domain of any member always includes all the parts of the enclosing type; a `private` member declared in one part is freely accessible from another part. It is a compile-time error to declare the same member in more than one part of the type, unless that member is a type having the `partial` modifier. [Example:

```csharp
partial class A
{
    int x;                     // Error, cannot declare x more than once
    
    partial class Inner       // Ok, Inner is a partial type
    {
        int y;
    }
}
    
partial class A
{
    int x;                   // Error, cannot declare x more than once
    
    partial class Inner     // Ok, Inner is a partial type
    {
        int z;
    }
}
```
end example]

Field initialization order can be significant within C# code, and some guarantees are provided, as defined in §15.5.6.1. Otherwise, the ordering of members within a type is rarely significant, but may be significant when interfacing with other languages and environments. In these cases, the ordering of members within a type declared in multiple parts is undefined.

### The instance type 

Each `class` declaration has an associated ***instance type***. For a generic `class` declaration, the instance type is formed by creating a constructed type (§9.4) from the type declaration, with each of the supplied type arguments being the corresponding type parameter. Since the instance type uses the type parameters, it can only be used where the type parameters are in scope; that is, inside the `class` declaration. The instance type is the type of this for code written inside the `class` declaration. For non-generic classes, the instance type is simply the declared class. [Example: The following shows several `class` declarations along with their instance types:

```csharp
class A<T>             // instance type: A<T>
{
    class B {}         // instance type: A<T>.B
    
    class C<U> {}      // instance type: A<T>.C<U>
}

class D {}             // instance type: D
```
end example]

### Members of constructed types

The non-inherited members of a constructed type are obtained by substituting, for each *type-parameter* in the member declaration, the corresponding *type-argument* of the constructed type. The substitution process is based on the semantic meaning of type declarations, and is not simply textual substitution.

[Example: Given the generic `class` declaration

```csharp
class Gen<T,U>
{
    public T[,] a;
    
    public void G(int i, `T` t, Gen<U,T> gt) {…}
    
    public U Prop { get {…} set {…} }
    
    public int H(double d) {…}
}
```

the constructed type `Gen<int[],IComparable<string>>` has the following members:

```csharp
public int[,][] a;

public void G(int i, int[] t, Gen<IComparable<string>,int[]> gt) {…}

public IComparable<string> Prop { get {…} set {…} }

public int H(double d) {…}
```

The type of the member `a` in the generic `class` declaration `Gen` is “two-dimensional array of T”, so the type of the member `a` in the constructed type above is “two-dimensional array of single-dimensional array of int”, or int[,][]. end example]

Within instance function members, the type of `this` is the instance type (§15.3.2) of the containing declaration.

All members of a generic `class` can use type parameters from any enclosing class, either directly or as part of a `constructed` type. When a particular closed `constructed` type (§9.4.3) is used at run-time, each use of a type parameter is replaced with the type argument supplied to the `constructed` type. [Example:

```csharp
class C<V>
{
    public V f1;
    public C<V> f2 = null;
    
    public C(V x) {
        this.f1 = x;
        this.f2 = this;
    }
}

class Application
{
    static void Main() {
        C<int> x1 = new C<int>(1);
        Console.WriteLine(x1.f1);         // Prints 1
        
        C<double> x2 = new C<double>(3.1415);
        Console.WriteLine(x2.f1);         // Prints 3.1415
    }
}
```

end example]

### Inheritance

A `class` ***inherits*** the members of its direct base class. Inheritance means that a `class` implicitly contains all members of its direct base class, except for the `instance` constructors, `finalizers`, and `static` constructors of the base class. Some important aspects of inheritance are:

-  Inheritance is transitive. If `C` is derived from `B`, and `B` is derived from `A`, then `C` inherits the members declared in `B` as well as the members declared in `A`.

-  A derived `class` *extends* its direct base class. A derived `class` can add new members to those it inherits, but it cannot remove the definition of an inherited member.

-  `Instance` constructors, `finalizers`, and `static` constructors are not inherited, but all other members are, regardless of their declared accessibility (§8.5). However, depending on their declared accessibility, inherited members might not be accessible in a derived class.

-  A derived `class` can ***hide*** (§8.7.2.3) inherited members by declaring new members with the same name or signature. However, hiding an inherited member does not remove that member—it merely makes that member inaccessible directly through the derived class.

-  An instance of a `class` contains a set of all instance fields declared in the `class` and its base classes, and an implicit conversion (§11.2.7) exists from a derived `class` type to any of its base class types. Thus, a reference to an instance of some derived `class` can be treated as a reference to an instance of any of its base classes.

-  A `class` can declare virtual `methods`, `properties`, `indexers`, and `events`, and derived classes can override the implementation of these function members. This enables classes to exhibit polymorphic behavior wherein the actions performed by a function member invocation vary depending on the run-time type of the instance through which that function member is invoked.

The inherited members of a constructed `class` type are the members of the immediate base class type (§15.2.4.2), which is found by substituting the type arguments of the constructed type for each occurrence of the corresponding type parameters in the *base-class-specification*. These members, in turn, are transformed by substituting, for each *type-parameter* in the member declaration, the corresponding *type-argument* of the *base-class-specification*. [Example:

```csharp
class B<U>
{
    public U F(long index) {…}
}

class D<T>: B<T[]>
{
    public T` G(string s) {…}
}
```

In the code above, the constructed type `D<int>` has a non-inherited member public `int` `G(string s)` obtained by substituting the type argument `int` for the type parameter `T`. `D<int>` also has an inherited member from the `class` declaration `B`. This inherited member is determined by first determining the base class type `B<int[]>` of `D<int>` by substituting `int` for `T` in the base class specification `B<T[]>`. Then, as a type argument to `B`, `int[]` is substituted for `U` in `public U F(long index)`, yielding the inherited member `public int[] F(long index)`. end example]

### The new modifier

A *class-member-declaration* is permitted to declare a member with the same name or signature as an inherited member. When this occurs, the derived `class` member is said to ***hide*** the base class member. See §8.7.2.3 for a precise specification of when a member hides an inherited member.

An inherited member `M` is considered to be ***available*** if `M` is accessible and there is no other inherited accessible member N that already hides `M`. Implicitly hiding an inherited member is not considered an error, but it does cause the compiler to issue a warning unless the declaration of the derived `class` member includes a `new` modifier to explicitly indicate that the derived member is intended to hide the base member. If one or more parts of a partial declaration (§15.2.7) of a nested type include the `new` modifier, no warning is issued if the nested type hides an available inherited member.

If a `new` modifier is included in a declaration that doesn’t hide an available inherited member, a warning to that effect is issued.

### Access modifiers

A *class-member-declaration* can have any one of the five possible kinds of declared accessibility (§8.5.2): `public`, `protected internal`, `protected`, `internal`, or `private`. Except for the `protected internal` combination, it is a compile-time error to specify more than one access modifier. When a *class-member-declaration* does not include any access modifiers, `private` is assumed.

### Constituent types

Types that are used in the declaration of a member are called the ***constituent types*** of that member. Possible constituent types are the type of a `constant`, `field`, `property`, `event`, or `indexer`, the return type of a `method` or `operator`, and the parameter types of a `method`, `indexer`, `operator`, or `instance` constructor. The constituent types of a member shall be at least as accessible as that member itself (§8.5.5).

### Static and instance members

Members of a `class` are either ***static members*** or ***instance members***. 

> [!NOTE] 
> Generally speaking, it is useful to think of `static` members as belonging to classes and instance members as belonging to objects (instances of classes). 

When a `field`, `method`, `property`, `event`, `operator`, or `constructor` declaration includes a `static` modifier, it declares a `static` member. In addition, a constant or type declaration implicitly declares a `static` member. Static members have the following characteristics:

-  When a `static` member `M` is referenced in a *member-access* (§12.7.5) of the form `E.M`, `E` shall denote a type that has a member `M`. It is a compile-time error for `E` to denote an instance.
-  A `static` field in a non-generic `class` identifies exactly one storage location. No matter how many instances of a non-generic `class` are created, there is only ever one copy of a `static` field. Each distinct closed constructed type (§9.4.3) has its own set of `static` fields, regardless of the number of instances of the closed constructed type.
-  A `static` function member (`method`, `property`, `event`, `operator`, or `constructor`) does not operate on a specific instance, and it is a compile-time error to refer to this in such a function member.

When a `field`, `method`, `property`, `event`, `indexer`, `constructor`, or `finalizer` declaration does not include a `static` modifier, it declares an `instance` member. (An `instance` member is sometimes called a non-static member.) Instance members have the following characteristics:

-  When an `instance` member `M` is referenced in a *member-access* (§12.7.5) of the form `E.M`, `E` shall denote an `instance` of a type that has a member `M`. It is a binding-time error for E to denote a type.
-  Every instance of a `class` contains a separate set of all `instance` fields of the class.
-  An `instance` function member (`method`, `property`, `indexer`, `instance` constructor, or `finalizer`) operates on a given instance of the class, and this instance can be accessed as `this` (§12.7.8).

[Example: The following example illustrates the rules for accessing static and instance members:

```csharp
class Test
{
    int x;
    static int y;
    
    void F() {
        x = 1;               // Ok, same as this.x = 1
        y = 1;               // Ok, same as Test.y = 1
    }
    
    static void G() {
        x = 1;               // Error, cannot access this.x
        y = 1;               // Ok, same as Test.y = 1
    }
    
    static void Main() {
        Test T = new Test();
        t.x = 1;             // Ok
        t.y = 1;             // Error, cannot access static member through instance
        Test.x = 1;          // Error, cannot access instance member through type
        Test.y = 1;          // Ok
    }
}
```

The `F` method shows that in an `instance` function member, a *simple-name* (§12.7.3) can be used to access both `instance` members and `static` members. The `G` method shows that in a `static` function member, it is a compile-time error to access an `instance` member through a *simple-name*. The `Main` method shows that in a *member-access* (§12.7.5), `instance` members shall be accessed through instances, and `static` members shall be accessed through types. end example]

### Nested types

#### General

A type declared within a `class` or `struct` is called a ***nested type***. A type that is declared within a compilation unit or namespace is called a ***non-nested type***. [Example: In the following example:

```csharp
using System;

class A
{
    class B
    {
        static void F() {
            Console.WriteLine("A.B.F");
        }
    }
}
```

class `B` is a nested type because it is declared within class `A`, and class `A` is a non-nested type because it is declared within a compilation unit. end example]

#### Fully qualified name

The fully qualified name (§8.8.3) for a nested type declarationis `S.N` where `S` is the fully qualified name of the type declarationin which type `N` is declared and `N` is the unqualified name (§8.8.2) of the nested type declaration (including any *generic-dimension-specifier* (§12.7.12)).

#### Declared accessibility

Non-nested types can have `public` or `internal` declared accessibility and have `internal` declared accessibility by default. Nested types can have these forms of declared accessibility too, plus one or more additional forms of declared accessibility, depending on whether the containing type is a `class` or `struct`:

-  A nested type that is declared in a `class` can have any of five forms of declared accessibility (`public`, `protected` `internal`, `protected`, `internal`, or `private`) and, like other `class` members, defaults to `private` declared accessibility.

-  A nested type that is declared in a `struct` can have any of three forms of declared accessibility (`public`, `internal`, or `private`) and, like other `struct` members, defaults to `private` declared accessibility.

[Example: The example
```csharp
public class List
{
    // Private data structure
    private class Node
    {
        public object Data;
        public Node Next;
        public Node(object data, Node next) {
        this.Data = data;
        this.Next = next;
        }
    }

    private Node first = null;
    private Node last = null;
    
    // Public interface
    public void AddToFront(object o) {…}
    public void AddToBack(object o) {…}
    public object RemoveFromFront() {…}
    public object RemoveFromBack() {…}
    public int Count { get {…} }
}
```
declares a private nested class `Node`. end example]

#### Hiding

A nested type may hide (§8.7.2.2) a base member. The `new` modifier (§15.3.5) is permitted on nested type declarations so that hiding can be expressed explicitly. [Example: The example

```csharp
using System;

class Base
{
    public static void M() {
        Console.WriteLine("Base.M");
    }
}

class Derived: Base
{
    new public class M
    {
        public static void F() {
            Console.WriteLine("Derived.M.F");
        }
    }
}

class Test
{
    static void Main() {
        Derived.M.F();
    }
}
```

shows a nested class `M` that hides the method `M` defined in `Base`. end example]

#### this access

A nested type and its containing type do not have a special relationship with regard to *this-access* (§12.7.8). Specifically, `this` within a nested type cannot be used to refer to instance members of the containing type. In cases where a nested type needs access to the instance members of its containing type, access can be provided by providing the `this` for the instance of the containing type as a constructor argument for the nested type. [Example: The following example

```csharp
using System;

class C
{
    int i = 123;
    public void F() {
        Nested n = new Nested(this);
        n.G();
    }

    public class Nested
    {
        C this_c;
        public Nested(C c) {
        this_c = c;
        }
        public void G() {
        Console.WriteLine(this_c.i);
        }
    }
}
    
class Test
{
    static void Main() {
        C c = new C();
        c.F();
    }
}
```
shows this technique. An instance of `C` creates an instance of `Nested`, and passes its own this to `Nested`'s constructor in order to provide subsequent access to `C`'s instance members. end example]

#### Access to private and protected members of the containing type

A nested type has access to all of the members that are accessible to its containing type, including members of the containing type that have `private` and `protected` declared accessibility. [Example: The example

```csharp
using System;

class C
{
    private static void F() {
        Console.WriteLine("C.F");
    }
    public class Nested
    {
        public static void G() {
            F();
        }
    }
}
    
class Test
{
    static void Main() {
        C.Nested.G();
    }
}
```

shows a class `C` that contains a nested class `Nested`. Within `Nested`, the method `G` calls the static method `F` defined in `C`, and `F` has private declared accessibility. end example]

A nested type also may access `protected` members defined in a base type of its containing type. [Example: In the following code

```csharp
using System;

class Base
{
    protected void F() {
        Console.WriteLine("Base.F");
    }
}
    
class Derived: Base
{
    public class Nested
    {
        public void G() {
            Derived d = new Derived();
            d.F(); // ok
        }
    }
}

class Test
{
    static void Main() {
        Derived.Nested n = new Derived.Nested();
        n.G();
    }
}
```

the nested class `Derived.Nested` accesses the protected method `F` defined in `Derived`'s base class, `Base`, by calling through an instance of `Derived`. end example]

#### Nested types in generic classes

A generic `class` declaration may contain nested type declarations. The type parameters of the enclosing `class` may be used within the nested types. A nested type declaration may contain additional type parameters that apply only to the nested type.

Every type declaration contained within a generic `class` declaration is implicitly a generic type declaration. When writing a reference to a type nested within a generic type, the containing `constructed` type, including its `type` arguments, shall be named. However, from within the outer `class`, the nested type may be used without qualification; the `instance` type of the outer `class` may be implicitly used when constructing the nested type. [Example: The following shows three different correct ways to refer to a constructed type created from `Inner`; the first two are equivalent:

```csharp
class Outer<T>
{
    class Inner<U>
    {
        public static void F(T t, U u) {…}
    }
    
    static void F(T t) {
        Outer<T>.Inner<string>.F(t, "abc");         // These two statements have
        Inner<string>.F(t, "abc");                  // the same effect
        
        Outer<int>.Inner<string>.F(3, "abc");       // This type is different
        
        Outer.Inner<string>.F(t, "abc");            // Error, Outer needs type arg
    }
}
```

end example]

Although it is bad programming style, a type parameter in a nested type can hide a member or type parameter declared in the outer type. [Example:

```csharp
class Outer<T>
{
    class Inner<T>                                 // Valid, hides Outer’s T
    {
        public T t;                                 // Refers to Inner’s T
    }
}
```

end example]

### Reserved member names

#### General

To facilitate the underlying C# run-time implementation, for each source member declaration that is a `property`, `event`, or `indexer`, the implementation shall reserve two method signatures based on the kind of the member declaration, its name, and its type (§15.3.10.2, §15.3.10.3, §15.3.10.4). It is a compile-time error for a program to declare a member whose signature matches a signature reserved by a member declared in the same scope, even if the underlying run-time implementation does not make use of these reservations.

The reserved names do not introduce declarations, thus they do not participate in member lookup. However, a declaration’s associated reserved method signatures do participate in inheritance (§15.3.4), and can be hidden with the `new` modifier (§15.3.5).

> [!NOTE] 
> The reservation of these names serves three purposes:

1.  To allow the underlying implementation to use an ordinary identifier as a method name for get or set access to the C# language feature.
2.  To allow other languages to interoperate using an ordinary identifier as a method name for get or set access to the C# language feature.
3.  To help ensure that the source accepted by one conforming compiler is accepted by another, by making the specifics of reserved member names consistent across all C# implementations.

The declaration of a `finalizer` (§15.13) also causes a signature to be reserved (§15.3.10.5).

#### Member names reserved for properties

For a property `P` (§15.7) of type `T`, the following signatures are reserved:

```csharp
T get_P();
void set_P(T value);
```
Both signatures are reserved, even if the property is read-only or write-only.

[Example: In the following code

```csharp
using System;

class A
{
    public int P {
        get { return 123; }
    }
}

class B: A
{
    new public int get_P() {
        return 456;
    }
    new public void set_P(int value) {
    }
}

class Test
{
    static void Main() {
    B b = new B();
    A a = b;
    Console.WriteLine(a.P);
    Console.WriteLine(b.P);
    Console.WriteLine(b.get_P());
    }
}
```

a class `A` defines a read-only property `P`, thus reserving signatures for `get_P` and `set_P` methods. `A` class `B` derives from `A` and hides both of these reserved signatures. The example produces the output:

```ANTLR
123
123
456
```

end example]

#### Member names reserved for events

For an event `E` (§15.8) of delegate type `T`, the following signatures are reserved:

```csharp
void add_E(T handler);
void remove_E(T handler);
```

#### Member names reserved for indexers

For an indexer (§15.9) of type `T` with parameter-list `L`, the following signatures are reserved:

```csharp
T get_Item(L);
void set_Item(L, T value);
```

Both signatures are reserved, even if the indexer is read-only or write-only.

Furthermore the member name `Item` is reserved.

#### Member names reserved for finalizers

For a `class` containing a finalizer (§15.13), the following signature is reserved:

```csharp
void Finalize();
```

## Constants

A ***constant*** is a `class` member that represents a constant value: a value that can be computed at compile-time. A *constant-declaration* introduces one or more constants of a given type.

```ANTLR
    constant-declaration:
    attributes~opt~ constant-modifiers~opt~ const type constant-declarators ;

constant-modifiers:
    constant-modifier
    constant-modifiers constant-modifier

constant-modifier:
    new
    public
    protected
    internal
    private

constant-declarators:
    constant-declarator
    constant-declarators , constant-declarator

constant-declarator:
    identifier = constant-expression
```

A *constant-declaration* may include a set of *attributes* (§22), a `new` modifier (§15.3.5), and a valid combination of the four access modifiers (§15.3.6). The attributes and modifiers apply to all of the members declared by the *constant-declaration*. Even though constants are considered `static` members, a *constant-declaration* neither requires nor allows a `static` modifier. It is an error for the same modifier to appear multiple times in a `constant` declaration.

The *type* of a *constant-declaration* specifies the type of the members introduced by the declaration. The type is followed by a list of *constant-declarator*s, each of which introduces a new member. A *constant-declarator* consists of an *identifier* that names the member, followed by an “`=`” token, followed by a *constant-expression* (§12.20) that gives the value of the member.

The *type* specified in a constant declaration shall be `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `float`, `double`, `decimal`, `bool`, `string`, an *enum-type*, or a *reference-type*. Each *constant-expression* shall yield a value of the target type or of a type that can be converted to the target type by an implicit conversion (§11.2.

The *type* of a constant shall be at least as accessible as the constant itself (§8.5.5).

The value of a constant is obtained in an expression using a *simple-name* (§12.7.3) or a *member-access* (§12.7.5).

A constant can itself participate in a *constant-expression*. Thus, a constant may be used in any construct that requires a *constant-expression*. 

> [!NOTE] 
> Examples of such constructs include `case` labels, `goto case` statements, `enum` member declarations, attributes, and other constant declarations. 

> [!NOTE] 
> As described in §12.20, a *constant-expression* is an expression that can be fully evaluated at compile-time. Since the only way to create a non-null value of a *reference-type* other than `string` is to apply the `new` operator, and since the `new` operator is not permitted in a *constant-expression*, the only possible value for constants of *reference-types* other than `string` is `null`. 

When a symbolic name for a constant value is desired, but when the type of that value is not permitted in a constant declaration, or when the value cannot be computed at compile-time by a *constant-expression*, a `readonly` field (§15.5.3) may be used instead. 

> [!NOTE] 
> The versioning semantics of const and `readonly` differ (§15.5.3.3). 

A constant declaration that declares multiple constants is equivalent to multiple declarations of single constants with the same attributes, modifiers, and type. [Example:

```csharp
class A
{
    public const double X = 1.0, Y = 2.0, Z = 3.0;
}
```

is equivalent to

```csharp
class A
{
    public const double X = 1.0;
    public const double Y = 2.0;
    public const double Z = 3.0;
}
```

end example]

Constants are permitted to depend on other constants within the same program as long as the dependencies are not of a circular nature. The compiler automatically arranges to evaluate the constant declarations in the appropriate order. [Example: In the following code

```csharp
class A
{
    public const int x = B.Z + 1;
    public const int y = 10;
}

class B
{
    public const int z = A.Y + 1;
}
```

the compiler first evaluates `A.Y`, then evaluates `B.Z`, and finally evaluates `A.X`, producing the values `10`, `11`, and `12`. end example] 

Constant declarations may depend on constants from other programs, but such dependencies are only possible in one direction. [Example: Referring to the example above, if `A` and `B` were declared in separate programs, it would be possible for `A.X` to depend on `B.Z`, but `B.Z` could then not simultaneously depend on `A.Y`. end example]

## Fields

### General

A ***field*** is a member that represents a variable associated with an `object` or `class`. A *field-declaration* introduces one or more fields of a given type.

```ANTLR
field-declaration:
    attributes~opt~ field-modifiers~opt~ type variable-declarators ;

field-modifiers:
    field-modifier
    field-modifiers field-modifier

field-modifier:
    new
    public
    protected
    internal
    private
    static
    readonly
    volatile

variable-declarators:
    variable-declarator
    variable-declarators , variable-declarator

variable-declarator:
    identifier
    identifier = variable-initializer

variable-initializer:
    expression
    array-initializer
```

A *field-declaration* may include a set of *attributes* (§22), a `new` modifier (§15.3.5), a valid combination of the four access modifiers (§15.3.6), and a `static` modifier (§15.5.2). In addition, a *field-declaration* may include a `readonly` modifier (§15.5.3) or a `volatile` modifier (§15.5.4), but not both. The attributes and modifiers apply to all of the members declared by the *field-declaration*. It is an error for the same modifier to appear multiple times in a *field declaration*.

The *type* of a *field-declaration* specifies the type of the members introduced by the declaration. The type is followed by a list of *variable-declarator*s, each of which introduces a new member. A *variable-declarator* consists of an *identifier* that names that member, optionally followed by an “`=`” token and a *variable-initializer* (§15.5.6) that gives the initial value of that member.

The *type* of a field shall be at least as accessible as the field itself (§8.5.5).

The value of a field is obtained in an expression using a *simple-name* (§12.7.3), a *member-access* (§12.7.5) or a base-access (§12.7.9). The value of a non-readonly field is modified using an *assignment* (§12.18). The value of a non-readonly field can be both obtained and modified using postfix increment and decrement operators (§12.7.10) and prefix increment and decrement operators (§12.8.6).

A field declaration that declares multiple fields is equivalent to multiple declarations of single fields with the same attributes, modifiers, and type. [Example:

```csharp
class A
{
    public static int x = 1, Y, Z = 100;
}
```

is equivalent to

```csharp
class A
{
    public static int x = 1;
    public static int y;
    public static int z = 100;
}
```

end example]

### Static and instance fields

When a field declaration includes a `static` modifier, the fields introduced by the declaration are ***static fields***. When no `static` modifier is present, the fields introduced by the declaration are ***instance fields***. Static fields and instance fields are two of the several kinds of variables (§10) supported by C#, and at times they are referred to as ***static variables*** and ***instance variables***, respectively.

As explained in §15.3.8, each instance of a `class` contains a complete set of the `instance` fields of the class, while there is only one set of `static` fields for each non-generic class or closed constructed type, regardless of the number of instances of the class or closed constructed type.

### Readonly fields

#### General

When a *field-declaration* includes a `readonly` modifier, the fields introduced by the declaration are ***readonly fields***. Direct assignments to `readonly` fields can only occur as part of that declaration or in an `instance` constructor or `static` constructor in the same class. (A `readonly` field can be assigned to multiple times in these contexts.) Specifically, direct assignments to a `readonly` field are permitted only in the following contexts:

-  In the *variable-declarator* that introduces the field (by including a *variable-initializer* in the declaration).
-  For an `instance` field, in the `instance` constructors of the `class` that contains the field declaration; for a `static` field, in the `static` constructor of the class that contains the field declaration. These are also the only contexts in which it is valid to pass a `readonly` field as an `out` or `ref` parameter.

Attempting to assign to a `readonly` field or pass it as an `out` or `ref` parameter in any other context is a compile-time error.

#### Using static readonly fields for constants

A `static readonly` field is useful when a symbolic name for a constant value is desired, but when the type of the value is not permitted in a `const` declaration, or when the value cannot be computed at compile-time. [Example: In the following code

```csharp
public class Color
{
    public static readonly Color Black = new Color(0, 0, 0);
    public static readonly Color White = new Color(255, 255, 255);
    public static readonly Color Red = new Color(255, 0, 0);
    public static readonly Color Green = new Color(0, 255, 0);
    public static readonly Color Blue = new Color(0, 0, 255);
    
    private byte red, green, blue;
    
    public Color(byte r, byte g, byte b) {
        red = r;
        green = g;
        blue = b;
    }
}
```

the `Black`, `White`, `Red`, `Green`, and `Blue` members cannot be declared as `const` members because their values cannot be computed at compile-time. However, declaring them `static readonly` instead has much the same effect. end example]

#### Versioning of constants and static readonly fields

Constants and `readonly` fields have different binary versioning semantics. When an expression references a constant, the value of the constant is obtained at compile-time, but when an expression references a `readonly` field, the value of the field is not obtained until run-time. [Example: Consider an application that consists of two separate programs:

```csharp
namespace Program1
    {
    public class Utils
    {
        public static readonly int x = 1;
    }
}
```

and

```csharp
using System;

namespace Program2
{
    class Test
    {
        static void Main() {
            Console.WriteLine(Program1.Utils.X);
        }
    }
}
```

The `Program1` and `Program2` namespaces denote two programs that are compiled separately. Because `Program1.Utils.X` is declared as a `static readonly` field, the value output by the `Console.WriteLine` statement is not known at compile-time, but rather is obtained at run-time. Thus, if the value of `X` is changed and `Program1` is recompiled, the `Console.WriteLine` statement will output the new value even if `Program2` isn’t recompiled. However, had `X` been a constant, the value of `X` would have been obtained at the time `Program2` was compiled, and would remain unaffected by changes in `Program1` until `Program2` is recompiled. end example]

### Volatile fields

When a *field-declaration* includes a `volatile` modifier, the fields introduced by that declaration are ***volatile fields***. For non-volatile fields, optimization techniques that reorder instructions can lead to unexpected and unpredictable results in multi-threaded programs that access fields without synchronization such as that provided by the *lock-statement* (§13.13). These optimizations can be performed by the compiler, by the run-time system, or by hardware. For `volatile` fields, such reordering optimizations are restricted:

-  A read of a `volatile` field is called a ***volatile read***. A volatile read has “acquire semantics”; that is, it is guaranteed to occur prior to any references to memory that occur after it in the instruction sequence.
-  A write of a `volatile` field is called a ***volatile write***. A volatile write has “release semantics”; that is, it is guaranteed to happen after any memory references prior to the write instruction in the instruction sequence.

These restrictions ensure that all threads will observe volatile writes performed by any other thread in the order in which they were performed. A conforming implementation is not required to provide a single total ordering of volatile writes as seen from all threads of execution. The type of a `volatile` field shall be one of the following:

-  A *reference-type*.
-  A *type-parameter* that is known to be a reference type (§15.2.5).
-  The type `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `char`, `float`, `bool`, `System.IntPtr`, or `System.UIntPtr`.
-  An *enum-type* having an enum base type of `byte`, `sbyte`, `short`, `ushort`, `int`, or `uint`.

[Example: The example

```csharp
using System;
using System.Threading;

class Test
{
    public static int result;
    public static volatile bool finished;
    
    static void Thread2() {
        result = 143;
        finished = true;
    }
    
    static void Main() {
        finished = false;                                // Run Thread2() in a new thread
        new Thread(new ThreadStart(Thread2)).Start();    // Wait for Thread2 to signal that it has a result by setting
                                                         // finished to true.
        for (;;) {
            if (finished) {
                Console.WriteLine("result = {0}", result);
                return;
            }
        }
    }
}
```

produces the output:

```csharp
result = 143
```

In this example, the method `Main` starts a new thread that runs the method `Thread2`. This method stores a value into a non-volatile field called `result`, then stores `true` in the volatile field `finished`. The main thread waits for the field `finished` to be set to true, then reads the field result. Since `finished` has been declared `volatile`, the main thread shall read the value `143` from the field `result`. If the field `finished` had not been declared `volatile`, then it would be permissible for the store to `result` to be visible to the main thread *after* the store to `finished`, and hence for the main thread to read the value 0 from the field `result`. Declaring `finished` as a `volatile` field prevents any such inconsistency. end example]

### Field initialization

The initial value of a field, whether it be a `static` field or an `instance` field, is the default value (§10.3) of the field’s type. It is not possible to observe the value of a field before this default initialization has occurred, and a field is thus never “uninitialized”. [Example: The example

```csharp
using System;

class Test
{
    static bool b;
    int i;
    
    static void Main() {
        Test T = new Test();
        Console.WriteLine("b = {0}, i = {1}", b, t.i);
    }
}
```

produces the output

```csharp
b = False, i = 0
```

because `b` and `i` are both automatically initialized to default values. end example]

### Variable initializers

#### General

Field declarations may include *variable-initializer*s. For `static` fields, variable initializers correspond to assignment statements that are executed during `class` initialization. For `instance` fields, variable initializers correspond to assignment statements that are executed when an instance of the `class` is created.

[Example: The example

```csharp
using System;

class Test
{
    static double x = Math.Sqrt(2.0);
    int i = 100;
    string S = "Hello";
    
    static void Main() {
        Test a = new Test();
        Console.WriteLine("x = {0}, i = {1}, `S` = {2}", x, a.i, a.s);
    }
}

produces the output

```csharp
x = 1.4142135623731, i = 100, S = Hello
```

because an assignment to `x` occurs when `static` field initializers execute and assignments to `i` and `s` occur when the `instance` field initializers execute. end example]

The default value initialization described in §15.5.5 occurs for all fields, including fields that have variable initializers. Thus, when a `class` is initialized, all `static` fields in that `class` are first initialized to their default values, and then the `static` field initializers are executed in textual order. Likewise, when an instance of a `class` is created, all `instance` fields in that instance are first initialized to their default values, and then the `instance` field initializers are executed in textual order. When there are field declarations in multiple partial type declarations for the same type, the order of the parts is unspecified. However, within each part the field initializers are executed in order.

It is possible for `static` fields with variable initializers to be observed in their default value state. [Example: However, this is strongly discouraged as a matter of style. The example

```csharp
using System;

class Test
{
    static int a = b + 1;
    static int b = a + 1;
    
    static void Main() {
        Console.WriteLine("a = {0}, b = {1}", a, b);
    }
}
```

exhibits this behavior. Despite the circular definitions of `a` and `b`, the program is valid. It results in the output

```csharp
a = 1, b = 2
```

because the static fields `a` and `b` are initialized to `0` (the default value for `int`) before their initializers are executed. When the initializer for `a` runs, the value of `b` is zero, and so `a` is initialized to `1`. When the initializer for `b` runs, the value of a is already `1`, and so `b` is initialized to `2`. end example]

#### Static field initialization

The `static` field variable initializers of a `class` correspond to a sequence of assignments that are executed in the textual order in which they appear in the `class` declaration (§15.5.6.1). Within a partial class, the meaning of "textual order" is specified by §15.5.6.1. If a `static` constructor (§15.12) exists in the class, execution of the `static` field initializers occurs immediately prior to executing that `static` constructor. Otherwise, the `static` field initializers are executed at an implementation-dependent time prior to the first use of a `static` field of that class. [Example: The example

```csharp
using System;

class Test
{
    static void Main() {
    Console.WriteLine("{0} {1}", B.Y, A.X);
}
public static int F(string s) {
    Console.WriteLine(s);
        return 1;
    }
}

class A
{
    public static int x = Test.F("Init A");
}

class B
{
    public static int y = Test.F("Init B");
}
```

might produce either the output:

```csharp
Init A
Init B
1 1
```

or the output:

```csharp
Init B
Init A
1 1
```

because the execution of `X`'s initializer and `Y`'s initializer could occur in either order; they are only constrained to occur before the references to those fields. However, in the example:

```csharp
using System;

class Test
{
    static void Main() {
        Console.WriteLine("{0} {1}", B.Y, A.X);
    }
    public static int F(string s) {
        Console.WriteLine(s);
        return 1;
    }
}

class A
{
    static A() {}
    public static int x = Test.F("Init A");
}

class B
{
    static B() {}
    public static int y = Test.F("Init B");
}
```

the output shall be:

```csharp
Init B
Init A
1 1
```

because the rules for when static constructors execute (as defined in §15.12) provide that `B`'s static constructor (and hence `B`'s `static` field initializers) shall run before `A`'s `static` constructor and field initializers. end example]

#### Instance field initialization

The `instance` field variable initializers of a `class` correspond to a sequence of assignments that are executed immediately upon entry to any one of the `instance` constructors (§15.11.3) of that class. Within a partial class, the meaning of "textual order" is specified by §15.5.6.1. The variable initializers are executed in the textual order in which they appear in the `class` declaration (§15.5.6.1). The `class` instance creation and initialization process is described further in §15.11.

A variable initializer for an `instance` field cannot reference the instance being created. Thus, it is a compile-time error to reference `this` in a variable initializer, as it is a compile-time error for a variable initializer to reference any instance member through a *simple-name*. [Example: In the following code

```csharp
class A
{
    int x = 1;
    int y = x + 1;     // Error, reference to instance member of this
}
```

the variable initializer for `y` results in a compile-time error because it references a member of the instance being created. end example]

## Methods

### General

A ***method*** is a member that implements a computation or action that can be performed by an `object` or class. Methods are declared using *method-declaration*s:
```ANTLR
method-declaration:
    method-header method-body

method-header:
    attributes~opt~ method-modifiers~opt~ partial~opt~ return-type member-name
    type-parameter-list~opt~
    ( formal-parameter-list~opt~ ) type-parameter-constraints-clauses~opt~

method-modifiers:
    method-modifier
    method-modifiers method-modifier

method-modifier:
    new
    public
    protected
    internal
    private
    static
    virtual
    sealed
    override
    abstract
    extern
    async

return-type:
    type
    void

member-name:
    identifier
    interface-type . identifier

method-body:
    block
    ;
```
A *method-declaration* may include a set of *attributes* (§22) and a valid combination of the four access modifiers (§15.3.6), the `new` (§15.3.5), `static` (§15.6.3), `virtual` (§15.6.4), `override` (§15.6.5), `sealed` (§15.6.6), `abstract` (§15.6.7), `extern` (§15.6.8) and `async` (§15.15) modifiers.

A declaration has a valid combination of modifiers if all of the following are true:

-  The declaration includes a valid combination of access modifiers (§15.3.6).
-  The declaration does not include the same modifier multiple times.
-  The declaration includes at most one of the following modifiers: `static`, `virtual`, and `override`.
-  The declaration includes at most one of the following modifiers: `new` and `override`.
-  If the declaration includes the `abstract` modifier, then the declaration does not include any of the following modifiers: `static`, `virtual`, `sealed`, or `extern`.
-  If the declaration includes the `private` modifier, then the declaration does not include any of the following modifiers: `virtual`, `override`, or `abstract`.
-  If the declaration includes the `sealed` modifier, then the declaration also includes the `override` modifier.
-  If the declaration includes the `partial` modifier, then it does not include any of the following modifiers: new, `public`, `protected`, `internal`, `private`, `virtual`, `sealed`, `override`, `abstract`, or `extern`.

The *return-type* of a method declaration specifies the type of the value computed and returned by the method. The *return-type* is `void` if the method does not return a value. If the declaration includes the `partial` modifier, then the return type shall be `void`.

A generic method is a method whose declaration includes a *type-parameter-list*. This specifies the type parameters for the method. The optional *type-parameter-constraints-clauses* specify the constraints for the type parameters. A *method-declaration* shall not have *type-parameter-constraints-clauses* unless it also has a *type-parameter-list*. A *method-declaration* for an explicit interface member implementation shall not have any *type-parameter-constraints-clauses*. A generic *method-declaration* for an explicit interface member implementation inherits any constraints from the constraints on the interface method. Similarly, a method declaration with the `override` modifier shall not have any *type-parameter-constraints-clauses* and the constraints of the method’s type parameters are inherited from the `virtual` method being overridden.The *member-name* specifies the name of the method. Unless the method is an explicit interface member implementation (§18.6.2), the *member-name* is simply an *identifier*. For an explicit interface member implementation, the *member-name* consists of an *interface-type* followed by a “`.`” and an *identifier*. In this case, the declaration shall include no modifiers other than (possibly) `extern` or `async`.

The optional *formal-parameter-list* specifies the parameters of the method (§15.6.2).

The *return-type* and each of the types referenced in the *formal-parameter-list* of a method shall be at least as accessible as the method itself (§8.5.5).

For `abstract` and `extern` methods, the *method-body* consists simply of a semicolon. For `partial` methods the *method-body* may consist of either a semicolon or a *block*. For all other methods, the *method-body* consists of a *block,* which specifies the statements to execute when the method is invoked.

If the *method-body* consists of a semicolon, the declaration shall not include the `async` modifier.

The name, the number of type parameters, and the formal parameter list of a method define the signature (§8.6) of the method. Specifically, the signature of a method consists of its name, the number of its type parameters, and the number, *parameter-mode-modifier*s (§15.6.2.1), and types of its formal parameters. The return type is not part of a method’s signature, nor are the names of the formal parameters, the names of the type parameters, or the constraints. When a formal parameter type references a type parameter of the method, the ordinal position of the type parameter (not the name of the type parameter) is used for type equivalence.

The name of a method shall differ from the names of all other non-methods declared in the same class. In addition, the signature of a method shall differ from the signatures of all other methods declared in the same class, and two methods declared in the same `class` may not have signatures that differ solely by `ref` and `out`.

The method’s *type-parameter*s are in scope throughout the *method-declaration*, and can be used to form types throughout that scope in *return-type*, *method-body*, and *type-parameter-constraints-clauses* but not in *attributes*.

All formal parameters and type parameters shall have different names.

### Method parameters

#### General

The parameters of a method, if any, are declared by the method’s *formal-parameter-list*.

```ANTLR
formal-parameter-list:
    fixed-parameters
    fixed-parameters ',' parameter-array
    parameter-array

fixed-parameters:
    fixed-parameter
    fixed-parameters ',' fixed-parameter

fixed-parameter:
    attributes~opt~ parameter-modifier~opt~ type identifier default-argument~opt~

default-argument:
    '=' expression

parameter-mode-modifier:
    ref
    out
    this

parameter-array:
    attributes~opt~ params array-type identifier
```

The formal parameter list consists of one or more comma-separated parameters of which only the last may be a *parameter-array*.

A *fixed-parameter* consists of an optional set of *attributes* (§22); an optional `ref`, `out`, or `this` modifier; a *type*; an *identifier*; and an optional *default-argument*. Each *fixed-parameter* declares a parameter of the given type with the given name. The `this` modifier designates the method as an extension method and is only allowed on the first parameter of a `static` method in a non-generic, non-nested `static` class. Extension methods are further described in §15.6.10. A *fixed-parameter* with a *default-argument* is known as an ***optional parameter***, whereas a *fixed-parameter* without a *default-argument* is a ***required parameter***. A required parameter may not appear after an optional parameter in a *formal-parameter-list*.

A parameter with a `ref`, `out` or `this` modifier cannot have a *default-argument*. The *expression* in a *default-argument* shall be one of the following:

-  a *constant-expression*
-  an expression of the form `new S()` where `S` is a value type
-  an expression of the form `default(S)` where `S` is a value type

The *expression* shall be implicitly convertible by an identity or nullable conversion to the type of the parameter.

If optional parameters occur in an implementing `partial` method declaration (§15.6.9), an explicit interface member implementation (§18.6.2), a single-parameter `indexer` declaration (§15.9), or in an operator declaration (§15.10.1) the compiler should give a warning, since these members can never be invoked in a way that permits arguments to be omitted.

A *parameter-array* consists of an optional set of *attributes* (§22), a `params` modifier, an *array-type*, and an *identifier*. A parameter array declares a single parameter of the given `array` type with the given name. The *array-type* of a parameter array shall be a single-dimensional array type (§17.2). In a method invocation, a parameter array permits either a single argument of the given array type to be specified, or it permits zero or more arguments of the array element type to be specified. Parameter arrays are described further in §15.6.2.5.

A *parameter-array* may occur after an optional parameter, but cannot have a default value – the omission of arguments for a *parameter-array* would instead result in the creation of an empty array.

[Example: The following illustrates different kinds of parameters:

```csharp
public void M(
    ref int i,
    decimal d,
    bool b = false,
    bool? n = false,
    string S = "Hello",
    object o = null,
    T t = default(T),
    params int[] a
) { }
```

In the *formal-parameter-list* for `M`, `i` is a required `ref` parameter, `d` is a required value parameter, `b`, `s`, `o` and `t` are optional value parameters and `a` is a parameter array. end example]

A method declaration creates a separate declaration space (§8.3) for parameters and type parameters. Names are introduced into this declaration space by the type parameter list and the formal parameter list of the method. The body of the method, if any, is considered to be nested within this declaration space. It is an error for two members of a method declaration space to have the same name. It is an error for the method declaration space and the local variable declaration space of a nested declaration space to contain elements with the same name.

A method invocation (§12.7.6.2) creates a copy, specific to that invocation, of the formal parameters and local variables of the method, and the argument list of the invocation assigns values or variable references to the newly created formal parameters. Within the *block* of a method, formal parameters can be referenced by their identifiers in *simple-name* expressions (§12.7.3).

There are four kinds of formal parameters:

-  Value parameters, which are declared without any modifiers.
-  Reference parameters, which are declared with the `ref` modifier.
-  Output parameters, which are declared with the `out` modifier.
-  Parameter arrays, which are declared with the `params` modifier.

> [!NOTE]
> As described in §8.6, the `ref` and `out` modifiers are part of a method’s signature, but the `params` modifier is not. 

#### Value parameters

A parameter declared with no modifiers is a value parameter. A value parameter corresponds to a local variable that gets its initial value from the corresponding argument supplied in the method invocation.

When a formal parameter is a value parameter, the corresponding argument in a method invocation shall be an expression that is implicitly convertible (§11.2) to the formal parameter type.

A method is permitted to assign new values to a value parameter. Such assignments only affect the local storage location represented by the value parameter—they have no effect on the actual argument given in the method invocation.

#### Reference parameters

A parameter declared with a `ref` modifier is a reference parameter. Unlike a value parameter, a reference parameter does not create a new storage location. Instead, a reference parameter represents the same storage location as the variable given as the argument in the method invocation.

When a formal parameter is a reference parameter, the corresponding argument in a method invocation shall consist of the keyword `ref` followed by a *variable-reference* (§10.5) of the same type as the formal parameter. A variable shall be definitely assigned before it can be passed as a reference parameter.

Within a method, a reference parameter is always considered definitely assigned.

A method declared as an iterator (§15.14) may not have reference parameters.

[Example: The example

```csharp
using System;

class Test
{
    static void Swap(ref int x, ref int y) {
        int temp = x;
        x = y;
        y = temp;
    }

static void Main() {
        int i = 1, j = 2;
        Swap(ref i, ref j);
        Console.WriteLine("i = {0}, j = {1}", i, j);
    }
}
```

produces the output

```csharp
i = 2, j = 1
```

For the invocation of `Swap` in `Main`, `x` represents `i` and `y` represents `j`. Thus, the invocation has the effect of swapping the values of `i` and `j`. end example]

In a method that takes reference parameters, it is possible for multiple names to represent the same storage location. [Example: In the following code

```csharp
class A
{
    string s;
    
    void F(ref string a, ref string b) {
        S = "One";
        a = "Two";
        b = "Three";
    }
    
    void G() {
        F(ref s, ref s);
    }
}
```

the invocation of `F` in `G` passes a reference to `s` for both `a` and `b`. Thus, for that invocation, the names `s`, `a`, and `b` all refer to the same storage location, and the three assignments all modify the instance field `s`. end example]

#### Output parameters

A parameter declared with an `out` modifier is an output parameter. Similar to a reference parameter, an output parameter does not create a new storage location. Instead, an output parameter represents the same storage location as the variable given as the argument in the method invocation.

When a formal parameter is an output parameter, the corresponding argument in a method invocation shall consist of the keyword `out` followed by a *variable-reference* (§10.5) of the same type as the formal parameter. A variable need not be definitely assigned before it can be passed as an output parameter, but following an invocation where a variable was passed as an output parameter, the variable is considered definitely assigned.

Within a method, just like a local variable, an output parameter is initially considered unassigned and shall be definitely assigned before its value is used.

Every output parameter of a method shall be definitely assigned before the method returns.

A method declared as a `partial` method (§15.6.9) or an iterator (§15.14) may not have output parameters.

Output parameters are typically used in methods that produce multiple return values. [Example:

```csharp
using System;

class Test
{
    static void SplitPath(string path, out string dir, out string name) {
        int i = path.Length;
        while (i > 0) {
            char ch = path[i – 1];
            if (ch == '\\\\' || ch == '/' || ch == ':') break;
            i--;
        }
        dir = path.Substring(0, i);
        name = path.Substring(i);
    }
    
    static void Main() {
        string dir, name;
        SplitPath("c:\\\Windows\\\\System\\\\hello.txt", out dir, out name);
        Console.WriteLine(dir);
        Console.WriteLine(name);
    }
}
```

The example produces the output:

```csharp
c:\Windows\System\
hello.txt
```

Note that the `dir` and `name` variables can be unassigned before they are passed to `SplitPath`, and that they are considered definitely assigned following the call. end example]

#### Parameter arrays

A parameter declared with a `params` modifier is a parameter array. If a formal parameter list includes a parameter array, it shall be the last parameter in the list and it shall be of a single-dimensional array type. [Example: The types `string[]` and `string[][]` can be used as the type of a parameter array, but the type `string[,]` can not. end example] It is not possible to combine the `params` modifier with the modifiers `ref` and `out`.

A parameter array permits arguments to be specified in one of two ways in a method invocation:

-  The argument given for a parameter array can be a single expression that is implicitly convertible (§11.2) to the parameter array type. In this case, the parameter array acts precisely like a value parameter.
-  Alternatively, the invocation can specify zero or more arguments for the parameter array, where each argument is an expression that is implicitly convertible (§11.2) to the element type of the parameter array. In this case, the invocation creates an instance of the parameter array type with a length corresponding to the number of arguments, initializes the elements of the `array` instance with the given argument values, and uses the newly created `array` instance as the actual argument.

Except for allowing a variable number of arguments in an invocation, a parameter array is precisely equivalent to a value parameter (§15.6.2.2) of the same type.

[Example: The example

```csharp
using System;

class Test
{
    static void F(params int[] args) {
        Console.Write("Array contains {0} elements:", args.Length);
        foreach (int i in args)
        Console.Write(" {0}", i);
        Console.WriteLine();
    }
    
    static void Main() {
        int[] arr = {1, 2, 3};
        F(arr);
        F(10, 20, 30, 40);
        F();
    }
}
```

produces the output

```csharp
Array contains 3 elements: 1 2 3
Array contains 4 elements: 10 20 30 40
Array contains 0 elements:
```

The first invocation of `F` simply passes the array `arr` as a value parameter. The second invocation of F automatically creates a four-element `int[]` with the given element values and passes that `array` instance as a value parameter. Likewise, the third invocation of `F` creates a zero-element `int[]` and passes that instance as a value parameter. The second and third invocations are precisely equivalent to writing:

```csharp
F(new int[] {10, 20, 30, 40});
F(new int[] {});
```

end example]

When performing overload resolution, a method with a parameter array might be applicable, either in its normal form or in its expanded form (§12.6.4.2). The expanded form of a method is available only if the normal form of the method is not applicable and only if an applicable method with the same signature as the expanded form is not already declared in the same type.

[Example: The example
```csharp
using System;

class Test
{
    static void F(params object[] a) {
        Console.WriteLine("F(object[])");
    }
    
    static void F() {
        Console.WriteLine("F()");
    }
    
    static void F(object a0, object a1) {
        Console.WriteLine("F(object,object)");
    }
    
    static void Main() {
        F();
        F(1);
        F(1, 2);
        F(1, 2, 3);
        F(1, 2, 3, 4);
    }
}
```

produces the output

```csharp
F();
F(object[]);
F(object,object);
F(object[]);
F(object[]);
```

In the example, two of the possible expanded forms of the method with a parameter array are already included in the `class` as regular methods. These expanded forms are therefore not considered when performing overload resolution, and the first and third method invocations thus select the regular methods. When a `class` declares a method with a parameter array, it is not uncommon to also include some of the expanded forms as regular methods. By doing so, it is possible to avoid the allocation of an `array` instance that occurs when an expanded form of a method with a parameter array is invoked. end example]

When the type of a parameter array is `object[]`, a potential ambiguity arises between the normal form of the method and the expanded form for a single `object` parameter. The reason for the ambiguity is that an `object[]` is itself implicitly convertible to type `object`. The ambiguity presents no problem, however, since it can be resolved by inserting a cast if needed.

[Example: The example

```csharp
using System;

class Test
{
    static void F(params object[] args) {
        foreach (object o in args) {
        Console.Write(o.GetType().FullName);
        Console.Write(" ");
    }
Console.WriteLine();
}
    
static void Main() {
    object[] a = {1, "Hello", 123.456};
    object o = a;
    F(a);
    F((object)a);
    F(o);
    F((object[])o);
    }
}
```
produces the output

```csharp
System.Int32 System.String System.Double
System.Object[]
System.Object[]
System.Int32 System.String System.Double
```

In the first and last invocations of `F`, the normal form of `F` is applicable because an implicit conversion exists from the argument type to the parameter type (both are of type `object[]`). Thus, overload resolution selects the normal form of `F`, and the argument is passed as a regular value parameter. In the second and third invocations, the normal form of `F` is not applicable because no implicit conversion exists from the argument type to the parameter type (type `object` cannot be implicitly converted to type `object[]`). However, the expanded form of `F` is applicable, so it is selected by overload resolution. As a result, a one-element `object[]` is created by the invocation, and the single element of the array is initialized with the given argument value (which itself is a reference to an `object[]`). end example]

### Static and instance methods

When a method declaration includes a `static` modifier, that method is said to be a `static` method. When no `static` modifier is present, the method is said to be an `instance` method.

A `static` method does not operate on a specific instance, and it is a compile-time error to refer to `this` in a `static` method.

An `instance` method operates on a given instance of a class, and that instance can be accessed as `this` (§12.7.8).

The differences between `static` and `instance` members are discussed further in §15.3.8.

### `virtual` methods

When an instance method declaration includes a `virtual` modifier, that method is said to be a ***virtual method***. When no `virtual` modifier is present, the method is said to be a ***non-`virtual` method***.

The implementation of a non-'virtual method is invariant: The implementation is the same whether the method is invoked on an instance of the `class` in which it is declared or an instance of a derived class. In contrast, the implementation of a `virtual` method can be superseded by derived classes. The process of superseding the implementation of an inherited `virtual` method is known as ***overriding*** that method (§15.6.5).

In a `virtual` method invocation, the ***run-time type*** of the instance for which that invocation takes place determines the actual method implementation to invoke. In a non-`virtual` method invocation, the ***compile-time type*** of the instance is the determining factor. In precise terms, when a method named `N` is invoked with an argument list `A` on an instance with a compile-time type `C` and a run-time type `R` (where `R` is either `C` or a `class` derived from `C`), the invocation is processed as follows:

-  At binding-time, overload resolution is applied to `C`, `N`, and `A`, to select a specific method `M` from the set of methods declared in and inherited by `C`. This is described in §12.7.6.2.
-  Then at run-time:
  -  If `M` is a non-`virtual` method, `M` is invoked.
  -  Otherwise, `M` is a `virtual` method, and the most derived implementation of `M` with respect to `R` is invoked.

For every `virtual` method declared in or inherited by a class, there exists a ***most derived implementation*** of the method with respect to that class. The most derived implementation of a `virtual` method `M` with respect to a class `R` is determined as follows:

-  If `R` contains the introducing virtual declaration of `M`, then this is the most derived implementation of `M` with respect to `R`.
-  Otherwise, if `R` contains an override of `M`, then this is the most derived implementation of `M` with respect to `R`.
-  Otherwise, the most derived implementation of `M` with respect to `R` is the same as the most derived implementation of `M` with respect to the direct base class of `R`.

[Example: The following example illustrates the differences between virtual and non-`virtual` methods:

```csharp
using System;

class A
{
    public void F() { Console.WriteLine("A.F"); }
    public virtual void G() { Console.WriteLine("A.G"); }
}

class B: A
{
    new public void F() { Console.WriteLine("B.F"); }
    public override void G() { Console.WriteLine("B.G"); }
}

class Test
{
    static void Main() {
        B b = new B();
        A a = b;
        a.F();
        b.F();
        a.G();
        b.G();
    }
}
```

In the example, `A` introduces a non-`virtual` method `F` and a `virtual` method `G`. The class `B` introduces a *new* non-`virtual` method `F`, thus *hiding* the inherited `F`, and also *overrides* the inherited method `G`. The example produces the output:

```csharp
A.F
B.F
B.G
B.G
```

Notice that the statement `a.G()` invokes `B.G`, not `A.G`. This is because the run-time type of the instance (which is `B`), not the compile-time type of the instance (which is `A`), determines the actual method implementation to invoke. end example]

Because methods are allowed to hide inherited methods, it is possible for a `class` to contain several `virtual` methods with the same signature. This does not present an ambiguity problem, since all but the most derived method are hidden. [Example: In the following code

```csharp
using System;

class A
{
    public virtual void F() { Console.WriteLine("A.F"); }
}

class B: A
{
    public override void F() { Console.WriteLine("B.F"); }
}

class C: B
{
    new public virtual void F() { Console.WriteLine("C.F"); }
}

class D: C
{
    public override void F() { Console.WriteLine("D.F"); }
}

class Test
{
    static void Main() {
        D d = new D();
        A a = d;
        B b = d;
        C c = d;
        a.F();
        b.F();
        c.F();
        d.F();
    }
}
```

the `C` and `D` classes contain two `virtual` methods with the same signature: The one introduced by `A` and the one introduced by `C`. The method introduced by `C` hides the method inherited from `A`. Thus, the override declaration in `D` overrides the method introduced by `C`, and it is not possible for `D` to override the method introduced by `A`. The example produces the output:

```csharp
B.F
B.F
D.F
D.F
```

Note that it is possible to invoke the hidden `virtual` method by accessing an instance of `D` through a less derived type in which the method is not hidden. end example]

### Override methods

When an instance method declaration includes an `override` modifier, the method is said to be an ***override method***. An `override` method overrides an inherited `virtual` method with the same signature. Whereas a `virtual` method declaration *introduces* a new method, an `override` method declaration *specializes* an existing inherited `virtual` method by providing a new implementation of that method.

The method overridden by an `override` declaration is known as the ***overridden base method*** For an override method `M` declared in a class `C`, the overridden base method is determined by examining each base class of `C`, starting with the direct base class of `C` and continuing with each successive direct base class, until in a given base class type at least one accessible method is located which has the same signature as `M` after substitution of type arguments. For the purposes of locating the overridden base method, a method is considered accessible if it is `public`, if it is `protected`, if it is `protected internal`, or if it is `internal` and declared in the same program as `C`.

A compile-time error occurs unless all of the following are true for an override declaration:

-  An overridden base method can be located as described above.
-  There is exactly one such overridden base method. This restriction has effect only if the base class type is a constructed type where the substitution of type arguments makes the signature of two methods the same.
-  The overridden base method is a `virtual`, `abstract`, or `override` method. In other words, the overridden base method cannot be static or non-virtual.
-  The overridden base method is not a sealed method.
-  There is an identity conversion between the return type of the overridden base method and the override method.
-  The override declaration and the overridden base method have the same declared accessibility. In other words, an override declaration cannot change the accessibility of the `virtual` method. However, if the overridden base method is protected internal and it is declared in a different assembly than the assembly containing the override declaration then the override declaration’s declared accessibility shall be protected.
-  The override declaration does not specify type-parameter-constraints-clauses. Instead, the constraints are inherited from the overridden base method. Constraints that are type parameters in the overridden method may be replaced by type arguments in the inherited constraint. This can lead to constraints that are not valid when explicitly specified, such as value types or sealed types.

[Example: The following demonstrates how the overriding rules work for generic classes:

```csharp
abstract class C<T>
{
    public virtual T F() {…}
    
    public virtual C<T> G() {…}
    
    public virtual void H(C<T> x) {…}
}

class D: C<string>
{
    public override string F() {…}                // Ok
    
    public override C<string> G() {…}             // Ok
    
    public override void H(C<T> x) {…}            // Error, should be C<string>
}

class E<T,U>: C<U>
{
    public override U F() {…}                     // Ok
    
    public override C<U> G() {…}                  // Ok
    
    public override void H(C<T> x) {…}            // Error, should be C<U>
}
```

end example]

An override declaration can access the overridden base method using a *base-access* (§12.7.9). [Example: In the following code

```csharp
class A
{
    int x;
    
    public virtual void PrintFields() {
        Console.WriteLine("x = {0}", x);
    }
}

class B: A
{
    int y;
    
    public override void PrintFields() {
        base.PrintFields();
        Console.WriteLine("y = {0}", y);
    }
}
```

the `base.PrintFields()` invocation in `B` invokes the PrintFields method declared in `A`. A *base-access* disables the virtual invocation mechanism and simply treats the base method as a non-`virtual` method. Had the invocation in `B` been written `((A)this).PrintFields()`, it would recursively invoke the `PrintFields` method declared in `B`, not the one declared in `A`, since `PrintFields` is virtual and the run-time type of `((A)this)` is `B`. end example]

Only by including an `override` modifier can a method override another method. In all other cases, a method with the same signature as an inherited method simply hides the inherited method. [Example: In the following code

```csharp
class A
{
    public virtual void F() {}
}

class B: A
{
    public virtual void F() {} // Warning, hiding inherited F()
}
```

the `F` method in `B` does not include an `override` modifier and therefore does not override the `F` method in `A`. Rather, the `F` method in `B` hides the method in `A`, and a warning is reported because the declaration does not include a new modifier. end example]

[Example: In the following code

```csharp
class A
{
    public virtual void F() {}
}

class B: A
{
    new private void F() {} // Hides A.F within body of B
}

class C: B
{
    public override void F() {} // Ok, overrides A.F
}
```

the `F` method in `B` hides the virtual `F` method inherited from `A`. Since the new `F` in `B` has private access, its scope only includes the `class` body of `B` and does not extend to `C`. Therefore, the declaration of `F` in `C` is permitted to override the `F` inherited from `A`. end example]

### Sealed methods

When an instance method declaration includes a `sealed` modifier, that method is said to be a ***sealed method***. A `sealed` method overrides an inherited `virtual` method with the same signature. A `sealed` method shall also be marked with the `override` modifier. Use of the `sealed` modifier prevents a derived `class` from further overriding the method.

[Example: The example

```csharp
using System;

class A
{
    public virtual void F() {
        Console.WriteLine("A.F");
    }
    
    public virtual void G() {
        Console.WriteLine("A.G");
    }
}
    
class B: A
    {
    public sealed override void F() {
        Console.WriteLine("B.F");
    }
    
    public override void G() {
        Console.WriteLine("B.G");
    }
}
    
class C: B
    {
    public override void G() {
        Console.WriteLine("C.G");
    }
}
```

the class `B` provides two override methods: an `F` method that has the `sealed` modifier and a `G` method that does not. `B`’s use of the `sealed` modifier prevents `C` from further overriding `F`. end example]

### Abstract methods

When an instance method declaration includes an `abstract` modifier, that method is said to be an ***abstract method***. Although an abstract method is implicitly also a `virtual` method, it cannot have the modifier `virtual`.

An `abstract` method declaration introduces a new `virtual` method but does not provide an implementation of that method. Instead, non-abstract derived classes are required to provide their own implementation by overriding that method. Because an `abstract` method provides no actual implementation, the *method-body* of an `abstract` method simply consists of a semicolon.

Abstract method declarations are only permitted in abstract classes (§15.2.2.2).

[Example: In the following code

```csharp
public abstract class Shape
{
    public abstract void Paint(Graphics g, Rectangle r);
}

public class Ellipse: Shape
{
    public override void Paint(Graphics g, Rectangle r) {
        g.DrawEllipse(r);
    }
}

public class Box: Shape
{
    public override void Paint(Graphics g, Rectangle r) {
        g.DrawRect(r);
    }
}
```

the `Shape` `class` defines the abstract notion of a geometrical shape `object` that can paint itself. The `Paint` method is abstract because there is no meaningful default implementation. The `Ellipse` and `Box` classes are concrete `Shape` implementations. Because these classes are non-abstract, they are required to override the `Paint` method and provide an actual implementation. end example]

It is a compile-time error for a *base-access* (§12.7.9) to reference an abstract method. [Example: In the following code

```csharp
abstract class A
{
    public abstract void F();
}

class B: A
{
    public override void F() {
        base.F(); // Error, base.F is abstract\
    }
}
```

a compile-time error is reported for the `base.F()` invocation because it references an `abstract` method. end example]

An `abstract` method declaration is permitted to override a `virtual` method. This allows an `abstract` class to force re-implementation of the method in derived classes, and makes the original implementation of the method unavailable. [Example: In the following code

```csharp
using System;

class A
{
    public virtual void F() {
        Console.WriteLine("A.F");
    }
}

abstract class B: A
{
    public abstract override void F();
}

class C: B
{
    public override void F() {
        Console.WriteLine("C.F");
    }
}
```

class `A` declares a `virtual` method, class `B` overrides this method with an `abstract` method, and class `C` overrides the `abstract` method to provide its own implementation. end example]

### External methods

When a method declaration includes an `extern` modifier, the method is said to be an ***external method***. External methods are implemented externally, typically using a language other than C#. Because an external method declaration provides no actual implementation, the *method-body* of an external method simply consists of a semicolon. An external method shall not be generic.

The mechanism by which linkage to an external method is achieved, is implementation-defined.

[Example: The following example demonstrates the use of the `extern` modifier and the `DllImport` attribute:

```csharp
using System.Text;
using System.Security.Permissions;
using System.Runtime.InteropServices;

class Path
{
    [DllImport("kernel32", SetLastError=true)]
    static extern bool CreateDirectory(string name, SecurityAttribute sa);
    
    [DllImport("kernel32", SetLastError=true)]
    static extern bool RemoveDirectory(string name);
    
    [DllImport("kernel32", SetLastError=true)]
    static extern `int` GetCurrentDirectory(int bufSize, StringBuilder buf);
    
    [DllImport("kernel32", SetLastError=true)]
    static extern bool SetCurrentDirectory(string name);
}
```

end example]

### `partial` methods

When a method declaration includes a `partial` modifier, that method is said to be a ***`partial` method***. `partial` methods may only be declared as members of `partial` types (§15.2.7), and are subject to a number of restrictions.

`partial` methods may be defined in one part of a type declaration and implemented in another. The implementation is optional; if no part implements the `partial` method, the `partial` method declaration and all calls to it are removed from the type declaration resulting from the combination of the parts.

`partial` methods shall not define access modifiers; they are implicitly private. Their return type shall be void, and their parameters shall not have the `out` modifier. The identifier partial is recognized as a contextual keyword (§7.4.4) in a method declaration only if it appears immediately before the void keyword. A `partial` method cannot explicitly implement interface methods.

There are two kinds of `partial` method declarations: If the body of the method declaration is a semicolon, the declaration is said to be a ***defining `partial` method declaration***. If the body is given as a *block*, the declaration is said to be an ***implementing `partial` method declaration***. Across the parts of a type declaration, there may be only one defining `partial` method declaration with a given signature, and there may be only one implementing `partial` method declaration with a given signature. If an implementing `partial` method declaration is given, a corresponding defining `partial` method declaration shall exist, and the declarations shall match as specified in the following:

-  The declarations shall have the same modifiers (although not necessarily in the same order), method name, number of type parameters and number of parameters.
-  Corresponding parameters in the declarations shall have the same modifiers (although not necessarily in the same order) and the same types (modulo differences in type parameter names).
-  Corresponding type parameters in the declarations shall have the same constraints (modulo differences in type parameter names).

An implementing `partial` method declaration can appear in the same part as the corresponding defining `partial` method declaration.

Only a defining `partial` method participates in overload resolution. Thus, whether or not an implementing declaration is given, invocation expressions may resolve to invocations of the `partial` method. Because a `partial` method always returns void, such invocation expressions will always be expression statements. Furthermore, because a `partial` method is implicitly private, such statements will always occur within one of the parts of the type declaration within which the `partial` method is declared.

If no part of a `partial` type declaration contains an implementing declaration for a given `partial` method, any expression statement invoking it is simply removed from the combined type declaration. Thus the invocation expression, including any subexpressions, has no effect at run-time. The `partial` method itself is also removed and will not be a member of the combined type declaration.

If an implementing declaration exists for a given `partial` method, the invocations of the `partial` methods are retained. The `partial` method gives rise to a method declaration similar to the implementing `partial` method declaration except for the following:

-  The `partial` modifier is not included

-  The attributes in the resulting method declaration are the combined attributes of the defining and the implementing `partial` method declaration in unspecified order. Duplicates are not removed.

-  The attributes on the parameters of the resulting method declaration are the combined attributes of the corresponding parameters of the defining and the implementing `partial` method declaration in unspecified order. Duplicates are not removed.

If a defining declaration but not an implementing declaration is given for a `partial` method `M`, the following restrictions apply:

-  It is a compile-time error to create a delegate from `M` (§12.7.11.6).

-  It is a compile-time error to refer to `M` inside an anonymous function that is converted to an expression tree type (§9.6).

-  Expressions occurring as part of an invocation of `M` do not affect the definite assignment state (§10.4), which can potentially lead to compile-time errors.

-  `M` cannot be the entry point for an application (§8.1).

`partial` methods are useful for allowing one part of a type declaration to customize the behavior of another part, e.g., one that is generated by a tool. Consider the following partial class declaration:

```csharp
partial class Customer
{
    string name;
        
    public string name {
        
        get { return name; }
            
        set {
            OnNameChanging(value);
            name = value;
            OnNameChanged();
        }
    
    }
    
    partial void OnNameChanging(string newName);
    
    partial void OnNameChanged();
}
```

If this `class` is compiled without any other parts, the defining `partial` method declarations and their invocations will be removed, and the resulting combined `class` declaration will be equivalent to the following:

```csharp
class Customer
{
    string name;
    
    public string name {
    
    get { return name; }
    
    set { name = value; }
    }
}
```

Assume that another part is given, however, which provides implementing declarations of the `partial` methods:

```csharp
partial class Customer
{
    partial void OnNameChanging(string newName)
    {
        Console.WriteLine(“Changing “ + name + “ to “ + newName);
    }
    
    partial void OnNameChanged()
    {
        Console.WriteLine(“Changed to “ + name);
    }
}
```

Then the resulting combined `class` declaration will be equivalent to the following:

```csharp
class Customer
{
    string name;
    
    public string name {
    
        get { return name; }
        
        set {
            OnNameChanging(value);
            name = value;
            OnNameChanged();
        }

    }
    
    void OnNameChanging(string newName)
    {
        Console.WriteLine(“Changing “ + name + “ to “ + newName);
    }
    
    void OnNameChanged()
    {
        Console.WriteLine(“Changed to “ + name);
    }
}
```

### Extension methods

When the first parameter of a method includes the `this` modifier, that method is said to be an ***extension method***. Extension methods shall only be declared in non-generic, non-nested `static class`es. The first parameter of an extension method may have no modifiers other than `this`, and the parameter type may not be a pointer type.

[Example: The following is an example of a `static class` that declares two extension methods:

```csharp
public static class Extensions
{
    public static int ToInt32(this string s) {
        return Int32.Parse(s);
    }
    
    public static T[] Slice<T>(this T[] source, int index, int count) {
        if (index < 0 || count < 0 || source.Length – index < count)
        throw new ArgumentException();
        T[] result = new T[count];
        Array.Copy(source, index, result, 0, count);
        return result;
    }
}
```

end example]

An extension method is a regular `static` method. In addition, where its enclosing `static` class is in scope, an extension method may be invoked using `instance` method invocation syntax (§12.7.6.3), using the receiver expression as the first argument.

[Example: The following program uses the extension methods declared above:

```csharp
static class Program
{
    static void Main() {
        string[] strings = { "1", "22", "333", "4444" };
        foreach (string `S` in strings.Slice(1, 2)) {
            Console.WriteLine(s.ToInt32());
        }
    }
}
```

The `Slice` method is available on the `string[]`, and the `ToInt32` method is available on `string`, because they have been declared as extension methods. The meaning of the program is the same as the following, using ordinary `static` method calls:

```csharp
static class Program
{
    static void Main() {
    string[] strings = { "1", "22", "333", "4444" };
    foreach (string `S` in Extensions.Slice(strings, 1, 2)) {
            Console.WriteLine(Extensions.ToInt32(s));
        }
    }
}
```

end example]

### Method body

The *method-body* of a method declaration consists of either a *block* or a semicolon.

Abstract and external method declarations do not provide a method implementation, so their method bodies simply consist of a semicolon. For any other method, the method body is a block (§13.3) that contains the statements to execute when that method is invoked.

The ***effective return type*** of a method is `void` if the return type is `void`, or if the method is async and the return type is `System.Threading.Tasks.Task`. Otherwise, the effective return type of a non-async method is its return type, and the effective return type of an async method with return type `System.Threading.Tasks.Task<T>` is `T`.

When the effective return type of a method is `void`, `return` statements (§13.10.5) in that method’s body are not permitted to specify an expression. If execution of the method body of a void method completes normally (that is, control flows off the end of the method body), that method simply returns to its caller.

When the effective return type of a method is not `void`, each return statement in that method's body shall specify an expression that is implicitly convertible to the effective return type. The endpoint of the method body of a value-returning method shall not be reachable. In other words, in a value-returning method, control is not permitted to flow off the end of the method body.

[Example: In the following code

```csharp
class A
{
    public int F() {} // Error, return value required
    
    public int G() {
        return 1;
    }
    
    public int H(bool b) {
        if (b) {
            return 1;
        }
        else {
        return 0;
        }
    }
}
```

the value-returning `F` method results in a compile-time error because control can flow off the end of the method body. The `G` and `H` methods are correct because all possible execution paths end in a return statement that specifies a return value. end example]

## Properties

### General

A ***property*** is a member that provides access to a characteristic of an `object` or a class. Examples of properties include the length of a string, the size of a font, the caption of a window, the name of a customer, and so on. Properties are a natural extension of fields—both are named members with associated types, and the syntax for accessing fields and properties is the same. However, unlike fields, properties do not denote storage locations. Instead, properties have ***accessors*** that specify the statements to be executed when their values are read or written. Properties thus provide a mechanism for associating actions with the reading and writing of an object’s characteristics; furthermore, they permit such characteristics to be computed.

Properties are declared using *property-declaration*s:

```ANTLR
property-declaration:
    attributes~opt~ property-modifiers~opt~ type member-name { accessor-declarations }

property-modifiers:
    property-modifier
    property-modifiers property-modifier

property-modifier:
    new
    public
    protected
    internal
    private
    static
    virtual
    sealed
    override
    abstract
    extern
```

A *property-declaration* may include a set of *attributes* (§22) and a valid combination of the four access modifiers (§15.3.6), the `new` (§15.3.5), `static` (§15.7.2), `virtual` (§15.6.4, §15.7.6), `override` (§15.6.5, §15.7.6), `sealed` (§15.6.6), `abstract` (§15.6.7, §15.7.6), and `extern` (§15.6.8) modifiers.

Property declarations are subject to the same rules as method declarations (§15.6) with regard to valid combinations of modifiers.

The *type* of a property declaration specifies the type of the property introduced by the declaration, and the *member-name* (§15.6.1) specifies the name of the property. Unless the property is an explicit interface member implementation, the *member-name* is simply an *identifier*. For an explicit interface member implementation (§18.6.2), the *member-name* consists of an *interface-type* followed by a “`.`” and an *identifier*.

The *type* of a property shall be at least as accessible as the property itself (§8.5.5).

The *accessor-declarations*, which shall be enclosed in “`{`” and “`}`” tokens, declare the accessors (§15.7.3) of the property. The accessors specify the executable statements associated with reading and writing the property.

Even though the syntax for accessing a property is the same as that for a field, a property is not classified as a variable. Thus, it is not possible to pass a property as a `ref` or `out` argument.

When a property declaration includes an `extern` modifier, the property is said to be an ***external property***. Because an external property declaration provides no actual implementation, each of its *accessor-declarations* consists of a semicolon.

### Static and instance properties

When a property declaration includes a `static` modifier, the property is said to be a ***static property***. When no `static` modifier is present, the property is said to be an ***instance property***.

A `static` property is not associated with a specific instance, and it is a compile-time error to refer to this in the accessors of a static property.

An `instance` property is associated with a given instance of a class, and that instance can be accessed as `this` (§12.7.8) in the accessors of that property.

The differences between `static` and `instance` members are discussed further in §15.3.8.

### Accessors

The *accessor-declarations* of a property specify the executable statements associated with reading and writing that property.

```ANTLR
accessor-declarations:
    get-accessor-declaration set-accessor-declaration~opt~
    set-accessor-declaration get-accessor-declaration~opt~

get-accessor-declaration:
    attributes~opt~ accessor-modifier~opt~ get accessor-body

set-accessor-declaration:
    attributes~opt~ accessor-modifier~opt~ set accessor-body

accessor-modifier:
    protected
    internal
    private
    protected internal
    internal protected

accessor-body:
    block
    ;
```

The accessor declarations consist of a *get-accessor-declaration*, a *set-accessor-declaration*, or both. Each accessor declaration consists of optional attributes, an optional *accessor-modifier*, the token `get` or `set`, followed by an *accessor-body*.

The use of *accessor-modifier*s is governed by the following restrictions:

-  An *accessor-modifier* shall not be used in an interface or in an explicit interface member implementation.
-  For a property or indexer that has no `override` modifier, an *accessor-modifier* is permitted only if the property or indexer has both a `get` and `set` accessor, and then is permitted only on one of those accessors.
-  For a property or indexer that includes an `override` modifier, an accessor shall match the *accessor-modifier*, if any, of the accessor being overridden.
-  The *accessor-modifier* shall declare an accessibility that is strictly more restrictive than the declared accessibility of the property or indexer itself. To be precise:
  -  If the property or indexer has a declared accessibility of `public`, the *accessor-modifier* may be either `protected internal`, `internal`, `protected`, or `private`.
  -  If the property or indexer has a declared accessibility of `protected internal`, the *accessor-modifier* may be either `internal`, `protected`, or `private`.
  -  If the property or indexer has a declared accessibility of `internal` or `protected`, the *accessor-modifier* shall be `private`.
  -  If the property or indexer has a declared accessibility of `private`, no *accessor-modifier* may be used.

For `abstract` and `extern` properties, the *accessor-body* for each accessor specified is simply a semicolon. A non-abstract, non-extern property may be an ***automatically implemented property***, in which case both `get` and `set` accessors shall be given, both with a semicolon body (§15.7.4). For the accessors of any other non-abstract, non-extern property, the *accessor-body* is a *block* that specifies the statements to be executed when the corresponding accessor is invoked.

A `get` accessor corresponds to a parameterless method with a return value of the `property` type. Except as the target of an assignment, when a property is referenced in an expression, the `get` accessor of the property is invoked to compute the value of the property (§12.2.2). The body of a `get` accessor shall conform to the rules for value-returning methods described in §15.6.11. In particular, all return statements in the body of a `get` accessor shall specify an expression that is implicitly convertible to the `property` type. Furthermore, the endpoint of a `get` accessor shall not be reachable.

A `set` accessor corresponds to a method with a single value parameter of the `property` type and a void return type. The implicit parameter of a `set` accessor is always named value. When a property is referenced as the target of an assignment (§12.18), or as the operand of `++` or `–-` (§12.7.10, 12.8.6), the `set` accessor is invoked with an argument that provides the new value (§12.18.2). The body of a `set` accessor shall conform to the rules for `void` methods described in §15.6.11. In particular, return statements in the `set` accessor body are not permitted to specify an expression. Since a `set` accessor implicitly has a parameter named value, it is a compile-time error for a local variable or constant declaration in a `set` accessor to have that name.

Based on the presence or absence of the `get` and `set` accessors, a property is classified as follows:

-  A property that includes both a `get` accessor and a `set` accessor is said to be a ***read-write*** property.
-  A property that has only a `get` accessor is said to be a ***read-only*** property. It is a compile-time error for a read-only property to be the target of an assignment.
-  A property that has only a `set` accessor is said to be a ***write-only*** property. Except as the target of an assignment, it is a compile-time error to reference a write-only property in an expression. 

> [!NOTE] 
> The pre- and postfix `++` and `--` operators and compound assignment operators cannot be applied to write-only properties, since these operators read the old value of their operand before they write the new one. 

[Example: In the following code

```csharp
public class Button: Control
{
    private string caption;

    public string Caption {
        get {
            return caption;
        }
        set {
            if (caption != value) {
                caption = value;
                Repaint();
            }
        }
    }

    public override void Paint(Graphics g, Rectangle r) {
        // Painting code goes here
    }
}
```

the `Button` control declares a public `Caption` property. The `get` accessor of the Caption property returns the `string` stored in the private `caption` field. The `set` accessor checks if the new value is different from the current value, and if so, it stores the new value and repaints the control. Properties often follow the pattern shown above: The `get` accessor simply returns a value stored in a `private` field, and the `set` accessor modifies that `private` field and then performs any additional actions required to update fully the state of the object.

Given the `Button` class above, the following is an example of use of the `Caption` property:

```csharp
Button okButton = new Button();
okButton.Caption = "OK"; // Invokes set accessor
string S = okButton.Caption; // Invokes get accessor
```

Here, the `set` accessor is invoked by assigning a value to the property, and the `get` accessor is invoked by referencing the property in an expression. end example]

The `get` and `set` accessors of a property are not distinct members, and it is not possible to declare the accessors of a property separately. [Example: The example

```csharp
class A
{
    private string name;

    public string name { // Error, duplicate member name
        get { return name; }
    }

    public string name { // Error, duplicate member name
        set { name = value; }
    }
}
```

does not declare a single read-write property. Rather, it declares two properties with the same name, one read-only and one write-only. Since two members declared in the same `class` cannot have the same name, the example causes a compile-time error to occur. end example]

When a derived `class` declares a property by the same name as an inherited property, the derived property hides the inherited property with respect to both reading and writing. [Example: In the following code

```csharp
class A
{
    public int P {
        set {…}
    }
}

class B: A
{
    new public int P {
        get {…}
    }
}
```

the `P` property in `B` hides the `P` property in `A` with respect to both reading and writing. Thus, in the statements

```csharp
B b = new B();
b.P = 1; // Error, B.P is read-only
((A)b).P = 1; // Ok, reference to A.P
```

the assignment to `b.P` causes a compile-time error to be reported, since the read-only `P` property in `B` hides the write-only `P` property in `A`. Note, however, that a cast can be used to access the hidden `P` property. end example]

Unlike `public` fields, properties provide a separation between an object’s internal state and its `public` interface. [Example: Consider the following code, which uses a `Point` struct to represent a location:

```csharp
class Label
{
    private int x, y;
    private string caption;

    public Label(int x, int y, string caption) {
        this.x = x;
        this.y = y;
        this.caption = caption;
    }

    public int X {
        get { return x; }
    }

    public int Y {
        get { return y; }
    }

    public Point Location {
        get { return new Point(x, y); }
    }

    public string Caption {
        get { return caption; }
    }
}
```

Here, the Label `class` uses two `int` fields, `x` and `y`, to store its location. The location is publicly exposed both as an `X` and a `Y` property and as a `Location` property of type `Point`. If, in a future version of `Label`, it becomes more convenient to store the location as a `Point` internally, the change can be made without affecting the public interface of the class:

```csharp
class Label
{
    private Point location;
    private string caption;

    public Label(int x, int y, string caption) {
        this.location = new Point(x, y);
        this.caption = caption;
    }

    public int X {
        get { return location.x; }
    }

    public int Y {
        get { return location.y; }
    }

    public Point Location {
        get { return location; }
    }

    public string Caption {
        get { return caption; }
    }
}
```

Had `x` and `y` instead been `public readonly` fields, it would have been impossible to make such a change to the `Label` class. end example]

> [!NOTE] 
> Exposing state through properties is not necessarily any less efficient than exposing fields directly. In particular, when a property is non-virtual and contains only a small amount of code, the execution environment might replace calls to accessors with the actual code of the accessors. This process is known as ***inlining***, and it makes property access as efficient as field access, yet preserves the increased flexibility of properties. 

[Example: Since invoking a `get` accessor is conceptually equivalent to reading the value of a field, it is considered bad programming style for `get` accessors to have observable side-effects. In the example

```csharp
class Counter
{
    private int next;

    public int Next {
        get { return next++; }
    }
}
```

the value of the `Next` property depends on the number of times the property has previously been accessed. Thus, accessing the property produces an observable side effect, and the property should be implemented as a method instead.

The “no side-effects” convention for `get` accessors doesn’t mean that `get` accessors should always be written simply to return values stored in fields. Indeed, `get` accessors often compute the value of a property by accessing multiple fields or invoking methods. However, a properly designed `get` accessor performs no actions that cause observable changes in the state of the object. end example]

Properties can be used to delay initialization of a resource until the moment it is first referenced. [Example:

```csharp
using System.IO;

public class Console
{
    private static TextReader reader;
    private static TextWriter writer;
    private static TextWriter error;

    public static TextReader In {
        get {
            if (reader == null) {
                reader = new StreamReader(Console.OpenStandardInput());
            }
            return reader;
        }
    }

    public static TextWriter Out {
        get {
            if (writer == null) {
                writer = new StreamWriter(Console.OpenStandardOutput());
            }
            return writer;
        }
    }

    public static TextWriter Error {
        get {
            if (error == null) {
                error = new StreamWriter(Console.OpenStandardError());
            }
            return error;
        }
    }
…
}
```

The Console `class` contains three properties, `In`, `Out`, and `Error`, that represent the standard input, output, and error devices, respectively. By exposing these members as properties, the `Console` class can delay their initialization until they are actually used. For example, upon first referencing the `Out` property, as in

```csharp
Console.Out.WriteLine("hello, world");
```

the underlying `TextWriter` for the output device is created. However, if the application makes no reference to the `In` and `Error` properties, then no objects are created for those devices. end example]

### Automatically implemented properties

When a property is specified as an automatically implemented property, a hidden backing field is automatically available for the property, and the accessors are implemented to read from and write to that backing field. The hidden backing field is inaccessible, it can be read and written only through the automatically implemented property accessors, even within the containing type.

[Example:

```csharp
public class Point {
    public int X { get; set; } // automatically implemented
    public int Y { get; set; } // automatically implemented
}
```

is equivalent to the following declaration:

```csharp
public class Point {
    private int x;
    private int y;
    public int X { get { return x; } set { x = value; } }
    public int Y { get { return y; } set { y = value; } }
}
```

end example]

Because the backing field is inaccessible, automatically implemented read-only or write-only properties do not make sense, and are disallowed. It is however possible to set the access level of each accessor differently. Thus, the effect of a read-only property with a private backing field can be mimicked like this:

```csharp
public class ReadOnlyPoint {
    public int X { get; private set; }
    public int Y { get; private set; }
    public ReadOnlyPoint(int x, int y) { X = x; Y = y; }
}
```

### Accessibility

If an accessor has an *accessor-modifier*, the accessibility domain (§8.5.3) of the accessor is determined using the declared accessibility of the *accessor-modifier*. If an accessor does not have an *accessor-modifier*, the accessibility domain of the accessor is determined from the declared accessibility of the property or indexer.

The presence of an *accessor-modifier* never affects member lookup (§12.5) or overload resolution (§12.6.4). The modifiers on the property or indexer always determine which property or indexer is bound to, regardless of the context of the access.

Once a particular property or indexer has been selected, the accessibility domains of the specific accessors involved are used to determine if that usage is valid:

-  If the usage is as a value (§12.2.2), the `get` accessor shall exist and be accessible.
-  If the usage is as the target of a simple assignment (§12.18.2), the `set` accessor shall exist and be accessible.
-  If the usage is as the target of compound assignment (§12.18.3), or as the target of the `++` or `--` operators (§12.7.10, §12.8.6), both the `get` accessors and the `set` accessor shall exist and be accessible.

[Example: In the following example, the property `A.Text` is hidden by the property` B.Text`, even in contexts where only the `set` accessor is called. In contrast, the property `B.Count` is not accessible to class `M`, so the accessible property `A.Count` is used instead.

```csharp
class A
{
    public string Text {
        get { return "hello"; }
        set { }
    }

    public int Count {
        get { return 5; }
        set { }
    }
}

class B: A
{
    private string text = "goodbye";
    private int count = 0;

    new public string Text {
        get { return text; }
        protected set { text = value; }
    }

    new protected int Count {
        get { return count; }
        set { count = value; }
    }
}

class M
{
    static void Main() {
        B b = new B();
        b.Count = 12; // Calls A.Count set accessor
        int i = b.Count; // Calls A.Count get accessor
        b.Text = "howdy"; // Error, B.Text set accessor not accessible
        string `S` = b.Text; // Calls B.Text get accessor
    }
}
```

end example]

An accessor that is used to implement an interface shall not have an *accessor-modifier*. If only one accessor is used to implement an interface, the other accessor may be declared with an *accessor-modifier*: [Example:

```csharp
public interface I
{
    string Prop { get; }
}

public class C: I
{
    public Prop {
        get { return "April"; } // Must not have a modifier here
        internal set {…} // Ok, because I.Prop has no set accessor
    }
}
```

end example]

### Virtual, sealed, override, and abstract accessors

A `virtual` property declaration specifies that the accessors of the property are virtual. The `virtual` modifier applies to all non-private accessors of a property. When an accessor of a `virtual` property has the private *accessor-modifier*, the `private` accessor is implicitly not `virtual`.

An `abstract` property declaration specifies that the accessors of the property are virtual, but does not provide an actual implementation of the accessors. Instead, non-abstract derived classes are required to provide their own implementation for the accessors by overriding the property. Because an accessor for an `abstract` property declaration provides no actual implementation, its *accessor-body* simply consists of a semicolon. An `abstract` property shall not have a `private` accessor.

A `property` declaration that includes both the `abstract` and `override` modifiers specifies that the property is abstract and overrides a base property. The accessors of such a property are also `abstract`.

`Abstract` property declarations are only permitted in `abstract` classes (§15.2.2.2). The accessors of an inherited `virtual` property can be overridden in a derived `class` by including a property declaration that specifies an `override` directive. This is known as an ***overriding property declaration***. An overriding property declaration does not declare a new property. Instead, it simply specializes the implementations of the accessors of an existing `virtual` property.

An overriding property declaration shall specify the exact same accessibility modifiers and name as the inherited property, and there shall be an identity conversion between the type of the overriding and the inherited property. If the inherited property has only a single accessor (i.e., if the inherited property is read-only or write-only), the overriding property shall include only that accessor. If the inherited property includes both accessors (i.e., if the inherited property is read-write), the overriding property can include either a single accessor or both accessors.

An overriding property declaration may include the `sealed` modifier. Use of this modifier prevents a derived `class` from further overriding the property. The accessors of a sealed property are also sealed.

Except for differences in declaration and invocation syntax, `virtual`, `sealed`, `override`, and `abstract` accessors behave exactly like `virtual`, `sealed`, `override` and `abstract` methods. Specifically, the rules described in §15.6.4, §15.6.5, §15.6.6, and §15.6.7 apply as if accessors were methods of a corresponding form:

-  A `get` accessor corresponds to a parameterless method with a return value of the property type and the same modifiers as the containing property.
-  A `set` accessor corresponds to a method with a single value parameter of the property type, a void return type, and the same modifiers as the containing property.

[Example: In the following code

```csharp
abstract class A
{
    int y;

    public virtual int x {
        get { return 0; }
    }

    public virtual int y {
        get { return y; }
        set { y = value; }
    }

    public abstract int z { get; set; }
}
```

`X` is a virtual read-only property, `Y` is a virtual read-write property, and `Z` is an abstract read-write property. Because `Z` is abstract, the containing `class` A shall also be declared abstract.

A `class` that derives from `A` is show below:

```csharp
class B: A
{
    int z;

    public override int x {
        get { return base.X + 1; }
    }

    public override int y {
        set { base.Y = value < 0? 0: value; }
    }

    public override int z {
        get { return z; }
        set { z = value; }
    }
}
```

Here, the declarations of `X`, `Y`, and `Z` are overriding property declarations. Each property declaration exactly matches the accessibility modifiers, type, and name of the corresponding inherited property. The `get` accessor of `X` and the `set` accessor of `Y` use the base keyword to access the inherited accessors. The declaration of `Z` overrides both abstract accessors—thus, there are no outstanding `abstract` function members in `B`, and `B` is permitted to be a non-abstract class. end example]

When a property is declared as an override, any overridden accessors shall be accessible to the overriding code. In addition, the declared accessibility of both the property or indexer itself, and of the accessors, shall match that of the overridden member and accessors. [Example:

```csharp
public class B
{
    public virtual int P {
        protected set {…}
        get {…}
    }
}

public class D: B
{
    public override int P {
        protected set {…} // Must specify protected here
        get {…} // Must not have a modifier here
    }
}
```

end example]

## Events

### General

An ***event*** is a member that enables an `object` or `class` to provide notifications. Clients can attach executable code for events by supplying ***event handlers***.

Events are declared using *event-declaration*s:

[](#Grammar_event_declaration)
```ANTLR
event-declaration:
  attributes~opt~ event-modifiers~opt~ event type variable-declarators ;
  attributes~opt~ event-modifiers~opt~ event type member-name
  { event-accessor-declarations }
```

[](#Grammar_event_modifiers)
```ANTLR
event-modifiers:
  event-modifier
  event-modifiers event-modifier
```

[](#Grammar_event_modifier)
```ANTLR
event-modifier:
  new
  public
  protected
  internal 
  private
  static
  virtual
  sealed
  override
  abstract
  extern
```

[](#Grammar_event_accessor_declarations)
```ANTLR
event-accessor-declarations:
  add-accessor-declaration remove-accessor-declaration
  remove-accessor-declaration add-accessor-declaration
```

[](#Grammar_add_accessor_declaration)
```ANTLR
add-accessor-declaration:
  vattributes~opt~ add block
```

[](#Grammar_remove_accessor_declaration)
```ANTLR
remove-accessor-declaration:
  attributes~opt~ *remove* block
```

An *event-declaration* may include a set of *attributes* (§22) and a valid combination of the four access modifiers (§15.3.6), the `new` (§15.3.5), `static` (§15.6.3, §15.8.4), `virtual` (§15.6.4, §15.8.5), `override` (§15.6.5, §15.8.5), `sealed` (§15.6.6), `abstract` (§15.6.7, §15.8.5), and `extern` (§15.6.8) modifiers.

Event declarations are subject to the same rules as method declarations (§15.6) with regard to valid combinations of modifiers.

The *type* of an event declaration shall be a *delegate-type* (§9.2.8), and that *delegate-type* shall be at least as accessible as the event itself (§8.5.5).

An event declaration can include *event-accessor-declaration*s. However, if it does not, for non-extern, non-abstract events, the compiler shall supply them automatically (§15.8.2); for `extern` events, the accessors are provided externally.

An event declaration that omits *event-accessor-declaration*s defines one or more events—one for each of the *variable-declarator*s. The attributes and modifiers apply to all of the members declared by such an *event-declaration*.

It is a compile-time error for an *event-declaration* to include both the `abstract` modifier and *event-accessor-declaration*s.

When an event declaration includes an `extern` modifier, the event is said to be an ***external event***. Because an external event declaration provides no actual implementation, it is an error for it to include both the `extern` modifier and *event-accessor-declaration*s.

It is a compile-time error for a *variable-declarator* of an event declaration with an `abstract` or `external` modifier to include a *variable-initializer*.

An event can be used as the left-hand operand of the `+=` and `-=` operators. These operators are used, respectively, to attach event handlers to, or to remove event handlers from an event, and the access modifiers of the event control the contexts in which such operations are permitted.

The only operations that are permitted on an event by code that is outside the type in which that event is declared, are `+=` and `-=`. Therefore, while such code can add and remove handlers for an event, it cannot directly obtain or modify the underlying list of event handlers.

In an operation of the form `x += y` or `x –= y`, when `x` is an event the result of the operation has type void (§12.18.4) (as opposed to having the type of `x`, with the value of `x` after the assignment, as for other the `+=` and `-=` operators defined on non-event types). This prevents external code from indirectly examining the underlying delegate of an event.

[Example: The following example shows how event handlers are attached to instances of the Button class:

```csharp
public delegate void EventHandler(object sender, EventArgs e);

public class Button: Control
{
    public event EventHandler Click;
}

public class LoginDialog: Form
{
    Button okButton;
    Button cancelButton;

    public LoginDialog() {
        okButton = new Button(…);
        okButton.Click += new EventHandler(OkButtonClick);
        cancelButton = new Button(…);
        cancelButton.Click += new EventHandler(CancelButtonClick);
    }

    void OkButtonClick(object sender, EventArgs e) {
        // Handle okButton.Click event
    }

    void CancelButtonClick(object sender, EventArgs e) {
        // Handle cancelButton.Click event
    }
}
```

Here, the `LoginDialog` instance constructor creates two `Button` instances and attaches event handlers to the `Click` events. end example]

### Field-like events

Within the program text of the `class` or `struct` that contains the declaration of an event, certain events can be used like fields. To be used in this way, an event shall not be abstract or extern, and shall not explicitly include *event-accessor-declaration*s. Such an event can be used in any context that permits a field. The field contains a delegate (§20), which refers to the list of event handlers that have been added to the event. If no event handlers have been added, the field contains null.

[Example: In the following code

```csharp
public delegate void EventHandler(object sender, EventArgs e);

public class Button: Control
{
    public event EventHandler Click;

    protected void OnClick(EventArgs e) {
        EventHandler handler = Click;
        if (handler != null)
            handler(this, e);
    }

    public void Reset() {
        Click = null;
    }
}
```

`Click` is used as a field within the `Button` class. As the example demonstrates, the field can be examined, modified, and used in delegate invocation expressions. The `OnClick` method in the `Button` class “raises” the `Click` event. The notion of raising an event is precisely equivalent to invoking the delegate represented by the event—thus, there are no special language constructs for raising events. Note that the delegate invocation is preceded by a check that ensures the delegate is non-null and that the check is made on a local copy to ensure thread safety.

Outside the declaration of the `Button` class, the `Click` member can only be used on the left-hand side of the `+=` and `–=` operators, as in

```csharp
b.Click += new EventHandler(…);
```

which appends a delegate to the invocation list of the `Click` event, and

```csharp
b.Click –= new EventHandler(…);
```

which removes a delegate from the invocation list of the `Click` event. end example]

When compiling a field-like event, the compiler automatically creates storage to hold the delegate, and creates accessors for the event that add or remove event handlers to the `delegate` field. The addition and removal operations are thread safe, and may (but are not required to) be done while holding the lock (§10.4.4.19) on the containing `object` for an instance event, or the type `object` (§12.7.11.7) for a static event.

> [!NOTE] 
> Thus, an instance event declaration of the form:

```csharp
class X
{
    public event D Ev;
}
```

shall be compiled to something equivalent to:

```csharp
class X
{
    private D __Ev; // field to hold the delegate

    public event D Ev {
        add {
            /* add the delegate in a thread safe way */
        }

        remove {
            /* remove the delegate in a thread safe way */
        }
    }
}
```

Within the class `X`, references to `Ev` on the left-hand side of the `+=` and `–=` operators cause the add and remove accessors to be invoked. All other references to `Ev` are compiled to reference the hidden field `__Ev` instead (§12.7.5). The name “`__Ev`” is arbitrary; the hidden field could have any name or no name at all. 

###  Event accessors

> [!NOTE] 
> Event declarations typically omit *event-accessor-declaration*s, as in the Button example above. For example, they might be included if the storage cost of one field per event is not acceptable. In such cases, a `class` can include *event-accessor-declaration*s and use a private mechanism for storing the list of event handlers. 

The *event-accessor-declarations* of an event specify the executable statements associated with adding and removing event handlers.

The accessor declarations consist of an *add-accessor-declaration* and a *remove-accessor-declaration*. Each accessor declaration consists of the token add or remove followed by a *block*. The *block* associated with an *add-accessor-declaration* specifies the statements to execute when an event handler is added, and the *block* associated with a *remove-accessor-declaration* specifies the statements to execute when an event handler is removed.

Each *add-accessor-declaration* and *remove-accessor-declaration* corresponds to a method with a single value parameter of the event type, and a void return type. The implicit parameter of an `event` accessor is named value. When an event is used in an event assignment, the appropriate `event` accessor is used. Specifically, if the assignment operator is `+=` then the `add` accessor is used, and if the assignment operator is `–=` then the `remove` accessor is used. In either case, the right-hand operand of the assignment operator is used as the argument to the `event` accessor. The block of an *add-accessor-declaration* or a *remove-accessor-declaration* shall conform to the rules for void methods described in §15.6.9. In particular, return statements in such a block are not permitted to specify an expression.

Since an `event` accessor implicitly has a parameter named value, it is a compile-time error for a local variable or constant declared in an `event` accessor to have that name.

[Example: In the following code

```csharp
class Control: Component
{
    // Unique keys for events
    static readonly `object` mouseDownEventKey = new object();
    static readonly `object` mouseUpEventKey = new object();

    // Return event handler associated with key
    protected Delegate GetEventHandler(object key) {…}

    // Add event handler associated with key
    protected void AddEventHandler(object key, Delegate handler) {…}

    // Remove event handler associated with key
    protected void RemoveEventHandler(object key, Delegate handler) {…}

    // MouseDown event
    public event MouseEventHandler MouseDown {
        add { AddEventHandler(mouseDownEventKey, value); }
        remove { RemoveEventHandler(mouseDownEventKey, value); }
    }

    // MouseUp event
    public event MouseEventHandler MouseUp {
        add { AddEventHandler(mouseUpEventKey, value); }
        remove { RemoveEventHandler(mouseUpEventKey, value); }
    }

    // Invoke the MouseUp event\
    protected void OnMouseUp(MouseEventArgs args) {
        MouseEventHandler handler;
        handler = (MouseEventHandler)GetEventHandler(mouseUpEventKey);
        if (handler != null)
            handler(this, args);
    }
}
```

the `Control` class implements an internal storage mechanism for events. The `AddEventHandler` method associates a delegate value with a key, the `GetEventHandler` method returns the delegate currently associated with a key, and the `RemoveEventHandler` method removes a delegate as an event handler for the specified event. Presumably, the underlying storage mechanism is designed such that there is no cost for associating a null delegate value with a key, and thus unhandled events consume no storage. end example]

### Static and instance events

When an event declaration includes a `static` modifier, the event is said to be a ***static event***. When no `static` modifier is present, the event is said to be an ***instance event***.

A `static` event is not associated with a `specific` instance, and it is a compile-time error to refer to this in the accessors of a `static` event.

An `instance` event is associated with a given instance of a class, and `this` instance can be accessed as `this` (§12.7.8) in the accessors of that event.

The differences between `static` and i`nstance` members are discussed further in §15.3.8.

### Virtual, sealed, override, and abstract accessors

A `virtual` event declaration specifies that the accessors of that event are virtual. The `virtual` modifier applies to both accessors of an event.

An `abstract` event declaration specifies that the accessors of the event are virtual, but does not provide an actual implementation of the accessors. Instead, non-abstract derived classes are required to provide their own implementation for the accessors by overriding the event. Because an accessor for an abstract event declaration provides no actual implementation, it shall not provide *event-accessor-declaration*s.

An event declaration that includes both the `abstract` and `override` modifiers specifies that the event is abstract and overrides a base event. The accessors of such an event are also abstract.

Abstract event declarations are only permitted in `abstract` classes (§15.2.2.2).

The accessors of an inherited virtual event can be overridden in a derived `class` by including an event declaration that specifies an `override` modifier. This is known as an ***overriding event declaration***. An overriding event declaration does not declare a new event. Instead, it simply specializes the implementations of the accessors of an existing `virtual` event.

An overriding event declaration shall specify the exact same accessibility modifiers and name as the overridden event, there shall be an identity conversion between the type of the overriding and the overridden event, and both the `add` and `remove` accessors shall be specified within the declaration.

An overriding event declaration can include the `sealed` modifier. Use of `this` modifier prevents a derived `class` from further overriding the event. The accessors of a `sealed` event are also sealed.

It is a compile-time error for an overriding event declaration to include a `new` modifier.

Except for differences in declaration and invocation syntax, `virtual`, `sealed`, `override`, and `abstract` accessors behave exactly like `virtual`, `sealed`, `override` and `abstract` methods. Specifically, the rules described in §15.6.4, §15.6.5, §15.6.6, and §15.6.7 apply as if accessors were methods of a corresponding form. Each accessor corresponds to a method with a single value parameter of the event type, a void return type, and the same modifiers as the containing event.

## Indexers

An ***indexer*** is a member that enables an `object` to be indexed in the same way as an array. Indexers are declared using *indexer-declaration*s:

[](#Grammar_indexer_declaration)
```ANTLR
indexer-declaration:
  attributes~opt~ indexer-modifiers~opt~ indexer-declarator { accessor-declarations }
```

[](#Grammar_indexer_modifiers)
```ANTLR
indexer-modifiers:
  indexer-modifier
  indexer-modifiers indexer-modifier
```

[](#Grammar_indexer_modifier)
```ANTLR
indexer-modifier:
  new
  public
  protected
  internal
  private
  virtual
  sealed
  override
  abstract
  extern
```

[](#Grammar_indexer_declarator)
```ANTLR
indexer-declarator:
  type this [ formal-parameter-list ]
  type interface-type . this [ formal-parameter-list ]
```

An *indexer-declaration* may include a set of *attributes* (§22) and a valid combination of the four access modifiers (§15.3.6), the new (§15.3.5), virtual (§15.6.4), override (§15.6.5), sealed (§15.6.6), abstract (§15.6.7), and extern (§15.6.8) modifiers.

Indexer declarations are subject to the same rules as method declarations (§15.6) with regard to valid combinations of modifiers, with the one exception being that the `static` modifier is not permitted on an indexer declaration.

The modifiers `virtual`, `override`, and `abstract` are mutually exclusive except in one case. The `abstract` and `override` modifiers may be used together so that an `abstract` indexer can override a virtual one.

The *type* of an indexer declaration specifies the element type of the indexer introduced by the declaration. Unless the indexer is an explicit interface member implementation, the *type* is followed by the keyword `this`. For an explicit interface member implementation, the *type* is followed by an *interface-type*, a “.”, and the keyword `this`. Unlike other members, indexers do not have user-defined names.

The *formal-parameter-list* specifies the parameters of the indexer. The formal parameter list of an indexer corresponds to that of a method (§15.6.2), except that at least one parameter shall be specified, and that the `this`, `ref`, and `out` parameter modifiers are not permitted.

The *type* of an indexer and each of the types referenced in the *formal-parameter-list* shall be at least as accessible as the indexer itself (§8.5.5).

The *accessor-declarations* (§15.7.3), which shall be enclosed in “`{`” and “`}`” tokens, declare the accessors of the indexer. The accessors specify the executable statements associated with reading and writing indexer elements.

Even though the syntax for accessing an indexer element is the same as that for an array element, an indexer element is not classified as a variable. Thus, it is not possible to pass an indexer element as a `ref` or `out` argument.

The *formal-parameter-list* of an indexer defines the signature (§8.6) of the indexer. Specifically, the signature of an indexer consists of the number and types of its formal parameters. The element type and names of the formal parameters are not part of an indexer’s signature.

The signature of an indexer shall differ from the signatures of all other indexers declared in the same class.

Indexers and properties are very similar in concept, but differ in the following ways:

-  A property is identified by its name, whereas an indexer is identified by its signature.
-  A property is accessed through a *simple-name* (§12.7.3) or a *member-access* (§12.7.5), whereas an indexer element is accessed through an *element-access* (§12.7.7.3).
-  A property can be a `static` member, whereas an indexer is always an instance member.
-  A `get` accessor of a property corresponds to a method with no parameters, whereas a `get` accessor of an indexer corresponds to a method with the same formal parameter list as the indexer.
-  A `set` accessor of a property corresponds to a method with a single parameter named value, whereas a `set` accessor of an indexer corresponds to a method with the same formal parameter list as the indexer, plus an additional parameter named value.
-  It is a compile-time error for an `indexer` accessor to declare a local variable or local constant with the same name as an `indexer` parameter.
-  In an overriding property declaration, the inherited property is accessed using the syntax `base.P`, where `P` is the property name. In an overriding indexer declaration, the inherited indexer is accessed using the syntax `base[E]`, where `E` is a comma-separated list of expressions.

Aside from these differences, all rules defined in §15.7.3 and §15.7.6 apply to `indexer` accessors as well as to `property` accessors.

When an indexer declaration includes an `extern` modifier, the indexer is said to be an ***external indexer***. Because an external indexer declaration provides no actual implementation, each of its *accessor-declarations* consists of a semicolon.

[Example: The example below declares a BitArray `class` that implements an indexer for accessing the individual bits in the bit array.

```csharp
using System;

class BitArray
{
    int[] bits;
    int length;

    public BitArray(int length) {
        if (length < 0) throw new ArgumentException();
        bits = new int[((length - 1) >> 5) + 1];
        this.length = length;
    }

    public int Length {
        get { return length; }
    }

    public bool this[int index] {
        get {
            if (index < 0 || index >= length) {
                throw new IndexOutOfRangeException();
            }
            return (bits[index >> 5] & 1 << index) != 0;
        }
        set {
            if (index < 0 || index >= length) {
                throw new IndexOutOfRangeException();
            }
            if (value) {
                bits[index >> 5] |= 1 << index;
            }
            else {
                bits[index >> 5] &= \~(1 << index);
            }
        }
    }
}
```

An instance of the `BitArray` class consumes substantially less memory than a corresponding `bool[]` (since each value of the former occupies only one bit instead of the latter’s one `byte`), but it permits the same operations as a `bool[]`.

The following `CountPrimes` class uses a BitArray and the classical “sieve” algorithm to compute the number of primes between2 and a given maximum:

```csharp
class CountPrimes
{
    static int Count(int max) {
        BitArray flags = new BitArray(max + 1);
        int count = 0;
        for (int i = 2; i <= max; i++) {
            if (!flags[i]) {
                for (int j = i * 2; j <= max; j += i) flags[j] = true;
                count++;
            }
        }
        return count;
    }

    static void Main(string[] args) {
        int max = int.Parse(args[0]);
        int count = Count(max);
        Console.WriteLine(
        "Found {0} primes between 2 and {1}", count, max);
    }
}
```

Note that the syntax for accessing elements of the `BitArray` is precisely the same as for a `bool[]`. end example]

[Example: The following example shows a 26×10 grid `class` that has an indexer with two parameters. The first parameter is required to be an upper- or lowercase letter in the range A–Z, and the second is required to be an integer in the range 0–9.

```csharp
using System;

class Grid
{
    const int NumRows = 26;
    const int NumCols = 10;
    int[,] cells = new int[NumRows, NumCols];
    
    public int this[char row, int col]
    {
        get {
            row = Char.ToUpper(row);
            if (row < 'A' || row > 'Z') {
                throw new ArgumentOutOfRangeException("row");
            }
            if (col < 0 || col >= NumCols) {
                throw new ArgumentOutOfRangeException ("col");
            }
            return cells[row - 'A', col];
        }
    
        set {
            row = Char.ToUpper(row);
            if (row < 'A' || row > 'Z') {
                throw new ArgumentOutOfRangeException ("row");
            }
            if (col < 0 || col >= NumCols) {
                throw new ArgumentOutOfRangeException ("col");
            }
            cells[row - 'A', col] = value;
        }
    }
}
```

end example]

## Operators

### General

An ***operator*** is a member that defines the meaning of an expression operator that can be applied to instances of the class. Operators are declared using *operator-declaration*s:

[](#Grammar_operator_declaration)
```ANTLR
operator-declaration:
  attributes~opt~ operator-modifiers operator-declarator operator-body
```

[](#Grammar_operator_modifiers)
```ANTLR
operator-modifiers:
  operator-modifier
  operator-modifiers operator-modifier
```

[](#Grammar_operator_modifier)
```ANTLR
operator-modifier:
  public
  static
  extern
```

[](#Grammar_operator_declarator)
```ANTLR
operator-declarator:
  unary-operator-declarator
  binary-operator-declarator
  conversion-operator-declarator
```

[](#Grammar_unary_operator_declarator)
```ANTLR
unary-operator-declarator:
  type operator overloadable-unary-operator ( fixed-parameter )
```

[](#Grammar_overloadable_unary_operator)
```ANTLR
overloadable-unary-operator: one of
  *+ - ! \~ ++ -- true false
```

[](#Grammar_binary_operator_declarator)
```ANTLR
binary-operator-declarator:
  type operator overloadable-binary-operator ( fixed-parameter , fixed-parameter )
```

[](#Grammar_overloadable_binary_operator)
```ANTLR
overloadable-binary-operator: one of
  + - \ / % & | \^ << right-shift
  == != > < >= <=
```

[](#Grammar_conversion_operator_declarator)
```ANTLR
conversion-operator-declarator:
  implicit operator type ( fixed-parameter )
  explicit operator type ( fixed-parameter )
```

[](#Grammar_operator_body)
```ANTLR
operator-body:
  block
  ;
```

There are three categories of overloadable operators: Unary operators (§15.10.2), binary operators (§15.10.3), and conversion operators (§15.10.4).

When an operator declaration includes an `extern` modifier, the operator is said to be an ***external operator***. Because an external operator provides no actual implementation, its *operator-body* consists of a semi-colon. For all other operators, the *operator-body* consists of a *block*, which specifies the statements to execute when the operator is invoked. The *block* of an operator shall conform to the rules for value-returning methods described in §15.6.11.

The following rules apply to all operator declarations:

-  An operator declaration shall include both a `public` and a `static` modifier.
-  The parameter(s) of an operator shall have no modifiers.
-  The signature of an operator (§15.10.2, §15.10.3, §15.10.4) shall differ from the signatures of all other operators declared in the same class.
-  All types referenced in an operator declaration shall be at least as accessible as the operator itself (§8.5.5).
-  It is an error for the same modifier to appear multiple times in an operator declaration.

Each operator category imposes additional restrictions, as described in the following subclauses.

Like other members, operators declared in a base class are inherited by derived classes. Because operator declarations always require the `class` or `struct` in which the operator is declared to participate in the signature of the operator, it is not possible for an operator declared in a derived `class` to hide an operator declared in a base class. Thus, the `new` modifier is never required, and therefore never permitted, in an operator declaration.

Additional information on unary and binary operators can be found in §12.4.

Additional information on conversion operators can be found in §11.5.

### Unary operators

The following rules apply to unary operator declarations, where `T` denotes the instance type of the `class` or `struct` that contains the operator declaration:

-  A unary `+`, `-`, `!`, or `~` operator shall take a single parameter of type `T` or `T?` and can return any type.
-  A unary `++` or `--` operator shall take a single parameter of type `T` or `T?` and shall return that same type or a type derived from it.
-  A unary true or false operator shall take a single parameter of type `T` or `T?` and shall return type `bool`.

The signature of a unary operator consists of the operator token (`+`, `-`, `!`, `~`, `++`, `--`, `true`, or `false`) and the type of the single formal parameter. The return type is not part of a unary operator’s signature, nor is the name of the formal parameter.

The `true` and `false` unary operators require pair-wise declaration. A compile-time error occurs if a `class` declares one of these operators without also declaring the other. The `true` and `false` operators are described further in §12.21.

[Example: The following example shows an implementation and subsequent usage of operator++ for an integer vector class:

```csharp
public class IntVector
{
    public IntVector(int length) {…}
    
    public int Length { … } // read-only property
    public int this[int index] { … } // read-write indexer
    
    public static IntVector operator++(IntVector iv) {
        IntVector temp = new IntVector(iv.Length);
        for (int i = 0; i < iv.Length; i++)
        temp[i] = iv[i] + 1;
        return temp;
    }
}

class Test
{
    static void Main() {
        IntVector iv1 = new IntVector(4); // vector of 4 x 0
        IntVector iv2;
        
        iv2 = iv1++; // iv2 contains 4 x 0, iv1 contains 4 x 1
        iv2 = ++iv1; // iv2 contains 4 x 2, iv1 contains 4 x 2
    }
}
```

Note how the operator method returns the value produced by adding 1 to the operand, just like the postfix increment and decrement operators (§12.7.10), and the prefix increment and decrement operators (§12.8.6). Unlike in C++, this method should not modify the value of its operand directly as this would violate the standard semantics of the postfix increment operator (§12.8.6). end example]

### Binary operators

The following rules apply to binary operator declarations, where `T` denotes the instance type of the `class` or `struct` that contains the operator declaration:

-  A binary non-shift operator shall take two parameters, at least one of which shall have type `T` or `T?`, and can return any type.
-  A binary `<<` or `>>` operator (§12.10) shall take two parameters, the first of which shall have type `T` or T? and the second of which shall have type `int` or `int?`, and can return any type.
The signature of a binary operator consists of the operator token (`+`, `-`, `*`, `/`, `%`, `&`, `|`, `\^`, `<<`, `>>`, `==`, `!=`, `>`, `<`, `>=`, or `<=`) and the types of the two formal parameters. The return type and the names of the formal parameters are not part of a binary operator’s signature.

Certain binary operators require pair-wise declaration. For every declaration of either operator of a pair, there shall be a matching declaration of the other operator of the pair. Two operator declarations match if identity conversions exist between their return types and their corresponding parameter types. The following operators require pair-wise declaration:

-  operator `==` and operator `!=`
-  operator `>` and operator `<`
-  operator `>=` and operator `<=`

### Conversion operators

A conversion operator declaration introduces a ***user-defined conversion*** (§11.5), which augments the pre-defined implicit and explicit conversions.

A conversion operator declaration that includes the implicit keyword introduces a user-defined implicit conversion. Implicit conversions can occur in a variety of situations, including function member invocations, cast expressions, and assignments. This is described further in §11.2.

A conversion operator declaration that includes the explicit keyword introduces a user-defined explicit conversion. Explicit conversions can occur in cast expressions, and are described further in §11.3.

A conversion operator converts from a source type, indicated by the parameter type of the conversion operator, to a target type, indicated by the return type of the conversion operator.

For a given source type `S` and target type `T`, if `S` or `T` are nullable value types, let `S~0~` and `T~0~` refer to their underlying types; otherwise, `S~0~` and `T~0~` are equal to `S` and `T` respectively. A `class` or `struct` is permitted to declare a conversion from a source type `S` to a target type `T` only if all of the following are true:

-  `S~0~` and `T~0~` are different types.

-  Either `S~0~` or `T~0~` is the instance type of the `class` or `struct` that contains the operator declaration.

-  Neither `S~0~` nor `T~0~` is an *interface-type*.

-  Excluding user-defined conversions, a conversion does not exist from `S` to `T` or from `T` to `S`.

For the purposes of these rules, any type parameters associated with `S` or `T` are considered to be unique types that have no inheritance relationship with other types, and any constraints on those type parameters are ignored.

[Example: In the following:

```csharp
class C<T> {…}

class D<T>: C<T>
{
    public static implicit operator C<int>(D<T> value) {…} // Ok

    public static implicit operator C<string>(D<T> value) {…} // Ok

    public static implicit operator C<T>(D<T> value) {…} // Error
}
```

the first two operator declarations are permitted because `T` and `int` and `string`, respectively are considered unique types with no relationship. However, the third operator is an error because `C<T>` is the base class of `D<T>`. end example]

From the second rule, it follows that a conversion operator shall convert either to or from the `class` or `struct` type in which the operator is declared. [Example: It is possible for a `class` or `struct` type `C` to define a conversion from `C` to `int` and from `int` to `C`, but not from `int` to `bool`. end example]

It is not possible to directly redefine a pre-defined conversion. Thus, conversion operators are not allowed to convert from or to `object` because implicit and explicit conversions already exist between `object` and all other types. Likewise, neither the source nor the target types of a conversion can be a base type of the other, since a conversion would then already exist. However, it *is* possible to declare operators on generic types that, for particular type arguments, specify conversions that already exist as pre-defined conversions. [Example:

```csharp
struct Convertible<T>
{
public static implicit operator Convertible<T>(T value) {…}

public static explicit operator T(Convertible<T> value) {…}
}
```

when type `object` is specified as a type argument for `T`, the second operator declares a conversion that already exists (an implicit, and therefore also an explicit, conversion exists from any type to type object). end example]

In cases where a pre-defined conversion exists between two types, any user-defined conversions between those types are ignored. Specifically:

-  If a pre-defined implicit conversion (§11.2) exists from type `S` to type `T`, all user-defined conversions (implicit or explicit) from `S` to `T` are ignored.
-  If a pre-defined explicit conversion (§11.3) exists from type `S` to type `T`, any user-defined explicit conversions from `S` to `T` are ignored. Furthermore:
    -  If either `S` or `T` is an interface type, user-defined implicit conversions from `S` to `T` are ignored.
    -  Otherwise, user-defined implicit conversions from `S` to `T` are still considered.

For all types but object, the operators declared by the `Convertible<T>` type above do not conflict with pre-defined conversions. [Example:

```csharp
    void F(int i, Convertible<int> n) {
    i = n; // Error
    i = (int)n; // User-defined explicit conversion
    n = i; // User-defined implicit conversion
    n = (Convertible<int>)i; // User-defined implicit conversion
}
```

However, for type object, pre-defined conversions hide the user-defined conversions in all cases but one:

```csharp
void F(object o, Convertible<object> n) {
    o = n; // Pre-defined boxing conversion
    o = (object)n; // Pre-defined boxing conversion
    n = o; // User-defined implicit conversion
    n = (Convertible<object>)o; // Pre-defined unboxing conversion
}
```

end example]

User-defined conversions are not allowed to convert from or to *interface-type*s. In particular, this restriction ensures that no user-defined transformations occur when converting to an *interface-type*, and that a conversion to an *interface-type* succeeds only if the `object` being converted actually implements the specified *interface-type*.

The signature of a conversion operator consists of the source type and the target type. (This is the only form of member for which the return type participates in the signature.) The implicit or explicit classification of a conversion operator is not part of the operator’s signature. Thus, a `class` or `struct` cannot declare both an implicit and an explicit conversion operator with the same source and target types.

> [!NOTE] 
> In general, user-defined implicit conversions should be designed to never throw exceptions and never lose information. If a user-defined conversion can give rise to exceptions (for example, because the source argument is out of range) or loss of information (such as discarding high-order bits), then that conversion should be defined as an explicit conversion. 

[Example: In the following code

```csharp
using System;

public struct Digit
{
    byte value;
    
    public Digit(byte value) {
        if (value < 0 || value > 9) throw new ArgumentException();
        this.value = value;
    }
    
    public static implicit operator byte(Digit d) {
        return d.value;
    }
    
    public static explicit operator Digit(byte b) {
        return new Digit(b);
    }
}
```

the conversion from `Digit` to `byte` is implicit because it never throws exceptions or loses information, but the conversion from `byte` to `Digit` is explicit since `Digit` can only represent a subset of the possible values of a `byte`. end example]

## Instance constructors

### General

An ***instance constructor*** is a member that implements the actions required to initialize an instance of a class. Instance constructors are declared using *constructor-declaration*s:

[](#Grammar_constructor_declaration)
```ANTLR
constructor-declaration:
  attributes~opt~ constructor-modifiers~opt~ constructor-declarator constructor-body
```

[](#Grammar_constructor_modifiers)
```ANTLR
constructor-modifiers:
  constructor-modifier
  constructor-modifiers constructor-modifier
```

[](#Grammar_constructor_modifier)
```ANTLR
constructor-modifier:
  public
  protected
  internal
  private
  extern
```

[](#Grammar_constructor_declarator)
```ANTLR
constructor-declarator:
  identifier ( formal-parameter-list~opt~ ) constructor-initializer~opt~
```

[](#Grammar_constructor_initializer)
```ANTLR
constructor-initializer:
  : base ( argument-list~opt~ )
  : this ( argument-list~opt~ )
```

[](#Grammar_constructor_body)
```ANTLR
constructor-body:
  block
  ;
```

A *constructor-declaration* may include a set of *attributes* (§22), a valid combination of the four access modifiers (§15.3.6), and an `extern` (§15.6.8) modifier. A constructor declaration is not permitted to include the same modifier multiple times.

The *identifier* of a *constructor-declarator* shall name the `class` in which the instance constructor is declared. If any other name is specified, a compile-time error occurs.

The optional *formal-parameter-list* of an instance constructor is subject to the same rules as the *formal-parameter-list* of a method (§15.6). As the `this` modifier for parameters only applies to extension methods (§15.6.10), no parameter in a constructor's *formal-parameter-list* shall contain the `this` modifier. The formal parameter list defines the signature (§8.6) of an instance constructor and governs the process whereby overload resolution (§12.6.4) selects a particular instance constructor in an invocation.

Each of the types referenced in the *formal-parameter-list* of an instance constructor shall be at least as accessible as the constructor itself (§8.5.5).

The optional *constructor-initializer* specifies another instance constructor to invoke before executing the statements given in the *constructor-body* of this instance constructor. This is described further in §15.11.2.

When a constructor declaration includes an `extern` modifier, the constructor is said to be an ***external constructor***. Because an external constructor declaration provides no actual implementation, its *constructor-body* consists of a semicolon. For all other constructors, the *constructor-body* consists of a *block*, which specifies the statements to initialize a new instance of the class. This corresponds exactly to the *block* of an instance method with a void return type (§15.6.11).

Instance constructors are not inherited. Thus, a `class` has no instance constructors other than those actually declared in the `class`, with the exception that if a `class` contains no instance constructor declarations, a default instance constructor is automatically provided (§15.11.5).

Instance constructors are invoked by *object-creation-expression*s (§12.7.11.2) and through *constructor-initializer*s.

### Constructor initializers

All instance constructors (except those for `class` object) implicitly include an invocation of another instance constructor immediately before the *constructor-body*. The constructor to implicitly invoke is determined by the *constructor-initializer*:

-  An instance constructor initializer of the form base(*argument-list~opt~*) causes an instance constructor from the direct base class to be invoked. That constructor is selected using *argument-list* and the overload resolution rules of §12.6.4. The set of candidate instance constructors consists of all the accessible instance constructors of the direct base class. If this set is empty, or if a single best instance constructor cannot be identified, a compile-time error occurs.
-  An instance constructor initializer of the form this(*argument-list~opt~*) invokes another instance constructor from the same class. The constructor is selected using *argument-list* and the overload resolution rules of §12.6.4. The set of candidate instance constructors consists of all instance constructors declared in the `class` itself. If the resulting set of applicable instance constructors is empty, or if a single best instance constructor cannot be identified, a compile-time error occurs. If an instance constructor declaration invokes itself through a chain of one or more constructor initializers, a compile-time error occurs.

If an instance constructor has no constructor initializer, a constructor initializer of the form base()is implicitly provided. 

> [!NOTE] 
> Thus, an instance constructor declaration of the form

```csharp
C(…) {…}
```

is exactly equivalent to

```csharp
C(…): base() {…}
```

The scope of the parameters given by the *formal-parameter-list* of an instance constructor declaration includes the constructor initializer of that declaration. Thus, a constructor initializer is permitted to access the parameters of the constructor. [Example:

```csharp
class A
{
    public A(int x, int y) {}
}

class B: A
{
    public B(int x, int y): base(x + y, x - y) {}
}
```

end example]

An instance constructor initializer cannot access the instance being created. Therefore it is a compile-time error to reference this in an argument expression of the constructor initializer, as it is a compile-time error for an argument expression to reference any instance member through a *simple-name*.

### Instance variable initializers

When an `instance` constructor has no constructor initializer, or it has a constructor initializer of the form base(…), that constructor implicitly performs the initializations specified by the *variable-initializer*s of the `instance` fields declared in its class. This corresponds to a sequence of assignments that are executed immediately upon entry to the constructor and before the implicit invocation of the direct base class constructor. The variable initializers are executed in the textual order in which they appear in the `class` declaration (§15.5.6).

### Constructor execution

Variable initializers are transformed into assignment statements, and these assignment statements are executed *before* the invocation of the base class instance constructor. This ordering ensures that all `instance` fields are initialized by their variable initializers before *any* statements that have access to that instance are executed. [Example: Given the following:

```csharp
using System;

class A
{
    public A() {
        PrintFields();
    }
    
    public virtual void PrintFields() {}
}
    
class B: A
{
    int x = 1;
    int y;
    
    public B() {
        y = -1;
    }
    
    public override void PrintFields() {
        Console.WriteLine("x = {0}, y = {1}", x, y);
    }
}
```

when new `B()` is used to create an instance of `B`, the following output is produced:

```csharp
x = 1, y = 0
```

The value of `x` is 1 because the variable initializer is executed before the base class instance constructor is invoked. However, the value of `y` is 0 (the default value of an int) because the assignment to `y` is not executed until after the base class constructor returns.

It is useful to think of instance variable initializers and constructor initializers as statements that are automatically inserted before the *constructor-body*. The example

```csharp
using System;
using System.Collections;

class A
{
    int x = 1, y = -1, count;
    
    public A() {
        count = 0;
    }
    
    public A(int n) {
        count = n;
    }
}
    
class B: A
{
    double sqrt2 = Math.Sqrt(2.0);
    ArrayList items = new ArrayList(100);
    int max;
    
    public B(): this(100) {
        items.Add("default");
    }
    
    public B(int n): base(n – 1) {
        max = n;
    }
}
```

contains several variable initializers; it also contains constructor initializers of both forms (`base` and `this`). The example corresponds to the code shown below, where each comment indicates an automatically inserted statement (the syntax used for the automatically inserted constructor invocations isn’t valid, but merely serves to illustrate the mechanism).

```csharp
using System.Collections;

class A
{
    int x, y, count;
    
    public A() {
        x = 1; // Variable initializer
        y = -1; // Variable initializer
        object(); // Invoke object() constructor
        count = 0;
    }
    
    public A(int n) {
        x = 1; // Variable initializer
        y = -1; // Variable initializer
        object(); // Invoke object() constructor
        count = n;
    }
}
    
class B: A
    {
    double sqrt2;
    ArrayList items;
    int max;
    
    public B(): this(100) {
        B(100); // Invoke B(int) constructor
        items.Add("default");
    }
    
    public B(int n): base(n – 1) {
        sqrt2 = Math.Sqrt(2.0); // Variable initializer
        items = new ArrayList(100); // Variable initializer
        A(n – 1); // Invoke A(int) constructor
        max = n;
    }
}
```

end example]

### Default constructors

If a `class` contains no instance constructor declarations, a default instance constructor is automatically provided. That default constructor simply invokes a constructor of the direct base class, as if it had a constructor initializer of the form `base()`. If the `class` is abstract then the declared accessibility for the default constructor is `protected`. Otherwise, the declared accessibility for the default constructor is `public`. 

> [!NOTE] 
> Thus, the default constructor is always of the form

```csharp
protected C(): base() {}
```

or

```csharp
public C(): base() {}
```

where `C` is the name of the class. 

If overload resolution is unable to determine a unique best candidate for the base-class constructor initializer then a compile-time error occurs.

[Example: In the following code

```csharp
class Message
{
    object sender;
    string text;
}
```

a default constructor is provided because the `class` contains no instance constructor declarations. Thus, the example is precisely equivalent to

```csharp
class Message
{
    object sender;
    string text;
    
    public Message(): base() {}
}
```

end example]

## Static constructors

A ***static constructor*** is a member that implements the actions required to initialize a closed class. Static constructors are declared using *static-constructor-declaration*s:

[](#Grammar_static_constructor_declaration)
```ANTLR
static-constructor-declaration:
  attributes~opt~ static-constructor-modifiers identifier ( ) static-constructor-body
```

[](#Grammar_static_constructor_modifiers)
```ANTLR
static-constructor-modifiers:
  extern~opt~ static
  static extern~opt~
```

[](#Grammar_static_constructor_body)
```ANTLR
static-constructor-body:
  block
  ;
```

A *static-constructor-declaration* may include a set of *attributes* (§22) and an `extern` modifier (§15.6.8).

The *identifier* of a *static-constructor-declaration* shall name the `class` in which the static constructor is declared. If any other name is specified, a compile-time error occurs.

When a static constructor declaration includes an `extern` modifier, the static constructor is said to be an ***external static constructor***. Because an external static constructor declaration provides no actual implementation, its *static-constructor-body* consists of a semicolon. For all other static constructor declarations, the *static-constructor-body* consists of a *block*, which specifies the statements to execute in order to initialize the class. This corresponds exactly to the *method-body* of a static method with a void return type (§15.6.11).

Static constructors are not inherited, and cannot be called directly.

The static constructor for a closed `class` executes at most once in a given application domain. The execution of a `static` constructor is triggered by the first of the following events to occur within an application domain:

-  An instance of the `class` is created.
-  Any of the static members of the `class` are referenced.

If a `class` contains the Main method (§8.1) in which execution begins, the `static` constructor for that `class` executes before the Main method is called.

To initialize a new closed `class` type, first a new set of `static` fields (§15.5.2) for that particular closed type is created. Each of the `static` fields is initialized to its default value (§15.5.5). Next, the `static` field initializers (§15.5.6.2) are executed for those `static` fields. Finally, the `static` constructor is executed.[Example: The example

```csharp
using System;

class Test
{
    static void Main() {
        A.F();
        B.F();
    }
}

class A
{
    static A() {
        Console.WriteLine("Init A");
    }
    public static void F() {
        Console.WriteLine("A.F");
    }
}

class B
{
    static B() {
        Console.WriteLine("Init B");
    }
    public static void F() {
        Console.WriteLine("B.F");
    }
}
```

must produce the output:

```csharp
Init A
A.F
Init B
B.F
```

because the execution of `A`'s static constructor is triggered by the call to `A.F`, and the execution of `B`'s static constructor is triggered by the call to `B.F`. end example]

It is possible to construct circular dependencies that allow `static` fields with variable initializers to be observed in their default value state.

[Example: The example

```csharp
using System;

class A
{
    public static int x;
    static A() {
        X = B.Y + 1;
    }
}

class B
{
    public static int y = A.X + 1;
    static B() {}
        static void Main() {
        Console.WriteLine("X = {0}, Y = {1}", A.X, B.Y);
    }
}
```

produces the output

```csharp
X = 1, Y = 2
```

To execute the `Main` method, the system first runs the initializer for `B.Y`, prior to class `B`'s `static` constructor. `Y`'s initializer causes `A`'s `static` constructor to be run because the value of `A.X` is referenced. The `static` constructor of `A` in turn proceeds to compute the value of `X`, and in doing so fetches the default value of `Y`, which is zero. `A.X` is thus initialized to 1. The process of running `A`'s static field initializers and `static` constructor then completes, returning to the calculation of the initial value of `Y`, the result of which becomes 2. end example]

Because the `static` constructor is executed exactly once for each closed constructed `class` type, it is a convenient place to enforce run-time checks on the type parameter that cannot be checked at compile-time via constraints (§15.2.5). [Example: The following type uses a `static` constructor to enforce that the type argument is an `enum`:

```csharp
class Gen<T> where T: struct
{
    static Gen() {
        if (!typeof(T).IsEnum) {
            throw new ArgumentException("T must be an enum");
        }
    }
}
```

end example]

## Finalizers

> [!NOTE] 
> In an earlier version of this standard, what is now referred to as a "finalizer" was called a "destructor". Experience has shown that the term "destructor" caused confusion and often resulted to incorrect expectations, especially to programmers knowing C++. In C++, a destructor is called in a determinate manner, whereas, in C#, a finalizer is not. To get determinate behavior from C#, one should use Dispose. 

A ***finalizer*** is a member that implements the actions required to finalize an instance of a class. A finalizer is declared using a *finalizer-declaration*:

[](#Grammar_destructor_declaration)
```ANTLR
finalizer-declaration:
    attributes~opt~ extern~opt~ \~ identifier ( ) finalizer-body
```

[](#Grammar_destructor_body)
```ANTLR
finalizer-body:
    block
    ;
```

A *finalizer-declaration* may include a set of *attributes* (§22).

The *identifier* of a *finalizer-declarator* shall name the `class` in which the finalizer is declared. If any other name is specified, a compile-time error occurs.

When a finalizer declaration includes an `extern` modifier, the finalizer is said to be an ***external finalizer***. Because an external finalizer declaration provides no actual implementation, its *finalizer-body* consists of a semicolon. For all other finalizers, the *finalizer-body* consists of a *block*, which specifies the statements to execute in order to finalize an instance of the class. A *finalizer-body* corresponds exactly to the *method-body* of an instance method with a void return type (§15.6.11).

Finalizers are not inherited. Thus, a `class` has no finalizers other than the one that may be declared in that class.

> [!NOTE] 
> Since a finalizer is required to have no parameters, it cannot be overloaded, so a `class` can have, at most, one finalizer. 

Finalizers are invoked automatically, and cannot be invoked explicitly. An instance becomes eligible for finalization when it is no longer possible for any code to use that instance. Execution of the finalizer for the instance may occur at any time after the instance becomes eligible for finalization (§8.9). When an instance is finalized, the finalizers in that instance’s inheritance chain are called, in order, from most derived to least derived. A finalizer may be executed on any thread. For further discussion of the rules that govern when and how a finalizer is executed, see §8.9.

[Example: The output of the example

```csharp
using System;

class A
{
    \~A() {
        Console.WriteLine("A's finalizer");
    }
}

class B: A
{
    \~B() {
        Console.WriteLine("B's finalizer");
    }
}

class Test
{
    static void Main() {
        B b = new B();
        b = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
```

is

```csharp
B’s finalizer
A’s finalizer
```

since finalizers in an inheritance chain are called in order, from most derived to least derived. end example]

Finalizers are implemented by overriding the `virtual` method Finalize on System.Object. C# programs are not permitted to override this method or call it (or overrides of it) directly. [Example: For instance, the program

```csharp
class A
{
    override protected void Finalize() {} // error
    public void F() {
        this.Finalize(); // error
    }
}
```

contains two errors. end example]

The compiler behaves as if this method, and overrides of it, do not exist at all. [Example: Thus, this program:

```csharp
class A
{
    void Finalize() {} // permitted
}
```

is valid and the method shown hides System.Object's Finalize method. end example]

For a discussion of the behavior when an exception is thrown from a finalizer, see §21.4.

## Iterators

### General

A function member (§12.6) implemented using an iterator block (§13.3) is called an ***iterator***.

An iterator block may be used as the body of a function member as long as the return type of the corresponding function member is one of the enumerator interfaces (§15.14.2) or one of the enumerable interfaces (§15.14.3). It may occur as a *method-body*, *operator-body* or *accessor-body*, whereas events, instance constructors, static constructors and finalizers may not be implemented as iterators.

When a function member is implemented using an iterator block, it is a compile-time error for the formal parameter list of the function member to specify any ref or out parameters.

### Enumerator interfaces

The ***enumerator interfaces*** are the non-generic interface `System.Collections.IEnumerator` and all instantiations of the generic interface `System.Collections.Generic.IEnumerator<T>`. For the sake of brevity, in this subclause and its siblings these interfaces are referenced as `IEnumerator` and `IEnumerator<T>`, respectively.

### Enumerable interfaces

The ***enumerable interfaces*** are the non-generic interface `System.Collections.IEnumerable` and all instantiations of the generic interface `System.Collections.Generic.IEnumerable<T>`. For the sake of brevity, in this subclause and its siblings these interfaces are referenced as `IEnumerable` and `IEnumerable<T>`, respectively.

### Yield type

An iterator produces a sequence of values, all of the same type. This type is called the ***yield type*** of the iterator.

-  The yield type of an iterator that returns `IEnumerator` or `IEnumerable` is object.
-  The yield type of an iterator that returns `IEnumerator<T>` or `IEnumerable<T>` is `T`.

### Enumerator objects

#### General

When a function member returning an enumerator interface type is implemented using an iterator block, invoking the function member does not immediately execute the code in the iterator block. Instead, an ***enumerator object*** is created and returned. This `object` encapsulates the code specified in the iterator block, and execution of the code in the iterator block occurs when the enumerator object’s `MoveNext` method is invoked. An enumerator `object` has the following characteristics:

-  It implements `IEnumerator` and `IEnumerator<T>`, where `T` is the yield type of the iterator.
-  It implements `System.IDisposable`.
-  It is initialized with a copy of the argument values (if any) and instance value passed to the function member.
-  It has four potential states, ***before***, ***running***, ***suspended***, and ***after***, and is initially in the ***before*** state.

An enumerator `object` is typically an instance of a compiler-generated enumerator `class` that encapsulates the code in the iterator block and implements the enumerator interfaces, but other methods of implementation are possible. If an enumerator `class` is generated by the compiler, that `class` will be nested, directly or indirectly, in the `class` containing the function member, it will have private accessibility, and it will have a name reserved for compiler use (§7.4.3).

An enumerator `object` may implement more interfaces than those specified above.

The following subclauses describe the required behavior of the `MoveNext`, `Current`, and `Dispose` members of the `IEnumerator` and `IEnumerator<T>` interface implementations provided by an enumerator object.

Enumerator objects do not support the `IEnumerator.Reset` method. Invoking this method causes a `System.NotSupportedException` to be thrown.

#### The MoveNext method

The `MoveNext` method of an enumerator `object` encapsulates the code of an iterator block. Invoking the `MoveNext` method executes code in the iterator block and sets the `Current` property of the enumerator `object` as appropriate. The precise action performed by `MoveNext` depends on the state of the enumerator `object` when `MoveNext` is invoked:

-  If the state of the enumerator `object` is ***before***, invoking `MoveNext`:
  -  Changes the state to ***running***.
  -  Initializes the parameters (including this) of the iterator block to the argument values and instance value saved when the enumerator `object` was initialized.
  -  Executes the iterator block from the beginning until execution is interrupted (as described below).
-  If the state of the enumerator `object` is ***running***, the result of invoking `MoveNext` is unspecified.
-  If the state of the enumerator `object` is ***suspended***, invoking MoveNext:
  -  Changes the state to ***running***.
  -  Restores the values of all local variables and parameters (including `this`) to the values saved when execution of the iterator block was last suspended. 

> [!NOTE] 
> The contents of any objects referenced by these variables may have changed since the previous call to `MoveNext`. 

-  Resumes execution of the iterator block immediately following the yield return statement that caused the suspension of execution and continues until execution is interrupted (as described below).
  -  If the state of the enumerator `object` is ***after***, invoking `MoveNext` returns false.

When `MoveNext` executes the iterator block, execution can be interrupted in four ways: By a yield return statement, by a yield break statement, by encountering the end of the iterator block, and by an exception being thrown and propagated out of the iterator block.

-  When a yield return statement is encountered (§10.4.4.20):
  -  The expression given in the statement is evaluated, implicitly converted to the yield type, and assigned to the `Current` property of the enumerator object.
  -  Execution of the iterator body is suspended. The values of all local variables and parameters (including this) are saved, as is the location of this yield return statement. If the yield return statement is within one or more try blocks, the associated finally blocks are *not* executed at this time.
  -  The state of the enumerator `object` is changed to ***suspended***.
  -  The `MoveNext` method returns true to its caller, indicating that the iteration successfully advanced to the next value.
-  When a yield break statement is encountered (§10.4.4.20):
  -  If the yield break statement is within one or more try blocks, the associated finally blocks are executed.
  -  The state of the enumerator `object` is changed to ***after***.
  -  The `MoveNext` method returns false to its caller, indicating that the iteration is complete.
-  When the end of the iterator body is encountered:
  -  The state of the enumerator `object` is changed to ***after***.
  -  The `MoveNext` method returns false to its caller, indicating that the iteration is complete.
-  When an exception is thrown and propagated out of the iterator block:
  -  Appropriate finally blocks in the iterator body will have been executed by the exception propagation.
  -  The state of the enumerator `object` is changed to ***after***.
  -  The exception propagation continues to the caller of the `MoveNext` method.

#### The Current property

An enumerator object’s `Current` property is affected by yield return statements in the iterator block.

When an enumerator `object` is in the ***suspended*** state, the value of `Current` is the value set by the previous call to `MoveNext`. When an enumerator `object` is in the ***before***, ***running***, or ***after*** states, the result of accessing `Current` is unspecified.

For an iterator with a yield type other than object, the result of accessing `Current` through the enumerator object’s `IEnumerable` implementation corresponds to accessing `Current` through the enumerator object’s `IEnumerator<T>` implementation and casting the result to object.

#### The Dispose method

The `Dispose` method is used to clean up the iteration by bringing the enumerator `object` to the ***after*** state.

-  If the state of the enumerator `object` is ***before***, invoking Dispose changes the state to ***after***.
-  If the state of the enumerator `object` is ***running***, the result of invoking `Dispose` is unspecified.
-  If the state of the enumerator `object` is ***suspended***, invoking `Dispose`:
  -  Changes the state to ***running***.
  -  Executes any finally blocks as if the last executed yield return statement were a yield break statement. If this causes an exception to be thrown and propagated out of the iterator body, the state of the enumerator `object` is set to ***after*** and the exception is propagated to the caller of the `Dispose` method.
  -  Changes the state to ***after***.
-  If the state of the enumerator `object` is ***after***, invoking `Dispose` has no affect.

### Enumerable objects

#### General

When a function member returning an enumerable interface type is implemented using an iterator block, invoking the function member does not immediately execute the code in the iterator block. Instead, an ***enumerable object*** is created and returned. The enumerable object’s `GetEnumerator` method returns an enumerator `object` that encapsulates the code specified in the iterator block, and execution of the code in the iterator block occurs when the enumerator object’s `MoveNext` method is invoked. An enumerable `object` has the following characteristics:

-  It implements `IEnumerable` and `IEnumerable<T>`, where `T` is the yield type of the iterator.

-  It is initialized with a copy of the argument values (if any) and instance value passed to the function member.

An enumerable `object` is typically an instance of a compiler-generated enumerable `class` that encapsulates the code in the iterator block and implements the enumerable interfaces, but other methods of implementation are possible. If an enumerable `class` is generated by the compiler, that `class` will be nested, directly or indirectly, in the `class` containing the function member, it will have private accessibility, and it will have a name reserved for compiler use (§7.4.3).

An enumerable `object` may implement more interfaces than those specified above. 
> [!NOTE] 
> For example, an enumerable `object` may also implement `IEnumerator` and `IEnumerator<T>`, enabling it to serve as both an enumerable and an enumerator. Typically, such an implementation would return its own instance (to save allocations) from the first call to `GetEnumerator`. Subsequent invocations of `GetEnumerator`, if any, would return a new `class` instance, typically of the same class, so that calls to different enumerator instances will not affect each other. It cannot return the same instance even if the previous enumerator has already enumerated past the end of the sequence, since all future calls to an exhausted enumerator must throw exceptions. 

#### The GetEnumerator method

An enumerable `object` provides an implementation of the `GetEnumerator` methods of the `IEnumerable` and `IEnumerable<T>` interfaces. The two `GetEnumerator` methods share a common implementation that acquires and returns an available enumerator object. The enumerator `object` is initialized with the argument values and instance value saved when the enumerable `object` was initialized, but otherwise the enumerator `object` functions as described in §15.14.5.

## Async Functions

### General

A method (§15.6) or anonymous function (§12.16) with the `async` modifier is called an ***async function***. In general, the term ***async*** is used to describe any kind of function that has the `async` modifier.

It is a compile-time error for the formal parameter list of an `async` function to specify any ref or out parameters.

The *return-type* of an async method shall be either void or a ***task type***. The task types are `System.Threading.Tasks.Task` and types constructed from `System.Threading.Tasks.Task<T>`. For the sake of brevity, in this chapter these types are referenced as `Task` and `Task<T>`, respectively. An async method returning a task type is said to be ***task-returning***.

The exact definition of the task types is implementation-defined, but from the language’s point of view, a task type is in one of the states *incomplete*, *succeeded* or *faulted*. A *faulted* task records a pertinent exception. A *succeeded* `Task<*T*>` records a result of type `*T*`. Task types are awaitable, and tasks can therefore be the operands of await expressions (§12.8.8).

An `async` function has the ability to suspend evaluation by means of await expressions (§12.8.8) in its body. Evaluation may later be resumed at the point of the suspending await expression by means of a ***resumption delegate***. The resumption delegate is of type System.Action, and when it is invoked, evaluation of the `async` function invocation will resume from the await expression where it left off. The ***current caller*** of an `async` function invocation is the original caller if the function invocation has never been suspended or the most recent caller of the resumption delegate otherwise.

### Evaluation of a task-returning async function

Invocation of a task-returning `async` function causes an instance of the returned task type to be generated. This is called the ***return task*** of the `async` function. The task is initially in an *incomplete* state.

The `async` function body is then evaluated until it is either suspended (by reaching an await expression) or terminates, at which point control is returned to the caller, along with the return task.

When the body of the `async` function terminates, the return task is moved out of the incomplete state:

-  If the function body terminates as the result of reaching a return statement or the end of the body, any result value is recorded in the return task, which is put into a *succeeded* state.
-  If the function body terminates as the result of an uncaught exception (§13.10.6) the exception is recorded in the return task which is put into a *faulted* state.

### Evaluation of a void-returning async function

If the return type of the `async` function is void, evaluation differs from the above in the following way: Because no task is returned, the function instead communicates completion and exceptions to the current thread’s ***synchronization context***. The exact definition of synchronization context is implementation-dependent, but is a representation of “where” the current thread is running. The synchronization context is notified when evaluation of a void-returning `async` function commences, completes successfully, or causes an uncaught exception to be thrown.

This allows the context to keep track of how many void-returning `async` functions are running under it, and to decide how to propagate exceptions coming out of them.
