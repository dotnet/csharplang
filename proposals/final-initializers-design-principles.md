# Final initializers - design principles

This document shares the motivation from the main [Final initializers](./final-initializers.md) proposal and aims to present the main design principles for discussion. When consensus is found, conclusions can be merged into the proposal spec.

## Breaking change to add a final initializer to an existing type

At the opposite end of lifecycle management, it's a documented breaking change to implement IDisposable on an existing type.

<https://learn.microsoft.com/en-us/dotnet/api/system.idisposable>:

> [!WARNING]
> It is a breaking change to add the `IDisposable` interface to an existing class. Because pre-existing consumers of your type cannot call `Dispose`, you cannot be certain that unmanaged resources held by your type will be released.

Adding new lifecycle management at the end of the lifecycle is a known footgun. For the same reasons, it's also a breaking change to add new lifecycle management at the _start_ of the lifecycle in the form of a final initializer. Code that was already compiled against a type will skip calling the final initializer if the type adds one later. This breaks the language feature's guarantee.

It's not _impossible_ to come up with a design that allows it to be a non-breaking change to add a final initializer to an existing type, but it is incredibly thorny.

### Foreign compilers blocked from the entirety of .NET vNext

Making it a non-breaking change would require blocking all downlevel and foreign compilers from compiling against .NET vNext at all, just in case they reference a type which doesn't have a final initializer yet but might later. That would solve the breaking change by bundling it with another breaking change, which is moving a library from .NET vCurrent to .NET vNext: once you migrate to .NET vNext, now you can only be referenced by callers who know about the _possibility_ of final initializers and can emit appropriate runtime checks.

It is likely a bridge too far to prevent downlevel and foreign compilers from targeting .NET vNext at all.

### Viral final initializer checks

Even with downlevel and foreign compilers out of the way, there's a second bitter pill to swallow to make it a non-breaking change. The new compiler would also have to emit a final initializer check for every `new T` expression if the type `T` doesn't currently have a final initializer, and is defined outside the current compilation. This way, if the binary from the current compilation begins running against a newer version of `T` which does implement a final initializer, the final initializer is not silently bypassed.

This can be implemented through one of two strategies:

1. The runtime adds a new virtual method to System.Object, and the compiler unconditionally calls this new method for every `new T` expression.
2. Or, the compiler adds some runtime check via an interface or a pattern-based runtime helper to resolve and call a final initializer if one exists.

Strategy 1 is our only option if we don't want it to _still_ be a breaking change to add a final initializer to an _unsealed_ type. If we start with a base class with no final initializer in assembly A and a derived class with no final initializer in assembly B, instantiating the derived class doesn't run any final initializers. Then if a new version of assembly A adds a final initializer to the base class and is used together with the _original_ assembly B, instantiating the derived class will still not run any final initializers. This bypasses the new final initializer in A, and there is the break. The only way to overcome this break is for the derived class to have inherited the final initializer method from System.Object, so that it was called all along even when neither class had yet declared a final initializer.

Either strategy would be a virally invasive change. It adds boilerplate IL to everything, from `throw new ArgumentException(...);` to `new List<int>()`. Adding a new call after every `new T` expression in System.Private.CoreLib increases code size by 6.5%. The JIT may be able to do some extra analysis and elide some of the calls at runtime to avoid the some of the performance hit.

If we accept as a design principle that it is a breaking change to add a final initializer to an existing type, we no longer have the burden of blocking downlevel compiler access to all of .NET vNext, we no longer have the burden of emitting some call for every `new T` expression whether or not it is in the tiny minority of types which have a final initializer, and we no longer are forced into adding a new virtual method to System.Object.

### Pay for play: no System.Object method

Once establishing that it is a breaking change to add a final initializer to an existing type, we now have the opportunity to make final initializers pay-for-play instead of augmenting the code generated behind every object creation expression. The previous section mentions some of the overheads of "just-in-case" final initializer calls at every object creation expression.

Both for the sake of making it easy to think about breaking changes and for the sake of achieving pay-for-play, a structural pivot emerges: _whether or not a type has a final initializer_. Some types do, and some don't, and the compiler (and serializers) will treat the types differently on this basis, and users thinking about their binary API will think about the types differently on this basis.

