# Non-boxing default-interface methods

Champion issue: <https://github.com/dotnet/csharplang/issues/9969>

## Summary

Introduce a syntax for declaring default interface methods that avoid boxing when implemented by value types. This is achieved by using a `static this` modifier that transforms the method into a static virtual method with an explicit receiver parameter, enabling non-boxing invocation on structs.

## Motivation

When default interface methods were added to C#, the type of `this` in such methods is the interface type itself. While this is not problematic for classes, for structs it implies boxing. This has implications not just for performance, but also for semantics, as modifications to the struct within the method affect the boxed copy rather than the original value.

Consider the following interface with a default method:

```csharp
interface ICounter
{
    int Count { get; set; }
    
    void Increment() => Count++;
}
```

When a struct implements this interface and the default method is called, the struct is boxed:

```csharp
struct Counter : ICounter
{
    public int Count { get; set; }
    // Uses default Increment() implementation
}

var c = new Counter();
((ICounter)c).Increment(); // Boxes 'c', increments the boxed copy
Console.WriteLine(c.Count); // Still 0, not 1
```

This behavior is both unexpected and can cause subtle bugs. It would be beneficial to have a way to define default interface methods that work with the actual struct type rather than a boxed interface reference.

## Detailed design

### Syntax

A new modifier combination `static this` is introduced for interface members. When applied to a method, property, or indexer, it indicates that the member should be treated as a static virtual member with an explicit receiver parameter:

```csharp
interface IFace<TSelf> where TSelf : IFace<TSelf>
{
    static this int M(ref TSelf @this) => 0;
}
```

The `static this` modifier:
- Takes the place of `virtual`/`abstract` modifiers
- Indicates the member has an explicit receiver parameter
- Whether the member is abstract or has a default implementation is inferred based on whether a body is provided

### Grammar changes

The grammar for interface members is extended to allow the `static this` modifier combination:

```diff
 interface_method_declaration
-    : attributes? 'new'? ('abstract' | 'virtual' | 'sealed')? return_type identifier type_parameter_list? '(' formal_parameter_list? ')' type_parameter_constraints_clause* ';'
+    : attributes? 'new'? ('abstract' | 'virtual' | 'sealed' | 'static' 'this')? return_type identifier type_parameter_list? '(' formal_parameter_list? ')' type_parameter_constraints_clause* (';' | method_body)
     ;
```

### Semantic rules

When `static this` is applied to an interface member:

1. The member becomes a static virtual (or static abstract if no body) member
2. The first parameter must be a reference to the self-constrained type parameter (using `ref`, `in`, or `ref readonly`)
3. The self-constrained type parameter must be constrained to the containing interface
4. Implementations can override this member using explicit interface implementation with the concrete type

### Lowering

The `static this` syntax is lowered to a static virtual method in the interface. However, rather than generating actual extension methods, the `static this` member is **treated as an instance method on the containing interface**. This means:

- The method can be invoked using instance method syntax on values of the implementing type
- For structs, the receiver is passed by reference (avoiding boxing)
- Method resolution treats these members as instance methods, even though the underlying implementation uses static virtual methods

#### Source code

```csharp
interface IFace<TSelf> where TSelf : IFace<TSelf>
{
    static this int M(ref TSelf @this) => 0;
}

struct S(int x) : IFace<S>
{
    private readonly int _x = x;
    static int IFace<S>.M(ref S @this) => @this._x;
}
```

#### Lowered equivalent

The compiler transforms the above into:

```csharp
interface IFace<TSelf> where TSelf : IFace<TSelf>
{
    static virtual int M(ref TSelf @this) => 0;
}

struct S(int x) : IFace<S>
{
    private readonly int _x = x;
    static int IFace<S>.M(ref S @this) => @this._x;
}
```

The key insight is that no extension methods are generated. Instead, the compiler treats the `static this` member as an instance method for lookup and invocation purposes, while the underlying implementation uses static virtual methods.

### Usage

With this feature, users can write:

```csharp
var s = new S(1);
Console.WriteLine(s.M()); // Outputs: 1
```

The call to `s.M()` is resolved by the compiler as if `M` were an instance method on the interface. The compiler generates a call to the static virtual method, passing the receiver by reference. No boxing occurs because the struct is never converted to the interface type.

### Signature collision rules

Because `static this` members are treated as instance methods on the interface, signature collision rules apply. For the purposes of determining signature collisions, the receiver parameter of a `static this` member is excluded (since it represents the implicit `this` when invoked as an instance method).

