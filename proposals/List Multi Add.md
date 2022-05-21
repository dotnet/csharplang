# List MultiAdd

* [x] Proposed
* [ ] Prototype: [Complete](https://github.com/PROTOTYPE_OWNER/roslyn/BRANCH_NAME)
* [ ] Implementation: [In Progress](https://github.com/dotnet/roslyn/BRANCH_NAME)
* [ ] Specification: [Not Started](pr/1)

## Summary
[summary]: #summary

A simple addition to the List<T>.Add() or the List<T>.AddRange() Method allowing for a `params T[] items` to be used to add a number of additional items not already contained in a list.

## Motivation
[motivation]: #motivation

Helps to write cleaner code, allowing writers to add a number of items to a list with only one call.

## Detailed design
[design]: #detailed-design
  
A concept of this design is this (using an extension method for readability)
```cs
public static void Add(this List<T> list, params T[] items) {
  for(int i=0;i<items.Length;i++) {
    list.Add(items[i]);
  }
}
```

A writer would use this feature as follows
```cs
List<int> ints = new ();
// Do other things with the list.
ints.Add(0,4,5,3,5); or ints.AddRange(0,4,5,3,5);
```

## Drawbacks
[drawbacks]: #drawbacks
At this time, there does not appear to be any drawbacks to implementing this design.

## Alternatives
[alternatives]: #alternatives
The impact of not doing this would simply mean that writers wanting to use this functionality would have to roll out an extension method of their own.

## Unresolved questions
[unresolved]: #unresolved-questions
Whether or not this functionality would be more clear under AddRange or if Add is sufficient.
