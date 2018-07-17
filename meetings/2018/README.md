# C# Language Design Notes for 2018

Overview of meetings and agendas for 2018


## Jan 3, 2018
[C# Language Design Notes for Jan 3, 2018](LDM-2018-01-03.md)

1. Scoping of expression variables in constructor initializer
2. Scoping of expression variables in field initializer
3. Scoping of expression variables in query clauses
4. Caller argument expression attribute
5. Other caller attributes
6. New constraints


## Jan 10, 2018
[C# Language Design Notes for Jan 10, 2018](LDM-2018-01-10.md)

1. Ranges and endpoint types


## Jan 18, 2018 
[C# Language Design Notes for Jan 18, 2018](LDM-2018-01-18.md)

We discussed the range operator in C# and the underlying types for it.

1. Scope of the feature
2. Range types
3. Type name
4. Open-ended ranges
5. Empty ranges
6. Enumerability
7. Language questions


## Jan 22, 2018
[C# Language Design Notes for Jan 22, 2018](LDM-2018-01-22.md)

We continued to discuss the range operator in C# and the underlying types for it.

1. Inclusive or exclusive?
2. Natural type of range expressions
3. Start/length notation


## Jan 24, 2018
[C# Language Design Notes for Jan 24, 2018](LDM-2018-01-24.md)

1. Ref reassignment
2. New constraints
3. Target typed stackalloc initializers
4. Deconstruct as ref extension method

## July 9, 2018
[C# Language Design Notes for July 9, 2018](LDM-2018-07-09.md)

1. `using var` feature
   1. Overview
   2. Tuple deconstruction grammar form
   3. `using expr;` grammar form
   4. Flow control safety
2. Pattern-based Dispose in the `using` statement
3. Relax Multiline interpolated string syntax (`$@`)

# July 11, 2018
[C# Language Design Notes for July 11, 2018](LDM-2018-07-11.md)

1. Controlling nullable reference types with feature flags
1. Interaction with NonNullTypesAttribute
1. Feature flag and 'warning waves'
1. How 'oblivious' null types interact with generics
1. Nullable and interface generic constraints

# July 16, 2018
[C# Language Design Notes for July 16, 2018](LDM-2018-07-16.md)

1. Null-coalescing assignment
   1. User-defined operators
   1. Unconstrained type parameters
   1. Throw expression the right-hand side
1. Nullable await
1. Nullable pointer access
1. Non-nullable reference types feature flag follow-up
