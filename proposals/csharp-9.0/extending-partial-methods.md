# Extending Partial Methods

## Summary
This proposal aims to remove all restrictions around the signatures of `partial`
methods in C#. The goal being to expand the set of scenarios in which these
methods can work with source generators as well as being a more general 
declaration form for C# methods.

See also the [original partial methods specification](../../spec/classes.md#partial-methods).

## Motivation
C# has limited support for developers splitting methods into declarations and 
definitions / implementations. 

```cs 
partial class C
{
    // The declaration of C.M
    partial void M(string message);
}

partial class C
{
    // The definition of C.M
    partial void M(string message) => Console.WriteLine(message);
}
```

One behavior of `partial` methods is that when the definition is absent then
the language will simply erase any calls to the `partial` method. Essentially 
it behaves like a call to a `[Conditional]` method where the condition was 
evaluated to false. 

```cs
partial class D
{
    partial void M(string message);

    void Example()
    {
        M(GetIt()); // Call to M and GetIt erased at compile time
    }

    string GetIt() => "Hello World";
}
```

The original motivation for this feature was source generation in the form of 
designer generated code. Users were constantly editing the generated code 
because they wanted to hook some aspect of the generated code. Most notably 
parts of the Windows Forms startup process, after components were initialized.

Editing the generated code was error prone because any action which caused the
designer to regenerate the code would cause the user edit to be erased. The 
`partial` method feature eased this tension because it allowed designers to
emit hooks in the form of `partial` methods. 

Designers could emit hooks like `partial void OnComponentInit()` and developers
could define declarations for them or not define them. In either case though 
the generated code would compile and developers who were interested in the 
process could hook in as needed. 

This does mean that partial methods have several restrictions:

1. Must have a `void` return type.
1. Cannot have `out` parameters. 
1. Cannot have any accessibility (implicitly `private`).

These restrictions exist because the language must be able to emit code when
the call site is erased. Given they can be erased `private` is the only possible
accessibility because the member can't be exposed in assembly metadata. These 
restrictions also serve to limit the set of scenarios in which `partial` methods
can be applied.

The proposal here is to remove all of the existing restrictions around `partial`
methods. Essentially let them have `out`, non-void return types or any 
type of accessibility. Such `partial` declarations would then have the added
requirement that a definition must exist. That means the language does not
have to consider the impact of erasing the call sites. 

This would expand the set of generator scenarios that `partial` methods could
participate in and hence link in nicely with our source generators feature. For
example a regex could be defined using the following pattern:

```cs
[RegexGenerated("(dog|cat|fish)")]
partial bool IsPetMatch(string input);
```

This gives both the developer a simple declarative way of opting into generators
as well as giving generators a very easy set of declarations to look through 
in the source code to drive their generated output. 

Compare that with the difficulty that a generator would have hooking up the 
following snippet of code. 

```cs
var regex = new RegularExpression("(dog|cat|fish)");
if (regex.IsMatch(someInput))
{

}
```

Given that the compiler doesn't allow generators to modify code hooking up this
pattern would be pretty much impossible for generators. They would need to
resort to reflection in the `IsMatch` implementation, or asking users to change
their call sites to a new method + refactor the regex to pass the string literal
as an argument. It's pretty messy.

## Detailed Design
The language will change to allow `partial` methods to be annotated with an 
explicit accessibility modifier. This means they can be labeled as `private`, 
`public`, etc ... 

When a `partial` method has an explicit accessibility modifier 
though the language will require that the declaration has a matching
definition even when the accessibility is `private`:

```cs
partial class C
{
    // Okay because no definition is required here
    partial void M1();

    // Okay because M2 has a definition
    private partial void M2();

    // Error: partial method M3 must have a definition
    private partial void M3();
}

partial class C
{
    private partial void M2() { }
}
```

Further the language will remove all restrictions on what can appear on a 
`partial` method which has an explicit accessibility. Such declarations can 
contain non-void return types, `out` parameters, `extern` modifier, 
etc ... These signatures will have the full expressivity of the C# language.

```cs
partial class D
{
    // Okay
    internal partial bool TryParse(string s, out int i); 
}

partial class D
{
    internal partial bool TryParse(string s, out int i) { }
}
```

This explicitly allows for `partial` methods to participate in `overrides` and 
`interface` implementations:

```cs
interface IStudent
{
    string GetName();
}

partial class C : IStudent
{
    public virtual partial string GetName(); 
}

partial class C
{
    public virtual partial string GetName() => "Jarde";
}
```

The compiler will change the error it emits when a `partial` method contains
an illegal element to essentially say:

> Cannot use `ref` on a `partial` method that lacks explicit accessibility 

This will help point developers in the right direction when using this feature.

Restrictions:
- `partial` declarations with explicit accessibility must have a definition
- `partial` declarations and definition signatures must match on all method
and parameter modifiers. The only aspects which can differ are parameter names
and attribute lists (this is not new but rather an existing requirement of
`partial` methods).

## Questions

### partial on all members
Given that we're expanding `partial` to be more friendly to source generators
should we also expand it to work on all class members? For example should we 
be able to declare `partial` constructors, operators, etc ...

**Resolution**
The idea is sound but at this point in the C# 9 schedule we're trying to avoid
unnecessary feature creep. Want to solve the immediate problem of expanding
the feature to work with modern source generators. 

Extending `partial` to support other members will be considered for the C# 10
release. Seems likely that we will consider this extension.

### Use abstract instead of partial
The crux of this proposal is essentially ensuring that a declaration has a
corresponding definition / implementation. Given that should we use `abstract`
since it's already a language keyword that forces the developer to think about
having an implementation?

**Resolution**
There was a healthy discussion about this but eventually it was decided against.
Yes the requirements are familiar but the concepts are significantly different.
Could easily lead the developer to believe they were creating virtual slots when
they were not doing so.
