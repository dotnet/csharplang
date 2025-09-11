# Readonly setter calls on non-variables

Champion issue: <https://github.com/dotnet/csharplang/issues/9174>

## Summary

Permits a readonly setter to be called on non-variable expressions, and permits object initializers to be used with value types:

```cs
var c = new C();
// Remove the current CS1612 error, because ArraySegment<T>.this is readonly:
c.ArraySegmentProp[10] = new object();

// Invocation expressions already omit the CS1612 error when the setter is readonly:
c.ArraySegmentMethod()[10] = new object();

// In limited cases, ref-returning indexers can be used to work around this:
c.RefReturningIndexerWorkaround[10] = new object();

_ = new C
{
    // Remove the current CS1918 error:
    ArraySegmentProp = { [10] = new object() },
    // Remove the current CS1918 error:
    RefReturningIndexerWorkaround = { [10] = new object() },
};

class C
{
    public ArraySegment<object> ArraySegmentProp { get; set; }
    public ArraySegment<object> ArraySegmentMethod() => ArraySegmentProp;

    public Span<object> RefReturningIndexerWorkaround => ArraySegmentProp.AsSpan();
}

// Partial declaration of System.ArraySegment<T> for demonstration
public readonly struct ArraySegment<T>
{
    // Implicitly readonly due to the readonly modifier on the struct
    public T this[int index] { get => ...; set => ...; }
}
```

Currently, the code above gives the error CS1612 "Cannot modify the return value of 'C.ArraySegmentProp' because it is not a variable." This restriction is unnecessary when the setter is readonly. The restriction is there to remind you to assign the modified struct value back to the property. But there is no supported modification to the struct value when the setter is readonly, so there is no reason to assign back to the property.

```cs
// Requested by the current CS1612 error:
var temp = c.ArraySegmentProp;
temp[10] = new object();
c.ArraySegmentProp = temp; // But this line is purposeless; 'temp' cannot have changed.
```

## Motivation

Folks who want to simulate named indexers in C# use properties that return a wrapper with an indexer on it. The wrapper that holds the indexer must be a class today, which increases GC overhead. The only reason the wrapper cannot be a struct is because of the restriction which this proposal removes:

```cs
var c = new C();

// Proposal: no error because the indexer's set accessor is readonly.
c.SimulatedNamedIndexer[42] = new object();

class C
{
    public WrapperStruct SimulatedNamedIndexer => new(this);

    public readonly struct WrapperStruct(C c)
    {
        public object this[int index]
        {
            // Indexer accesses private state or calls private methods in 'C'
            get => ...;
            set => ...;
        }
    }
}
```

This addresses recurring community requests. These requests are often around document object models, entity component systems, numerics and geometry, and other modeling scenarios, but sometimes more ordinary helpers or utilities. This is the common thread: folks would like to not have to declare a pair of accessor methods when the accessors would make logical sense together as a single property or indexer. And, they don't want a class wrapper providing the API, they want a readonly struct wrapper.

```cs
// Undesirable:
c.SetXyz("key", c.GetXyz("key") + 1);

// Desirable:
c.Xyz["key"]++;
```

These use cases would no longer be blocked if CS1612 is fully updated with an understanding of readonly structs and readonly members.

## Detailed design

For part 1, the CS1612 error is not produced for assignments where the setter is readonly. (If the whole struct is readonly, then the setter is also readonly.) The setter call is emitted the same way as any non-accessor readonly instance method call.

For part 2, object initializers are now permitted to be used on value types. This means that the CS1918 error will no longer be produced at all. This opens the door to assignments using readonly setters or using refs returned by getters. However, this does not open the door to assignments that would not be permitted in the desugared form. Errors such as CS1612 and CS0313 will be updated to appear within object initializers now that CS1918 is no longer blocking off the entire space.

### Null-conditional assignment

