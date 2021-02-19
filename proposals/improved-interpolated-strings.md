# Improved Interpolated Strings

## Summary

We introduce a new pattern for creating and using interpolated string expressions to allow for efficient formatting and use in both general `string` scenarios
and more specialized scenarios such as logging frameworks, without incurring unnecessary allocations from formatting the string in the framework.

## Motivation

Today, string interpolation mainly lowers down to a call to `string.Format`. This, while general purpose, can be inefficient for a number of reasons:

1. It boxes any struct arguments, unless the runtime has happened to introduce an overload of `string.Format` that takes exactly the correct types of arguments
in exactly the correct order.
    * This ordering is why the runtime is hesitant to introduce generic versions of the method, as it would lead to combinatoric explosion of generic instanciations
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

When an _interpolated\_string\_expression_ is passed as an argument to a method, we look at the receiver of the method. If the receiver has an invocable member
`GetInterpolatedStringBuilder` that can invoked with 2 int parameters, `baseLength` and `formatHoleCount`, and that returns a type that is identity-convertible
to the type of the corresponding parameter, and that type has instance `TryFormat` methods can be invoked for every part of the interpolated string, then we
lower the interpolation using that, instead of into a traditional call to `string.Format(formatStr, args)`. A more concrete example is helpful for picturing this:

```cs
// The builder that will actually "build" the interpolated string"
public ref struct LoggerParamsBuilder
{
    // Storage for the built-up string

    private bool _logLevelEnabled;

    public LoggerParamsBuilder(int baseLength, int formatHoleCount, bool logLevelEnabled)
    {
        // Initialization logic
        _logLevelEnabled = logLevelEnabled
    }

    public bool TryFormat(string s)
    {
        if (!_logLevelEnabled) return false;

        // Store and format part as required
        return true;
    }

    public bool TryFormat<T>(T t)
    {
        if (!_logLevelEnabled) return false;

        // Store and format part as required
        return true;
    }
}

// The logger class. The user has an instance of this, accesses it via static state, or some other access
// mechanism
public class Logger
{
    // Initialization code omitted
    private LogLevel _myLogLevel;

    public class LoggerImpl
    {
        LogLevel _myLogLevel;
        Logger _parent;
        internal LoggerImpl(LogLevel myLogLevel, Logger parent)
        {
            _myLogLevel = myLogLevel;
            _parent = parent;
        }

        public LoggerParamsBuilder GetInterpolatedStringBuilder(int baseLength, int formatHoleCount)
        {
            return new LoggerParamsBuilder(baseLength, formatHoleCount, logLevelEnabled: _parent._currentLogLevel >= _myLogLevel);
        }

        public void Log(LoggerParamsBuilder builder)
        {
            // Impl of logging
        }
    }

    public LoggerImpl Trace { get; } = new Logger(LogLevel.Trace, this); // Would need to be in a constructor to use `this` in real code.
}

Logger logger = GetLogger(LogLevel.Info);

// Given the above definitions, usage looks like this:
logger.Trace.Log($"{"this"} will never be printed because info is < trace!");

// This is converted to:
var receiverTemp = logger.Trace;
var builder = receiverTemp.GetInterpolatedStringBuilder(baseLength: 47, formatHoleCount: 1);
_ = builder.TryFormat("this") && builder.TryFormat(" will never be printed because info is < trace!");
receiverTemp.Log(builder);
```

Here, because `logger.Trace` has an instance method called `GetInterpolatedStringBuilder` with the correct parameters, that returns a value of the type that `Log` was
expecting, we say that the interpolated string has an implicit builder conversion to that parameter, and it lowers to the pattern shown above. The specese needed for
this is a bit complicated, and is expanded below.

#### Builder type applicability

A type is said to be an _applicable\_interpolated\_string\_builder\_type_ if, given an _interpolated\_string\_literal_ `S`, the following is true:

* Overload resolution with an identifier of `TryFormat` and a parameter type of `string` succeeds, and contains a single instance method that returns a `bool`.
* For every _regular\_balanced\_text_ component of `S` (`Si`) without an _interpolation\_format_ component, overload resolution with an identifier of `TryFormat` and parameter
of the type of `Si` succeeds, and contains a single instance method that returns a `bool`.
* For every _regular\_balanced\_text_ component of `S` (`Si`) with an _interpolation\_format_ component, overload resolution with an identifier of `TryFormat` and parameter
types of `Si` and `string` succeeds, and contains a single instance method that returns a `bool`.

