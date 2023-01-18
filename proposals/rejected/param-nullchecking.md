# Parameter Null Checking

## Summary
This proposal provides a simplified syntax for validating method arguments are not `null` and throwing 
`ArgumentNullException` appropriately.

## Motivation
The work on designing nullable reference types has caused us to examine the code necessary for `null` argument 
validation. Given that NRT doesn't affect code execution developers still must add `if (arg is null) throw` boiler 
plate code even in projects which are fully `null` clean. This gave us the desire to explore a minimal syntax for 
argument `null` validation in the language. 

While this `null` parameter validation syntax is expected to pair frequently with NRT, the proposal is fully independent
of it. The syntax can be used independent of `#nullable` directives.

## Detailed Design 

### Null validation parameter syntax
The bang-bang operator, `!!`, can be positioned after a parameter name in a parameter list and this will cause the C# 
compiler to emit `null` checking code for that parameter. This is referred to as `null` validation parameter
syntax. For example:

``` csharp
void M(string name!!) {
    ...
}
```

Will be translated into code similar to the following:

``` csharp
void M(string name) {
    if (name is null) {
        throw new ArgumentNullException(nameof(name));
    }
    ...
}
```

There are a few guidelines limiting where `!!` can be used:

1. Only a parameter of something with an implementation can use it. For example, an abstract method parameter cannot use `!!`. Further examples include:
   - extern method parameters
   - delegate parameters
   - interface method parameters when the method is not a DIM
2. It must be possible to include an equivalent "check then throw" of the given parameter at the beginning of the method, ignoring syntactic limitations such as the need to replace an expression body with a block body.

Because of (2), the `!!` operator cannot be used on a discard.
``` csharp
System.Action<string, string> lambda = (_!!, _!!) => { }; // error
```

Also because of (2), the `!!` operator cannot be used on an `out` parameter, but it can be used on a `ref` or `in` parameter.
``` csharp
void M1(ref string x!!) { } // ok
void M2(in string y!!) { } // ok
void M3(out string z!!) { } // error
```

Declarations that have parameters and implementations can generally use `!!`. Therefore, it's permitted to use it on an indexer parameter, and the behavior is that all the indexer's accessors will insert a null check.

``` csharp
public string this[string key!!] { get { ... } set { ... } } // ok
```

The implementation behavior must be that if the parameter is null, it creates and throws an `ArgumentNullException` with the parameter name as a constructor argument. The implementation is free to use any strategy that achieves this. This could result in observable differences between different compliant implementations, such as whether calls to helper methods are present above the call to the method with the null-checked parameter in the exception stack trace.

