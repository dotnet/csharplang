# Compromise design for extensions

## Summary
[summary]: #summary

This proposal mixes the type- and member-based approaches to extension declarations: It combines a type-based approach for all member kinds with a member-based syntax for member-specific annotations and backwards-compatible extension method declarations.

## Motivation
[motivation]: #motivation

We're currently facing the conundrum that the type-based approach to extensions does not provide a compatible way for existing extension methods to move forward into new syntax, whereas the member-based approach imposes a high syntactic burden on new extension members (both methods and others) that do not need such compatibility.

This proposal tries to bring the best of the two approaches together without creating too many different ways of doing the same thing. At its core it uses the type-based approach, where type parameters and a parameter-like "for-clause" with the receiver type are specified on the extension type declaration. Non-method extension members cannot declare type parameters. Extension members _can_ specify for-clauses of their own, but, with one exception, only to add additional annotations such as attributes and ref-ness modifiers that are specific to how this particular member interacts with the receiver.

The one exception is "compatible extension methods". These are extension methods that _do_ override the for-clause of the extension declaration. Such extension methods generate static methods that are guaranteed to be fully source and binary compatible with corresponding classic extension methods.

While the proposal encompasses core tenets of both type-based and member-based proposals, it also loses some expressiveness from both. Compared to pure member-based proposals, it doesn't allow the general grouping of extension members with different receiver types into the same extension declaration. There isn't a proposal on the table that achieves that without raising the as-yet unaddressed issue of type parameters and type arguments for member kinds other than members.

On the type-based side the proposal loses the proposed implicit "this-style" parameter passing mode, which automatically treats the receiver as a value parameter when it is of a reference type, and as a reference parameter when it is of a value type. While some explicit version of this may be possible to add, doing it implicitly likely introduces too much of an arbitrary behavior difference with classic extension methods.

## Detailed design
[design]: #detailed-design

There are many variations on what a merged syntax could look like. This is one attempt, but pretty much anything is debatable!

``` antlr
extension_declaration
    : attributes? extension_modifier* 'partial'? 'extension' identifier
        type_parameter_list? for_clause? type_parameter_constraints_clause*
        extension_body ';'?
    ;
    
extension_modifier
    : 'new'
    | 'public'
    | 'protected'
    | 'internal'
    | 'private'
    | 'sealed'
    | 'static'
    | unsafe_modifier   // unsafe code support
    ;
    
for_clause
    : 'for' attributes? extension_mode_modifier? type
    ;
    
extension_mode_modifier:
    | 'ref'
    | 'ref readonly'
    | 'in'
    ;

extension_body
    : '{' extension_member_declaration* '}'
    ;
    
extension_member_declaration
    : constant_declaration
    | field_declaration
    | extension_method_declaration
    | extension_property_declaration
    | extension_event_declaration
    | extension_indexer_declaration
    | operator_declaration
    | constructor_declaration
    | static_constructor_declaration
    | type_declaration
    ;
    
extension_method_declaration
    : attributes? method_modifiers return_type extension_method_header method_body
    | attributes? ref_method_modifiers ref_kind ref_return_type extension_method_header
      ref_method_body
    ;
    
extension_method_header
    : member_name '(' formal_parameter_list? ')' method_for_clause?
    | member_name type_parameter_list '(' formal_parameter_list? ')' method_for_clause?
      type_parameter_constraints_clause*
    ;

method_for_clause
    : for_clause identifier?
    ;
    
extension_property_declaration
    : attributes? property_modifier* type member_name for_clause? property_body
    | attributes? property_modifier* ref_kind type member_name for_clause? ref_property_body
    ;    

extension_event_declaration
    : attributes? event_modifier* 'event' type member_name for_clause?
        '{' event_accessor_declarations '}'
    ;

extension_indexer_declaration
    : attributes? indexer_modifier* indexer_declarator for_clause? indexer_body
    | attributes? indexer_modifier* ref_kind indexer_declarator for_clause? ref_indexer_body
    ;
```

An `extension_method_declaration` with a `for_clause` that includes an `identifier` is called a "_compatible extension method_."

The following syntactic restrictions apply to `extension_declaration`s:

- `field_declaration`s must be `static`.
- `extension_member_declaration`s may not have a `new`, `protected`, `virtual`, `sealed`, `override` or `abstract` modifier.
- `extension_member_declaration`s with a `static` modifier may not have a `for_clause`.
- Compatible extension methods may not occur within a generic `extension_declaration`.
- An `extension_declaration` may omit its `for_clause` only if all its member declarations are compatible extension methods.
- An accessor body of a non-static `extension_property_declaration` may not be `;` and may not use `field` keyword.

The `for_clause` of any `extension_member_declaration` other than a compatible extension method must specify a `type` that is identity-convertible to that of the enclosing extension declaration's `for_clause`.

The `identifier` of a compatible extension method's `method_for_clause` may not be referenced within the method body, but is added to the local variable declaration space of the method body to prevent other declarations from using it.

