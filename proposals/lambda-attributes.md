# Lambda Attributes

* [x] Proposed
* [ ] Prototype
* [ ] Implementation
* [ ] Specification

## Summary
[summary]: #summary

Allow attributes to be applied to lambdas (and anonymous methods) and to lambda / anonymous method parameters, as they can be on regular methods.

## Motivation
[motivation]: #motivation

Two primary motivations:

1. To provide metadata visible to analyzers at compile-time.
2. To provide metadata visible to reflection and tooling at run-time.

As an example of (1):
For performance-sensitive code, it is helpful to be able to have an analyzer that flags when closures and delegates are being allocated for lambdas that close over state.  Often a developer of such code will go out of his or her way to avoid capturing any state, so that the compiler can generate a static method and a cacheable delegate for the method, or the developer will ensure that the only state being closed over is `this`, allowing the compiler at least to avoid allocating a closure object.  But, without language support for limiting what may be captured, it is all too easy to accidentally close over state.  It would be valuable if a developer could annotate lambdas with attributes to indicate what state they're allowed to close over, for example:

```csharp
[CaptureNone] // can't close over any instance state
[CaptureThis] // can only capture `this` and no other instance state
[CaptureAny] // can close over any instance state
```

Then an analyzer can be written to flag when state is captured incorrectly, for example:

```csharp
var results = collection.Select([CaptureNone](i) => Process(item)); // Analyzer error: [CaptureNone] lambdas captures `this`
...
private U Process(T item) { ... }
```

## Detailed design
[design]: #detailed-design

- Using the same attribute syntax as on normal methods, attributes may be applied at the beginning of a lambda or anonymous method, for example:

```csharp
[SomeAttribute(...)] () => { ... }
[SomeAttribute(...)] delegate (int i) { ... }
```

- To avoid ambiguity as to whether an attribute applies to the lambda method or to one of the arguments, attributes may only be used when parens are used around any arguments, for example:

```csharp
[SomeAttribute] i => { ... } // ERROR
[SomeAttribute] (i) => { ... } // Ok
[SomeAttribute] (int i) => { ... } // Ok
```

- With anonymous methods, parens are not needed in order to apply an attribute to the method before the `delegate` keyword, for example:

```csharp
[SomeAttribute] delegate { ... } // Ok
[SomeAttribute] delegate (int i) => { ... } // Ok
```

- Multiple attributes may be applied, either via standard comma-delimited syntax or via full-attribute syntax, for example:

```csharp
[FirstAttribute, SecondAttribute] (i) => { ... } // Ok
[FirstAttribute] [SecondAttribute] (i) => { .... } // Ok
```

- Attributes may be applied to the parameters to an anonymous method or lambda, but only when parens are used around any arguments, for example:

```csharp
[SomeAttribute] i => { ... } // ERROR
([SomeAttribute] i) => { .... } // Ok
([SomeAttribute] int i) => { ... } // Ok
([SomeAttribute] i, [SomeOtherAttribute] j) => { ... } // Ok
```

- Multiple attributes may be applied to the parameters of an anonymous method or lambda, using either the comma-delimited or full-attribute syntax, for example:

```csharp
([FirstAttribute, SecondAttribute] i) => { ... } // Ok
([FirstAttribute] [SecondAttribute] i) => { ... } // Ok
```

- `return`-targeted attributes may also be used on lambdas, for example:

```csharp
([return: SomeAttribute] (i) => { ... }) // Ok
```

- The compiler outputs the attributes onto the generated method and arguments to those methods as it would for any other method.

## Drawbacks
[drawbacks]: #drawbacks

n/a

## Alternatives
[alternatives]: #alternatives

n/a

## Unresolved questions
[unresolved]: #unresolved-questions

n/a

## Design meetings

n/a