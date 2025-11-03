# Partial Events and Constructors

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

Champion issue: <https://github.com/dotnet/csharplang/issues/9058>

## Summary

Allow the `partial` modifier on events and constructors to separate declaration and implementation parts,
similar to [partial methods][partial-methods-ext] and [partial properties/indexers][partial-props].

```cs
partial class C
{
    partial C(int x, string y);
    partial event Action<int, string> MyEvent;
}

partial class C
{
    partial C(int x, string y) { }
    partial event Action<int, string> MyEvent
    {
        add { }
        remove { }
    }
}
```

## Motivation

C# already supports partial methods, properties, and indexers.
Partial events and constructors are missing.

Partial events would be useful for [weak event][weak-events] libraries, where the user could write definitions:

```cs
partial class C
{
    [WeakEvent]
    partial event Action<int, string> MyEvent;

    void M()
    {
        RaiseMyEvent(0, "a");
    }
}
```

And a library-provided source generator would provide implementations:

```cs
partial class C
{
    private readonly WeakEvent _myEvent;

    partial event Action<int, string> MyEvent
    {
        add { _myEvent.Add(value); }
        remove { _myEvent.Remove(value); }
    }

    protected void RaiseMyEvent(int x, string y)
    {
        _myEvent.Invoke(x, y);
    }
}
```

Partial events and partial constructors would be also useful for generating interop code like in [Xamarin][xamarin]
where the user could write partial constructor and event definitions:

```cs
partial class AVAudioCompressedBuffer : AVAudioBuffer
{
    [Export("initWithFormat:packetCapacity:")]
    public partial AVAudioCompressedBuffer(AVAudioFormat format, uint packetCapacity);

    [Export("create:")]
    public partial event EventHandler Created;
}
```

And the source generator would generate the bindings (to Objective-C in this case):

```cs
partial class AVAudioCompressedBuffer : AVAudioBuffer
{
    [BindingImpl(BindingImplOptions.GeneratedCode | BindingImplOptions.Optimizable)]
    public partial AVAudioCompressedBuffer(AVAudioFormat format, uint packetCapacity) : base(NSObjectFlag.Empty)
    {
        // Call Objective-C runtime:
        InitializeHandle(
            global::ObjCRuntime.NativeHandle_objc_msgSendSuper_NativeHandle_UInt32(
                this.SuperHandle,
                Selector.GetHandle("initWithFormat:packetCapacity:"),
                format.GetNonNullHandle(nameof(format)),
                packetCapacity),
            "initWithFormat:packetCapacity:");
    }

    public partial event EventHandler Created
    {
        add { /* ... */ }
        remove { /* ... */ }
    }
}
```

## Detailed design

### General

Event declaration syntax ([§15.8.1][event-syntax]) is extended to allow the `partial` modifier:

```diff
 event_declaration
-    : attributes? event_modifier* 'event' type variable_declarators ';'
+    : attributes? event_modifier* 'partial'? 'event' type variable_declarators ';'
-    | attributes? event_modifier* 'event' type member_name
+    | attributes? event_modifier* 'partial'? 'event' type member_name
         '{' event_accessor_declarations '}'
     ;
```

Instance constructor declaration syntax ([§15.11.1][ctor-syntax]) is extended to allow the `partial` modifier:

```diff
 constructor_declaration
-    : attributes? constructor_modifier* constructor_declarator constructor_body
+    : attributes? constructor_modifier* 'partial'? constructor_declarator constructor_body
     ;
```

Note that there is [a proposal][partial-ordering] to allow the `partial` modifier anywhere among modifiers,
rather than only as the last one (also for method, property, and type declarations).

An event declaration with the `partial` modifier is said to be a *partial event declaration*
and it is associated with one or more *partial events* with the specified names
(note that one event declaration without accessors can define multiple events).

A constructor declaration with the `partial` modifier is said to be a *partial constructor declaration*
and it is associated with a *partial constructor* with the specified signature.

A partial event declaration is said to be an *implementing declaration*
when it specifies the `event_accessor_declarations` or it has the `extern` modifier.
Otherwise, it is a *defining declaration*.

A partial constructor declaration is said to be a *defining declaration*
when it has a semicolon body and it lacks the `extern` modifier.
Otherwise, it is an *implementing declaration*.

```cs
partial class C
{
    // defining declarations
    partial C();
    partial C(int x);
    partial event Action E, F;

    // implementing declarations
    partial C() { }
    partial C(int x) { }
    partial event Action E { add { } remove { } }
    partial event Action F { add { } remove { } }
}
```

