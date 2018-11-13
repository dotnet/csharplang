# Capture returned object

* [x] Proposed
* [ ] Prototype: [Complete](https://github.com/PROTOTYPE_OWNER/roslyn/BRANCH_NAME)
* [ ] Implementation: [In Progress](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

Having a way to easily capture and edit an object without having to write lot of useless code

```c#
world.getComponent<CLocation>(id) { // call only if not null
	x = 5, y = 5
}
```

## Motivation
[motivation]: #motivation

I often have to write lot of duplicate code to achieve simple stuff

```c#
var location = world.getComponent<CLocation>(id);
if(location != null)
{
	location.x = 5;
	location.y = 5;
}
```

Kotlin have something similar

```kotlin
    getComponent<CPosition>().apply {
        x = 5
        y = 5
    }
```

## Detailed design
[design]: #detailed-design

- TBD -


## Drawbacks
[drawbacks]: #drawbacks

- TBD -

## Alternatives
[alternatives]: #alternatives

I can write extention methods or methods to init fields inside the ``CLocation`` class but i'm following ECS principles and DOD, so no logic in components, and it'll just bloat my classes, i'll then have to have multiple init methods for different fields wich isn't C# way to doing things


It is better to be a language feature

## Unresolved questions
[unresolved]: #unresolved-questions

- nothing

## Design meetings

- none
