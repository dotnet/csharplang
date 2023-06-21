# Improved Interpolated Strings

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

## Summary

We introduce a new pattern for creating and using interpolated string expressions to allow for efficient formatting and use in both general `string` scenarios
and more specialized scenarios such as logging frameworks, without incurring unnecessary allocations from formatting the string in the framework.

## Motivation

Today, string interpolation mainly lowers down to a call to `string.Format`. This, while general purpose, can be inefficient for a number of reasons:

1. It boxes any struct arguments, unless the runtime has happened to introduce an overload of `string.Format` that takes exactly the correct types of arguments
in exactly the correct order.
    * This ordering is why the runtime is hesitant to introduce generic versions of the method, as it would lead to combinatoric explosion of generic instantiations
    of a very common method.
2. It has to allocate an array for the arguments in most cases.
3. There is no opportunity to avoid instantiating the instance if it's not needed. Logging frameworks, for example, will recommend avoiding string interpolation
because it will cause a string to be realized that may not be needed, depending on the current log-level of the application.
4. It can never use `Span` or other ref struct types today, because ref structs are not allowed as generic type parameters, meaning that if a user wants to avoid
copying to intermediate locations they have to manually format strings.

Internally, the runtime has a type called `ValueStringBuilder` to help deal with the first 2 of these scenarios. They pass a stackalloc'd buffer to the builder,
repeatedly call `AppendFormat` with every part, and then get a final string out. If the resulting string goes past the bounds of the stack buffer, they can then
move to an array on the heap. However, this type is dangerous to expose directly, as incorrect usage could lead to a rented array to be double-disposed, which
then will cause all sorts of undefined behavior in the program as two locations think they have sole access to the rented array. This proposal creates a way to
use this type safely from native C# code by just writing an interpolated string literal, leaving written code unchanged while improving every interpolated string
that a user writes. It also extends this pattern to allow for interpolated strings passed as arguments to other methods to use a handler pattern, defined by
receiver of the method, that will allow things like logging frameworks to avoid allocating strings that will never be needed, and giving C# users familiar,
convenient interpolation syntax.

## Detailed Design

### The handler pattern

We introduce a new handler pattern that can represent an interpolated string passed as an argument to a method. The simple English of the pattern is as follows:

When an _interpolated\_string\_expression_ is passed as an argument to a method, we look at the type of the parameter. If the parameter type has a constructor
that can be invoked with 2 int parameters, `literalLength` and `formattedCount`, optionally takes additional parameters specified by an attribute on the original
parameter, optionally has an out boolean trailing parameter, and the type of the original parameter has instance `AppendLiteral` and `AppendFormatted` methods that
can be invoked for every part of the interpolated string, then we lower the interpolation using that, instead of into a traditional call to
`string.Format(formatStr, args)`. A more concrete example is helpful for picturing this:

```cs
// The handler that will actually "build" the interpolated string"
[InterpolatedStringHandler]
public ref struct TraceLoggerParamsInterpolatedStringHandler
{
    // Storage for the built-up string

    private bool _logLevelEnabled;

    public TraceLoggerParamsInterpolatedStringHandler(int literalLength, int formattedCount, Logger logger, out bool handlerIsValid)
    {
        if (!logger._logLevelEnabled)
        {
            handlerIsValid = false;
            return;
        }

        handlerIsValid = true;
        _logLevelEnabled = logger.EnabledLevel;
    }

    public void AppendLiteral(string s)
    {
        // Store and format part as required
    }

    public void AppendFormatted<T>(T t)
    {
        // Store and format part as required
    }
}

// The logger class. The user has an instance of this, accesses it via static state, or some other access
// mechanism
public class Logger
{
    // Initialization code omitted
    public LogLevel EnabledLevel;

    public void LogTrace([InterpolatedStringHandlerArguments("")]TraceLoggerParamsInterpolatedStringHandler handler)
    {
        // Impl of logging
    }
}

Logger logger = GetLogger(LogLevel.Info);

// Given the above definitions, usage looks like this:
var name = "Fred Silberberg";
logger.LogTrace($"{name} will never be printed because info is < trace!");

// This is converted to:
var name = "Fred Silberberg";
var receiverTemp = logger;
var handler = new TraceLoggerParamsInterpolatedStringHandler(literalLength: 47, formattedCount: 1, receiverTemp, out var handlerIsValid);
if (handlerIsValid)
{
    handler.AppendFormatted(name);
    handler.AppendLiteral(" will never be printed because info is < trace!");
}
receiverTemp.LogTrace(handler);
```