Note that these rules do not permit extension methods for the `TryFormat` calls. We could consider enabling that if we choose, but this is analogous to the enumerator
pattern, where we allow `GetEnumerator` to be an extension method, but not `Current` or `MoveNext()`.

#### Interpolated string builder conversion

We add a new implicit conversion type: The _implicit\_string\_builder\_conversion_. An _implicit\_string\_builder\_conversion_ permits an _interpolated\_string\_expression_
to be converted to an _applicable\_interpolated\_string\_builder\_type_. There are 2 ways that this conversion can occur:

1. A method argument is converted as part of determining applicable function members (covered below), or
2. Given an _interpolated\_string\_expression_ `S` being converted to type `T`, the following is true:
    * `T` is an _applicable\_interpolated\_string\_builder\_type_, and
    * `T` has an accessible constructor that takes 2 int parameters with the names `baseLength` and `formatHoleCount`, in that order.

#### Applicable function member adjustments

We adjust the wording of the [applicable function member algorithm](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#applicable-function-member)
as follows (a new sub-bullet is added at the front of each section, in bold):

A function member is said to be an ***applicable function member*** with respect to an argument list `A` when all of the following are true:
*  Each argument in `A` corresponds to a parameter in the function member declaration as described in [Corresponding parameters](expressions.md#corresponding-parameters), and any parameter to which no argument corresponds is an optional parameter.
*  For each argument in `A`, the parameter passing mode of the argument (i.e., value, `ref`, or `out`) is identical to the parameter passing mode of the corresponding parameter, and
   *  **for an interpolated string argument to a value parameter, the type of the corresponding parameter is an _applicable\_interpolated\_string\_builder\_type_, and overload resolution on the instance receiver of `A` (if `A` is an instance method or extension method invoked in extension form) or the containing type of `A` (if instance overload resolution failed or if `A` is a static method not called as an extension method) with an identifier of `GetInterpolatedStringBuilder` with 2 int parameters of names `baseLength` and `formatHoleCount` succeeds with 1 invocable member, and the return type of that member is _identity\_convertible_ to the type of the corresponding parameter. An interpolated string argument applicable in this way is said to be immediately converted to the corresponding parameter type with an implicit _interpolated\_string\_builder\_conversion_. Or**
   *  for a value parameter or a parameter array, an implicit conversion ([Implicit conversions](conversions.md#implicit-conversions)) exists from the argument to the type of the corresponding parameter, or
   *  for a `ref` or `out` parameter, the type of the argument is identical to the type of the corresponding parameter. After all, a `ref` or `out` parameter is an alias for the argument passed.
For a function member that includes a parameter array, if the function member is applicable by the above rules, it is said to be applicable in its ***normal form***. If a function member that includes a parameter array is not applicable in its normal form, the function member may instead be applicable in its ***expanded form***:
*  The expanded form is constructed by replacing the parameter array in the function member declaration with zero or more value parameters of the element type of the parameter array such that the number of arguments in the argument list `A` matches the total number of parameters. If `A` has fewer arguments than the number of fixed parameters in the function member declaration, the expanded form of the function member cannot be constructed and is thus not applicable.
*  Otherwise, the expanded form is applicable if for each argument in `A` the parameter passing mode of the argument is identical to the parameter passing mode of the corresponding parameter, and
   *  **for an interpolated string argument to a fixed value parameter or a value parameter created by the expansion, the type of the corresponding parameter is an _applicable\_interpolated\_string\_builder\_type_, and overload resolution on the instance receiver of `A` (if `A` is an instance method or extension method invoked in extension form) or the containing type of `A` (if instance overload resolution failed or if `A` is a static method not called as an extension method) with an identifier of `GetInterpolatedStringBuilder` with 2 int parameters of names `baseLength` and `formatHoleCount` succeeds with 1 invocable member, and the return type of that member is _identity\_convertible_ to the type of the corresponding parameter. An interpolated string argument applicable in this way is said to be immediately converted to the corresponding parameter type with an implicit _interpolated\_string\_builder\_conversion_. Or**
   *  for a fixed value parameter or a value parameter created by the expansion, an implicit conversion ([Implicit conversions](conversions.md#implicit-conversions)) exists from the type of the argument to the type of the corresponding parameter, or
   *  for a `ref` or `out` parameter, the type of the argument is identical to the type of the corresponding parameter.

Important note: this means that if there are 2 otherwise equivalent overloads, one with a builder type that creates an _interpolated\_string\_builder\_conversion_ without
needing the receiver, and one that creates one by calling a method on the receiver, these overloads will be considered ambiguous. We could potentially make changes to the
better function member algorithm to resolve this if we so choose, but it would require distinguishing "naturally-occuring" conversions from conversions that only occur
because the receiver has an applicable `GetInterpolatedStringBuilder` method.

#### Better conversion from expression adjustments

We change the [better conversion from expression](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#better-conversion-from-expression) section to the
following:

Given an implicit conversion `C1` that converts from an expression `E` to a type `T1`, and an implicit conversion `C2` that converts from an expression `E` to a type `T2`, `C1` is a ***better conversion*** than `C2` if:
1. `E` is a non-constant _interpolated\_string\_expression_, `C1` is an _interpolated\_string\_builder\_conversion_, `T1` is an _applicable\_interpolated\_string\_builder\_type_, and `C2` is not an _interpolated\_string\_builder\_conversion_, or
2. `E` does not exactly match `T2` and at least one of the following holds:
    * `E` exactly matches `T1` ([Exactly matching Expression](expressions.md#exactly-matching-expression))
    * `T1` is a better conversion target than `T2` ([Better conversion target](expressions.md#better-conversion-target))

This does mean that there are some potentially non-obvious overload resolution rules, depending on whether the interpolated string in question is a constant-expression or not. For example:

```cs
void Log(string s) { ... }
void Log(LoggerParamsBuilder p) { ... }

Log($""); // Calls Log(string s), because $"" is a constant expression
Log($"{"test"}"); // Calls Log(string s), because $"{"test"}" is a constant expression
Log($"{1}"); // Calls Log(LoggerParamsBuilder p), because $"{1}" is not a constant expression
```

This is introduced so that things that can simply be emitted as constants do so, and don't incur any overhead, while things that cannot be constant use the builder pattern.

### InterpolatedStringBuilder and Usage

We introduce a new type in `System.Runtime.CompilerServices`: `InterpolatedStringBuilder`. This is a ref struct with many of the same semantics as `ValueStringBuilder`,
intended for direct use by the C# compiler. This struct would look approximately like this:

```cs
public ref struct InterpolatedStringBuilder
{
    private char[] _array;
    internal int _count;
    public InterpolatedStringBuilder(int baseLength, int formatHoleCount)
    {
        _array = ArrayPool<char>.Shared.Rent(baseLength /* Or some calculation based on what we see on average for the length of format holes */);
        _count = 0;
    }
    public string ToString()
    {
        string result = _array.AsSpan(0, _count).ToString();
        ArrayPool<char>.Shared.Return(_array);
        Return result;
    }
    public bool TryFormat(string s) => TryFormat((ReadOnlySpan<char>)s);
    public bool TryFormat(ReadOnlySpan<char> s)
    {
        if (s.Length >= _array.Length - _count) Grow();
        s.AsSpan().CopyTo(_array);
        _count += s.Length;
        return true;
    }
    … // other TryFormat overloads for other types (including ReadOnlySpan<T>), a generic, etc.
}
```

We also provide a new `string.Format` overload, as follows:

```cs
public class String
{
    public static string Format(InterpolatedStringBuilder builder) => builder.ToString();
}
```

We make a slight change to the rules for the meaning of an [_interpolated\_string\_expression_](https://github.com/dotnet/csharplang/blob/master/spec/expressions.md#interpolated-strings):

If the type of an interpolated string is `System.IFormattable` or `System.FormattableString`, the meaning is a call to `System.Runtime.CompilerServices.FormattableStringFactory.Create`. If the type is `string`, the meaning of the expression is a call to `string.Format`. In both cases **if there exists an overload that takes an instance of an _applicable\_interpolated\_string\_builder\_type_, that overload is used according to the builder pattern. Otherwise**, the argument list of the call consists of a format string literal with placeholders for each interpolation, and an argument for each expression corresponding to the place holders.

### Lowering

Both the general pattern and the specific changes for interpolated strings directly converted to `string`s follow the same lowering pattern. The `GetInterpolatedStringBuilder` method is
invoked on the receiver (whether that's the temporary method receiver for an _interpolated\_string\_builder\_conversion_ derived from the applicable function member algorithm, or a
standard conversion derived from the target type), and stored into a temp local. `TryFormat` is then repeatedly invoked on that temp, with each part of the interpolated string, in order.
The temp is then evaluated as the result of the expression.

## Other considerations

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

### Non-try version of the API

For simplicity, this spec currently just proposes recognizing a `TryFormat` method, and things that always succeed (like `InterpolatedStringBuilder`) would always return true from the method.
This was done to support partial formatting scenarios where the user wants to stop formatting if an error occurs or if it's unnecessary, such as the logging case, but could potentially
introduce a bunch of unnecessary branches in standard interpolated string usage. We could consider an addendum where we use just `Format` methods if no `TryFormat` method is present, but
it does present questions about what we do if there's a mix of both TryFormat and Format calls.

### Allow `string` types to be convertible to builders as well

For type author simplicity, we could consider allowing expressions of type `string` to be implicitly-convertible to _applicable\_interpolated\_string\_builder\_types_. As proposed today,
authors will likely need to overload on both that builder type and regular `string` types, so their users don't have to understand the difference. This may be an annoying and non-obvious
overhead, as a `string` expression can be viewed as an interpolation with `expression.Length` prefilled length and 0 holes to be filled.

## Other use cases

### `TryFormat` on `Span` receivers

The BCL has a number of helper methods that and usages of `ValueStringBuilder` that attempt to format a given string into a `Span`, and instead of moving to the heap if needed, give up if
the `Span` isn't big enough to hold the resulting text. With this proposal, it would be possible to support these cases by defining an extension method that looks like this:

```cs
public static class MemoryExtensions
{
    public static bool TryWrite(this Span<char> span, SpanInterpolatedStringBuilder builder, out int charsWritten)
    {
        charsWritten = builder._count;
        return builder._success;
    }
 
    public static SpanInterpolatedStringBuilder GetInterpolatedStringBuilder(this Span<char> span, int baseLength, int formatHoleCount) =>
        new SpanInterpolatedStringBuilder(span, baseLength + formatHoleCount * AverageFormatHoleLengthConst);
}
 
public ref struct SpanInterpolatedStringBuilder
{
    private Span<char> _span;
    internal bool _success;
    internal int _count;
 
    public SpanInterpolatedStringBuilder(Span<char> span, int baseLength)
    {
        _span = span;
        _success = baseLength <= span.Length;
        _count = 0;
    }
 
    public bool TryFormat(string s)
    {
        if (!_success)
            return false;
        if (s.Length > _span.Length)
        {
            _success = false;
            return false;
        }
        s.AsSpan().CopyTo(_span);
        _span = _span.Slice(s.Length);
        _count += s.Length;
        return true;
    }
 
    … // other TryFormat overloads for other types, a generic, etc.
}

bool success = destinationSpan.TryWrite($”{a} = {b}”, out int charsWritten);

// Maps to

var receiverTemp = destinationSpan;
var builder = receiverTemp.GetInterpolatedStringBuilder(baseLength: 3, formatHoleCount: 2);
_ = builder.TryFormat(a) && builder.TryFormat(“ = “) && builder.TryFormat(b);
bool success = receiverTemp.TryWrite(builder, out int charsWritten);
```

### Utf8Formatter.TryFormat

We could enable utf8-encoding of interpolated strings via a pattern similar to this:

```cs
public static partial class Utf8Formatter
{
    public Utf8StringBuilder WithSpan(Span<byte> span) => new Utf8StringBuilder(span);
}

public ref struct Utf8StringBuilder
{
    private Span<byte> _bytes;
    public Utf8StringBuilder(Span<byte> bytes) => _bytes = bytes;

    public Utf8StringBuilder GetInterpolatedStringBuilder(int baseLength, int formatHoleCount)
    {
        return this;
    }

    public bool TryFormat(Utf8StringBuilder builder, out int bytesWritten)
    {
        ...
    }

    public bool TryFormat(string s)
    {
        ...
    }
 
    … // other TryFormat overloads for other types, a generic, etc.
}

Span<byte> myBytes = stackalloc[50];
bool success = Utf8Formatter.WithSpan(myBytes).TryFormat($"Hello world! {myVar}");

// Maps to

var receiverTemp = Utf8Formatter.WithSpan(myBytes);
var builder = receiverTemp.GetInterpolatedStringBuilder(baseLength: 13, formatHoleCount: 1);
_ = builder.TryFormat("Hello world! ") && builder.TryFormat(myVar);
bool success = receiverTemp.TryFormat(builder, out int bytesWritten);
```

This differs from the existing patterns in the Utf8Formatter type, which take the `Span` to write into as an argument to the `TryFormat` method itself. This proposal is somewhat incompatible
with that approach, as it uses the receiver of the method to inform the builder of context, rather than using arguments to the method. It could theoretically be feasible to thread arguments
from the current method into the implicit call to `GetInterpolatedString`, but that raises a host of thorny issues around figuring out what corresponds to what in the signature, and significantly
complicates the determination of _applicable\_interpolated\_string\_builder\_types_.
