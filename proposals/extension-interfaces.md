# Extension Interface Implementation

This covers open semantic questions and unresolved problems in extension interface implementation.

## What

The basic concept for extension interfaces is to allow additional interfaces to be implemented on existing types. For instance,
```C#
extension EquatableUri for Uri : IEquatable<Uri> {
    public bool Equals(Uri other) => this == other;
}
```
This extension would “implement” the interface IEquatable<T> for Uri. The C# compiler would, in an appropriate context, “attach” this implementation to a given instance of System.Uri to satisfy an implementation requirement.
This is a very high-level explanation. The exact semantics and requirements of the feature will be detailed below.

## Why
The basic idea and inspiration traces back to Wadler’s paper [1] on a feature called “type classes.” The basic goal of the feature is to allow the description of arbitrary sets of functions (interfaces) and arbitrary implementations of those sets (interface implementations). C# already allows some of those features, but the current design has severe limitations in two areas:
1.	Existing types cannot be made to conform to new abstractions without changing the original type
2.	Interface implementation is tied to type definitions, meaning that different implementations cannot be provided for different substituted generic types, nor can implementations only be valid for certain generic substitutions and not others.

These limitations commonly cause problems for developers, but here are two representative examples:
1.	A developer wants to build a new abstract framework, like a new logging system. There is no universally accepted implementation of logging for arbitrary types, so they want to provide an interface that users implement to customize logging for their types. However, there’s no way to provide reasonable default implementations for built-in types in the core framework. This creates friction both in logging instances of those types directly, and in implementing logging in their own types that contain instances of built-in types.
2.	A developer would like to build a new serialization framework. Serialization of certain types, like collections, is not valid unless the generic type parameters to the collection are also serializable. In other words, if serialization is defined using an interface `ISerialize`, `List<T>` should only be considered as implementing `ISerialize` when `T` implements `ISerialize`. Because users cannot implement interfaces on individual type substitutions, and instead must implement an interface using a type definition, it is impossible to encode the correct serialization contract in .NET.

## Usage

Above we defined the general idea of extension interfaces. We will now elaborate on the exact semantics. We will do so using individual motivating examples and describe the reasoning behind preferring certain semantics.

### Interface Parameter

First, let’s consider one of the simplest possible examples: extending an existing primitive type with a new interface, and passing an instance of this type to a parameter of the interface type.
```
interface IPrettyPrint {
   public string Print();
}
extension IntExt for int : IPrettyPrint {
  public string Print() => this.Tostring();
}
extension stringExt for string : IPrettyPrint {
  public string Print() => this.Tostring();
}
string M1(string s, int i) => M2(s) + M2(i);
string M2(IPrettyPrint p) { ... }
```
The most obvious impact of the extension feature is that the `s` and `i` instances of `string` and `int`, respectively, are considered to be convertible to the `IPrettyPrint` interface in the calls to `M2`, despite neither type originally implementing `IPrettyPrint`. However, there are also some non-obvious implications. If we consider that `M2` may be an existing method, implemented by a third-party with no knowledge of the ‘extensions’ feature, there are a number of assumptions that they may hold that are reasonable and should be preserved if at all possible. In particular,
1.	If the value of `p` was originally a reference type, `p` should be reference-equals to the original value. For instance, a caching strategy employed by `M2`’s author may depend on reference equivalence for performance, or they may have access to the original value through a different method and rely on equivalence for correctness.
2.	If the value of `s` is `null`, `p` should also be `null` inside M2. Moreover, a null check inside `M2` should properly check for the nullability of the original value. If this is not true, the author of `M2` cannot reliably check for null before invoking instance members and the method might cause crashing behavior on valid input.
3.	`isinst` type checks and casts should be equivalent to the same check on the original value, if the original value directly implemented the interface. Many libraries rely on type checks for correctness and performance – it is not acceptable to invalidate these checks.
4.	Reflection operations like the `.GetType()` method should return the same results as if the original value directly implemented the interface. Like casts, many libraries rely on reflection access for correct implementation.
5.	If the original value is a value type, like `int`, a copy operation should be performed. This is consistent with the existing conversion rules for value types, which performs a boxing operation. Boxing produces an observable effect on mutating interface members. Extension interface should not alter that effect.

The above rules state changes to semantics that should not happen due to an extension interface implementation. This appears to be broadly true for the feature. It appears that the only two desired semantic changes are that a conversion should exist an extension is valid, and that the interface implementation in the extension should be invoked through interface dispatch when a direct interface implementation is not otherwise available.

[ ] Open question: Which implementation should be chosen if a direct implementation and an extension implementation are available? Should an error be produced instead?