Here, because `TraceLoggerParamsInterpolatedStringHandler` has a constructor with the correct parameters, we say that the interpolated string
has an implicit handler conversion to that parameter, and it lowers to the pattern shown above. The specese needed for this is a bit complicated,
and is expanded below.

The rest of this proposal will use `Append...` to refer to either of `AppendLiteral` or `AppendFormatted` in cases when both are applicable.

#### New attributes

The compiler recognizes the `System.Runtime.CompilerServices.InterpolatedStringHandlerAttribute`:

```cs
using System;
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class InterpolatedStringHandlerAttribute : Attribute
    {
        public InterpolatedStringHandlerAttribute()
        {
        }
    }
}
```

This attribute is used by the compiler to determine if a type is a valid interpolated string handler type.

The compiler also recognizes the `System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentAttribute`:

```cs
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class InterpolatedStringHandlerArgumentAttribute : Attribute
    {
        public InterpolatedHandlerArgumentAttribute(string argument);
        public InterpolatedHandlerArgumentAttribute(params string[] arguments);

        public string[] Arguments { get; }
    }
}
```

This attribute is used on parameters, to inform the compiler how to lower an interpolated string handler pattern used in a parameter position.

#### Interpolated string handler conversion

Type `T` is said to be an _applicable\_interpolated\_string\_handler\_type_ if it is attributed with `System.Runtime.CompilerServices.InterpolatedStringHandlerAttribute`.
There exists an implicit _interpolated\_string\_handler\_conversion_ to `T` from an _interpolated\_string\_expression_, or an _additive\_expression_ composed entirely of
_interpolated\_string\_expression_s and using only `+` operators.

For simplicity in the rest of this speclet, _interpolated\_string\_expression_ refers to both a simple _interpolated\_string\_expression_, and to an _additive\_expression_ composed
entirely of _interpolated\_string\_expression_s and using only `+` operators.

Note that this conversion always exists, regardless of whether there will be later errors when actually attempting to lower the interpolation using the handler pattern. This is
done to help ensure that there are predictable and useful errors and that runtime behavior doesn't change based on the content of an interpolated string.

#### Applicable function member adjustments

We adjust the wording of the applicable function member algorithm ([§11.6.4.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11642-applicable-function-member))
as follows (a new sub-bullet is added to each section, in bold):

