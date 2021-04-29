# Improved Interpolated Strings

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
3. There is no opportunity to avoid instanciating the instance if it's not needed. Logging frameworks, for example, will recommend avoiding string interpolation
because it will cause a string to be realized that may not be needed, depending on the current log-level of the application.
4. It can never use `Span` or other ref struct types today, because ref structs are not allowed as generic type parameters, meaning that if a user wants to avoid
copying to intermediate locations they have to manually format strings.

Internally, the runtime has a type called `ValueStringBuilder` to help deal with the first 2 of these scenarios. They pass a stackalloc'd buffer to the builder,
repeatedly call `AppendFormat` with every part, and then get a final string out. If the resulting string goes past the bounds of the stack buffer, they can then
move to an array on the heap. However, this type is dangerous to expose directly, as incorrect usage could lead to a rented array to be double-disposed, which
then will cause all sorts of undefined behavior in the program as two locations think they have sole access to the rented array. This proposal creates a way to
use this type safely from native C# code by just writing an interpolated string literal, leaving written code unchanged while improving every interpolated string
that a user writes. It also extends this pattern to allow for interpolated strings passed as arguments to other methods to use a builder pattern, defined by
receiver of the method, that will allow things like logging frameworks to avoid allocating strings that will never be needed, and giving C# users familiar,
convenient interpolation syntax.

## Detailed Design

### The builder pattern

We introduce a new builder pattern that can represent an interpolated string passed as an argument to a method. The simple English of the pattern is as follows:

When an _interpolated\_string\_expression_ is passed as an argument to a method, we look at the type of the parameter. If the parameter type has a static method
`Create` that can be invoked with 2 int parameters, `literalLength` and `formattedCount`, optionally takes a parameter the receiver is convertible to,
and has an out parameter of the type of original method's parameter and that type has instance `AppendLiteral` and `AppendFormatted` methods that
can be invoked for every part of the interpolated string, then we lower the interpolation using that, instead of into a traditional call to
`string.Format(formatStr, args)`. A more concrete example is helpful for picturing this:

```cs
// The builder that will actually "build" the interpolated string"
[InterpolatedStringBuilder]
public ref struct TraceLoggerParamsBuilder
{
    public static TraceLoggerParamsBuilder Create(int literalLength, int formattedCount, Logger logger, out bool builderIsValid)
    {
        if (!logger._logLevelEnabled)
        {
            builderIsValid = false;
            return default;
        }

        builderIsValid = true;
        return TraceLoggerParamsBuilder(literalLength, formattedCount, logger.EnabledLevel);
    }

    // Storage for the built-up string

    private bool _logLevelEnabled;

    private TraceLoggerParamsBuilder(int literalLength, int formattedCount, bool logLevelEnabled)
    {
        // Initialization logic
        _logLevelEnabled = logLevelEnabled
    }

    public bool AppendLiteral(string s)
    {
        // Store and format part as required
        return true;
    }

    public bool AppendFormatted<T>(T t)
    {
        // Store and format part as required
        return true;
    }
}

// The logger class. The user has an instance of this, accesses it via static state, or some other access
// mechanism
public class Logger
{
    // Initialization code omitted
    public LogLevel EnabledLevel;

    public void LogTrace([InterpolatedStringBuilderArguments("")]TraceLoggerParamsBuilder builder)
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
var builder = TraceLoggerParamsBuilder.Create(literalLength: 47, formattedCount: 1, receiverTemp, out var builderIsValid);
_ = builderIsValid &&
    builder.AppendFormatted(name) &&
    builder.AppendLiteral(" will never be printed because info is < trace!");
receiverTemp.LogTrace(builder);
```

Here, because `TraceLoggerParamsBuilder` has static method called `Create` with the correct parameters and returns the type the `LogTrace` call was expecting,
we say that the interpolated string has an implicit builder conversion to that parameter, and it lowers to the pattern shown above. The specese needed for this
is a bit complicated, and is expanded below.

The rest of this proposal will use `Append...` to refer to either of `AppendLiteral` or `AppendFormatted` in cases when both are applicable.

#### New attributes

