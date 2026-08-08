# C# Ownership

Ownership systems have recently achieved popularity through the Rust language. Ownership is the feature which lets Rust achieve its primary claim to fame: memory safety without a garbage collector / automatic memory manager.

While making memory management safe without a GC is certainly a great use case, there are many other interesting use cases for an owner system. However, to understand them it is still useful to understand ownership in the context of memory management. Therefore, this document will start with memory management and move to other use cases.

To understand Rust, it's helpful to understand the basic design of C++ memory management, the flaws that lead to unsafety, and the ways that ownership systems prevent safety problems.

First, the C++ memory management system. The most basic system is one from C: malloc and free. The simple rules are as follows:

1. All mallocs must be followed by at most one free.
2. Freed references must not be used (dereferenced) after free.
3. All access must fall in the range of memory initially allocated.

In real languages like Rust, C, or C++ there are in fact many more rules needed to ensure memory safety, but for our purposes these are sufficient. In fact, the first two are our main concern.

These rules seem simple but are very difficult to follow in practice. The core problem is _aliasing_. In real programs, variable reference doesn't follow a simple linear path through the method. Pointers are not assigned to a single variable — numerous copies are made and dereferenced both inside the method and inside the various helper methods that are called by both the original function and the helper functions themselves. At each point confusion can arise. It can be unclear whether the callee function or caller function is meant to free the variable. And it can be equally confusing to determine when all aliased uses are complete and the variable can be freed. Worse, the aliases are not invalidated after free, so it is easy for functions to mistakenly believe their alias is still valid.

The main innovation on this front came from C++ in the form of RAII. Put simply, RAII is a system where variables are automatically, and transitively, freed at the end of variable scope. The most important thing is that it ties the variable's memory to its lexical scope. This cleanly removes confusion about where and when a variable is freed — it is freed by the allocating `unique_ptr`, and it is freed at the end of lexical scope. `unique_ptr` makes it possible to call all methods directly, as though accessing a standard pointer. However, it prohibits copying, so new `unique_ptr` aliases cannot be created. C++ references (`&`) and raw pointers may still be created for temporary access in helper functions.

This is a huge improvement for C++ but it has one huge drawback — it is entirely advisory. There are many ways to directly access the underlying pointer and many ways to violate the rules, either by accessing a pointer after the `unique_ptr` lifetime has ended or by freeing a raw pointer that was managed by a `unique_ptr`.

What Rust tried to solve is enforcement. The `unique_ptr` pattern is practical and effective, but code should not be able to violate the rules. And, equally important, this validation should happen at compile time, with no runtime overhead. The result is a series of type system features and a rule enforcement system called the "borrow checker."

The Rust ownership type system rules look very similar to C++ `unique_ptr` rules, but enforced by the language at compile time. The basic rules are:

1. All values have a unique "owning" variable.
2. When the owning variable exits scope, all owned values are destroyed.
3. Ownership may be transferred to a new owning reference, but cannot be shared.
4. Owning variables may produce "borrowed" references that may not destroy the borrowed value and whose lifetime cannot exceed the lifetime of the owned variable.

There are more rules, especially around mutability, but they will be examined in more detail later. There are also specific details around how these rules are implemented by Rust — these are mostly not relevant to the C# design and will be described inline if needed.

## Motivation

The obvious question a C# programmer might ask is: why should I want ownership rules? What do I do with them? This is a good question to ask. After all, Rust designed the ownership system to provide memory safety without a memory management runtime. C# already has one (the GC) so why bother?

The simple answer is that an ownership system may be used to manage memory, but that isn't the only thing it can do. C#'s memory model views memory as infinite, but there are many other things in C# apps that are not. For instance, file handles on most operating systems are both finite and easily exhaustible. They should also not be accessed after they have been closed, and they should only be closed once. Similarly, the standard library has a popular API called `ArrayPool`. Arrays may be borrowed from the pool using `Rent` and returned using `Return`. Returning the same object twice is invalid, as is using the object after it has been returned. The results can be severe — multiple writers may believe they own the pool and effectively cause memory corruption. This has caused serious issues in practice — multiple CVEs have been issued in the framework due to incorrect usage of `ArrayPool`.