A function member is said to be an ***applicable function member*** with respect to an argument list `A` when all of the following are true:
*  Each argument in `A` corresponds to a parameter in the function member declaration as described in Corresponding parameters ([§11.6.2.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11622-corresponding-parameters)), and any parameter to which no argument corresponds is an optional parameter.
*  For each argument in `A`, the parameter passing mode of the argument (i.e., value, `ref`, or `out`) is identical to the parameter passing mode of the corresponding parameter, and
   *  for a value parameter or a parameter array, an implicit conversion ([§10.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/conversions.md#102-implicit-conversions)) exists from the argument to the type of the corresponding parameter, or
   *  **for a `ref` parameter whose type is a struct type, an implicit _interpolated\_string\_handler\_conversion_ exists from the argument to the type of the corresponding parameter, or**
   *  for a `ref` or `out` parameter, the type of the argument is identical to the type of the corresponding parameter. After all, a `ref` or `out` parameter is an alias for the argument passed.

For a function member that includes a parameter array, if the function member is applicable by the above rules, it is said to be applicable in its ***normal form***. If a function member that includes a parameter array is not applicable in its normal form, the function member may instead be applicable in its ***expanded form***:
*  The expanded form is constructed by replacing the parameter array in the function member declaration with zero or more value parameters of the element type of the parameter array such that the number of arguments in the argument list `A` matches the total number of parameters. If `A` has fewer arguments than the number of fixed parameters in the function member declaration, the expanded form of the function member cannot be constructed and is thus not applicable.
*  Otherwise, the expanded form is applicable if for each argument in `A` the parameter passing mode of the argument is identical to the parameter passing mode of the corresponding parameter, and
   *  for a fixed value parameter or a value parameter created by the expansion, an implicit conversion ([§10.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/conversions.md#102-implicit-conversions)) exists from the type of the argument to the type of the corresponding parameter, or
   *  **for a `ref` parameter whose type is a struct type, an implicit _interpolated\_string\_handler\_conversion_ exists from the argument to the type of the corresponding parameter, or**
   *  for a `ref` or `out` parameter, the type of the argument is identical to the type of the corresponding parameter.

Important note: this means that if there are 2 otherwise equivalent overloads, that only differ by the type of the _applicable\_interpolated\_string\_handler\_type_, these overloads will
be considered ambiguous. Further, because we do not see through explicit casts, it is possible that there could arise an unresolvable scenario where both applicable overloads use
`InterpolatedStringHandlerArguments` and are totally uncallable without manually performing the handler lowering pattern. We could potentially make changes to the better function member
algorithm to resolve this if we so choose, but this scenario unlikely to occur and isn't a priority to address.

#### Better conversion from expression adjustments

We change the better conversion from expression ([§11.6.4.4](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11644-better-conversion-from-expression)) section to the
following:

Given an implicit conversion `C1` that converts from an expression `E` to a type `T1`, and an implicit conversion `C2` that converts from an expression `E` to a type `T2`, `C1` is a ***better conversion*** than `C2` if:
1. `E` is a non-constant _interpolated\_string\_expression_, `C1` is an _implicit\_string\_handler\_conversion_, `T1` is an _applicable\_interpolated\_string\_handler\_type_, and `C2` is not an _implicit\_string\_handler\_conversion_, or
2. `E` does not exactly match `T2` and at least one of the following holds:
    * `E` exactly matches `T1` ([§11.6.4.4](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11644-better-conversion-from-expression))
    * `T1` is a better conversion target than `T2` ([§11.6.4.6](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11646-better-conversion-target))

This does mean that there are some potentially non-obvious overload resolution rules, depending on whether the interpolated string in question is a constant-expression or not. For example:

```cs
void Log(string s) { ... }
void Log(TraceLoggerParamsInterpolatedStringHandler p) { ... }

Log($""); // Calls Log(string s), because $"" is a constant expression
Log($"{"test"}"); // Calls Log(string s), because $"{"test"}" is a constant expression
Log($"{1}"); // Calls Log(TraceLoggerParamsInterpolatedStringHandler p), because $"{1}" is not a constant expression
```

This is introduced so that things that can simply be emitted as constants do so, and don't incur any overhead, while things that cannot be constant use the handler pattern.

### InterpolatedStringHandler and Usage

We introduce a new type in `System.Runtime.CompilerServices`: `DefaultInterpolatedStringHandler`. This is a ref struct with many of the same semantics as `ValueStringBuilder`,
intended for direct use by the C# compiler. This struct would look approximately like this:

```cs
// API Proposal issue: https://github.com/dotnet/runtime/issues/50601
namespace System.Runtime.CompilerServices
{
    [InterpolatedStringHandler]
    public ref struct DefaultInterpolatedStringHandler
    {
        public DefaultInterpolatedStringHandler(int literalLength, int formattedCount);
        public string ToStringAndClear();

        public void AppendLiteral(string value);

        public void AppendFormatted<T>(T value);
        public void AppendFormatted<T>(T value, string? format);
        public void AppendFormatted<T>(T value, int alignment);
        public void AppendFormatted<T>(T value, int alignment, string? format);

        public void AppendFormatted(ReadOnlySpan<char> value);
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null);

        public void AppendFormatted(string? value);
        public void AppendFormatted(string? value, int alignment = 0, string? format = null);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null);
    }
}
```

We make a slight change to the rules for the meaning of an _interpolated\_string\_expression_ ([§11.7.3](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#1173-interpolated-string-expressions)):

**If the type of an interpolated string is `string` and the type `System.Runtime.CompilerServices.DefaultInterpolatedStringHandler` exists, and the current context supports using that type, the string**
**is lowered using the handler pattern. The final `string` value is then obtained by calling `ToStringAndClear()` on the handler type.**
**Otherwise, if** the type of an interpolated string is `System.IFormattable` or `System.FormattableString` [the rest is unchanged]

The "and the current context supports using that type" rule is intentionally vague to give the compiler leeway in optimizing usage of this pattern. The handler type is likely to be a ref struct
type, and ref struct types are normally not permitted in async methods. For this particular case, the compiler would be allowed to make use the handler if none of the interpolation holes contain
an `await` expression, as we can statically determine that the handler type is safely used without additional complicated analysis because the handler will be dropped after the interpolated string
expression is evaluated.

**~~Open~~ Question**:

Do we want to instead just make the compiler know about `DefaultInterpolatedStringHandler` and skip the `string.Format` call entirely? It would allow us to hide a method that we don't necessarily
want to put in people's faces when they manually call `string.Format`.

_Answer_: Yes.

**~~Open~~ Question**:

Do we want to have handlers for `System.IFormattable` and `System.FormattableString` as well?

_Answer_: No.

### Handler pattern codegen

In this section, method invocation resolution refers to the steps listed in [§11.7.8.2](https://github.com/dotnet/csharpstandard/blob/draft-v6/standard/expressions.md#11782-method-invocations).

#### Constructor resolution

Given an _applicable\_interpolated\_string\_handler\_type_ `T` and an _interpolated\_string\_expression_ `i`, method invocation resolution and validation for a valid constructor on `T`
is performed as follows:

1. Member lookup for instance constructors is performed on `T`. The resulting method group is called `M`.
2. The argument list `A` is constructed as follows:
    1. The first two arguments are integer constants, representing the literal length of `i`, and the number of _interpolation_ components in `i`, respectively.
    2. If `i` is used as an argument to some parameter `pi` in method `M1`, and parameter `pi` is attributed with `System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentAttribute`,
    then for every name `Argx` in the `Arguments` array of that attribute the compiler matches it to a parameter `px` that has the same name. The empty string is matched to the receiver
    of `M1`.
        * If any `Argx` is not able to be matched to a parameter of `M1`, or an `Argx` requests the receiver of `M1` and `M1` is a static method, an error is produced and no further
        steps are taken.
        * Otherwise, the type of every resolved `px` is added to the argument list, in the order specified by the `Arguments` array. Each `px` is passed with the same `ref` semantics as is specified in `M1`.
    3. The final argument is a `bool`, passed as an `out` parameter.
3. Traditional method invocation resolution is performed with method group `M` and argument list `A`. For the purposes of method invocation final validation, the context of `M` is treated
as a _member\_access_ through type `T`.
    * If a single-best constructor `F` was found, the result of overload resolution is `F`.
    * If no applicable constructors were found, step 3 is retried, removing the final `bool` parameter from `A`. If this retry also finds no applicable members, an error is produced and
    no further steps are taken.
    * If no single-best method was found, the result of overload resolution is ambiguous, an error is produced, and no further steps are taken.
4. Final validation on `F` is performed.
    * If any element of `A` occurred lexically after `i`, an error is produced and no further steps are taken.
    * If any `A` requests the receiver of `F`, and `F` is an indexer being used as an _initializer\_target_ in a _member\_initializer_, then an error is reported and no further steps are taken.

Note: the resolution here intentionally do _not_ use the actual expressions passed as other arguments for `Argx` elements. We only consider the types post-conversion. This makes sure that we
don't have double-conversion issues, or unexpected cases where a lambda is bound to one delegate type when passed to `M1` and bound to a different delegate type when passed to `M`.

Note: We report an error for indexers uses as member initializers because of the order of evaluation for nested member initializers. Consider this code snippet:

```cs

var x1 = new C1 { C2 = { [GetString()] = { A = 2, B = 4 } } };

/* Lowering:
__c1 = new C1();
string argTemp = GetString();
__c1.C2[argTemp][1] = 2;
__c1.C2[argTemp][3] = 4;

Prints:
GetString
get_C2
get_C2
*/

string GetString()
{
    Console.WriteLine("GetString");
    return "";
}

class C1
{
    private C2 c2 = new C2();
    public C2 C2 { get { Console.WriteLine("get_C2"); return c2; } set { } }
}

class C2
{
    public C3 this[string s]
    {
        get => new C3();
        set { }
    }
}

class C3
{
    public int A
    {
        get => 0;
        set { }
    }
    public int B
    {
        get => 0;
        set { }
    }
}
```

The arguments to `__c1.C2[]` are evaluated _before_ the receiver of the indexer. While we could come up with a lowering that works for this scenario (either by creating a temp for `__c1.C2`
and sharing it across both indexer invocations, or only using it for the first indexer invocation and sharing the argument across both invocations) we think that any lowering would be
confusing for what we believe is a pathological scenario. Therefore, we forbid the scenario entirely.

**~~Open Question~~**:

If we use a constructor instead of `Create`, we'd improve runtime codegen, at the expense of narrowing the pattern a bit.

_Answer_: We will restrict to constructors for now. We can revisit adding a general `Create` method later if the scenario arises.

#### `Append...` method overload resolution

Given an _applicable\_interpolated\_string\_handler\_type_ `T` and an _interpolated\_string\_expression_ `i`, overload resolution for a set of valid `Append...` methods on `T` is
performed as follows:

1. If there are any _interpolated\_regular\_string\_character_ components in `i`:
    1. Member lookup on `T` with the name `AppendLiteral` is performed. The resulting method group is called `Ml`.
    2. The argument list `Al` is constructed with one value parameter of type `string`.
    3. Traditional method invocation resolution is performed with method group `Ml` and argument list `Al`. For the purposes of method invocation final validation, the context of `Ml`
    is treated as a _member\_access_ through an instance of `T`.
        * If a single-best method `Fi` is found and no errors were produced, the result of method invocation resolution is `Fi`.
        * Otherwise, an error is reported.
2. For every _interpolation_ `ix` component of `i`:
    1. Member lookup on `T` with the name `AppendFormatted` is performed. The resulting method group is called `Mf`.
    2. The argument list `Af` is constructed:
        1. The first parameter is the `expression` of `ix`, passed by value.
        2. If `ix` directly contains a _constant\_expression_ component, then an integer value parameter is added, with the name `alignment` specified.
        3. If `ix` is directly followed by an _interpolation\_format_, then a string value parameter is added, with the name `format` specified.
    3. Traditional method invocation resolution is performed with method group `Mf` and argument list `Af`. For the purposes of method invocation final validation, the context of `Mf`
    is treated as a _member\_access_ through an instance of `T`.
        * If a single-best method `Fi` is found, the result of method invocation resolution is `Fi`.
        * Otherwise, an error is reported.
3. Finally, for every `Fi` discovered in steps 1 and 2, final validation is performed:
    * If any `Fi` does not return `bool` by value or `void`, an error is reported.
    * If all `Fi` do not return the same type, an error is reported.


Note that these rules do not permit extension methods for the `Append...` calls. We could consider enabling that if we choose, but this is analogous to the enumerator
pattern, where we allow `GetEnumerator` to be an extension method, but not `Current` or `MoveNext()`.

These rules _do_ permit default parameters for the `Append...` calls, which will work with things like `CallerLineNumber` or `CallerArgumentExpression` (when supported by
the language).

We have separate overload lookup rules for base elements vs interpolation holes because some handlers will want to be able to understand the difference between the components
that were interpolated and the components that were part of the base string.

**~~Open~~ Question**

Some scenarios, like structured logging, want to be able to provide names for interpolation elements. For example, today a logging call might look like
`Log("{name} bought {itemCount} items", name, items.Count);`. The names inside the `{}` provide important structure information for loggers that help with ensuring output
is consistent and uniform. Some cases might be able to reuse the `:format` component of an interpolation hole for this, but many loggers already understand format specifiers
and have existing behavior for output formatting based on this info. Is there some syntax we can use to enable putting these named specifiers in?

Some cases may be able to get away with `CallerArgumentExpression`, provided that support does land in C# 10. But for cases that invoke a method/property, that may not be
sufficient.

_Answer_:

While there are some interesting parts to templated strings we could explore in an orthogonal language feature, we don't think a specific syntax here has much benefit over
solutions such as using a tuple: `$"{("StructuredCategory", myExpression)}"`.

#### Performing the conversion

Given an _applicable\_interpolated\_string\_handler\_type_ `T` and an _interpolated\_string\_expression_ `i` that had a valid constructor `Fc` and `Append...` methods `Fa` resolved,
lowering for `i` is performed as follows:

1. Any arguments to `Fc` that occur lexically before `i` are evaluated and stored into temporary variables in lexical order. In order to preserve lexical ordering, if `i` occurred as part
of a larger expression `e`, any components of `e` that occurred before `i` will be evaluated as well, again in lexical order.
2. `Fc` is called with the length of the interpolated string literal components, the number of _interpolation_ holes, any previously evaluated arguments, and a `bool` out argument
(if `Fc` was resolved with one as the last parameter). The result is stored into a temporary value `ib`.
    1. The length of the literal components is calculated after replacing any _open_brace_escape_sequence_ with a single `{`, and any _close_brace_escape_sequence_
    with a single `}`.
3. If `Fc` ended with a `bool` out argument, a check on that `bool` value is generated. If true, the methods in `Fa` will be called. Otherwise, they will not be called.
4. For every `Fax` in `Fa`, `Fax` is called on `ib` with either the current literal component or _interpolation_ expression, as appropriate. If `Fax` returns a `bool`, the result is
logically anded with all preceding `Fax` calls.
    1. If `Fax` is a call to `AppendLiteral`, the literal component is unescaped by replacing any _open_brace_escape_sequence_ with a single `{`, and any _close_brace_escape_sequence_
    with a single `}`.
5. The result of the conversion is `ib`.

Again, note that arguments passed to `Fc` and arguments passed to `e` are the same temp. Conversions may occur on top of the temp to convert to a form that `Fc` requires, but for example
lambdas cannot be bound to a different delegate type between `Fc` and `e`.

**~~Open~~ Question**

This lowering means that subsequent parts of the interpolated string after a false-returning `Append...` call don't get evaluated. This could potentially be very confusing, particularly
if the format hole is side-effecting. We could instead evaluate all format holes first, then repeatedly call `Append...` with the results, stopping if it returns false. This would ensure
that all expressions get evaluated as one might expect, but we call as few methods as we need to. While the partial evaluation might be desirable for some more advanced cases, it is perhaps
non-intuitive for the general case.

Another alternative, if we want to always evaluate all format holes, is to remove the `Append...` version of the API and just do repeated `Format` calls. The handler can track whether it
should just be dropping the argument and immediately returning for this version.

_Answer_: We will have conditional evaluation of the holes.

**~~Open~~ Question**

Do we need to dispose of disposable handler types, and wrap calls with try/finally to ensure that Dispose is called? For example, the interpolated string handler in the bcl might have a
rented array inside it, and if one of the interpolation holes throws an exception during evaluation, that rented array could be leaked if it wasn't disposed.

_Answer_: No. handlers can be assigned to locals (such as `MyHandler handler = $"{MyCode()};`), and the lifetime of such handlers is unclear. Unlike foreach enumerators, where the lifetime
is obvious and no user-defined local is created for the enumerator.

### Impact on nullable reference types

To minimize complexity of the implementation, we have a few limitations on how we perform nullable analysis on interpolated string handler constructors used as arguments to a method or indexer.
In particular, we do not flow information from the constructor back through to the original slots of parameters or arguments from the original context, and we do not use constructor parameter
types to inform generic type inference for type parameters in the containing method. An example of where this can have an impact is:

```cs
string s = "";
C c = new C();
c.M(s, $"", c.ToString(), s.ToString()); // No warnings on c.ToString() or s.ToString(), as the `MaybeNull` does not flow back.

public class C
{
    public void M(string s1, [InterpolatedStringHandlerArgument("", "s1")] CustomHandler c1, string s2, string s3) { }
}

[InterpolatedStringHandler]
public partial struct CustomHandler
{
    public CustomHandler(int literalLength, int formattedCount, [MaybeNull] C c, [MaybeNull] string s) : this()
    {
    }
}
```

```cs
string? s = null;
M(s, $""); // Infers `string` for `T` because of the `T?` parameter, not `string?`, as flow analysis does not consider the unannotated `T` parameter of the constructor

void M<T>(T? t, [InterpolatedStringHandlerArgument("s1")] CustomHandler<T> c) { }

[InterpolatedStringHandler]
public partial struct CustomHandler<T>
{
    public CustomHandler(int literalLength, int formattedCount, T t) : this()
    {
    }
}
```

## Other considerations

### Allow `string` types to be convertible to handlers as well

For type author simplicity, we could consider allowing expressions of type `string` to be implicitly-convertible to _applicable\_interpolated\_string\_handler\_types_. As proposed today,
authors will likely need to overload on both that handler type and regular `string` types, so their users don't have to understand the difference. This may be an annoying and non-obvious
overhead, as a `string` expression can be viewed as an interpolation with `expression.Length` prefilled length and 0 holes to be filled.

This would allow new APIs to only expose a handler, without also having to expose a `string`-accepting overload. However, it won't get around the need for changes to better conversion from
expression, so while it would work it may be unnecessary overhead.

_Answer_:

We think that this could end up being confusing, and there's an easy workaround for custom handler types: add a user-defined conversion from string.

### Incorporating spans for heap-less strings

`ValueStringBuilder` as it exists today has 2 constructors: one that takes a count, and allocates on the heap eagerly, and one that takes a `Span<char>`. That `Span<char>` is usually
a fixed size in the runtime codebase, around 250 elements on average. To truly replace that type, we should consider an extension to this where we also recognize `GetInterpolatedString`
methods that take a `Span<char>`, instead of just the count version. However, we see a few potential thorny cases to resolve here:

* We don't want to stackalloc repeatedly in a hot loop. If we were to do this extension to the feature, we'd likely want to share the stackalloc'd span between loop
iterations. We know this is safe, as `Span<T>` is a ref struct that can't be stored on the heap, and users would have to be pretty devious to manage to extract a
reference to that `Span` (such as creating a method that accepts such a handler then deliberately retrieving the `Span` from the handler and returning it to the
caller). However, allocating ahead of time produces other questions:
    * Should we eagerly stackalloc? What if the loop is never entered, or exits before it needs the space?
    * If we don't eagerly stackalloc, does that mean we introduce a hidden branch on every loop? Most loops likely won't care about this, but it could affect some tight loops that don't
    want to pay the cost.
* Some strings can be quite big, and the appropriate amount to `stackalloc` is dependent on a number of factors, including runtime factors. We don't really want the C# compiler and
specification to have to determine this ahead of time, so we'd want to resolve https://github.com/dotnet/runtime/issues/25423 and add an API for the compiler to call in these cases. It
also adds more pros and cons to the points from the previous loop, where we don't want to potentially allocate large arrays on the heap many times or before one is needed.

_Answer_:

This is out of scope for C# 10. We can look at this in general when we look at the more general `params Span<T>` feature.

### Non-try version of the API

For simplicity, this spec currently just proposes recognizing a `Append...` method, and things that always succeed (like `InterpolatedStringHandler`) would always return true from the method.
This was done to support partial formatting scenarios where the user wants to stop formatting if an error occurs or if it's unnecessary, such as the logging case, but could potentially
introduce a bunch of unnecessary branches in standard interpolated string usage. We could consider an addendum where we use just `FormatX` methods if no `Append...` method is present, but
it does present questions about what we do if there's a mix of both `Append...` and `FormatX` calls.

_Answer_:

We want the non-try version of the API. The proposal has been updated to reflect this.

### Passing previous arguments to the handler

There is unfortunate lack of symmetry in the proposal at it currently exists: invoking an extension method in reduced form produces different semantics than invoking the extension method in
normal form. This is different from most other locations in the language, where reduced form is just a sugar. We propose adding an attribute to the framework that we will recognize when
binding a method, that informs the compiler that certain parameters should be passed to the constructor on the handler. Usage looks like this:

```cs
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class InterpolatedStringHandlerArgumentAttribute : Attribute
    {
        public InterpolatedStringHandlerArgumentAttribute(string argument);
        public InterpolatedStringHandlerArgumentAttribute(params string[] arguments);

        public string[] Arguments { get; }
    }
}
```

Usage of this is then:

```cs
namespace System
{
    public sealed class String
    {
        public static string Format(IFormatProvider? provider, [InterpolatedStringHandlerArgument("provider")] ref DefaultInterpolatedStringHandler handler);
        …
    }
}

namespace System.Runtime.CompilerServices
{
    public ref struct DefaultInterpolatedStringHandler
    {
        public DefaultInterpolatedStringHandler(int baseLength, int holeCount, IFormatProvider? provider); // additional factory
        …
    }
}

var formatted = string.Format(CultureInfo.InvariantCulture, $"{X} = {Y}");

// Is lowered to

var tmp1 = CultureInfo.InvariantCulture;
var handler = new DefaultInterpolatedStringHandler(3, 2, tmp1);
handler.AppendFormatted(X);
handler.AppendLiteral(" = ");
handler.AppendFormatted(Y);
var formatted = string.Format(tmp1, handler);
```

The questions we need to answer:

1. Do we like this pattern in general?
2. Do we want to allow these arguments to come from after the handler parameter? Some existing patterns in the BCL, such as `Utf8Formatter`, put the value to be formatted _before_ the thing
needed to format into. To fit in best with these patterns, we'd likely want to allow this, but we need to decide if this out-of-order evaluate is ok.

_Answer_:

We want to support this. The spec has been updated to reflect this. Arguments will be required to be specified in lexical order at the call site, and if a needed argument to the create method
is specified after the interpolated string literal, an error is produced.

### `await` usage in interpolation holes

Because `$"{await A()}"` is a valid expression today, we need to rationalize how interpolation holes with await. We could solve this with a few rules:

1. If an interpolated string used as a `string`, `IFormattable`, or `FormattableString` has an `await` in an interpolation hole, fall back to old-style formatter.
2. If an interpolated string is subject to an _implicit\_string\_handler\_conversion_ and _applicable\_interpolated\_string\_handler\_type_ is a `ref struct`, `await` is not allowed to be used
in the format holes.

Fundamentally, this desugaring could use a ref struct in an async method as long as we guarantee that the `ref struct` will not need to be saved to the heap, which should be possible if we forbid
`await`s in the interpolation holes.

Alternatively, we could simply make all handler types non-ref structs, including the framework handler for interpolated strings. This would, however, preclude us from someday recognizing a `Span`
version that does not need to allocate any scratch space at all.

_Answer_:

We will treat interpolated string handlers the same as any other type: this means that if the handler type is a ref struct and the current context doesn't allow the usage of ref structs, it is
illegal to use handler here. The spec around lowering of string literals used as strings is intentionally vague to allow the compiler to decide on what rules it deems appropriate, but for custom
handler types they will have to follow the same rules as the rest of the language.

### Handlers as ref parameters

Some handlers might want to be passed as ref parameters (either `in` or `ref`). Should we allow either? And if so, what will a `ref` handler look like? `ref $""` is confusing, as you're not actually
passing the string by ref, you're passing the handler that is created from the ref by ref, and has similar potential issues with async methods.

_Answer_:

We want to support this. The spec has been updated to reflect this. The rules should reflect the same rules that apply to extension methods on value types.

### Interpolated strings through binary expressions and conversions

Because this proposal makes interpolated strings context sensitive, we would like to allow the compiler to treat a binary expression composed entirely of interpolated strings,
or an interpolated string subjected to a cast, as an interpolated string literal for the purposes of overload resolution. For example, take the following scenario:

```cs
struct Handler1
{
    public Handler1(int literalLength, int formattedCount, C c) => ...;
    // AppendX... methods as necessary
}
struct Handler2
{
    public Handler2(int literalLength, int formattedCount, C c) => ...;
    // AppendX... methods as necessary
}

class C
{
    void M(Handler1 handler) => ...;
    void M(Handler2 handler) => ...;
}

c.M($"{X}"); // Ambiguous between the M overloads
```

This would be ambiguous, necessitating a cast to either `Handler1` or `Handler2` in order to resolve. However, in making that cast, we would potentially throw away the information
that there is context from the method receiver, meaning that the cast would fail because there is nothing to fill in the information of `c`. A similar issue arises with binary concatenation
of strings: the user could want to format the literal across several lines to avoid line wrapping, but would not be able to because that would no longer be an interpolated string literal
convertible to the handler type.

To resolve these cases, we make the following changes:

* An _additive\_expression_ composed entirely of _interpolated\_string\_expressions_ and using only `+` operators is considered to be an _interpolated\_string\_literal_ for the purposes of
conversions and overload resolution. The final interpolated string is created by logically concatinating all individual _interpolated\_string\_expression_ components, from left to right.
* A _cast\_expression_ or a _relational\_expression_ with operator `as` whose operand is an _interpolated\_string\_expressions_ is considered an _interpolated\_string\_expressions_ for the
purposes of conversions and overload resolution.

**Open Questions**:

Do we want to do this? We don't do this for `System.FormattableString`, for example, but that can be broken out onto a different line, whereas this can be context-dependent and therefore not
able to be broken out into a different line. There are also no overload resolution concerns with `FormattableString` and `IFormattable`.

_Answer_:

We think that this is a valid use case for additive expressions, but that the cast version is not compelling enough at this time. We can add it later if necessary. The spec has been updated to
reflect this decision.

## Other use cases

See https://github.com/dotnet/runtime/issues/50635 for examples of proposed handler APIs using this pattern.
