# `cref` method groups

* [x] Proposed
* [ ] Prototype: [Complete](https://github.com/sharwell/roslyn/features/overload-cref)
* [ ] Implementation: [In Progress](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

<!-- One paragraph explanation of the feature. -->

This feature allows users to reference method groups within documentation comments by omitting the argument list from references to a method.

## Motivation
[motivation]: #motivation

<!-- Why are we doing this? What use cases does it support? What is the expected outcome? -->

Currently the following works. It compiles without warning and the documentation comment contains a reference to the `Foo()` method.

``` csharp
/// <summary>
/// Reference to a method group with one item: <see cref="Foo"/>
/// </summary>
void Foo() { }
```

However, the following does not work:

``` csharp
/// <summary>
/// Reference to a method group with two items: <see cref="Foo"/>
/// </summary>
void Foo() { }
void Foo(int x) { }
```

The specific warning produced is:

> warning CS0419: Ambiguous reference in cref attribute: 'Foo'. Assuming 'TypeName.Foo()', but could have also matched other overloads including 'TypeName.Foo(int)'.

To reference a method group, the following syntax is required:

``` csharp
/// <summary>
/// Reference to a method group with two items:
/// <see cref="O:Full.Declaring.Namespace.TypeName.Foo"/>
/// </summary>
void Foo() { }
void Foo(int x) { }
```

This is problematic for the following reasons:
1. The syntax is not validated during the build. Errors made while typing are not reported until if/when Sandcastle Help File Builder processes the comments.
2. The syntax is extremely verbose.
3. The syntax *only* works if there are more than one method with the same name. (This limitation needs to be resolved by tooling, and is not addressed by this proposal specifically.)
4. There is no editor support for this syntax, including the following features:
    * The reference has no syntax highlighting
    * The reference will not be located by Find All References
    * Go To Definition does not work on the reference
    * QuickInfo does not work on the reference

I propose the following modification to the way parameterless method references are resolved:
1. If no argument list is provided and the method group contains exactly one method, compile the comment as a direct reference to that method. This is how the compiler already behaves for this case.
2. If no parameter list is provided and the method group contains more than one method, compile the comment as a reference to the overloads of the method, with the `O:` form listed above.

## Detailed design
[design]: #detailed-design

<!-- This is the bulk of the proposal. Explain the design in enough detail for somebody familiar with the language to understand, and for somebody familiar with the compiler to implement,  and include examples of how the feature is used. This section can start out light before the prototyping phase but should get into specifics and corner-cases as the feature is iteratively designed and implemented. -->

Processing of documentation comments during the compilation process behaves as a source-to-source transformation. The C# Language Specification is not clear with respect to the syntax and/or limitations of referencing code elements from a `cref` attribute. However, the structure of the compiled output is more clear.

### Changes to input form

There are no changes to the lexical structure of the input or to symbol resolution when building the semantic model. When generating documentation comment IDs for the purpose of writing resolved references to the output, a new special case is provided for references which do not specify parameters or type parameters. In this case, when the reference resolves to more than one candidate symbol, all of which are methods, the compiler will no longer report CS0419.

Open questions:

* Is it possible to resolve multiple candidates in otherwise-valid code where one or more references resolve to non-method symbols?
* Does resolution of documentation comment references treat extension methods as extension methods, or only as static methods? If the former, are the results mixed with non-extension methods? If extension methods are included we likely need to limit resolution to overloaded members of the object type or report a warning if only extension members exist and they appear in more than one type.

### Changes to output form

The ID string format defined in the language specification is amended to include the following member kind:

> | Character | Description |
> | --- | --- |
> | O | Overloaded method group (containing one or more methods) |

In addition, the description of the format for methods and properties is modified to read as follows:

> For **single** methods and properties with arguments, the argument list follows, enclosed in parentheses. **For groups of more than one method, and for single methods** without arguments, the parentheses **and arguments** are omitted. The arguments are separated by commas. The encoding of each argument is the same as a CLI signature, as follows:

## Drawbacks
[drawbacks]: #drawbacks

<!-- Why should we *not* do this? -->

* This feature would result in certain code which compiles with a warning today producing no warnings and instead producing different output. Prior to this feature, a `cref` reference to a method group with multiple overloads would produce a warning, and the output documentation file would include a reference to one of the overloads.
* If this feature ignores the number of overloads when choosing between the use of `O:` and `M:`, some code which compiles today and produces `M:` outputs will instead compile with `O:` outputs. This is a form of breaking change, even though the breaking change is only visible in the presence of post-processing developer tools.

## Alternatives
[alternatives]: #alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

* Currently, [Sandcastle Help File Builder](https://github.com/EWSoftware/SHFB/) provides this functionality as a post-processing step for documentation. By including the `autoUpgrade="true"` attribute on `see` or `seealso` references, the tool will convert references to methods with references to the method group when multiple overloads exist.

## Unresolved questions
[unresolved]: #unresolved-questions

<!-- What parts of the design are still undecided? -->

* Should the `O:` prefix always be used when the parameter list is omitted from a `cref` reference, or should it only be used when multiple overloads exist?
* How do overloads with restricted accessibility play into the use of `O:` vs. other prefixes?
* How do extension methods play into the use of `O:` vs. other prefixes?

## Design meetings

<!-- Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to. -->

