# Union implementation challenges

This heavily references https://github.com/dotnet/csharplang/blob/main/proposals/TypeUnions.md.

## Proposal: start with union classes

The four kinds of unions outlined in [TypeUnions.md](https://github.com/dotnet/csharplang/blob/main/proposals/TypeUnions.md) do not need to be viewed as alternatives - any or all of them could coexist nicely at the language level.

However, some of them have significant challenges at the implementation level, which may need to some hard choices being made at the language level. Some of these challenges are outlined below. 

The one approach that seems pretty straightforward from an implementation view point is [Union classes](https://github.com/dotnet/csharplang/blob/main/proposals/TypeUnions.md#standard---union-classes). We also estimate that it has high value on its own, addressing a large chunk of scenarios. We therefore propose that we double down on language design and implementation for this direction, with the likely outcome of it arriving in C# before the others.

Along the way we can still try to make progress on the others.

## Challenges with union structs

[Union structs](https://github.com/dotnet/csharplang/blob/main/proposals/TypeUnions.md#specialized---union-structs) are an alternative to class structs where the union value doesn't need to be allocated, which can be a performance benefit.

However, the way class unions represent the different cases is through inheritance, and structs do not have that. The most straightforward way for a union struct to be able to represent all its different cases is for it to have a field for each of them, as well as a `Kind` field indicating which of the cases the current value belongs to.

This easily leads to large structs, with a lot of copying when values are passed around, and a lot of wasted memory, since all but one of the case fields is empty. There are ways of compacting the representation, e.g. by overlapping fields, but the more you do, the more time is spent packing and unpacking values. This starts to offset the benefits of avoiding allocation.

If compaction uses unsafe techniques, the runtime might get confused and turn off its own optimizations. Runtime work to address this directly would likely be costly.

Any representation also needs to deal gracefully with evolution of unions. If a new member is added, and that causes the representation to change materially, will existing compiled code continue to work correctly? The public representation of a union struct needs to be stable against recompilation.

## Challenges with ad-hoc unions

[Ad-hoc unions](https://github.com/dotnet/csharplang/blob/main/proposals/TypeUnions.md#ad-hoc---ad-hoc-unions) are anonymous type expressions combining other types.

The most obvious way to implement ad-hoc unions is via erasure: the compiler simply replaces occurrences of any ad-hoc union with `object` (or perhaps a common base type of the constituent types), and adds some metadata to public signatures to describe what the types "really" are. This is the same approach we use for `dynamic`, tuple names and nullable annotations.

This mostly works! However, it isn't quite safe at runtime. The most confounding problem is when ad-hoc unions are used as type parameters. Imagine this type:

``` c#
public class MyCollection<T>
{
    public bool TryAdd(object o)
    {
        if (o is T t)
        {
            // add t
            return true;
        }
        else return false;
    }
    ...
}
```

If you use an ad-hoc union as the type argument, it will be erased to `object` and the type check in `TryAdd` will always succeed, violating the type safety of the collection!

An alternative is to not erase, and instead have implementation types such as `ValueUnion<T1, T2>` etc. However, this has semantic consequences: Now `(string or bool)` will not be the same type as `(bool or string)`! We've investigated runtime approaches to dealing with this, but they are imperfect and very expensive!