The compiler recognizes the `System.Runtime.CompilerServices.InterpolatedStringBuilderAttribute`:

```cs
using System;
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class InterpolatedStringBuilderAttribute : Attribute
    {
        public InterpolatedStringBuilderAttribute()
        {
        }
    }
}
```

This attribute is used by the compiler to determine if a type is a valid interpolated string builder type.

The compiler also recognizes the `System.Runtime.CompilerServices.InterpolatedStringBuilderArgumentAttribute`:

```cs
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class InterpolatedStringBuilderArgumentAttribute : Attribute
    {
        public InterpolatedBuilderArgumentAttribute(string argument);
        public InterpolatedBuilderArgumentAttribute(params string[] arguments);

        public string[] Arguments { get; }
    }
}
```

This attribute is used on parameters, to inform the compiler how to lower an interpolated string builder pattern used in a parameter position.

#### Interpolated string builder conversion

Type `T` is said to be an _applicable\_interpolated\_string\_builder\_type_ if it is attributed with `System.Runtime.CompilerServices.InterpolatedStringBuilderAttribute`.
There exists an implicit _interpolated\_string\_builder\_conversion_ to `T` from an _interpolated\_string\_expression_, or an _additive\_expression_ composed entirely of
_interpolated\_string\_expression_s and using only `+` operators.

For simplicity in the rest of this speclet, _interpolated\_string\_expression_ refers to both a simple _interpolated\_string\_expression_, and to an _additive\_expression_ composed
entirely of _interpolated\_string\_expression_s and using only `+` operators.

Note that this conversion always exists, regardless of whether there will be later errors when actually attempting to lower the interpolation using the builder pattern. This is
done to help ensure that there are predictable and useful errors and that runtime behavior doesn't change based on the content of an interpolated string.

#### Applicable function member adjustments

