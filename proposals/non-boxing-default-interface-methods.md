# Non-boxing default-interface methods

Champion issue: <https://github.com/dotnet/csharplang/issues/9969>

## Summary

Introduce a syntax for declaring default interface methods that avoid boxing when implemented by value types. This is achieved by using a `this` modifier on interface members, which provides an implicit receiver typed as the self-constrained type parameter, enabling non-boxing invocation on structs.

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

A new `this` modifier is introduced for interface members. When applied to a method, property, or indexer, it indicates that the member has an implicit receiver typed as the self-constrained type parameter (rather than the interface type):

```csharp
interface ICounter<TSelf> where TSelf : ICounter<TSelf>
{
    int Count { get; set; }
    
    this void Increment() => Count++;
}
```

The `this` modifier:
- Indicates the member has an implicit receiver of type `TSelf` (the first type parameter with a recursive constraint)
- Takes the place of `virtual`/`abstract` modifiers
- Whether the member is abstract or has a default implementation is inferred based on whether a body is provided

### Type parameter requirements

The `this` modifier requires the interface to have a self-constrained type parameter:
- The type parameter must be the **first** type parameter on the interface
- It must have a **recursive constraint** to the containing interface (e.g., `TSelf : ICounter<TSelf>`)

```csharp
// Valid: TSelf is first parameter with recursive constraint
interface IExample<TSelf> where TSelf : IExample<TSelf>
{
    this void M();
}

// Invalid: no self-constrained type parameter
interface IInvalid
{
    this void M(); // Error: interface must have a self-constrained type parameter
}

// Invalid: self-constraint is not on the first type parameter
interface IInvalidParameterOrder<T, TSelf> where TSelf : IInvalidParameterOrder<T, TSelf>
{
    this void M(); // Error: TSelf must be the first type parameter
}
```

### Grammar changes

The grammar for interface members is extended to allow the `this` modifier:

```diff
 interface_method_declaration
-    : attributes? 'new'? ('abstract' | 'virtual' | 'sealed')? return_type identifier type_parameter_list? '(' formal_parameter_list? ')' type_parameter_constraints_clause* ';'
+    : attributes? 'new'? ('abstract' | 'virtual' | 'sealed' | 'this')? return_type identifier type_parameter_list? '(' formal_parameter_list? ')' type_parameter_constraints_clause* (';' | method_body)
     ;
```

### Semantic rules

When `this` is applied to an interface member:

1. The interface must have a first type parameter with a recursive constraint to the containing interface
2. Within the member body, `this` has the type of that self-constrained type parameter (not the interface type)
3. For structs, the receiver is passed by reference (avoiding boxing)
4. The member is lowered to a static virtual method with an explicit receiver parameter
5. Implementations can override this member using explicit interface implementation

### Lowering

The `this` modifier is lowered to a static virtual method with an explicit receiver parameter. However, the member is **treated as an instance method on the containing interface** for lookup and invocation purposes. This means:

- The method can be invoked using instance method syntax on values of the implementing type
- For structs, the receiver is passed by reference (avoiding boxing)
- Method resolution treats these members as instance methods, even though the underlying implementation uses static virtual methods

#### Source code

```csharp
interface ICounter<TSelf> where TSelf : ICounter<TSelf>
{
    int Count { get; set; }
    
    this void Increment() => Count++;
}

struct Counter : ICounter<Counter>
{
    public int Count { get; set; }
    // Uses default Increment() implementation
}
```

#### Lowered equivalent

The compiler transforms the above into:

```csharp
interface ICounter<TSelf> where TSelf : ICounter<TSelf>
{
    int Count { get; set; }
    
    static virtual void Increment(ref TSelf @this) => @this.Count++;
}

struct Counter : ICounter<Counter>
{
    public int Count { get; set; }
    // Uses default Increment() implementation
}
```

The key insight is that the `this` modifier provides a simpler syntax that lowers to static virtual methods with explicit receiver parameters. The compiler generates the receiver parameter automatically.

### Usage

With this feature, users can write:

```csharp
var c = new Counter();
c.Increment();
Console.WriteLine(c.Count); // Outputs: 1
```

The call to `c.Increment()` is resolved by the compiler as if `Increment` were an instance method on the interface. The compiler generates a call to the static virtual method, passing the receiver by reference. No boxing occurs because the struct is never converted to the interface type.

