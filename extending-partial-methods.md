Extending Partial Methods
=====

## Summary
This proposal aims to remove all restrictions around the signatures of `partial`
methods in C#. The goal being to expand the set of scenarios in which these
methods can work with source generators as well as being a more general 
declaration form for C# methods.

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

One behavior of `partial` methods is that when the definition is abscent then
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
designer generated code. Users were constantly editting the generated code 
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
1. Cannot have `ref` or `out` parameters. 
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
The compiler will allow `partial` methods to be annotated with an explicit 
accessibility. 

## Questions

### partial on all members

### abstract or virtual

## Considerations