We adjust the wording of the [applicable function member algorithm](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#applicable-function-member)
as follows (a new sub-bullet is added to each section, in bold):

A function member is said to be an ***applicable function member*** with respect to an argument list `A` when all of the following are true:
*  Each argument in `A` corresponds to a parameter in the function member declaration as described in [Corresponding parameters](expressions.md#corresponding-parameters), and any parameter to which no argument corresponds is an optional parameter.
*  For each argument in `A`, the parameter passing mode of the argument (i.e., value, `ref`, or `out`) is identical to the parameter passing mode of the corresponding parameter, and
   *  for a value parameter or a parameter array, an implicit conversion ([Implicit conversions](conversions.md#implicit-conversions)) exists from the argument to the type of the corresponding parameter, or
   *  **for a `ref` parameter whose type is a struct type, an implicit _interpolated\_string\_builder\_conversion_ exists from the argument to the type of the corresponding parameter, or**
   *  for a `ref` or `out` parameter, the type of the argument is identical to the type of the corresponding parameter. After all, a `ref` or `out` parameter is an alias for the argument passed.

For a function member that includes a parameter array, if the function member is applicable by the above rules, it is said to be applicable in its ***normal form***. If a function member that includes a parameter array is not applicable in its normal form, the function member may instead be applicable in its ***expanded form***:
*  The expanded form is constructed by replacing the parameter array in the function member declaration with zero or more value parameters of the element type of the parameter array such that the number of arguments in the argument list `A` matches the total number of parameters. If `A` has fewer arguments than the number of fixed parameters in the function member declaration, the expanded form of the function member cannot be constructed and is thus not applicable.
*  Otherwise, the expanded form is applicable if for each argument in `A` the parameter passing mode of the argument is identical to the parameter passing mode of the corresponding parameter, and
   *  for a fixed value parameter or a value parameter created by the expansion, an implicit conversion ([Implicit conversions](conversions.md#implicit-conversions)) exists from the type of the argument to the type of the corresponding parameter, or
   *  **for a `ref` parameter whose type is a struct type, an implicit _interpolated\_string\_builder\_conversion_ exists from the argument to the type of the corresponding parameter, or**
   *  for a `ref` or `out` parameter, the type of the argument is identical to the type of the corresponding parameter.

Important note: this means that if there are 2 otherwise equivalent overloads, that only differ by the type of the _applicable\_interpolated\_string\_builder\_type_, these overloads will
be considered ambiguous. Further, because we do not see through explicit casts, it is possible that there could arise an unresolvable scenario where both applicable overloads use
`InterpolatedStringBuilderArguments` and are totally uncallable without manually performing the builder lowering pattern. We could potentially make changes to the better function member
algorithm to resolve this if we so choose, but this scenario unlikely to occur and isn't a priority to address.

#### Better conversion from expression adjustments

We change the [better conversion from expression](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#better-conversion-from-expression) section to the
following:

Given an implicit conversion `C1` that converts from an expression `E` to a type `T1`, and an implicit conversion `C2` that converts from an expression `E` to a type `T2`, `C1` is a ***better conversion*** than `C2` if:
1. `E` is a non-constant _interpolated\_string\_expression_, `C1` is an _implicit\_string\_builder\_conversion_, `T1` is an _applicable\_interpolated\_string\_builder\_type_, and `C2` is not an _implicit\_string\_builder\_conversion_, or
2. `E` does not exactly match `T2` and at least one of the following holds:
    * `E` exactly matches `T1` ([Exactly matching Expression](expressions.md#exactly-matching-expression))
    * `T1` is a better conversion target than `T2` ([Better conversion target](expressions.md#better-conversion-target))

This does mean that there are some potentially non-obvious overload resolution rules, depending on whether the interpolated string in question is a constant-expression or not. For example:

```cs
void Log(string s) { ... }
void Log(TraceLoggerParamsBuilder p) { ... }

Log($""); // Calls Log(string s), because $"" is a constant expression
Log($"{"test"}"); // Calls Log(string s), because $"{"test"}" is a constant expression
Log($"{1}"); // Calls Log(TraceLoggerParamsBuilder p), because $"{1}" is not a constant expression
```

This is introduced so that things that can simply be emitted as constants do so, and don't incur any overhead, while things that cannot be constant use the builder pattern.

### InterpolatedStringBuilder and Usage

We introduce a new type in `System.Runtime.CompilerServices`: `InterpolatedStringBuilder`. This is a ref struct with many of the same semantics as `ValueStringBuilder`,
intended for direct use by the C# compiler. This struct would look approximately like this:

```cs
// API Proposal issue: https://github.com/dotnet/runtime/issues/50601
namespace System.Runtime.CompilerServices
{
    [InterpolatedStringBuilder]
    public ref struct InterpolatedStringDefaultBuilder
    {
        public static InterpolatedStringDefaultBuilder Create(int literalLength, int formattedCount);
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

We make a slight change to the rules for the meaning of an [_interpolated\_string\_expression_](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#interpolated-strings):

**If the type of an interpolated string is `string` and the type `System.Runtime.CompilerServices.InterpolatedStringDefaultBuilder` exists, and the current context supports using that type, the string**
**is lowered using the builder pattern. The final `string` value is then obtained by calling `ToStringAndClear()` on the builder type.**
**Otherwise, if** the type of an interpolated string is `System.IFormattable` or `System.FormattableString` [the rest is unchanged]

The "and the current context supports using that type" rule is intentionally vague to give the compiler leeway in optimizing usage of this pattern. The builder type is likely to be a ref struct
type, and ref struct types are normally not permitted in async methods. For this particular case, the compiler would be allowed to make use the builder if none of the interpolation holes contain
an `await` expression, as we can statically determine that the builder type is safely used without additional complicated analysis because the builder will be dropped after the interpolated string
expression is evaluated.

**~~Open~~ Question**:

Do we want to instead just make the compiler know about `InterpolatedStringBuilder` and skip the `string.Format` call entirely? It would allow us to hide a method that we don't necessarily
want to put in people's faces when they manually call `string.Format`.

_Answer_: Yes.

**~~Open~~ Question**:

Do we want to have builders for `System.IFormattable` and `System.FormattableString` as well?

_Answer_: No.

### Builder pattern codegen

In this section, method invocation resolution refers to the steps listed [here](https://github.com/dotnet/csharplang/blob/main/spec/expressions.md#method-invocations).

#### `Create` method invocation resolution

Given an _applicable\_interpolated\_string\_builder\_type_ `T` and an _interpolated\_string\_expression_ `i`, method invocation resolution and validation for a valid `Create` method on `T`
is performed as follows:

1. Member lookup for members with the name `Create` is performed on `T`. The resulting method group is called `M`.
2. The argument list `A` is constructed as follows:
    1. The first two arguments are integer constants, representing the literal length of the `i`, and the number of _interpolation_ components in the `i`, respectively.
    2. If `i` is used as an argument to some parameter `pi` in method `M1`, and parameter `pi` is attributed with `System.Runtime.CompilerServices.InterpolatedStringBuilderArgumentAttribute`,
    then for every name `Argx` in the `Arguments` array of that attribute the compiler matches it to a parameter `px` that has the same name. The empty string is matched to the receiver
    of `M1`.
        * If any `Argx` is not able to be matched to a parameter of `M1`, or an `Argx` requests the receiver of `M1` and `M1` is a static method, an error is produced and no further
        steps are taken.
        * Otherwise, the type of every resolved `px` is added to the argument list, in the order specified by the `Arguments` array. These are all passed by value.
    3. The final argument is a `bool`, passed as an `out` parameter.
3. Traditional method invocation resolution is performed with method group `M` and argument list `A`. For the purposes of method invocation final validation, the context of `M` is treated
as a _member\_access_ through type `T`.
    * If a single-best method `F` was found, the result of overload resolution is `F`.
    * If no applicable methods were found, step 3 is retried, removing the final `bool` parameter from `A`. If this retry also finds no applicable members, an error is produced and
    no further steps are taken.
    * If no single-best method was found, the result of overload resolution is ambiguous, an error is produced, and no further steps are taken.
4. Final validation on `F` is performed.
    * If `F`'s return type is not a `T`, an error is produced and no further steps are taken.
    * If any element of `A` occurred lexically after `i`, an error is produced and no further steps are taken.

By requiring `Create` to be a `static` method instead of a constructor, we allow the implementation to pool builders if it so decides to.
If we limited the pattern to constructors, then the implementation would be required to always return new instances.

Additionally, by returning the builder type instead of requiring it to go in an out parameter, we marginally improve the codegen and significantly simplify the rules around
ref struct lifetimes vs the traditional .NET `TryX` pattern, and we expect a number of these builder types to be ref structs.

This can and will be impacted by abstract statics in interfaces for generic contexts. We will need to make sure the interactions are considered and tested.

**Open Question**:

If we use a constructor instead of `Create`, we'd improve runtime codegen, at the expense of narrowing the pattern a bit.

#### `Append...` method overload resolution

Given an _applicable\_interpolated\_string\_builder\_type_ `T` and an _interpolated\_string\_expression_ `i`, overload resolution for a set of valid `Append...` methods on `T` is
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

We have separate overload lookup rules for base elements vs interpolation holes because some builders will want to be able to understand the difference between the components
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

Given an _applicable\_interpolated\_string\_builder\_type_ `T` and an _interpolated\_string\_expression_ `i` that had a valid `Create` method `Fc` and `Append...` methods `Fa` resolved,
lowering for `i` is performed as follows:

1. Any arguments to `Fc` that occur lexically before `i` are evaluated and stored into temporary variables in lexical order. In order to preserve lexical ordering, if `i` occurred as part
of a larger expression `e`, any components of `e` that occurred before `i` will be evaluated as well, again in lexical order.
2. `Fc` is called with the length of the interpolated string literal components, the number of _interpolation_ holes, any previously evaluated arguments, and a `bool` out argument
(if `Fc` was resolved with one as the last parameter). The result is stored into a temporary value `ib`.
3. If `Fc` ended with a `bool` out argument, a check on that `bool` value is generated. If true, the methods in `Fa` will be called. Otherwise, they will not be called.
4. For every `Fax` in `Fa`, `Fax` is called on `ib` with either the current literal component or _interpolation_ expression, as appropriate. If `Fax` returns a `bool`, the result is
logically anded with all preceeding `Fax` calls.
5. The result of the conversion is `ib`.

**~~Open~~ Question**

This lowering means that subsequent parts of the interpolated string after a false-returning `Append...` call don't get evaluated. This could potentially be very confusing, particularly
if the format hole is side-effecting. We could instead evaluate all format holes first, then repeatedly call `Append...` with the results, stopping if it returns false. This would ensure
that all expressions get evaluated as one might expect, but we call as few methods as we need to. While the partial evaluation might be desirable for some more advanced cases, it is perhaps
non-intuitive for the general case.

Another alternative, if we want to always evaluate all format holes, is to remove the `Append...` version of the API and just do repeated `Format` calls. The builder can track whether it
should just be dropping the argument and immediately returning for this version.

_Answer_: We will have conditional evaluation of the holes.

**~~Open~~ Question**

Do we need to dispose of disposable builder types, and wrap calls with try/finally to ensure that Dispose is called? For example, the interpolated string builder in the bcl might have a
rented array inside it, and if one of the interpolation holes throws an exception during evaluation, that rented array could be leaked if it wasn't disposed.

_Answer_: No. Builders can be assigned to locals (such as `MyBuilder builder = $"{MyCode()};`), and the lifetime of such builders is unclear. Unlike foreach enumerators, where the lifetime
is obvious and no user-defined local is created for the enumerator.

## Other considerations

### Allow `string` types to be convertible to builders as well

For type author simplicity, we could consider allowing expressions of type `string` to be implicitly-convertible to _applicable\_interpolated\_string\_builder\_types_. As proposed today,
authors will likely need to overload on both that builder type and regular `string` types, so their users don't have to understand the difference. This may be an annoying and non-obvious
overhead, as a `string` expression can be viewed as an interpolation with `expression.Length` prefilled length and 0 holes to be filled.

This would allow new APIs to only expose a builder, without also having to expose a `string`-accepting overload. However, it won't get around the need for changes to better conversion from
expression, so while it would work it may be unnecessary overhead.

### Incorporating spans for heap-less strings

`ValueStringBuilder` as it exists today has 2 constructors: one that takes a count, and allocates on the heap eagerly, and one that takes a `Span<char>`. That `Span<char>` is usually
a fixed size in the runtime codebase, around 250 elements on average. To truly replace that type, we should consider an extension to this where we also recognize `GetInterpolatedString`
methods that take a `Span<char>`, instead of just the count version. However, we see a few potential thorny cases to resolve here:

* We don't want to stackalloc repeatedly in a hot loop. If we were to do this extension to the feature, we'd likely want to share the stackalloc'd span between loop
iterations. We know this is safe, as `Span<T>` is a ref struct that can't be stored on the heap, and users would have to be pretty devious to manage to extract a
reference to that `Span` (such as creating a method that accepts such a builder then deliberately retrieving the `Span` from the builder and returning it to the
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

For simplicity, this spec currently just proposes recognizing a `Append...` method, and things that always succeed (like `InterpolatedStringBuilder`) would always return true from the method.
This was done to support partial formatting scenarios where the user wants to stop formatting if an error occurs or if it's unnecessary, such as the logging case, but could potentially
introduce a bunch of unnecessary branches in standard interpolated string usage. We could consider an addendum where we use just `FormatX` methods if no `Append...` method is present, but
it does present questions about what we do if there's a mix of both `Append...` and `FormatX` calls.

_Answer_:

We want the non-try version of the API. The proposal has been updated to reflect this.

### Passing previous arguments to the builder

There is unfortunate lack of symmetry in the proposal at it currently exists: invoking an extension method in reduced form produces different semantics than invoking the extension method in
normal form. This is different from most other locations in the language, where reduced form is just a sugar. We propose adding an attribute to the framework that we will recognize when
binding a method, that informs the compiler that certain parameters should be passed to the `Create` method on the builder. Usage looks like this:

```cs
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class InterpolatedStringBuilderArgumentAttribute : Attribute
    {
        public InterpolatedStringBuilderArgumentAttribute(string argument);
        public InterpolatedStringBuilderArgumentAttribute(params string[] arguments);

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
        public static string Format(IFormatProvider? provider, [InterpolatedStringBuilderArgument("provider")] ref InterpolatedStringBuilder builder);
        …
    }
}

namespace System.Runtime.CompilerServices
{
    public ref struct InterpolatedStringBuilder
    {
        public static InterpolatedStringBuilder Create(int baseLength, int holeCount, IFormatProvider? provider); // additional factory
        …
    }
}

var formatted = string.Format(CultureInfo.InvariantCulture, $"{X} = {Y}");

// Is lowered to

var tmp1 = CultureInfo.InvariantCulture;
var builder = InterpolatedStringBuilder.Create(3, 2, tmp1);
builder.AppendFormatted(X);
builder.AppendLiteral(" = ");
builder.AppendFormatted(Y);
var formatted = string.Format(tmp1, builder);
```

The questions we need to answer:

1. Do we like this pattern in general?
2. Do we want to allow these arguments to come from after the builder parameter? Some existing patterns in the BCL, such as `Utf8Formatter`, put the value to be formatted _before_ the thing
needed to format into. To fit in best with these patterns, we'd likely want to allow this, but we need to decide if this out-of-order evaluate is ok.

_Answer_:

We want to support this. The spec has been updated to reflect this. Arguments will be required to be specified in lexical order at the call site, and if a needed argument to the create method
is specified after the interpolated string literal, an error is produced.

### `await` usage in interpolation holes

Because `$"{await A()}"` is a valid expression today, we need to rationalize how interpolation holes with await. We could solve this with a few rules:

1. If an interpolated string used as a `string`, `IFormattable`, or `FormattableString` has an `await` in an interpolation hole, fall back to old-style formatter.
2. If an interpolated string is subject to an _implicit\_string\_builder\_conversion_ and _applicable\_interpolated\_string\_builder\_type_ is a `ref struct`, `await` is not allowed to be used
in the format holes.

Fundamentally, this desugaring could use a ref struct in an async method as long as we guarantee that the `ref struct` will not need to be saved to the heap, which should be possible if we forbid
`await`s in the interpolation holes.

Alternatively, we could simply make all builder types non-ref structs, including the framework builder for interpolated strings. This would, however, preclude us from someday recognizing a `Span`
version that does not need to allocate any scratch space at all.

### Builders as ref parameters

Some builders might want to be passed as ref parameters (either `in` or `ref`). Should we allow either? And if so, what will a `ref` builder look like? `ref $""` is confusing, as you're not actually
passing the string by ref, you're passing the builder that is created from the ref by ref, and has similar potential issues with async methods.

_Answer_:

We want to support this. The spec has been updated to reflect this. The rules should reflect the same rules that apply to extension methods on value types.

### Interpolated strings through binary expressions and conversions

Because this proposal makes interpolated strings context sensitive, we would like to allow the compiler to treat a binary expression composed entirely of interpolated strings,
or an interpolated string subjected to a cast, as an interpolated string literal for the purposes of overload resolution. For example, take the following scenario:

```cs
struct Builder1
{
    public static Builder1 Create(int literalLength, int formattedCount, C c) => ...;
    // AppendX... methods as necessary
}
struct Builder2
{
    public static Builder2 Create(int literalLength, int formattedCount, C c) => ...;
    // AppendX... methods as necessary
}

class C
{
    void M(Builder1 builder) => ...;
    void M(Builder2 builder) => ...;
}

c.M($"{X}"); // Ambiguous between the M overloads
```

This would be ambiguous, necessitating a cast to either `Builder1` or `Builder2` in order to resolve. However, in making that cast, we would potentially throw away the information
that there is context from the method receiver, meaning that the cast would fail because there is nothing to fill in the information of `c`. A similar issue arises with binary concatenation
of strings: the user could want to format the literal across several lines to avoid line wrapping, but would not be able to because that would no longer be an interpolated string literal
convertible to the builder type.

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

See https://github.com/dotnet/runtime/issues/50635 for examples of proposed builder APIs using this pattern.
