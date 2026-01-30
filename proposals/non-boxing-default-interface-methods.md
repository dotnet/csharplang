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

The `static this` syntax is lowered to the existing runtime encoding for static virtual methods combined with extension methods for convenient invocation.

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

The compiler transforms the above into the following. Note that the extension syntax uses the [C# 14 extension members](csharp-14.0/extensions.md) feature:

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

// Compiler-generated extension for convenient invocation
static class IFaceExtensions
{
    extension<T>(ref T @this) where T : struct, IFace<T>
    {
        public int M() => T.M(ref @this);
    }
}
```

### Usage

With this feature, users can write:

```csharp
var s = new S(1);
Console.WriteLine(s.M()); // Outputs: 1
```

The call to `s.M()` is resolved to the extension method, which in turn calls the static virtual method. No boxing occurs because the struct is passed by reference.

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
- The lowering involves generating extension methods, which adds to compilation complexity
- Developers must understand the difference between regular default interface methods and `static this` members

### Breaking changes

- None anticipated, as this is purely additive syntax

### Interop concerns

- The lowered form uses existing CLR features (static virtual methods), so runtime support should not be an issue
- However, languages that don't understand the extension generation pattern may not be able to use these members conveniently

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

However, this requires significant boilerplate and is error-prone. The proposed syntax automates this pattern.

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

1. **Extension method generation**: Should the compiler always generate extension methods, or should this be opt-in?

2. **Class support**: Should `static this` members also work with classes, or only with struct constraints?

3. **Ref kind flexibility**: Should the receiver parameter support `ref`, `in`, `ref readonly`, or all of them? The proposal currently allows any ref kind.

4. **Naming**: Is `static this` the best modifier combination, or would alternatives like `instance static` or a new keyword be clearer?

5. **Visibility of generated extensions**: Should the generated extension methods be public or should there be a way to control their visibility?

## Design meetings

TBD
