# Improved overload candidates

## Summary
[summary]: #summary

The overload resolution rules have been updated in nearly every C# language update to improve the experience for programmers, making ambiguous invocations select the "obvious" choice. This has to be done carefully to preserve backward compatibility, but since we are usually resolving what would otherwise be error cases, these enhancements usually work out nicely.

1. When a method group contains both instance and static members, we discard the instance members if invoked without an instance receiver or context, and discard the static members if invoked with an instance receiver. When there is no receiver, we include only static members in a static context, otherwise both static and instance members. When the receiver is ambiguously an instance or type due to a color-color situation, we include both. A static context, where an implicit this instance receiver cannot be used, includes the body of members where no this is defined, such as static members, as well as places where this cannot be used, such as field initializers and constructor-initializers.
2. When a method group contains some generic methods whose type arguments do not satisfy their constraints, these members are removed from the candidate set.
3. For a method group conversion, candidate methods whose return type doesn't match up with the delegate's return type are removed from the set.