Even simple cases that don't seem to look like memory management may be solvable using ownership. There is a popular .NET type called `ImmutableArray`. Because `ImmutableArray` is a fully immutable type, it is often created using a builder type that produces the immutable array using a final `ToImmutable()` method. Internally, even if the array is exactly the right size, a copy is made. This is because there is no way to prove that the builder is discarded after `ToImmutable` is called. If it is not, the builder would still have a reference to the underlying array and could mutate the contents — even though it is supposed to be impossible. Ownership provides the exact semantics desired — if `ToImmutable` takes the Builder ownership, it can ensure that no copies were made and no references are still valid. Therefore, it would be safe to return the underlying array.

## Design

At this point, the goal is clear. C# should have an optional notion of ownership that can be applied to user-defined types. Note that this must be optional precisely because existing C# types do not follow ownership requirements. Unlimited aliasing and copying is the default behavior of C# today.

To indicate that a C# type is an owned resource we will introduce a new interface — `IResource`. The definition of `IResource` is as follows:

```csharp
interface IResource
{
    void Drop();
}
```

Any type that implements `IResource` will be considered an owned resource, which carries some special requirements and privileges. Types will not have to provide an implementation for `Drop()`; the language will automatically provide an implementation that calls `Drop` on all fields that implement `IResource`, in field declaration order. If a type does provide an implementation of `Drop`, that is _in addition to_ the default implementation — after a user implementation of `Drop` is run, the language defined transitive `Drop` implementation is run. This ensures that resources cannot accidentally forget to be dropped.

The following rules apply to all resource types `R`:

- All variables of plain type `R` are considered owning references.
  - The result of a constructor of `R` is an owning reference.
  - At any given point in the program, only one owning reference to a value is permitted. Therefore,
    - Copying resource variables is not permitted. All implicit struct copy behavior is disabled for resource types.
    - Resource variables may not appear as fields of non-resource types.
    - Assigning an owning reference to another owning reference is considered a transfer of ownership.
      - If the variable may be assigned before another assignment, assigning it ends its lifetime
      - After transfer, use of the original reference is disallowed.
  - There may be only one mutable reference, or any number of read-only references (aliasing rules).
  - `R` may not be substituted for generic type parameters
  - `R` may not be boxed into `object`, an interface, `dynamic`, or captured inside a delegate.

Let's look at a few examples:

- For a resource type `R`:
  - The expression `new R()` produces an owning reference to a value of `R`.
  - The assignment `var v = new R();` produces a transfer of ownership from the new owning reference `R` to the new owner, `v`.
- Both classes and structs may implement `IResource` and be considered resource types. Plain variables are considered "owners" in both cases, regardless of the fact that variables of struct type are not generally considered "references".
- The assignment expression (vs assignment statement) is a somewhat interesting case. It both assigns the right-hand side to the variable on the left-hand side, and produces the value through the evaluation of the expression. For a resource RHS in an ownership assignment, ownership would be transferred to the LHS. However, it would then immediately be transferred to the evaluation result of the expression. This renders the LHS variable unusable and makes the form useless.

```csharp
struct R : IResource
{
    public void M() {}
}

static void Helper(R r)
{
    r.M();
}
```

```csharp
R local = new R();
Helper(local);
local.M();
```

In the above code, the expression `local.M()` produces an error. This is because `Helper(local)` has taken ownership of the value called `R`. After ownership has been transferred, the previous owner of a value may no longer access it.

The same principle is true for the following:

```csharp
R local = new R();
R r2 = local;
local.M();
```

In the above, `local.M();` is an error. `r2 = local` did not make a copy or create a new alias — it took ownership of the value, and `local` lost it.

### Drop rules

- `Drop()` is compiler-invoked and cannot be called directly.
- `Drop` is invoked when leaving the variable's lexical declaring scope
    - This includes exception unwinding
- When an owning variable is re-assigned, `Drop` is called on the overwritten value
- Drop implementations should not throw