Therefore, a `static this void M(ref TSelf @this)` has the "invocation signature" of `void M()`, and it is an error to declare both:

```csharp
interface IExample<TSelf> where TSelf : IExample<TSelf>
{
    void Foo();                           // Instance method with signature Foo()
    static this void Foo(ref TSelf @this); // Error: invocation signature is also Foo()
}
```

Additional parameters beyond the receiver are included in the signature:

```csharp
interface IExample<TSelf> where TSelf : IExample<TSelf>
{
    void Bar();                                       // Instance method Bar()
    static this void Bar(ref TSelf @this, int value); // OK: invocation signature is Bar(int)
}
```

This rule ensures that method resolution is unambiguous when invoking members on implementing types.

### Properties and indexers

The `static this` modifier can also be applied to properties and indexers:

```csharp
interface IHasValue<TSelf> where TSelf : IHasValue<TSelf>
{
    static this int Value { get; }
}

// Lowered equivalent:
interface IHasValue<TSelf> where TSelf : IHasValue<TSelf>
{
    static virtual int get_Value(ref TSelf @this);
}
```

### Implementation in structs

When a struct implements an interface with `static this` members, it can provide an implementation using explicit interface implementation:

```csharp
struct MyStruct : IFace<MyStruct>
{
    private int _value;
    
    // Explicit implementation of the static this member
    static int IFace<MyStruct>.M(ref MyStruct @this) => @this._value;
}
```

### Default implementations

If a `static this` member has a body, that body serves as the default implementation:

```csharp
interface ICloneable<TSelf> where TSelf : ICloneable<TSelf>
{
    // Abstract - no default implementation
    static this TSelf Clone(ref TSelf @this);
    
    // Has default implementation
    static this TSelf CloneAndModify(ref TSelf @this, Action<TSelf> modify)
    {
        var clone = TSelf.Clone(ref @this);
        modify(clone);
        return clone;
    }
}
```

## Drawbacks

### Complexity

- Introduces a new modifier combination (`static this`) that may be confusing to developers unfamiliar with the feature
- The compiler must treat these members as instance methods for resolution purposes while emitting static virtual methods, adding implementation complexity
- Developers must understand the difference between regular default interface methods and `static this` members
- Signature collision rules add additional constraints to learn

### Breaking changes

- None anticipated, as this is purely additive syntax

### Interop concerns

- The lowered form uses existing CLR features (static virtual methods), so runtime support should not be an issue
- Languages that don't understand the `static this` semantic treatment may see these as regular static virtual methods and require explicit static invocation syntax

## Alternatives

### Manual static virtual + extension pattern

Developers can already achieve this behavior manually using static virtual methods and [C# 14 extension members](csharp-14.0/extensions.md):

```csharp
interface IFace<TSelf> where TSelf : IFace<TSelf>
{
    static virtual int M(ref TSelf @this) => 0;
}

static class IFaceExt
{
    extension<T>(ref T @this) where T : struct, IFace<T>
    {
        public int M() => T.M(ref @this);
    }
}
```

However, this requires significant boilerplate and is error-prone. The proposed `static this` syntax achieves the same semantics without requiring explicit extension method definitions.

### Using `virtual` with explicit receiver

An alternative syntax could repurpose existing modifiers:

```csharp
interface IFace<TSelf> where TSelf : IFace<TSelf>
{
    virtual int M(ref TSelf @this) => 0;
}
```

This was rejected because it doesn't clearly indicate that the method is static and would conflict with existing virtual method semantics.

### Do nothing

Developers could continue using the manual pattern or accept boxing for default interface methods on structs. However, the boilerplate required is substantial and the boxing behavior causes subtle bugs.

## Open questions

1. **Class support**: Should `static this` members also work with classes, or only with struct constraints?

2. **Ref kind flexibility**: Should the receiver parameter support `ref`, `in`, `ref readonly`, or all of them? The proposal currently allows any ref kind.

3. **Naming**: Is `static this` the best modifier combination, or would alternatives like `instance static` or a new keyword be clearer?

4. **Lookup precedence**: When a `static this` member and a true instance member could both match, what are the exact precedence rules?

5. **Explicit invocation syntax**: Should there be a way to explicitly invoke the underlying static virtual method (e.g., for cases where the implicit instance-like syntax is not desired)?

## Design meetings

TBD
