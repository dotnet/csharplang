# Expanding ref support

## Summary

This proposal expands the capabilities of `ref` and `scoped` in the language. The goal being to leverage the existing types of rules in the model to allow `ref struct` usage in more locations and provide more lifetime expressiveness for APIs.

## Motivation

There are still a number of scenarios around `ref` which cannot be safely expressed in the language. These are generally when using multiple mutable `ref struct` parameters where many are passed by `ref` or when trying to use `ref struct` in `ref` fields.

To _fully_ satisfy all of these scenarios would require us to introduce explicit lifetime parameters and relationships into the language. That is a _huge_ investment that is not yet motivated by need. Instead this proposal takes our existing lifetime annotation, `scoped`, and sees how much further `ref` safety can be taken without introducing any other annotations or keywords.

This doesn't solve all scenarios but does remove several known friction points in the language. It also serves to show us exactly where the limits are without introducing explicit lifetime parameters.

## Detailed Design

The rules for `ref struct` safety are defined in the following documents:

- [ref safety proposal](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/span-safety.md).
- [ref fields proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/low-level-struct-improvements.md)

This proposal will be building on top of those previous ones.

The more detailed rules will rely on the [annotation syntax](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/low-level-struct-improvements.md#annotations) to describe the detailed rules. This is the most direct way to discuss how syntax behaves in the greater model. Readers interested in the very low level details should familiarize themselves with that syntax before digesting this proposal.

### ref scoped parameters

The language will allow for parameters to be declared as `ref scoped`. This will serve to constrain the _safe-to-escape_ of the value such that it cannot be returned from the current method.

```csharp
Span<int> M(Span<T> p1, ref scoped Span<int> p2)
{
    // Error: cannot return scoped value
    return p2;

    // Error: the safe-to-escape of p1 is not convertible to p2.
    p2 = p1;

    // Okay: heap can always be assigned
    p2 = default;

    // Okay
    p2[0] = 42;
}
```

This capability will help cases where multiple `ref struct` values with different lifetimes are passed by `ref`. Having `ref scoped` allows developers to note which values do not escape and that allows for more call site flexibility.

```csharp
ref struct Data { ... }
void Copy1(ref Data source, ref Data dest) { ... }
void Copy2(ref Data source, ref scoped Data dest) { ... }

void Use(ref Data data)
{
    // STE: current method
    var local = new Data(stackalloc int[42]);

    // Error: compiler has to assume local copied to data 
    Copy1(ref data, ref local);

    // Okay: compiler knows lifetime only flows data -> local
    Copy2(ref data, ref local);
}

```

This is accomplished by giving every `ref scoped` parameter a new escape scope named _current parameter N_ where _N_ is the numeric order of the parameter. For example the first parameter has a _safe-to-escape_ of _current parameter 1_. An escape scope of _current parameter N_ can be converted to _current method_ but has no other defined relationship. That serves to restrict their usage to the current method.

It's important to note each parameter has a different _current parameter N_ scope. That means they cannot be assigned to each other. This is necessary to prevent `ref scoped` parameters from returning each others data.

```csharp
void Swap(ref scoped Span<int> p1, ref scoped Span<int> p2)
{
    // Error: can't assign current parameter 2 to current parameter 1
    p2 = p1;

    // Error: can't assign current parameter 1 to current parameter 2
    p1 = p2;

    // Okay: as current parameter 1 and 2 can be converted to current method
    scoped Span<int> local1 = p1; 
    scoped Span<int> local2 = p2; 

    // Okay: however the safe-to-escape here is current parameter N, not 
    // current method so this could cause a bit of confusion later on
    Span<int> local3 = p1; 
    Span<int> local4 = p2; 

    // Okay: the safe-to-escape of the value is inferred in this case as it is 
    // done for ref locals today.
    ref Span<int> refLocal1 = ref p1;
    ref Span<int> refLocal2 = ref p2;
}
```

A `ref scoped` parameter is also implicitly `scoped ref`. That means neither the value nor its `ref` can be returned from the method. Both `ref` and `in` parameters can have their values modified with `scoped`. An `out` parameter cannot have its value modified with `scoped` as such a declaration is non-sensical. 

```csharp
void M(
    ref scoped Span<int> p1,    // Okay
    in scoped Span<int> p2,     // Okay
    out scoped Span<int> p2,    // Error
)
```

The [method arguments must match](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/low-level-struct-improvements.md#rules-method-arguments-must-match) rules will be updated to take `ref scoped` into account. Values passed to such parameters do not need to be considered when calculating the return scopes.

Detailed notes:

- A `ref scoped` parameter is implicitly `scoped ref`
- An `out scoped` parameter declaration is an error

### ref scoped locals

The language will allow for locals to be declared as `ref scoped`. This will serve to have the lifetime of the value be _current method_. This lifetime is exactly _current method_ unlike parameters which are just known to be wider than _current method_.

This is mostly useful in combination with `ref readonly` locals and their variance rules described in a later section.

### ref field to ref struct

The language will allow for `ref struct` to appear as `ref scoped` fields. This `scoped` will serve to ensure the values cannot be escaped outside the containing instance but can be read and manipulated within it.

```csharp
ref struct Deserializer
{
    ref scoped Utf8JsonReader reader;

    ReadOnlySpan<byte> M1()
    {
        // okay: implicitly scoped to current method
        var span = reader.ValueSpan; 

        // okay
        reader.Skip();

        // Error: can't escape the ref data the ref scoped field refers to
        return reader.ValueSpan;
    }
}
```

This is accomplished by giving every `ref scoped` field two new escape scopes named _current field N_ and _current ref field N_ where _N_ is the numeric order of the field. For example, the first field has a _safe-to-escape_ of _current field 1_ and a _ref-safe-to-escape_ of _current ref field N_. Both escape scopes can be converted to _current method_, and _current field N_ can be converted to _current ref field N_, but no other defined relationships exist. That serves to restrict their usage to the current method where the containing value is used. This escape scope applies to both.

Below are a few examples of these rules in action

```csharp
ref struct NestedRefStruct { }
ref struct RefStruct
{
    public NestedRefStruct NestedField;
}

ref struct S
{
    ref scoped RefStruct field;

    RefStruct M1(RefStruct s)
    {
        // Okay
        field = new(); 

        // Error: calling-method is not convertible to current-field-1 as they have 
        // no relationship
        field = s;

        // Error: safe-to-escape is current-field-1 which isn't returnable 
        return field;
    }

    NestedRefStruct M2()
    {
        // Error: safe-to-escape is current-field-1 which isn't returnable 
        return field.NestedField;
    }

    ref RefStruct M3()
    {
        // Error: safe-to-escape is current-ref-field-1 which isn't returnable 
        return ref field;
    }

    // In an unsafe context the diagnostic is a warning.
    unsafe ref RefStruct M3_Unsafe()
    {
        // Warning: safe-to-escape is current-ref-field-1 which isn't returnable 
        return ref field;
    }
}
```

The [method arguments must match](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/low-level-struct-improvements.md#rules-method-arguments-must-match) rules do not need to be updated here as they already account for `ref` parameters being captured as `ref` field. Even though a `ref` to `ref struct` was not directly returnable before, it could be returned indirectly by a `ref` to a `struct` field of the value.

The language will also allow for `ref` fields to be declared as `scoped ref`. There are less use cases for this but `ref scoped` implies `scoped ref` hence the rules must be adjusted to account for this. As such the syntax will be exposed because while the use cases are small the infrastructure already exists. The _ref-safe-to-escape_ of such fields follows the logic above for `ref scoped` fields.

Detailed notes:

- A `ref` field where the type is a `ref struct` must be `ref scoped` or `ref readonly scoped`
- A `ref` field may be marked `scoped ref`

### ref readonly and variance

The lifetime of a value pointed to by a `ref` is invariant. This is imporant because the _exact_ lifetime of the value must be known in order to validate that assignments into that value are safe. If the lifetime were variant then it would be possible to assign a value with a narrower lifetime into a `ref` with a wider lifetime.

```csharp
void Example(ref Span<int> p)
{
    Span<int> local = stackalloc int[42];
    ref Span<int> refLocal = ref local;

    // Error:
    // The lifetime refLocal is narrower than p. For a non-ref reassignment 
    // this would be allowed as its safe to assign wider lifetimes to narrower ones.
    // In the case of ref reassignment though this rule prevents it as the 
    // safe-context values are different.
    refLocal = ref p;

    // If it were allowed this would be legal as the lifetime of refLocal
    // is current method and that is satisfied by stackalloc. At the same time
    // it would be assigning through p and escaping the stackalloc to the calling
    // method
    // 
    // This is equivalent of saying p = stackalloc int[13]!!! 
    refLocal = stackalloc int[13];
}
```

This variance problem only exists for writes because we need to know the _exact_ lifetime in order to validate writes. There is no such issue for reads. Those are legal as long as the lifetime of the read is equal to or narrower than the value being read from. This means that the lifetime of the value referred to by `ref readonly` is variant.

```csharp
void ReadOnlyExample(ref Span<int> p)
{
    Span<int> local = stackalloc int[42];

    // The lifetime of the value is current method
    ref readonly Span<int> refLocal = ref local;

    // Legal because the lifetime of p is wider than current method hence the variance check succeeeds
    refLocal = ref p;

    // This is reading p into local which is legal as the lifetime is narrower
    local = refLocal;
}
```

This `ref readonly` variance applies to any `ref` including `ref` fields. This allows for safe consumption of a `ref` field of a `ref struct` as a `ref readonly` value.

```csharp
ref struct S
{
    public ref scoped Span<int> Span;
}

void M(S s)
{
    // Error: cannot access a ref field of ref struct as ref
    ref Span<int> refSpat = ref s.Span;

    // Okay: the lifetime of the value is implcitily current method
    ref readonly Span<int> refReadOnlySpan = ref s.Span;
}
```

### Sunset restricted types

The ability for any type to be a `ref` field allows us to fully sunset the notion of restricted types.  The compiler has a concept of a set of _restricted types_ which is largely undocumented. These types were given a special status because in C# 1.0 there was no general purpose way to express their behavior. Most notably the fact that the types can contain references to the execution stack. Instead the compiler had special knowledge of them and restricted their use to ways that would always be safe: disallowed returns, cannot use as array elements, cannot use in generics, etc ...

Once `ref` fields are available and extended to support `ref struct` these types can be fully rationalized within those rules. As such the compiler will no longer have the notion of restricted types when using a language version that supports `ref` fields of `ref struct`.

To support this our `ref` safety rules will be updated as follows:

- `__makeref(e)` will be logically treated as a method with the signature `static TypedReference __makeref(ref T value)` were `T` is the type of `e`.
- `__refvalue(e, T)`
  - When `T` is a `ref struct`: will be treated as accessing a field declared as `ref scoped T` inside `e`.
  - Will be treated as accessing a field declared as `ref T` inside `e`
- `__arglist` as a parameter will be implicitly `scoped`
- `__arglist(...)` as an expression will have a _ref-safe-to-escape_ and _safe-to-escape_ of _current method_.

Conforming runtimes will ensure that `TypedReference`, `RuntimeArgumentHandle` and `ArgIterator` are defined as `ref struct`. Further `TypedReference` must be viewed as having a `ref` field to a `ref struct` for any possible type (it can store any value). That combined with the above rules will ensure references to the stack do not escape beyond their lifetime.

```csharp

// This is the logical equivalent of the __makeref call in terms of lifetime semantics
TypeReference CreateTypedReference(ref object value, Type type);

TypedReference M1()
{
    int x = 42;
    // This is logically the same as the following which means the lifetime is current method
    // TypedReference tr = Create(ref x, typeof(int)); 
    TypedReference tr = __makeref(x);

    // Error: safe-to-escape is current method which is not returnable
    return tr;
}

ref int M2(TypedReference tr)
{
    // Logically safe as
    // ref tr.Field;
    return __refvalue(tr, int);
}

void M3(TypedReference tr)
{
    // The type of this call is `ref scoped Span<int>` hence this is legal but the value 
    // is implicitly scoped to the current method.
    Span<int> span = __refvalue(tr, Span<int>);

    // This identical to the above but with the explicit `scoped` annotation.
    scoped Span<int> scopedSpan = __refvalue(tr, Span<int>);

    // Error: cannot access a ref field of ref struct as ref
    ref Span<int> refSpan = __refvalue(tr, Span<int>);

    unsafe
    {
        // Warning: accessing a ref field of ref struct as ref 
        ref span = ref __refvalue(tr, Span<int>);
    }

    // Okay: this ref readonly allows for variance and the lifetime of the value is 
    // known to be wider than current method
    ref readonly scoped Span<int> refReadOnlySpan = ref __refvalue(tr, Span<int>);
}
```

Note: strictly speaking this is a compiler implementation detail vs. part of the language. But given the relationship with `ref` fields it is being included in the language proposal for simplicity.

### Annotation Definition

<a name="annotations-param"></a>

At an annotation level every parameter marked `ref scoped` will have a new lifetime parameter defined. The name will be `$paramN` where _N_ is the numerical order of the parameter. That lifetime will only have the relationship `where $paramN : $local`.

```csharp
ref struct S { } 
void M(ref scoped S s) 

// maps to 

void M<$param1>(ref<$local> S<$param1> s)
    where $param1 : $local
```

This definition prevents the value from escaping from the method as the lifetime is not returnable. It also prevents local data from escaping from the current method through the parameter as the lifetime is wider than `$local` but not equivalent.

```csharp
void M<$param1>(ref<$local> S<$param1> p)
    where $param1 : $local
{

    S<$local> s = new S<$local>(stackalloc int[42]);

    // error: cannot convert S<$local> to S<$param1>
    p = s;
}
```

<a name="annotations-field"></a>

At an annotation level every field marked `scoped ref` (explicitly or implicitly via `ref scoped`) will have a new lifetime parameter defined. The name will be `$refFieldN` where _N_ is the numerical order of the field. That lifetime will have the relationship `where $refFieldN : $local` in all methods that use the type.

```csharp
ref struct S
{
    scoped ref int i;
}
S M(S p) { }

// maps to 
ref struct S<out $this, $refField1>
{
    ref<$refField1> int i;
}

S<$cm> M<$cm, $l1>(S<$cm, $l1> p)
    where $l1 : $local
{

}
```

Every field marked as `ref scoped` will have a new lifetime parameter defined. The name will be `$fieldN` where _N_ is the numerical order of the field. That lifetime will have the relationship `where $fieldN : $refFieldN` defined on the type. It will also have the relationship `where $fieldN : $local` in all method that use the type.

```csharp
ref struct S1 { }
ref struct S2 
{
    ref scoped S1 field;
}

S2 M(S2 p) { }

// maps to 
ref struct S1<out $this> { }
ref struct S2<out $this, $refField1, $field1>
    where $field1 : $refField1
{
    ref<$refField1> S1<$field1> field;
}

S1<$cm, $l1, $l2> M<$cm, $l1>M(S<$cm, $l1, $l2> p)
    where $l2 : $l1
    where $l1 : $local
{

}
```

These definitions prevent the values (`ref` or value) from escaping as their lifetimes are never returnable. It does allow for them to be manipulated and adjusted though. Non `ref` data, or data known to have `$heap` lifetime, can be assigned into such fields.

## Open Issues

### Ability to mark this as ref scoped

The proposal does not provide any way to mark `this` as `ref scoped` for a given method. At this time the author can see no significant benefits to this. If such scenarios do come along then an attribute such as `[RefScoped]` could be introduced similar to how `[UnscopedRef]` works.

### Requiring ref fields to ref struct to be scoped

Certain readers are likely to be disappointed that `ref` field to `ref struct` must be `ref scoped`. That limits the number of scenarios which can assign `ref` data into such fields. 

This is unfortunately necessary given the constraints of the design. Having a plain `ref` effectively requires that explicit lifetime annotations exist in the language. There is no other way to safely express the relationship between the value and the container.
