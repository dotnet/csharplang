
# Extensions WG 2024-06-07

## Type erasure encoding

We need to encode the extension type (for extension-aware compiler) and the underlying type (erasure and for non-extension-aware tools).

We'll need to encode tuple names, dynamic bits, nullability, native integer for both types. 
Ideally, this should be done in a way that doesn't break tools that understand the current attribute/constructor encoding.

Options for encoding extension type:
- generic attribute (pinging David to ask limitations/bugs on older frameworks)
- attribute with typeof (may not refer to type parameters)
- nested type
- nested type with parallel method

Decision: we'll spec out the parallel method in nested type design.

### modopt
```
// source
void M(E e)

// codegen
void M(C modopt(E) e)
```

```
virtual void M2(C c)
// other assembly
override void M2(E e)
```

Con: binary break to change API from extension to underlying type and back
Con: problem for override at language level (might be solvable)
Con: need to new solution encode the second set of tuples names/dynamic/nullability

### Generic attribute
```
// source
void M(E e)

// codegen
void M([Extension<E>] Underlying e) // System.Extension`1 (new)
```
Con: Attribute doesn't exist downlevel (we should synthesize)
Con: Generic attribute not supported before .NET 7 (prove there's enough support, or block off)
Con: need to new solution encode the second set of tuples names/dynamic/nullability

### Attribute with typeof

Limitation with typeof in attribute referencing type parameter (typeof encoded as fully-qualified name, context-free, but no such syntax exists for type parameters)


```

class C<T>
{
    void M(E<List<T>> e)
    // encoding: void M([Extension(typeof(E<List<T>>))] object e) // not possible
}
explicit extension E<U> for object { }
```

### Nested type

Doesn't work with method type parameter. We could use a generic private implementation nested type with same number of type parameters.
```
explicit extension E<T1, T2> where T2 : IDisposable { }
class C<T>
{
    // source: void M<U>(E<T, U> e) where U : IDisposable
    void M<U>([Extension("Secret1", "Field1")] Underlying e) // problem with method type parameter

    private class Secret1<U> where U : IDisposable
    {
        E<T, U> Field1;
    }
}
```

### Nested type with parallel methods

Pro: Nested type reduces pollution of members for other tools
Pro: Runtime may not need to load the members of nested type (to be confirmed)
Pro: natural encoding of tuple names/dynamic/etc
Con: copies all the parameters
Pro: it naturally offers a place to encode the second set of tuples names/dynamic/nullability information

Secret class is abstract and parallel methods are abstract
Note: Hard to point backwards from parallel method, so attribute is on visible method

```
class C<T>
{
    [SecretMethod("Method1")] // explicit matching, rather than complex auto-matching
    U M<U>(U e, int x) where U : IDisposable

    private abstract class Secret
    {
        abstract void/E<T, U1> Method1<U1>(E<T, U1> "", Secret "") where U1 : IDisposable;
    }
}
```

#### Optimization? one secret method per type to encode
```
    private class Secret
    {
        // More parameter cause more methods, but we can share
        E<T, U1> Method1<U1> where U1 : IDisposable { }
        E<T, U1> Method1<U2> where U1 : IDisposable { }
    }
```

#### Optimization? remove parameters that don't involve extensions/erased
We could use parameter names encoding ordinals from original method:
```
    private class Secret
    {
        void Method1<U1>(E<T, U1> p0 /*, Secret x */) where U1 : IDisposable
    }
```

#### Properties

```
   // E this[E e]
   No parallel property, only have parallel accessors
```

#### Types?
```
   // class C<T> : I<E<T>> { }
   class C<T> : [SecretField("Field1")] I<Underlying>
   {
       private abstract class Secret
       {
          I<E<T>> Field1;
       }
   }
```

## Translation from instance methods to static metadata methods

CLS rules for events requires a specific signature (single parameter), but we need a second parameter. See ECMA 335 II.22.28.
How bad is it? May break other compilers. Would those compilers allow to consume those methods or treat as invalid metadata?
Disallow events?

