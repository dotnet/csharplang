# Non-boxing default-interface methods

Champion issue: <https://github.com/dotnet/csharplang/issues/9969>

## Summary

Introduce a syntax for declaring default interface methods that avoid boxing when implemented by value types. This is achieved by using a `this<TSelf>` modifier on interface members, which provides an implicit receiver typed as the method-level type parameter, enabling non-boxing invocation on structs.

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

A new `this<TSelf>` modifier is introduced for interface members. When applied to a method, property, or indexer, it declares a type parameter for the receiver and indicates that the member has an implicit receiver typed as that type parameter (rather than the interface type):

```csharp
interface ICounter
{
    int Count { get; set; }
    
    this<TSelf> void Increment() where TSelf : ICounter => Count++;
}
```

The `this<TSelf>` modifier:
- Declares a type parameter `TSelf` that represents the receiver type
- The type parameter must be constrained to the containing interface
- Provides an implicit receiver of type `TSelf` within the method body
- Takes the place of `virtual`/`abstract` modifiers
- Whether the member is abstract or has a default implementation is inferred based on whether a body is provided

### Type parameter requirements

The type parameter on the `this` modifier must be constrained to the containing interface:

```csharp
// Valid: TSelf is constrained to IExample
interface IExample
{
    this<TSelf> void M() where TSelf : IExample;
}

// Invalid: no constraint to the containing interface
interface IInvalid
{
    this<TSelf> void M(); // Error: TSelf must be constrained to IInvalid
}

// Valid: additional constraints are allowed
interface IComparable
{
    this<TSelf> int CompareTo(TSelf other) where TSelf : IComparable;
}
```

### Grammar changes

The grammar for interface members is extended to allow the `this` modifier with a type parameter:

```diff
 interface_method_declaration
-    : attributes? 'new'? ('abstract' | 'virtual' | 'sealed')? return_type identifier type_parameter_list? '(' formal_parameter_list? ')' type_parameter_constraints_clause* ';'
+    : attributes? 'new'? ('abstract' | 'virtual' | 'sealed' | 'this' type_parameter_list)? return_type identifier type_parameter_list? '(' formal_parameter_list? ')' type_parameter_constraints_clause* (';' | method_body)
     ;
```

### Semantic rules

When `this<TSelf>` is applied to an interface member:

1. The type parameter `TSelf` must be constrained to the containing interface
2. Within the member body, `this` has the type `TSelf` (not the interface type)
3. The receiver is always passed by reference
   - For structs, this avoids boxing
   - For classes, the caller passes a reference to a temporary local copy of the reference variable (to avoid mutating the caller's variable if the method reassigns `this`)
4. The member is lowered to a static virtual method with an explicit receiver parameter
5. Implementations can override this member using explicit interface implementation

### Lowering

The `this<TSelf>` modifier is lowered to a static virtual method with an explicit receiver parameter. However, the member is **treated as an instance method on the containing interface** for lookup and invocation purposes. This means:

- The method can be invoked using instance method syntax on values of the implementing type
- The receiver is always passed by reference, regardless of whether it's a struct or class
  - For structs, this avoids boxing
  - For classes, the caller passes a reference to a temporary local copy of the reference variable (to avoid mutating the caller's variable if the method reassigns `this`)
- Method resolution treats these members as instance methods, even though the underlying implementation uses static virtual methods

#### Source code

```csharp
interface ICounter
{
    int Count { get; set; }
    
    this<TSelf> void Increment() where TSelf : ICounter => Count++;
}

struct Counter : ICounter
{
    public int Count { get; set; }
    // Uses default Increment() implementation
}
```

#### Lowered equivalent

The compiler transforms the above into:

```csharp
interface ICounter
{
    int Count { get; set; }
    
    static virtual void Increment<TSelf>(ref TSelf @this) where TSelf : ICounter => @this.Count++;
}

struct Counter : ICounter
{
    public int Count { get; set; }
    // Uses default Increment() implementation
}
```

The key insight is that the `this<TSelf>` modifier provides a simpler syntax that lowers to static virtual methods with explicit receiver parameters. The compiler generates the receiver parameter automatically and moves the type parameter to the method signature.

### Usage

With this feature, users can write:

```csharp
var c = new Counter();
c.Increment();
Console.WriteLine(c.Count); // Outputs: 1
```

The call to `c.Increment()` is resolved by the compiler as if `Increment` were an instance method on the interface. The compiler generates a call to the static virtual method, passing the receiver by reference. No boxing occurs because the struct is never converted to the interface type.

### Signature collision rules

Because `this<TSelf>` members are treated as instance methods on the interface, signature collision rules apply. A `this<TSelf>` member with the same name and parameter types as an instance method is an error:

```csharp
interface IExample
{
    void Foo();                                    // Instance method with signature Foo()
    this<TSelf> void Foo() where TSelf : IExample; // Error: collision with Foo()
}
```

Members with different parameters are allowed:

```csharp
interface IExample
{
    void Bar();                                         // Instance method Bar()
    this<TSelf> void Bar(int value) where TSelf : IExample; // OK: signature is Bar(int)
}
```

This rule ensures that method resolution is unambiguous when invoking members on implementing types.

### Properties and indexers

The `this<TSelf>` modifier can also be applied to properties and indexers:

```csharp
interface IHasValue
{
    this<TSelf> int Value where TSelf : IHasValue { get; }
}

// Lowered equivalent:
interface IHasValue
{
    static virtual int get_Value<TSelf>(ref TSelf @this) where TSelf : IHasValue;
}
```

### Implementation in structs

When a struct implements an interface with `this<TSelf>` members, the default implementation is used automatically. Custom implementations can be provided using explicit interface implementation with the lowered static virtual method signature:

```csharp
struct Counter : ICounter
{
    public int Count { get; set; }
    
    // Custom implementation of the this<TSelf> member
    static void ICounter.Increment<TSelf>(ref TSelf @this) => @this.Count += 2; // Custom increment
}
```

### Default implementations

If a `this<TSelf>` member has a body, that body serves as the default implementation:

```csharp
interface ICloneable
{
    // Abstract - no default implementation
    this<TSelf> TSelf Clone() where TSelf : ICloneable;
    
    // Has default implementation - can call other 'this' members directly
    this<TSelf> TSelf CloneAndModify(Action<TSelf> modify) where TSelf : ICloneable
    {
        var clone = Clone(); // Calls this.Clone() implicitly
        modify(clone);
        return clone;
    }
}
```

## Drawbacks

### Complexity

- Introduces a new `this<TSelf>` modifier that may be confusing to developers unfamiliar with the feature
- The compiler must treat these members as instance methods for resolution purposes while emitting static virtual methods, adding implementation complexity
- Developers must understand the difference between regular default interface methods and `this<TSelf>` members

### Breaking changes

- None anticipated, as this is purely additive syntax

### Interop concerns

- The lowered form uses existing CLR features (static virtual methods), so runtime support should not be an issue
- Languages that don't understand the `this<TSelf>` semantic treatment may see these as regular static virtual methods and require explicit static invocation syntax

## Alternatives

### Manual static virtual + extension pattern

Developers can already achieve this behavior manually using static virtual methods and [C# 14 extension members](csharp-14.0/extensions.md):

```csharp
interface ICounter
{
    int Count { get; set; }
    
    static virtual void Increment<TSelf>(ref TSelf @this) where TSelf : ICounter => @this.Count++;
}

static class ICounterExt
{
    extension<T>(ref T @this) where T : struct, ICounter
    {
        public void Increment() => ICounter.Increment(ref @this);
    }
}
```

However, this requires significant boilerplate and is error-prone. The proposed `this<TSelf>` modifier achieves the same semantics without requiring explicit extension method definitions.

### Using interface-level type parameter

An alternative syntax could require the interface to have a type parameter:

```csharp
interface ICounter<TSelf> where TSelf : ICounter<TSelf>
{
    int Count { get; set; }
    
    this void Increment() => Count++;
}
```

This was considered but rejected because it forces the interface to be generic even when it doesn't need to be, and requires implementing types to specify themselves as the type argument (e.g., `struct Counter : ICounter<Counter>`).

### Do nothing

Developers could continue using the manual pattern or accept boxing for default interface methods on structs. However, the boilerplate required is substantial and the boxing behavior causes subtle bugs.

## Open questions

1. **Ref kind**: Should the implicit receiver be `ref`, `in`, or `ref readonly`? The proposal currently assumes `ref` for mutability.

2. **Naming**: Is `this<TSelf>` the best modifier syntax, or would alternatives like `self<TSelf>` or a new keyword be clearer?

3. **Lookup precedence**: When a `this<TSelf>` member and a true instance member could both match, what are the exact precedence rules?

4. **Explicit invocation syntax**: Should there be a way to explicitly invoke the underlying static virtual method (e.g., for cases where the implicit instance-like syntax is not desired)?

## Design meetings

TBD