### Constrained generic parameter
This case is almost identical to _Interface parameter_, but with a generic type parameter as the parameter type instead.
```
string M1(string s, int i) => M2(s) + M2(i);
string M2<T>(T p) where T : IPrettyPrint { ... }
```
Unlike in the Interface parameter example, no conversion is performed. However, in the above example, both `string` and `int` should be considered valid substitutions for the type parameter `T`, even though they would not have originally satisfied the `IPrettyPrint` constraint. Once again, the interface members on `IPrettyPrint` should be invocable on `p` and they should use the implementation provided in the extension implementations.
The main differences in this example and Interface parameter are around boxing. While a value type would have been copied and boxed with an `IPrettyPrint` parameter type, a generic substitution would normally not box the parameter during the invocation of `M2` or during a call to `PrettyPrint` if the input argument directly implemented `IPrettyPrint`. The same should be true of an extension implementation, and side-effects of `M2` and `PrettyPrint` should be propagated back to the `i` parameter in the call `M2(i)`.

### Generic types with interface arguments
In this case we will consider a more complex conversion, where we will attempt to convert between generic instantiations with an extension implementation. The type definitions from Interface parameter remain the same. The methods M1 and M2 are adjusted as follows:
```
string M1(List<string> listS, List<int> listI) => M2(listS, listI);
string M2(List<IPrettyPrint> list)  { ... }
```
Even with the extension implemention we would not expect the above to succeed. This is because this conversion would be illegal in C# even if `string` and `int` implemented the interface directly.

### Generic types with constrained generic arguments
This is a variant of Generic types with interface arguments but with constrained generic type parameters as arguments instead of interfaces.
```
string M1(List<string> listS, List<int> listI) => M2(listS, listI);
string M2<T>(List<T> list)  where T : IPrettyPrint { ... }
```
In both cases, we would expect the `M2` invocation to succeed here. Once again, no language conversion would be performed in this case. The type `string` and `int` would be substituted for `T` and, due to the extension implementation, these substitutions would be considered valid. The arguments would then be considered of type `List<string>` and `List<int>`, and the parameters would be `List<string>` and `List<int>`, respectively.
This is an important scenario for uses like the `Contains` method. The `Contains` method for `List<T>` is naturally implemented
```
bool Contains<T>(List<T> list, T value) where T : IEquatable<T> {
  foreach (var item in list) {
    if (item.Equals(value) {
       return true;
    }
  }
}
```
If `IEquatable<T>` is implemented through an extension, the above `Contains` method should be callable without modification. This is a core part of the value proposition of extension implementation.
Variant conversions
This scenario is like Generic types with interface arguments, but with interface variant conversions. 
```
string M1(List<string> listS, List<int> listI) => M2(listS, listI);
string M2(IEnumerable<IPrettyPrint> list)  { ... }
```
Unlike Generic types with interface arguments, this would be expected to succeed for string only, not for int, as string would be valid if the interface implementation were provided directly, instead of on an extension. The conversion would not be valid for int even with direct implementation, and therfore would also not be valid with an extension implementation.

### Composite extensions
Consider the scenario where a generic parameter is constrained to multiple interfaces which are implemented by extension. For instance,
```
interface Iface1 {}
interface Iface2 {}
extension IntExt1 for int : Iface1 {}
extension IntExt2 for int : Iface2 {}
void M1(int i) => M2(i);
void M2<T>(T t) where T : Iface1, IFace2 {}
```
This should be a valid set of extensions, equivalent to if only one interface were provided or if the interfaces were implemented directly on `int`. Notably, the definitions `IntExt1` and IntExt2` are syntactically separate and would likely be separate. In other languages with type classes it is common to separate extensions for separate interfaces.

### Ambiguous implementations
When extensions can be independently provided for existing types and interfaces, it is possible to arrive at overlapping or duplicate implementations for the same interface for the same type. For example,
```
extension IntExt1 for int : IPrettyPrint { … }
extension IntExt2 for int : IPrettyPrint { … }
string M1(int i) => M2(i);
string M2(IPrettyPrint p) { … }
```
This represents a problem for both the compiler and the runtime.

[ ] Open question: What should be the behavior when extensions are ambiguous? There are a number of options:
 - It could be illegal to define duplicate instances (multiple extensions for the exact same type and exact same interface). This would only work for extensions defined in the same assembly. In addition, what about “overlapping” but not duplicate instances?
 - Extensions have names. In some cases, the extension could be disambiguated via name. However, this does not work for Composite extensions as detailed previously as there is no single extension which conforms to the stated constraints.
 - If extension disambiguation is by treating extensions as types, it runs into the semantic violations detailed in Interface parameter. Extensions are broadly not compatible with conventional notions of types in C#. Treating them as types risks large scale confusion.
 - It could be illegal to define duplicate or overlapping instances via an “orphan rule.” The rule defined by Rust is that either the type being extended or the interface being implemented must be defined in the same compilation unit as the extension. This would allow the compiler to check for overlapping at extension definition time.

### Incompatible implementations
Consider a Dictionary implementation which depended on an `IHashable` interface instead of `object.GetHashCode()`. Then, consider the usage of such a dictionary from two different assemblies, each of which define their own extension implementations of `IHashable` for a given type.
```
interface IHashable {
    int GetHashCode();
}
class Dictionary<TKey, TValue> where TKey : IHashable { … }