We make these allowances because parameter null checks are used frequently in libraries with tight performance and size constraints. For example, to optimize code size, inlining, etc., the implementation may use helper methods to perform the null check a la the [ArgumentNullException.ThrowIfNull](https://github.com/dotnet/runtime/blob/1d08e154b942a41e72cbe044e01fff8b13c74496/src/libraries/System.Private.CoreLib/src/System/ArgumentNullException.cs#L56-L69) methods.

The generated `null` check will occur before any developer authored code in the method. When multiple parameters contain
the `!!` operator then the checks will occur in the same order as the parameters are declared.

``` csharp
void M(string p1, string p2) {
    if (p1 is null) {
        throw new ArgumentNullException(nameof(p1));
    }
    if (p2 is null) {
        throw new ArgumentNullException(nameof(p2));
    }
    ...
}
```

The check will be specifically for reference equality to `null`, it does not invoke `==` or any user defined operators. 
This also means the `!!` operator can only be added to parameters whose type can be tested for equality against `null`. 
This means it can't be used on a parameter whose type is known to be a value type.

``` csharp
// Error: Cannot use !! on parameters who types derive from System.ValueType
void G<T>(T arg!!) where T : struct {

}
```

In the case of a constructor, the `null` validation will occur before any other code in the constructor. That includes: 

- Chaining to other constructors with `this` or `base` 
- Field initializers which implicitly occur in the constructor

For example:

``` csharp
class C {
    string field = GetString();
    C(string name!!): this(name) {
        ...
    }
}
```

Will be roughly translated into the following:

``` csharp
class C {
    C(string name)
        if (name is null) {
            throw new ArgumentNullException(nameof(name));
        }
        field = GetString();
        :this(name);
        ...
}
```

Note: this is not legal C# code but instead just an approximation of what the implementation does. 

The `null` validation parameter syntax will also be valid on lambda parameter lists. This is valid even in the single
parameter syntax that lacks parens.

``` csharp
void G() {
    // An identity lambda which throws on a null input
    Func<string, string> s = x!! => x;
}
```

`async` methods can have null-checked parameters. The null check occurs when the method is invoked.

The syntax is also valid on parameters to iterator methods. Unlike other code in the iterator the `null` validation will
occur when the iterator method is invoked, not when the underlying enumerator is walked. This is true for traditional
or `async` iterators.

``` csharp
class Iterators {
    IEnumerable<char> GetCharacters(string s!!) {
        foreach (var c in s) {
            yield return c;
        }
    }

    void Use() {
        // The invocation of GetCharacters will throw
        IEnumerable<char> e = GetCharacters(null);
    }
}
```

The `!!` operator can only be used for parameter lists which have an associated method body. This
means it cannot be used in an `abstract` method, `interface`, `delegate` or `partial` method 
definition.

### Extending is null
The types for which the expression `is null` is valid will be extended to include unconstrained type parameters. This 
will allow it to fill the intent of checking for `null` on all types which a `null` check is valid. Specifically that
is types which are not definitely known to be value types. For example Type parameters which are constrained to 
`struct` cannot be used with this syntax.

``` csharp
void NullCheck<T1, T2>(T1 p1, T2 p2) where T2 : struct {
    // Okay: T1 could be a class or struct here.
    if (p1 is null) {
        ...
    }

    // Error 
    if (p2 is null) { 
        ...
    }
}
```

The behavior of `is null` on a type parameter will be the same as `== null` today. In the cases where the type parameter
is instantiated as a value type the code will be evaluated as `false`. For cases where it is a reference type the 
code will do a proper `is null` check.

### Intersection with Nullable Reference Types
Any parameter which has a `!!` operator applied to it's name will start with the nullable state being not `null`. This is
true even if the type of the parameter itself is potentially `null`. That can occur with an explicitly nullable type, 
such as say `string?`, or with an unconstrained type parameter. 

When a `!!` syntax on parameters is combined with an explicitly nullable type on the parameter then a warning will
be issued by the compiler:

``` csharp
void WarnCase<T>(
    string? name!!, // Warning: combining explicit null checking with a nullable type
    T value1!! // Okay
)
```

## Open Issues
None

## Considerations

### Constructors
The code generation for constructors means there is a small, but observable, behavior change when moving from standard
`null` validation today and the `null` validation parameter syntax (`!!`). The `null` check in standard validation 
occurs after both field initializers and any `base` or `this` calls. This means a developer can't necessarily migrate
100% of their `null` validation to the new syntax. Constructors at least require some inspection.

After discussion though it was decided that this is very unlikely to cause any significant adoption issues. It's more
logical that the `null` check run before any logic in the constructor does. Can revisit if significant compat issues
are discovered.

### Warning when mixing ? and !
There was a lengthy discussion on whether or not a warning should be issued when the `!!` syntax is applied to a
parameter which is explicitly typed to a nullable type. On the surface it seems like a nonsensical declaration by 
the developer but there are cases where type hierarchies could force developers into such a situation. 

Consider the following class hierarchy across a series of assemblies (assuming all are compiled with `null` checking
enabled):

``` csharp
// Assembly1
abstract class C1 {
    protected abstract void M(object o); 
}

// Assembly2
abstract class C2 : C1 {

}

// Assembly3
abstract class C3 : C2 { 
    protected override void M(object o!!) {
        ...
    }
}
```

Here the author of `C3` decided to add `null` validation to the parameter `o`. This is completely in line with how the
feature is intended to be used.

Now imagine at a later date the author of Assembly2 decides to add the following override:

``` csharp
// Assembly2
abstract class C2 : C1 {
   protected override void M(object? o) { 
       ...
   }
}
```

This is allowed by nullable reference types as it's legal to make the contract more flexible for input positions. The 
NRT feature in general allows for reasonable co/contravariance on parameter / return nullability. However the language
does the co/contravariance checking based on the most specific override, not the original declaration. This means the
author of Assembly3 will get a warning about the type of `o` not matching and will need to change the signature to the
following to eliminate it: 

``` csharp
// Assembly3
abstract class C3 : C2 { 
    protected override void M(object? o!!) {
        ...
    }
}
```

At this point the author of Assembly3 has a few choices:

- They can accept / suppress the warning about `object?` and `object` mismatch.
- They can accept / suppress the warning about `object?` and `!!` mismatch.
- They can just remove the `null` validation check (delete `!!` and do explicit checking)

This is a real scenario but for now the idea is to move forward with the warning. If it turns out the warning happens
more frequently than we anticipate then we can remove it later (the reverse is not true).

### Implicit property setter arguments
The `value` argument of a parameter is implicit and does not appear in any parameter list. That means it cannot be a 
target of this feature. The property setter syntax could be extended to include a parameter list to allow the `!!` 
operator to be applied. But that cuts against the idea of this feature making `null` validation simpler. As such the 
implicit `value` argument just won't work with this feature.

## Future Considerations
None
