# C# Language Design Notes for 2015

Overview of meetings and agendas for 2015


## Jan 21, 2015

[C# Design Meeting Notes for Jan 21, 2015](LDM-2015-01-21.md)

This is the first design meeting for the version of C# coming after C# 6. We shall colloquially refer to it as C# 7. The meeting focused on setting the stage for the design process and homing in on major themes and features.

1. Design process
2. Themes
3. Features


## Jan 28, 2015

[C# Design Meeting Notes for Jan 28, 2015](LDM-2015-01-28.md)

1. Immutable types
2. Safe fixed-size buffers
3. Pattern matching
4. Records


## Feb 4, 2015

[C# Design Meeting Notes for Feb 4, 2015](LDM-2015-02-04.md)

1. Internal Implementation Only (C# 6 investigation)
2. Tuples, records and deconstruction
3. Classes with value semantics


## Feb 11, 2015

[C# Design Meeting Notes for Feb 11, 2015](LDM-2015-02-11.md)

1. Destructible types <*we recognize the problem but do not think this is quite the right solution for C#*>
2. Tuples <*we like the proposal, but there are several things to iron out*>


## Mar 4, 2015

[C# Design Meeting Notes for Mar 4, 2015](LDM-2015-03-04.md)

1. `InternalImplementationOnly` attribute <*no*>
2. Should `var x = nameof(x)` work? <*no*>
3. Record types with serialization, data binding, etc. <*keep thinking*>
4. "If I had a [billion dollars](http://www.infoq.com/presentations/Null-References-The-Billion-Dollar-Mistake-Tony-Hoare)...": nullability <*daunting but worth pursuing*>


## Mar 10 and 17, 2015

[C# Design Meeting Notes for Mar 10 and 17, 2015](LDM-2015-03-10-17.md)

These two meetings looked exclusively at nullable/non-nullable reference types. I've written them up together to add more of the clarity of insight we had when the meetings were over, rather than represent the circuitous path we took to get there.

1. Nullable and non-nullable reference types
2. Opt-in diagnostics
3. Representation
4. Potentially useful rules
5. Safely dereferencing nullable reference types
6. Generating null checks


## Mar 18, 2015

[C# Design Meeting Notes for Mar 18, 2015](LDM-2015-03-18.md)

In this meeting we looked over the top [C# language feature requests on UserVoice](http://visualstudio.uservoice.com/forums/121579-visual-studio/category/30931-languages-c) to see which ones are reasonable to push on further in C# 7.

1. Non-nullable reference types (*already working on them*)
2. Non-nullary constructor constraints (*require CLR support*)
3. Support for INotifyPropertyChanged (*too specific; metaprogramming?*)
4. GPU and DirectX support (*mostly library work; numeric constraints?*)
5. Extension properties and static members (*certainly interesting*)
6. More code analysis (*this is what Roslyn analyzers are for*)
7. Extension methods in instance members (*fair request, small*)
8. XML comments (*Not a language request*) 
9. Unmanaged constraint (*requires CLR support*)
10. Compilable strings (*this is what nameof is for*)
11. Multiple returns (*working on it, via tuples*)
12. ISupportInitialize (*too specific; hooks on object initializers?*)
13. ToNullable (*potentially part of nullability support*)
14. Statement lambdas in expression trees (*fair request, big feature!*)
15. Language support for Lists, Dictionaries and Tuples (*Fair; already working on tuples*)

A number of these are already on the table.


## Mar 24, 2015

[C# Design Meeting Notes for Mar 24, 2015](LDM-2015-03-24.md)

In this meeting we went through a number of the performance and reliability features we have discussed, to get a better reading on which ones have legs. They end up falling roughly into three categories:

* Green: interesting - let's keep looking
* Yellow: there's something there but this is not it
* Red: probably not

As follows:

1. ref returns and locals <*green*> (#118)
2. readonly locals and parameters <*green*> (#115)
3. Method contracts <*green*> (#119)
4. Does not return <*green*> (#1226)
5. Slicing <*green*> (#120)
6. Lambda capture lists <*yellow - maybe attributes on lambdas*> (#117)
7. Immutable types <*yellow in current form, but warrants more discussion*> (#159)
8. Destructible types <*yellow - fixing deterministic disposal is interesting*> (#161)
9. Move <*red*> (#160)
10. Exception contracts <*red*>
11. Static delegates <*red*>
12. Safe fixed-size buffers in structs <*red*> (#126)

Some of these were discussed again, some we just reiterated our position.


## Mar 25, 2015 (Design Review)

[C# Language Design Review, Mar 25, 2015](LDM-2015-03-25-Design-Review.md)
[Additional Notes](LDM-2015-03-25-Notes.md)

We've recently changed gears a little on the C# design team. In order to keep a high design velocity, part of the design team meets one or two times each week to do detailed design work. Roughly monthly the full design team gets together to review and discuss the direction. This was the first such review.

1. Overall direction
2. Nullability features
3. Performance and reliability features
4. Tuples
5. Records
6. Pattern matching


## Apr 1 and Apr 8, 2015

[C# Design Meeting Notes for Apr 1 and Apr 8, 2015](LDM-2015-04-01-08.md)

Matt Warren wrote a Roslyn analyzer as a low cost way to experiment with nullability semantics. In these two meetings we looked at evolving versions of this analyzer, and what they imply for language design.

The analyzer is [here](https://github.com/mattwar/nullaby).


## Apr 14, 2015

[C# Design Meeting Notes for Apr 14, 2015](LDM-2015-04-14.md)

Bart De Smet visited from the Bing team to discuss their use of Expression Trees, and in particular the consequences of their current shortcomings.

The Expression Tree API today is not able to represent all language features, and the language supports lambda conversions even for a smaller subset than that.


## Apr 15, 2015

[C# Design Meeting Notes for Apr 15, 2015](LDM-2015-04-15.md)

In this meeting we looked at nullability and generics. So far we have more challenges than solutions, and while we visited some of them, we don't have an overall approach worked out yet.

1. Unconstrained generics
2. Overriding annotations
3. FirstOrDefault
4. TryGet


## Apr 22, 2015 (Design Review)

[C# Language Design Review, Apr 22, 2015](LDM-2015-04-22-Design-Review.md)

1. Expression tree extension
2. Nullable reference types
3. Facilitating wire formats
4. Bucketing


## May 20, 2015

[C# Design Meeting Notes for May 20, 2015](LDM-2015-05-20.md)

1. We discussed whether and how to add local functions to C#, with the aim of prototyping the feature in the near future.


## May 25, 2015

[C# Design Meeting Notes for May 25, 2015](LDM-2015-05-25.md)

Today we went through a bunch of the proposals on GitHub and triaged them for our list of features. 


## Jul 1, 2015

[C# Design Meeting Notes for Jul 1, 2015](LDM-2015-07-01.md)

We are gearing up to prototype the tuple feature, and put some stakes in the ground for its initial design. This doesn't mean that the final design will be this way, and some choices are arbitrary. We merely want to get to where a prototype can be shared with a broader group of C# users, so that we can gather feedback and learn from it.


## Jul 7, 2015

[C# Design Meeting Notes for Jul 7, 2015](LDM-2015-07-07.md)

With Eric Lippert from Coverity as an honored guest, we looked further at the nullability feature.

1. Adding new warnings
2. Generics and nullability


## Aug 18, 2015

[C# Design Meeting Notes for Aug 18, 2015](LDM-2015-08-18.md)

A summary of the design we (roughly) landed on in #5031 was put out on GitHub as #5032, and this meeting further discussed it.

1. Array creation
2. Null checking operator
3. Generics


## Sep 1, 2015

[C# Design Meeting Notes for Sep 1, 2015](LDM-2015-09-01.md)

The meeting focused on design decisions for prototypes. There's no commitment for these decisions to stick through to a final feature; on the contrary the prototypes are being made in order to learn new things and improve the design.

1. ref returns and ref locals
2. pattern matching


## Sep 2, 2015

[C# Design Meeting Notes for Sep 2, 2015](LDM-2015-09-02.md)

1. Extending nameof
2. What do branches mean
3. Supersedes


## Sep 8, 2015

[C# Design Meeting Notes for Sep 8, 2015](LDM-2015-09-08.md)

1. Check-in on some of our desirable features to see if we have further thoughts
2. Start brainstorming on async streams


## Oct 7, 2015 (Design Review)

[Preparatory Notes on Records and Pattern Matching for Oct 7, 2015 Design Review](LDM-2015-10-07-Design-Review.md)

## Nov 2, 2015 (Design Demo)

[Outline of demo at the Nov 2, 2015 MVP summit](LDM-2015-11-02-Design-Demo.md)

