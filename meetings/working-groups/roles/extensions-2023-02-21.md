# Extensions 2023-02-21

Since the latest syntax proposal is `implicit`/`explicit` `extension`, we're going
to refer to the feature as "extensions" or "extension types", rather than "roles".

## Allow omitting `implicit` or `explicit`?

Without `implicit` or `explicit`, `extension` would likely mean `implicit`.  
But that view is not obvious. There's a historical view where extensions come first
and roles follow, but there's also a view where roles are fundational and extensions
are roles plus implicit lookup rules...  
For now, we're leaning to require `implicit`/`explicit`. We can decide to relax this 
and pick what the default means later.

## When to keep using extension methods?

Overall, extension types are more powerful than extension methods.  
But we could think of two scenarios where one could prefer to use extension methods:
1. Extending multiple times whilst sharing code:
  ```
  void M(this OneType one)
  void M(this OtherType other)
  ```
2. Targeting a framework that doesn't support ref fields

## Types may not be called "extension" 

Although there's no grammatical ambiguity (especially since we decided to require 
`implicit`/`explicit` before `extension`), it feels simpler from a language and
implementation point of view to disallow types named "extension".

Here are some productions to consider:  
`implicit extension E`  
`explicit extension E`  
`implicit operator`  
`explicit operator`  
`explicit I.operator(...)`  
`explicit extension.operator(...)` // reserving keyword helps a bit

Options:
1. add an error
2. existing warning wave is sufficient

Decision: let's go with (1).

## Allow optional underlying type?

There are two scenarios where omitting the underlying type could be useful:
1. partial scenarios, where the underlying type is derived from other parts  
  `partial implicit extension R { }`
2. deriving from other extension types, where the underlying type could be derived from the base extensions (somehow)  
  `implicit extension R : R1, R2` 

Decision: we definitely see value in (1), but are not yet sure about (2). We can add that support later.
Let's make room in the syntax and support (1) for now.

## Explicit identity conversion for sideways conversions?

In the following example, should we give some kind of type safety so that 
instances of `CustomerExtension` and `EmailExtension` don't mix?  
```
explicit extension CustomerExtension for ReadOnlySpan<byte> : PersonExtension
explicit extension EmailExtension for ReadOnlySpan<byte>
```

Options:
1. implicit identity conversion all the way
2. implicit up-down, but explicit sideways (Customer<->Email requires explicit conversion).  
  2A. TODO: do we have an explicit identity conversion or an implicit one that warns? 
3. distinguish between up and down, ... 
4. explicit all the way

There's some benefits of such type safety, so we're interested to explore this more.   
But the notion of "explicit identity conversion" is problematic. Maybe we could spec this 
as having an implicit conversion that warns?  

There may be value in distinguishing identity conversions up and down as well.  
There could be validation scenarios on the way "down". This would need further exploration. How 
does a type opt into such validation? We couldn't use the presence of an explicit conversion operator,
since a user-defined conversion operator is problematic for an identity conversion (it would break the
identity invariant).  
A "validator" would have to be bool-returning. If the validation succeeds, then we handle the conversion.  
This could come into play into patterns (almost like active patterns).  

Decision: let's start with (2), consider "validator" for later on. 
