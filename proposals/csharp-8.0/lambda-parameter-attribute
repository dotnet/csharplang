# lambda-parameter-attribute

* [x] Proposed
* [ ] Prototype: [Complete](https://github.com/PROTOTYPE_OWNER/roslyn/BRANCH_NAME)
* [ ] Implementation: [In Progress](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

The parameters of delegate and lambda cannot be marked with attributes, making many attributes unavailable.
I hope the parameters of delegate and lambda support attribute.

```
(string s, [RequiredService]person p) =>{}
```

## Motivation
[motivation]: #motivation

I have a demo of dependency injection here. I hope that the lambda parameter can be marked with RequiredServiceAttribute,so that my program throws an exception when it is injected, rather than at runtime. 
[Sample code](https://github.com/dotnet/csharplang/issues/3653#issuecomment-655245739)

Hope to support the format:

```
test.Use<IPerson, ITeacher>((s, [RequiredService]p) =>{})
```

## Detailed design
[design]: #detailed-design

The same as the method, only need to support marking attribute on the parameter, example:
1. Method writing:

```
     public static void Add([My]object obj )
        {

        }
```

2. Convert the method of Example 1 to lambda writing:

```
      Action<object> action=([My]o) => { }
```

## Drawbacks
[drawbacks]: #drawbacks

No shortcomings, just enhance lambda and delegate.
Make it closer to the method.

## Alternatives
[alternatives]: #alternatives


## Unresolved questions
[unresolved]: #unresolved-questions


## Design meetings