### Borrowing references

Now that we've covered owning references we need to outline borrowed references. Borrowing is very important because it makes helper functions and modularity possible. Consider a generic `ReadByte` function that operates on a `File` type. If `ReadByte` were forced to take ownership of the `File`, `ReadByte` could only be called a single time on a given reference — very inconvenient for files more than one byte long.

Instead, helper functions should use borrowed references. These are references that explicitly do not take ownership of the resource. However, they are guaranteed to live no longer than the resource. Notably, borrowed references do not transfer when ownership transfers. Or, put another way, all borrow lifetimes must end before ownership is transferred.

Lastly, it is important to note that borrowed references are truly references to the owned value, not copies. Operations on borrowed references must occur on the same value as the owned value. This has some different implications for classes and structs. For structs it implies that borrowed references can never be "normal" struct values — borrowed struct references are always true managed by-ref variables.

For classes, the opposite is true. A by-ref to a class variable produces a reference to the local variable. Borrowed references are aliases of the owned value, not the owning reference. This means that all class borrowed references are regular variables with no special ref kind. However, they are different from regular references.

To represent borrowed class references, two new special types will be added to the core library. The definition is as follows:

```csharp
struct Borrow<T>(T value) where T : class
{
    public T Value { get; set; } = value;
}
readonly struct ReadOnlyBorrow<T>(T value) where T : class
{
    public T Value { get; } = value;
}
```

Note that there is a mutable and read-only version of the `Borrow` type. These are analogous to `ref` and `ref readonly` and serve similar purposes. They fall into the same aliasing restrictions above: there may be either one mutable borrowed reference or any number of read-only borrows, but they are mutually exclusive.

Also note that the Borrow types are intrinsic -- they can violate some other rules, like substitution of resource types for generics. It is also illegal to copy `Borrow<T>`, as this would create multiple mutable references. In addition, the owner cannot be used, moved, or dropped while borrowed.

The `Value` property will be illegal to access by all code except the compiler. Note that all instance methods of resource types consider their receiver borrowed, so this includes all instance members. In fact, the compiler is responsible for analyzing all operations on `Borrow<T>` as if they were operations on `T` and automatically translating them through calls to `Value`.

> **N.B.** All instance members have a borrowed receiver.

Any by-ref, borrowed reference, or value carrying a by-ref or borrow lifetime returned from an instance member of a resource type is considered derived from the member's implicit borrowed receiver. Its lifetime is therefore no wider than the lifetime of `this`. Conceptually, a `Span<T>` returned from a member whose receiver is `Borrow<$a, R>` is treated as `Span<$a, T>`.

This rule applies at the return boundary. It does not change the lifetime of values used within the member. For example, a member may create an ordinary heap-backed span internally:

```csharp
public Span<T> Span
{
    get
    {
        Span<T> span = _rented;
        return span;
    }
}
```

Within the getter, `span` has the ordinary heap lifetime derived from `_rented`. Returning it narrows its lifetime to that of `this`. The receiver remains borrowed for as long as the returned value is live, so the resource cannot be transferred, reassigned, or dropped during that time. Ordinary value returns are unaffected.

TBD:
- [ ] subtyping rules, analogous to rules for by-refs

#### Borrow lifetimes

Unlike owned values, borrowed values have a special restriction: they can't live longer than their parent value. This rule is enforced by the language through static analysis. Luckily, the language already has a concept of lifetimes and a lifetime analysis system for by-ref types. We can adopt this system almost unmodified. The [formalization of the lifetime system](https://github.com/dotnet/csharplang/pull/9418) includes polymorphic lifetime variables — and in particular the formalization includes a definition of by-ref variables as `ByRef<$a, T>` where `$a` is the lifetime parameter. This lifetime variable is most relevant for our purposes. We want this lifetime to capture the ownership lifetime of the target, not its storage lifetime. Fortunately, these are compatible — the storage lifetime is always <u>at least as long as</u> the ownership lifetime. This is because ownership may shorten a variable's lifetime by transferring away ownership, but it may not extend it. Thus we may treat ownership and storage lifetimes as unified into a single lifetime concept and simply add a new unification rule: when two lifetimes are combined, storage or ownership, the resulting lifetime is the shorter of the two. Or, in type theory terms, it is the widest of the two lifetime types.