// Assembly 1
extension IntHash for int : IHashable {
    int GetHashCode() => this;
}
Dictionary<int, int> M() {
  var dict = new Dictionary<int, int>();
  dict[3] = 3;
  return dict;
}
// Assembly 2
extension IntHash for int : IHashable {
  int GetHashCode() => this + 3;
}
void M2(Dictionary<int, int> dict) {
  Console.WriteLine(dict[3]);
}
```
If the dictionary from `M` were to ever somehow appear as an argument to `M2` then a big problem would occur. It is not clear what implementation to use for any given substitution. Even though `int` implements `IHashable` in both assemblies, the implementations are not compatible. If the extension implementation in Assembly 2 is used for the Dictionary, the hash code result is not the same for the same input. If that implementation is not used, it is not clear how Assembly 2 would properly type check, given that the validity of the type `Dictionary<int, int>` depends on the extension `IntHash` only visible in Assembly 2.
One way to clearly make the above situation impossible is to consider an “orphan rule”, as described in Ambiguous Implementations

[ ] Open questions:
 - Is the above code valid? Does it produce any errors?
 - Is there a difference in type between the Dictionary<int, int> clauses in Assembly 1 and Assembly 2?

## Implementations

It is currently unknown what the best implementation would be for extension interface implementation would be in .NET. Instead, this section will start by describing the implementations commonly used for type classes in other languages.

The first thing to note, based on the semantics described above, is that there is a distinct separation between a _type_ and _interface dispatch_. When programing with type classes it is easy to attach and combine arbitrary interface dispatch implementations with arbitrary type instances, as long as those implementations are unambiguous and compatible. This distinction is usually reflected in implementations, which formally separate types and interface implementations.

To start, let's consider generic methods. Generic methods may take a number of type parameters, each of which may have distinct constraints. These constraints can be used to perform interface dispatch. Type class implementations generally handle this by taking N additional implicit parameters for N generic parameters. These additional parameters are sometimes called "witness tables" and are analagous to vtables, with an entry for method in every interface used in the constraint. If translated to C#, we might take some input like

```C#
void M<T1, T2>(T1 t1, T2 t2)
  where T1 : I1, I2
  where T2 : I3, I4
{ ... }
```

and translate it to

```C#
struct __T1W { ... } struct __T2W { ... }
void M<T1, T2>(T1 t1, T2 t2, __T1W w1, __T2W w2)
{ ... }
```

In the above `__T1W` and `__T2W` are witness tables which would contain a delegate or function pointer for each interface method. At this level the instance/static distinction would be erased and all functions would be invoked with implicit receivers mapped to explicit parameters. The runtime or compiler would be responsible for ensuring that each call to an interface on `T1` or `T2` would be dispatched through `w1` or `w2`, respectively.

Notably, an interface table is not per object instance in the case of generics, but per unique type in the definition. So, for the original method

```C#
void M<T>(T[] array) where T : I1 { ... }
```

there would need to be exactly one witness table for `T`, not one for every element in the array.

Bare interfaces become more complicated as it is possible for each interface instance to represent a different converted type, and therefore be associated with a different witness table. In cases like these type class implementations often consider interface types to be "containers" of a generic type and bundle the type witness into the interface. This could be done either by a "fat pointer" implementation that carries a pointer to the witness table along with a pointer to a boxed interface value, or it could be done by storing the witness table inside the boxed interface value representation. The "fat pointer" implementation would be notably problematic for .NET as it generally relies on interface types having a word-sized representation and being atomically swappable.

Lastly, there are generic types. Here implementations can vary. Some languages have so-called "associated types" which can complicate the implementation. For languages without strong promises about the size or layout of types, the witness tables could be carried as fields inside the type instance. Otherwise, languages often carry separate "value witness" types which describe individual instantiations of generic types. For example, the type `ValueTuple<bool, bool>` would have a "value witness" to describe the particular instantiation `bool, bool` over the `ValueTuple` definition. In this representation, the "witness table" for interface dispatch can be stored as part of the "value witness" table for any particular instantiation of a generic type.

[1] Wadler, Blott. How to make ad-hoc polymorphism less ad hoc. https://dl.acm.org/doi/pdf/10.1145/75277.75283