### Signature collision rules

Because `this` members are treated as instance methods on the interface, signature collision rules apply. A `this` member with the same name and parameter types as an instance method is an error:

```csharp
interface IExample<TSelf> where TSelf : IExample<TSelf>
{
    void Foo();          // Instance method with signature Foo()
    this void Foo();     // Error: collision with Foo()
}
```

Members with different parameters are allowed:

```csharp
interface IExample<TSelf> where TSelf : IExample<TSelf>
{
    void Bar();              // Instance method Bar()
    this void Bar(int value); // OK: signature is Bar(int)
}
```

This rule ensures that method resolution is unambiguous when invoking members on implementing types.

### Properties and indexers

The `this` modifier can also be applied to properties and indexers:

```csharp
interface IHasValue<TSelf> where TSelf : IHasValue<TSelf>
{
    this int Value { get; }
}

// Lowered equivalent:
interface IHasValue<TSelf> where TSelf : IHasValue<TSelf>
{
    static virtual int get_Value(ref TSelf @this);
}
```

### Implementation in structs

When a struct implements an interface with `this` members, it can provide an implementation using explicit interface implementation:

```csharp
struct Counter : ICounter<Counter>
{
    public int Count { get; set; }
    
    // Explicit implementation of the this member
    static void ICounter<Counter>.Increment(ref Counter @this) => @this.Count += 2; // Custom increment
}
```

### Default implementations

If a `this` member has a body, that body serves as the default implementation:

```csharp
interface ICloneable<TSelf> where TSelf : ICloneable<TSelf>
{
    // Abstract - no default implementation
    this TSelf Clone();
    
    // Has default implementation - can call other 'this' members directly
    this TSelf CloneAndModify(Action<TSelf> modify)
    {
        var clone = Clone(); // Calls this.Clone() implicitly
        modify(clone);
        return clone;
    }
}
```

## Drawbacks

### Complexity

- Introduces a new `this` modifier that may be confusing to developers unfamiliar with the feature
- The compiler must treat these members as instance methods for resolution purposes while emitting static virtual methods, adding implementation complexity
- Developers must understand the difference between regular default interface methods and `this` members
- Requires understanding the type parameter requirements (first parameter, recursive constraint)

### Breaking changes

- None anticipated, as this is purely additive syntax

### Interop concerns

- The lowered form uses existing CLR features (static virtual methods), so runtime support should not be an issue
- Languages that don't understand the `this` semantic treatment may see these as regular static virtual methods and require explicit static invocation syntax

## Alternatives

### Manual static virtual + extension pattern

Developers can already achieve this behavior manually using static virtual methods and [C# 14 extension members](csharp-14.0/extensions.md):

```csharp
interface ICounter<TSelf> where TSelf : ICounter<TSelf>
{
    int Count { get; set; }
    
    static virtual void Increment(ref TSelf @this) => @this.Count++;
}

static class ICounterExt
{
    extension<T>(ref T @this) where T : struct, ICounter<T>
    {
        public void Increment() => T.Increment(ref @this);
    }
}
```

However, this requires significant boilerplate and is error-prone. The proposed `this` modifier achieves the same semantics without requiring explicit extension method definitions.

### Using `static this` with explicit receiver

An alternative syntax could require an explicit receiver parameter:

```csharp
interface ICounter<TSelf> where TSelf : ICounter<TSelf>
{
    int Count { get; set; }
    
    static this void Increment(ref TSelf @this) => @this.Count++;
}
```

This was considered but rejected in favor of the simpler implicit receiver syntax, which is more consistent with how instance methods work.

### Do nothing

Developers could continue using the manual pattern or accept boxing for default interface methods on structs. However, the boilerplate required is substantial and the boxing behavior causes subtle bugs.

## Open questions

1. **Class support**: Should `this` members also work with classes, or only with struct constraints?

2. **Ref kind**: Should the implicit receiver be `ref`, `in`, or `ref readonly`? The proposal currently assumes `ref` for mutability.

3. **Naming**: Is `this` the best modifier, or would alternatives like `self` or a new keyword be clearer?

4. **Lookup precedence**: When a `this` member and a true instance member could both match, what are the exact precedence rules?

5. **Explicit invocation syntax**: Should there be a way to explicitly invoke the underlying static virtual method (e.g., for cases where the implicit instance-like syntax is not desired)?

## Design meetings

TBD
