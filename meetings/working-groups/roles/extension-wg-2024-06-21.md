# Extensions WG 2024-06-21

- update on instance invocation
- open issue: what kind of type is an extension type?
- spec: conversions, operators, adapting to existing type kind rules, any updates from design review (Mads)

# Instance invocation

See description in https://github.com/dotnet/roslyn/pull/74012

We'll have follow-up issue for nullability of the extra parameter.  
We'll have follow-up issue for capturing the receiver when it is a type parameter.  

Note: When implementing interfaces, a modopt(ExtensionAttribute) could be brought into the picture and cause a conflict/ambiguity.

## Open issue: what kind of type is an extension type?

Many sections of the spec need to consider the kind of type. Consider a few examples:  
- the spec for a conditional element access `P?[A]B`
considers whether `P` is a nullable value type. So it will need to handle the case
where `P` is an instance of an extension type on a nullable value type,
- the spec for an object creation considers whether the type is
a value_type, a type_parameter, a class_type or a struct_type,
- the spec for satisfying constraints also consider what kind of type were dealing with.

It may be possible to address all those cases without changing each such section of the spec,
but rather by adding general rules ("an extension on a class type is considered a class type" or some such).

1. extension type on class type is a class type, on a struct type is a struct type, on a type parameter is considered a type parameter, ... (not sure how to word this, maybe "for purpose of")
2. but nullable value type needs to be handled case by case. System.Nullable<...>
3. are there other cases we need to consider (keep an eye out)

explicit extension E<T> for T { void M(); }
new C().M();
new S().M();