Only the defining declaration of a partial member participates in lookup
and is considered at use sites and for emitting the metadata.
(Except for documentation comments as detailed [below](#documentation-comments).)
The implementing declaration signature is used for nullable analysis of the associated bodies.

A partial event or constructor:
- May only be declared as a member of a partial type.
- Must have one defining and one implementing declaration.
- Is not permitted to have the `abstract` modifier.
- Cannot explicitly implement an interface member.

A partial event is not field-like ([§15.8.2][event-field-like]), i.e.:
- It does not have any backing storage or accessors generated by the compiler.
- It can only be used in `+=` and `-=` operations, not as a value.

A defining partial constructor declaration cannot have a constructor initializer
(`: this()` or `: base()`; [§15.11.2][ctor-init]).

### Parsing break

Allowing the `partial` modifier in more contexts is a breaking change:

```cs
class C
{
    partial F() => new partial(); // previously a method, now a constructor
    @partial F() => new partial(); // workaround to keep it a method
}

class partial { }
```

To simplify the language parser,
the `partial` modifier is accepted at any method-like declaration
(i.e., local functions and top-level script methods, as well),
even though we do not specify the grammar changes explicitly above.

### Attributes

The attributes of the resulting event or constructor are the combined attributes of the partial declarations in the corresponding positions.
The combined attributes are concatenated in an unspecified order and duplicates are not removed.

The `method` *attribute_target* ([§22.3][attribute-spec]) is ignored on partial event declarations.
Accessor attributes are used only from accessor declarations (which can be present only under the implementing declaration).
Note that `param` and `return` *attribute_target*s are already ignored on all event declarations.

Caller-info attributes on the implementing declaration are ignored by the compiler as specified
by the partial properties proposal in section [Caller-info attributes][partial-props-caller-info]
(notice that it applies to all partial *members* which includes partial events and constructors).

### Signatures

Both declarations of a partial member must have matching signatures [similar to partial properties][partial-props-signatures]:
1. Type and ref kind differences between partial declarations which are significant to the runtime result in a compile-time error.
2. Differences in tuple element names between partial declarations result in a compile-time error.
3. The declarations must have the same modifiers, though the modifiers may appear in a different order.
   - Exception: this does not apply to the `extern` modifier which may only appear on the implementing declaration.
4. All other syntactic differences in the signatures of partial declarations result in a compile-time warning, with the following exceptions:
   - Attribute lists do not need to match as described [above](#attributes).
   - Nullable context differences (such as oblivious vs. annotated) do not cause warnings.
   - Default parameter values do not need to match, but a warning is reported when the implementing constructor declaration has default parameter values
     (because those would be ignored since only the defining declaration participates in lookup).
5. A warning occurs when parameter names differ across defining and implementing constructor declarations.
6. Nullability differences which do not involve oblivious nullability result in warnings.

### Documentation comments

It is permitted to include doc comments on both the defining and implementing declaration.
Note that doc comments are not supported on event accessors.

When doc comments are present on only one of the declarations of a partial member, those doc comments are used normally
(surfaced through Roslyn APIs, emitted to the documentation XML file).

When doc comments are present on both declarations of a partial member,
all the doc comments on the defining declaration are dropped,
and only the doc comments on the implementing declaration are used.

When parameter names differ between declarations of a partial member,
`paramref` elements use the parameter names from the declaration associated with the documentation comment in source code.
For example, a `paramref` on a doc comment placed on an implementing declaration
refers to the parameter symbols of the implementing declaration using their parameter names.
This can be confusing, because the metadata signature will use parameter names from the defining declaration.
It is recommended to ensure that parameter names match across the declarations of a partial member to avoid this confusion.

<!-- ## Drawbacks -->

<!-- ## Alternatives -->

## Open questions

### Member kinds

Do we want partial events, constructors, operators, fields?
We propose the first two member kinds, but any other subset could be considered.

Partial *primary* constructors could be also considered,
e.g., permitting the user to have the same parameter list on multiple partial type declarations.

### Attribute locations

Should we recognize the `[method:]` attribute target specifier for partial events (or just the defining declarations)?
Then the resulting accessor attributes would be the concatenation of `method`-targeting attributes from both (or just the defining) declaration parts
plus self-targeting and `method`-targeting attributes from the accessors of the implementing declaration.
Combining attributes from different declaration kinds would be unprecedented and indeed
the current implementation of attribute matching in Roslyn does not support that.

We can also consider recognizing `[param:]` and `[return:]`, not just on partial events, but all field-like and extern events.
For more details, see https://github.com/dotnet/roslyn/issues/77254.

<!--------
## Links
--------->

[partial-methods-ext]: ../csharp-9.0/extending-partial-methods.md
[partial-props]: ../csharp-13.0/partial-properties.md
[partial-props-caller-info]: ../csharp-13.0/partial-properties.md#caller-info-attributes
[partial-props-signatures]: ../csharp-13.0/partial-properties.md#matching-signatures
[partial-events-discussion]: https://github.com/dotnet/csharplang/discussions/8064
[partial-ordering]: https://github.com/dotnet/csharplang/issues/8966
[xamarin]: https://github.com/xamarin/xamarin-macios/issues/21308#issuecomment-2447535524
[weak-events]: https://learn.microsoft.com/dotNet/api/system.windows.weakeventmanager
[event-syntax]: https://github.com/dotnet/csharpstandard/blob/f3c66477dc2d0a76d5c278e457a63c1695ddae08/standard/classes.md#1581-general
[event-field-like]: https://github.com/dotnet/csharpstandard/blob/f3c66477dc2d0a76d5c278e457a63c1695ddae08/standard/classes.md#1582-field-like-events
[ctor-syntax]: https://github.com/dotnet/csharpstandard/blob/f3c66477dc2d0a76d5c278e457a63c1695ddae08/standard/classes.md#1511-instance-constructors
[ctor-init]: https://github.com/dotnet/csharpstandard/blob/f3c66477dc2d0a76d5c278e457a63c1695ddae08/standard/classes.md#15112-constructor-initializers
[ctor-var-init]: https://github.com/dotnet/csharpstandard/blob/f3c66477dc2d0a76d5c278e457a63c1695ddae08/standard/classes.md#15113-instance-variable-initializers
[attribute-spec]: https://github.com/dotnet/csharpstandard/blob/f3c66477dc2d0a76d5c278e457a63c1695ddae08/standard/attributes.md#223-attribute-specification
