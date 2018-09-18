# "const var"

* [ ] Proposed
* [ ] Prototype
* [ ] Implementation
* [ ] Specification

## Summary
[summary]: #summary

Allow `const` to be used with inferred "var" local variable declaration.
"const var" declarations would need to follow all the rules of normal "const" declarations, but would have their type inferred
in the same manner that inferred local variables are currently inferred.

## Motivation
[motivation]: #motivation

Many developers like "var" and prefer to use it as the defacto way of declaring all, or the majority, of their local variables.  
There are even IDE options to enforce this style of development.  However, if the developer wants to make their code stricter,
by using "const" on a local variable, they are forced into having to specify the type of the local, even though it can still
be inferred.

## Detailed design
[design]: #detailed-design

Update the grammar of C# from the following:

```
local_constant_declaration
    : 'const' type constant_declarators
    ;
```

To:

```
local_constant_declaration
    : 'const' local_variable_type constant_declarators
    ;
```

Note that ```local_variable_type``` is already defined as:

```
local_variable_type
    : type
    | 'var'
    ;
```

## Drawbacks
[drawbacks]: #drawbacks

One drawback is any confusion that might arise from somone saying "var" means "can vary".  "How can it vary, if it is const". 
In that interpretation, this combination seems weird.  However, as 'var' simply means 'type is inferred from initializer' there
is no problem combining 'const' with 'var'.

## Alternatives
[alternatives]: #alternatives

Other variants considered:

We can also introduce (or replace the above) with:

```
local_constant_declaration
    : 'const' local_variable_type constant_declarators
    : 'const' constant_declarators
    ;
```

this would allow code of the form "const i = 0".  This has the benefit of being just as expression, while also being quite brief. 
People who do not want to have to specify the type can use "var v = ..." when they do not have a constant, "const c = ..." when
they have a constant, and possibly "let y = ..." in the future if they have readonly variable.  There is a nice symmetry here.

However, importantly, this oterh variant is *complimentary* to the main proposal ("const var").  "const var" simply removes a
restriction that does not seem necessary or beneficial to the language.  We can still, in the future, consider increased brevity
forms to cut things down even more.

## Unresolved questions
[unresolved]: #unresolved-questions

n/a

## Design meetings

n/a

## Implementation

Implementation can be found here: https://github.com/dotnet/roslyn/pull/21149