This central structural pivot would be obscured by adding a new virtual method to System.Object. To do so would not serve these purposes well. Instead, it would broadcast fairly directly and strongly that all types _do_ have a final initializer, and it would make it nuanced and difficult to check to see whether a type actually does have one. Adding a new virtual method to System.Object has other downsides as well which also fly in the face of pay-for-play:

1. The vtable gets larger for every managed type.
1. The new method will be displayed in every managed type by tools that display member lists. The blast radius is not just special kinds of types like records, or just types which declare a final initializer. Additionally, if it is decided to prevent normal calls to the final initializer, and the prevention is via making the method unspeakable, this becomes even less palatable.

Instead, a final initializer will be implemented only on types that declare a final initializer (or derive from ones that do). It will be implemented as a method with a well-known signature and name (speakable or not, see [Prevent ordinary calls to the final initializer](#prevent-ordinary-calls-to-the-final-initializer)). It will be virtual if the type is unsealed. This is the same approach taken for the clone methods of records.

If desired as an implementation detail to make runtime checks and helpers more efficient, a new interface such as `System.Runtime.CompilerServices.IFinalInitializable` could also be introduced which matches this method. The exposure of this interface would need special considerations; users would likely need to be blocked from calling the interface method directly (see [Prevent ordinary calls to the final initializer](#prevent-ordinary-calls-to-the-final-initializer)), and it would have to be decided what (if anything) should happen if users add it as a type parameter constraint.

### Concrete type unknown

However, it is still necessary to augment the code generation of every object creation expression and `with` expression where the concrete type is unknown, in case the concrete type has a final initializer. These cases are:

1. An object creation expression on a generic type parameter which is not constrained to a class with a final initializer, unless this feature is struck (see [Allow passing to `: new()` type parameters](#allow-passing-to--new-type-parameters)).
2. A `with` expression on any expression whose type has no final initializer and is not sealed.

In future language versions, this list could expand:

- Extension constructors: `new C` could resolves to an extension constructor whose return type has no final initializer and is not sealed
- Factory methods: `C.Factory() { A = 42 }` when the factory method's return type has no final initializer and is not sealed

Object creation scenarios which name a concrete type remain pay-for-play, and they are by far the most common. `with` expressions on all unsealed types will end up having to check at runtime if the type implements a final initializer, and this will not be pay-for-play.

Checking for a possible final initializer and running it can be accomplished regardless of the chosen implementation strategy (new System.Object method, new interface, or new virtual method with a well-known name), through a runtime helper for example.

## Downlevel compiler protection

A type with a final initializer must gain a `[CompilerFeatureRequired]` attribute on the type itself to block a downlevel compiler. A downlevel compiler needs to be blocked not just from instantiating or inheriting such a type directly, for which placing the attribute on constructors would suffice; it also must be prevented from using the type as a type parameter with a `: new()` constraint. Otherwise, it will be possible to use the type without calling the final initializer, which breaks the language feature's guarantee.

This cannot be mitigated by tying the language feature to a new runtime version and augmenting `Activator.CreateInstance<T>()` on that runtime to run a final initializer. Generic code such as `new T { InterfaceProp = 1 }` must run the final initializer after the `InterfaceProp` setter has been invoked.

Without this protection, it would not be safe to create a brand-new type with a final initializer, because not all consumers will be forced to use a compiler which knows about final initializers.

## Unconditionality for object creation

`new A()`, `new A() { }`, and `new A() { B = 1 }` should not differ in whether `A`'s final initializer is called. Types may place important initialization code in their final initializers. Users should be able to move code back and forth between their constructor and their final initializer, knowing that they are only changing the execution time of their code relative to object initializer calls without changing whether or not the code executes.

## Open questions

### Prevent ordinary calls to the final initializer method?

Is there a sufficient reason to prevent the ordinary calls to the method that implements the final initializer?

One possible pitfall is if a final initializer is implemented in such a way that was assumed to have an no-more-than-once guarantee and therefore was free of thread synchronization.

> [!NOTE]
> An implementer might also assume that the final initializer is running before certain other members (such as methods not called from inside the class) could possibly run, but that would always be an incorrect assumption regardless of how this question is decided. During object construction, an extension property, indexer, or Add method can be invoked which has full access to all members on the partially-constructed object. This language feature does not guarantee that the final initializer gets a chance to run before other members. Therefore, any implementer of a final initializer may wish to harden the final initializer against being invoked unexpectedly late in the object's usage, or at least think through the potential outcomes.

If this prevention is not made part of the design, it will need to be clearly documented to authors of final initializers that they may be invoked normally, and to consider them as any other method in their design. If they wish to prevent more-than-once calls, they will need to implement the check themselves (and decide whether to make the check thread-safe or not).

If this prevention is made part of the design, it remains to be decided how this prevention is implemented.

- The method could be declared with an unspeakable name, though this risks having it show in an unpleasant way in tooling that lists members. If combined with a choice to place the method on System.Object, the new unspeakable name would show up on every type.
- The method's signature could be modreq'd so that only the compiler can invoke, override, or implement it. There is another lifecycle instance method on all objects which has an ordinary name in metadata, but which may not be called from code (`object.Finalize`).

### Conditionality for object creation?

Should `expr with { }` (containing no assignments) skip calling a final initializer? Its state may have changed between the last time the final initializer ran and the `with`, but that state change wasn't going to start being considered "uninitialized" needing "reinitialization" in the original object; does the clone need "reinitialization"?

### Allow passing to `: new()` type parameters?

A type with a final initializer cannot be used as a type argument to a type parameter with a `new()` constraint. Such a type parameter can be used to do `new T()` or `new T { InterfaceProp = 1 }`, and this would skip calling the final initializer, breaking the guarantee of the language feature. The compiler could augment the codegen for these expressions to use a runtime helper, like it does with `Activator.CreateInstance<T>()` for the constructor call itself, but this doesn't solve the problem of existing binaries that have the current codegen which does not use the helper. The runtime also cannot infer when to run the final initializer from looking at IL that doesn't explicitly call it (see [Appendix A](#appendix-a-final-initializer-calls-cannot-be-inferred-by-the-runtime)).

To allow passing to a `: new()` type parameter, there are two routes that could be taken.

1. Allow compilations to opt into having final-initializer-safe `new()` constraints. When the compilation opts in, it begins to call a runtime helper for all generic `new T` expressions to run a final initializer if there is one, and it place a reserved attribute on the assembly declaring that its `: new()` type parameters respect final initializers and are safe for types which have them. The opt-in flag is required because assembly itself would lose the ability to use its own `: new()` type parameters as arguments to a type parameter with `: new()` in an assembly which does not have this opt-in declaration.

   If users are blocked by wanting to use their types with `: new()` type parameters in existing libraries, they can request the library author to add a target for the latest langversion, opt in to final initializer safety, and publish. No extra work is needed on the part of the library author (though the library author may be transitively blocked on passing their own `: new()` type parameter to the `: new()` type parameter of an upstream dependency which also needs the same publish).

2. Place the burden on the user of opting into the new behavior a type parameter at a time with `allows init` for example. This seems like a fairly unpleasant assertion: hardly anyone will have a use case for accepting all types except for ones with final initializers, and most everyone will want to enable it, so the explicitness becomes an attractive nuisance.

Alternatively, the option would remain open to implement this later, or never. Types with final initializers would be blocked on all `: new()` type parameters in the meantime.

## Appendix A: Final initializer calls cannot be inferred by the runtime

For any `new T` expression where you want a final initializer to be picked up (or potentially picked up), the compiler must output an explicit call in IL. It is not possible to avoid the overheads associated with final initializer calls by tying the language feature to a new runtime inference which automatically inserts calls to a final initializer.

This is because IL does not track where the end of an object creation expression happens. To see why the runtime cannot infer when to call the final initializer, see the samples below.

```cs
class C
{
    public int A { set => Console.WriteLine("set A"); }
    public int B { set => Console.WriteLine("set B"); }
    init => Console.WriteLine("Final initializer");
}
```

These two code samples behave differently:

```cs
var c = new C { A = 1 };
c.B = 2;
// Output:
// set A
// Final initializer
// set B
```

```cs
var c = new C { A = 1, B = 2 };
// Output:
// set A
// set B
// Final initializer
```

The only thing that would make these examples behave differently is the explicit call to the final initializer method. If you remove that call, the IL is identical between these two examples for today's release-optimized builds. The runtime cannot infer the call.