Within the bodies of all non-static `extension_member_declaration`s, the receiver is referred to by the keyword `this`. 

Compatible extension method declarations implicitly generate a static method with the same signature, except that the `method_for_clause` is used as an additional first parameter. Within the body of the generated method, implicit and explicit occurrences of `this` are replaced with the identifier from the `method_for_clause`.

Other extension member declarations do not implicitly generate additional members that are visible at the language level. Their lowering is an implementation detail, and does not affect language level semantics.

The `extension_mode_modifier` is interpreted the same way as a `parameter_mode_modifier`, and determines the parameter passing mode of `this` in the body of non-static extension members. There is no proposed way to specify the "mixed" parameter passing mode that applies to `this` within instance members in classes (by value) and structs (by ref).

This proposal is for declaration syntax and its meaning. It does not take a stance on lookup, type inference and overload resolution, but can likely accommodate most variations under discussion.


## Examples

``` c#
// Different kinds of extension members
public extension E for C
{
    // Instance members - assume f is an accessible instance field on C
    public string P { get => f; set => f = value.Trim(); }         // Property
    public T M<T>() where T : IParsable<T> => T.Parse(f, default); // Method
    public char this[int index] => f[index];                       // Indexer
    public C(string f) => this.f = f;                              // Constructor

    // Static members
    public static int ff = 0;                                // Static field
    public static int PP { get; set => field = Abs(value); } // Static property
    public static C MM(string s) => new C(s);                // Static method
    public static C operator +(C c1, C c2) => c1.f + c2.f;   // Operator
    public static implicit operator C(string s) => new C(s); // UD conversion
}

// Type- and member-level for-clauses for attributes, nullability and ref-ness
public extension NullableStringExtensions for string?
{
    public bool IsNullOrEmpty for [NotNullWhen(false)] string? 
        => this is null or [];
    public string AsNotNull => this is null ? "" : this;
    public void MakeNotNull for [NotNull] ref string? => this ??= "";
}

// Core LINQ methods as a non-compatible generic extension
public extension Enumerable<T> for IEnumerable<T>
{
    public IEnumerable<T> Where(Func<bool> predicate) { ... }
    public IEnumerable<TResult> Select<TResult>(Func<T, TResult> selector) { ... }
}

// Core LINQ methods as compatible extension methods
public extension Enumerable
{
    public IEnumerable<TResult> Select<TSource, TResult>(Func<TSource, TResult> selector) for IEnumerable<TSource> source { ... }
    public IEnumerable<TSource> Where<TSource>(Func<TSource, bool> predicate) for IEnumerable<TSource> source { ... }
}
```

## Drawbacks
[drawbacks]: #drawbacks

- As a compromise, this proposal may be less principled and conceptually clear to users.
- The proposal does not allow grouping of extension members with different receiver types (except for compatible extension methods). The same is true for any proposal that puts type parameters on the extension declaration itself.
- The proposal takes a parameter-like view of the underlying type, and therefore does not allow - let alone default to - a "this-style" mixed parameter passing modes for the receiver that depends on whether the receiver is a reference or a value type. 

## Alternatives
[alternatives]: #alternatives

There are many possible variations on the details of the proposed syntax, restrictions and semantics. Here are a few:

- For-clauses are syntactically identical between the type and the member levels. This underscores their "parameter-ness" and supports the notion that member-level clauses just override type-level ones. Another approach would be to let the type-level for-clause only specify a type, and leave any other detail to member-level ones.
- Compatible extension methods have a subtle syntactic marker in the form of an identifier - a parameter name - on their for-clause. This attempts to strike a balance of making them blend in with the new syntax, while still giving the user a way to express their intent. One could argue for removing this distinction, e.g. by allowing parameter names on all for-clauses. Determining whether a method is compatible would then depend on syntactic and semantic context. At the other end, the distinction could be made more pronounced, e.g. by putting parentheses around the for-clause elements of a compatible extension method.
- Non-static extension member bodies refer to the receiver with the keyword `this`. We could make receivers even more parameter-like by letting them have a parameter name as part of the for-clause, and then using that name in the bodies. This would be more verbose of course, both because of the added parameter names, and because bodies cannot make use of implicit `this.` to refer to fellow members with simple names.
- Even compatible extension methods, which do have parameter names in this proposal, use `this` in the body. That choice was made for greater syntactic uniformity, but one could argue that compatible extension methods should be more like classic extension methods in this regard.
- The proposal disallows member-level specification of the receiver type (except to modify a type-level one for nullability etc.) for non-method members on the grounds that, since those member kinds can't have type parameters, they can't specify open generic types as receiver types in their for-clauses, and then it's cleaner that they can't specify receiver types at all. That line could be drawn more leniently to allow closed types. About 25% of current extension methods are on open generic types.

## Unresolved questions
[unresolved]: #unresolved-questions

This proposal is only about declaration syntax and its meaning. There are many decisions, especially around lookup, type inference and overload resolution, that lie ahead. Since those felt largely orthogonal - or at least secondary - to the syntax, they've been left for later.