The [Null-conditional assignment](https://github.com/dotnet/csharplang/blob/main/proposals/null-conditional-assignment.md) proposal enables the following syntax:

```cs
a?.b = value;
a?.b.c = value;
a?[index] = value;
```

CS1612 errors will be produced for this new syntax in the same way that they are for the existing syntax below:

```cs
a2.b = value;
a2.b.c = value;
a2[index] = value;
```

Thus, when the setter is readonly, the assignments will be permitted in both cases, and when the setter is not readonly, the normal CS1612 error for structs will remain in both cases.

### InlineArray

InlineArray properties are covered by a separate error which this proposal does not affect:

```cs
var c = new C();

// ❌ CS0313 The left-hand side of an assignment must be a variable, property or indexer
c.InlineArrayProp[42] = new object();

class C
{
    public InlineArray43<object> InlineArrayProp { get; set; }
}

[System.Runtime.CompilerServices.InlineArray(43)]
public struct InlineArray43<T> { public T Element0; }
```

It's desirable for this error to remain, because the setter _does_ mutate the struct value.

## Specification

Insertions are in **bold**, deletions are in ~~strikethrough~~.

### Updates permitting readonly setter calls on non-variables

The current v8 specification draft does not yet specify readonly members (<https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/readonly-instance-members.md>). The following updates intend to leverage the concept of a _readonly member_. A _readonly member_ is a member which either is directly marked with the `readonly` modifier or which is contained inside a _struct_type_ which is marked with the `readonly` modifier.

[§12.21.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12212-simple-assignment) _Simple assignment_ is updated:

> When a property or indexer declared in a _struct_type_ is the target of an assignment, **either** the instance expression associated with the property or indexer access shall be classified as a variable, **or the set accessor of the property or indexer shall be a readonly member ([§16.2.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1622-struct-modifiers))**. If the instance expression is classified as a value **and the set accessor is not a readonly member**, a binding-time error occurs.

### Updates permitting object initializers for value types

The CS1918 error is completely removed. There is no need to block value types. "\[T]he assignments in the nested object initializer are treated as assignments to members of the field or property." Such assignments must already conform to rules such as the one enforced by CS1612, including when inside an object initializer.

[§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers) _Object initializers_ is updated:

> A member initializer that specifies an object initializer after the equals sign is a ***nested object initializer***, i.e., an initialization of an embedded object. Instead of assigning a new value to the field or property, the assignments in the nested object initializer are treated as assignments to members of the field or property. ~~Nested object initializers cannot be applied to properties with a value type, or to read-only fields with a value type.~~

## Downsides

If this proposal is taken, it becomes a source-breaking change to remove the `readonly` keyword from a struct or setter. Without the `readonly` keyword, the errors would then be relevant and would reappear.

Due to what looks like an unintentional change in the compiler, this source-breaking change is already in effect when the setter is called on an invocation expression:

```cs
// Removing 'readonly' from S1 causes a CS1612 error.
M().Prop = 1;

S1 M() => default;

public readonly struct S1
{
    public int Prop { get => 0; set { } }
}
```

```cs
// Removing 'readonly' from S2.Prop.set causes a CS1612 error.
M().Prop = 1;

S2 M() => default;

public struct S2
{
    public int Prop { get => 0; readonly set { } }
}
```

## Answered LDM questions

### Should similar assignments be permitted in object initializers?

There's a separate error, CS1918 that blocks assignments through readonly setters when the assignments appear in object initializers. In addition, this error even blocks assignments to ref-returning properties and indexers, and those assignments are not blocked when they appear outside of object initializers.

```cs
// ❌ CS1918 Members of property 'C.ArraySegmentProp' of type 'ArraySegment<object>' cannot be assigned with an object
// initializer because it is of a value type
_ = new C { ArraySegmentProp = { [42] = new object() } };
//          ~~~~~~~~~~~~~~~~

// ❌ CS1918 Members of property 'C.StructWithRefReturningIndexer' of type 'Span<object>' cannot be assigned with an object
// initializer because it is of a value type
_ = new C { StructWithRefReturningIndexer = { [42] = new object() } };
//          ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

class C
{
    public ArraySegment<object> ArraySegmentProp { get; set; }
    public Span<object> StructWithRefReturningIndexer => ArraySegmentProp.AsSpan();
}
```

Such assignments desugar to the following form, the same form in which the CS1612 warning is being removed:

```cs
var temp = new C();
// Warning being removed:
// CS1612 Cannot modify the return value of 'C.ArraySegmentProp' because it is not a variable
temp.ArraySegmentProp[42] = new object();
```

```cs
var temp = new C();
// Permitted today
temp.StructWithRefReturningIndexer[42] = new object();
```

Should this check be made more granular, so that members of struct types may be assigned when they would be allowed to be assigned in the desugared form?

#### Answer

Yes. This expansion will be included. [(LDM 2025-04-02)](https://github.com/dotnet/csharplang/blob/main/meetings/2025/LDM-2025-04-02.md#expansions)