For by-ref variables, we do not need to make any further modifications. By-ref variables already carry a lifetime variable and will continue to do so in the same way — the lifetime will now also track ownership lifetime. The most important change is that we now have a new type of ref — the `Borrow<T>` type. Remember the ownership constraint — a `Borrow<T>` instance may not live longer than its target. To do so, `Borrow` must be instantiated with the lifetime of its target. Therefore, the full formal definition of `Borrow` must include a lifetime parameter, just like by-ref. Therefore the full formal definition is

```csharp
struct Borrow<$a, T> { ... }
```

analogous to the formal notation for by-ref:

```csharp
struct ByRef<$a, T> { ... }
```

This does have some further implications. In particular, types that contain `Borrow` instances. Just like ref structs, they must allow for lifetime parameterization. For example, the ref struct `E` can be defined to hold a by-ref:

```csharp
ref struct E
{
    public ref int Value;
}
```

We've already established the full formal definition is the type:

```csharp
ref struct E<$a>
{
    public ref<$a> int Value;
}
```

Quite simply, ref structs are parameterized by N lifetime variables for N by-ref nested fields. Currently, ref structs are the only type definitions that require lifetime parameters. Now, however, any type may contain `Borrow<T>` fields, which carry parameterized lifetimes. Therefore, all types must now allow lifetime parameterization. Just like ref structs, all types now contain N lifetime parameters for all N nested `Borrow<T>` fields. For example, we might define

```csharp
struct E<$a>
{
    public Borrow<$a, string> Value;
}
```

analogous to our by-ref struct `E`.

The other half is, of course, lifetime inference. By-ref variables currently acquire their lifetime from the target variable's language-defined lifetime. Owned variables are not quite so simple. In particular, an owned reference may have local scope, but heap storage. For example,

```csharp
class C : IResource {}

void M()
{
    var c = new C();
    ...
}
```

In this example, the lifetime of `c` is defined based on the lexical lifetime of variable `c`. Therefore if we were to introduce a borrow,

```csharp
void M()
{
    var c = new C();
    Borrow<C> b = c;
    M2(b);
    ...
}

void M2<$a>(Borrow<$a, C> b)
{
    ...
}
```

We can see that the lifetime parameterization of `Borrow<C>` is based on the assignment of the `c` variable. We will, however, note that the lifetime is based on the ownership of `c` — not the storage. So even though the data of `C` lives on the heap, the ownership lifetime is bound to the lexical lifetime of `c`, namely the body of the method `M`.

### Mutability

At any point, a resource value permits either:

- one mutable borrow, or
- any number of read-only borrows.

These states are mutually exclusive. While any borrow is live, the owner
may not be accessed, transferred, reassigned, or dropped.

To accommodate read-only and mutable borrows we also have to adjust the rules for classes themselves. The `readonly` keyword will now be legal for class methods, just like struct methods. It will have the same rules. An ordinary (non-readonly) class instance method will have a `Borrow<this>` receiver. A `readonly` instance member has a `ReadOnlyBorrow<this>` receiver. Importantly, read-only borrows may only call readonly members. Note that this is shallow mutability -- non-resource members may effectively be mutated due to lack of mutability requirements on interior members.


### Worked examples

We now have the fundamental building blocks of our ownership system. To that end, let's revisit all our examples and demonstrate how they are solved with an ownership system. The three examples were:

- Native finite resources
- `ArrayPool`
- `ImmutableArray` Builder

Let's examine each in order.

For finite native resources, `File` is a good example. We can reliably close the file inside Drop, preventing leaking file handles or duplicate close. Note that for a real File, we may have buffered I/O that needs to be written -- because Drop is not `async` we cannot guarantee that all data is written, just that the file is closed.

