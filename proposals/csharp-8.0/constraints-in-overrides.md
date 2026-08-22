## Override with constraints

In C# 8.0, we added a feature to permit the specification of certain type parameter constraints in an `override` method declaration. This is a placeholder for its specification.


Rex Jaeschke's contribution to this spec, using section numbering from the draft-v8 Ecma standard

## 14 Classes|14.6 Methods|14.6.1 General

Replace

> A *method_declaration* for an explicit interface member implementation shall not have any *type_parameter_constraints_clause*s. A generic *method_declaration* for an explicit interface member implementation inherits any constraints from the constraints on the interface method. Similarly, a method declaration with the `override` modifier shall not have any *type_parameter_constraints_clause*s and the constraints of the method’s type parameters are inherited from the virtual method being overridden.

with

> In the absence of *type_parameter_constraints_clause*s, a generic *method_declaration* for an explicit interface member implementation inherits any constraints from the constraints on the interface method. Similarly, in the absence of *type_parameter_constraints_clause*s, a method declaration with the `override` modifier inherits any constraints from the virtual method being overridden.

## 14 Classes|14.6 Methods|14.6.5 Override methods

Replace

> The override declaration does not specify any *type_parameter_constraints_clause*s. Instead, the constraints are inherited from the overridden base method.

with

> If *type_parameter_constraints_clauses* is present, each of its *type_parameter_constraints* shall be `class` or `struct`, and for the constraint `class` in the override must correspond to a type parameter in the base method that is known to be a non-nullable reference type. Any type parameter that has the `struct` constraint in the override shall correspond to a type parameter in the base method that is known to be a non-nullable value type. In the absence of *type_parameter_constraints_clauses*, the constraints are inherited from the overridden base method, and for the constraint `class` a parameter type `T?` is interpreted as `System.Nullable<T>`.

And add the following example:

> *Example*: The following demonstrates how the overriding rules work when type parameters are involved:
>
> ```csharp
> #nullable enable
> class A
> {
>     public virtual void Foo<T>(T? value) where T : class { }
>     public virtual void Foo<T>(T? value) where T : struct { }
> }
> class B: A
> {
>     public override void Foo<T>(T? value) where T : class { }
>     public override void Foo<T>(T? value) where T : struct { }
> }
> ```
>
> Without the type parameters in the overriding methods, the compiler won’t know which base method is being overridden. *end example*

## 17 Interfaces|17.6 Interface implementations|17.6.2 Explicit interface member implementations

Replace

> It is a compile-time error for an explicit interface method implementation to include *type_parameter_constraints_clause*s. The constraints for a generic explicit interface method implementation are inherited from the interface method.
  
With

> If *type_parameter_constraints_clauses* is present in an explicit interface method implementation, each of its *type_parameter_constraints* shall be `class` or `struct`, and for the constraint `class` in the implementation must correspond to a type parameter in the interface method that is known to be a non-nullable reference type. Any type parameter that has the `struct` constraint in the implementation must correspond to a type parameter in the interface method that is known to be a non-nullable value type. In the absence of *type_parameter_constraints_clauses*, the constraints for an explicit interface method implementation are inherited from the interface method, and for the constraint `class` a parameter type `T?` is interpreted as `System.Nullable<T>`.

> *Example*: The following demonstrates how the overriding rules work when type parameters are involved:
>
> ```csharp
> #nullable enable
> interface I
> {
>     void Foo<T>(T? value) where T : class;
>     void Foo<T>(T? value) where T : struct;
> }
>
> class C : I
> {
>     void I.Foo<T>(T? value) where T : class { }
>     void I.Foo<T>(T? value) where T : struct { }
> }
> ```
>
> Without the type parameters in the implementing methods, the compiler won’t know which method is being implemented. *end example*

## 17 Interfaces|17.6 Interface implementations|17.6.4 Implementation of generic methods

Strike

> *Note*: When a generic method explicitly implements an interface method no constraints are allowed on the implementing method ([§14.7.1](classes.md#1471-general), [§17.6.2](interfaces.md#1762-explicit-interface-member-implementations)). *end note*
