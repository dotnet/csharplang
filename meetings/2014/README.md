# C# Language Design Notes for 2014

Overview of meetings and agendas for 2014

## Jan 6, 2014

[C# Language Design Notes for Jan 6, 2014](LDM-2014-01-06.md)

1.	Syntactic ambiguities with declaration expressions <_a solution adopted_>
2.	Scopes for declaration expressions <_more refinement added to rules_>

## Feb 3, 2014

[C# Language Design Notes for Feb 3, 2014](LDM-2014-02-03.md)

1.  Capture of primary constructor parameters <_only when explicitly asked for with new syntax_>
2.  Grammar around indexed names <_details settled_>
3.  Null-propagating operator details <_allow indexing, bail with unconstrained generics_>


## Feb 10, 2014

[C# Language Design Notes for Feb 10, 2014](LDM-2014-02-10.md)

1.	Design of using static <_design adopted_>
2.	Initializers in structs <_allow in certain situations_>
3.	Null-propagation and unconstrained generics <_keep current design_>


## Apr 21, 2014

[C# Language Design Notes for Apr 21, 2014](LDM-2014-04-21.md)

1.	Indexed members <_lukewarm response, feature withdrawn_>
2.	Initializer scope <_new scope solves all kinds of problems with initialization_>
3.	Primary constructor bodies <_added syntax for a primary constructor body_>
4.	Assignment to getter-only auto-properties from constructors <_added_>
5.	Separate accessibility for type and primary constructor <_not worthy of new syntax_>
6.	Separate doc comments for field parameters and fields <_not worthy of new syntax_>
7.	Left associative vs short circuiting null propagation <_short circuiting_>


## May 7, 2014

[C# Language Design Notes for May 7, 2014](LDM-2014-05-07.md)

1.	protected and internal <_feature cut – not worth the confusion_>
2.	Field parameters in primary constructors <_feature cut – we want to keep the design space open_>
3.	Property declarations in primary constructors <_interesting but not now_>
4.	Typeswitch <_Not now – more likely as part of a future more general matching feature_>


## May 21, 2014

[C# Language Design Notes for May 21, 2014](LDM-2014-05-21.md)

1.	Limit the nameof feature? <_keep current design_>
2.	Extend params IEnumerable? <_keep current design_>
3.	String interpolation <_design nailed down_>


## Jul 9, 2014

[C# Language Design Notes for Jul 9, 2014](LDM-2014-07-09.md)

1.	Detailed design of nameof <_details settled_>
2.	Design of #pragma warning extensions <_allow identifiers_>


## Aug 27, 2014

[C# Language Design Notes for Aug 27, 2014](LDM-2014-08-27.md)

1.	Allowing parameterless constructors in structs <_allow, but some unresolved details_>
2.	Definite assignment for imported structs <_revert to Dev12 behavior_>


## Sep 3, 2014

[C# Language Design Notes for Sep 3, 2014](LDM-2014-09-03.md)

1.	Removing “spill out” from declaration expressions in simple statements <_yes, remove_>
2.	Same name declared in subsequent else-if’s <_condition decls out of scope in else-branch_>
3.	Add semicolon expressions <_not in this version_>
4.	Make variables in declaration expressions readonly <_no_>

## Oct 1, 2014

[C# Language Design Notes for Oct 1, 2014](LDM-2014-10-01.md)

1. Assignment to readonly autoprops in constructors (we fleshed out details)
2. A new compiler warning to prevent outsiders from implementing your interface? (no, leave this to analyzers)


## Oct 15, 2014

[C# Language Design Notes for Oct 15, 2014](LDM-2014-10-15.md)

1. nameof operator: spec v5
2. [String Interpolation for C#](http://1drv.ms/1tFUvbq)