```csharp
class File : IResource
{
    private IntPtr _nativeHandle;

    void IResource.Drop()
    {
        NativeClose(_nativeHandle);
    }
}
```

This may seem simple, but it is effective. This is enough to verify that all file objects are dropped automatically and at most once.

Next we have `ArrayPool`. This is more complicated because the restriction is stricter, and we are more likely to need to interact with code we don't own. One possible new API shape is as follows.

```csharp
class OwnedArrayPool<T> // unowned
{
    private readonly ArrayPool<T> _pool;

    public static OwnedArrayPool<T> Shared { get; } =
        new OwnedArrayPool<T>(ArrayPool<T>.Shared);

    private OwnedArrayPool(ArrayPool<T> pool)
    {
        _pool = pool;
    }

    public RentedArray Rent(int minimumLength)
    {
        return new RentedArray(_pool.Rent(minimumLength), this);
    }

    private void Return(T[] array)
    {
        _pool.Return(array);
    }

    public readonly struct RentedArray : IResource
    {
        private readonly T[] _rented;
        private readonly OwnedArrayPool<T> _pool;

        private RentedArray(T[] rented, OwnedArrayPool<T> pool)
        {
            _rented = rented;
            _pool = pool;
        }

        public Span<T> Span => _rented;

        void IResource.Drop()
        {
            _pool.Return(_rented);
        }
    }
}
```

The wrapper returns a new `RentedArray` resource type instead of exposing the rented array. `RentedArray` provides access through a `Span`, which borrows the rented array. This is sufficient for many scenarios that currently use the array directly.

Creating a span from an ordinary array would normally give the span a heap lifetime. Here the `Span` property has an implicit borrowed receiver because the receiver type, `RentedArray`, is a resource type. A by-ref, borrow, or lifetime-parameterized value returned from an instance member of a resource type is considered derived from the member’s implicit borrowed this receiver. Its lifetime is therefore no wider than the lifetime of `this`.

The usage would look like:

```csharp
void Write(string s)
{
    if (s.Length < MAXSTACK)
    {
        Span<byte> utf8 = stackalloc byte[MAXSTACK * 2];
        ToUtf8(s, utf8);
    }
    else
    {
        var rented = OwnedArrayPool<byte>.Shared.Rent(s.Length * 2);
        ToUtf8(s, rented.Span);
    }
}
```

This is the classic "stackalloc a span if small, or use a rented array if large" pattern. In both cases the usage is local and simple. Unlike direct use of `ArrayPool`, there is no risk of forgetting to return the array or returning it twice. `Return` is not exposed to the renter. Instead, the `RentedArray` is automatically dropped at the end of its scope, and its `Drop` implementation returns the array to the pool. Access is limited to a `Span` whose lifetime is tied to the `RentedArray`, so the span cannot outlive the rental.

Last is the case of `ImmutableArray.Builder`. This looks extremely similar to the `ArrayPool` case.

```csharp
struct OwnedImmutableBuilder<T> : IResource
{
    private T[] _backing;

    public OwnedImmutableBuilder(int len)
    {
        _backing = new T[len];
    }

    public T this[int index] { ... }

    public static ImmutableArray<T> ToImmutable(OwnedImmutableBuilder builder)
    {
        return ImmutableArray.UnsafeCreate( // Assume an internal unsafe method
            builder._backing                //   to make an ImmutableArray
        );
    }
}
```

We use it very simply:

```csharp
var builder = new OwnedImmutableBuilder<int>(10);
for (int i = 0; i < 10; i++)
{
    builder[i] = i;
}
var array = OwnedImmutableBuilder.ToImmutable(builder);
```

This looks very similar to how we use the rented array, with one notable exception: the static method `ToImmutable`. The main difference here is between instance and static methods on resource types. Static methods, or any method except the receiver of an instance method, will consider parameters of a resource type to be <u>owning references</u>, not borrowing references. Functionally, this is what allows us to guarantee that the array is transferred safely — it is never exposed by the builder, the builder is a resource so it is never copied, and the `ToImmutable` function takes ownership of the builder, meaning no references could live past the transfer.
