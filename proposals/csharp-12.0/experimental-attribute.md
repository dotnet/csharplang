ExperimentalAttribute
=====================
Report warnings for references to types and members marked with `System.Diagnostics.CodeAnalysis.ExperimentalAttribute`.
```
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Module |
                    AttributeTargets.Class |
                    AttributeTargets.Struct |
                    AttributeTargets.Enum |
                    AttributeTargets.Constructor |
                    AttributeTargets.Method |
                    AttributeTargets.Property |
                    AttributeTargets.Field |
                    AttributeTargets.Event |
                    AttributeTargets.Interface |
                    AttributeTargets.Delegate, Inherited = false)]
    public sealed class ExperimentalAttribute : Attribute
    {
        public ExperimentalAttribute(string diagnosticId)
        {
            DiagnosticId = diagnosticId;
        }

        public string DiagnosticId { get; }
        public string? UrlFormat { get; set; }
    }
}
```

## Warnings
The warning message is a specific message, where `'{0}'` is the fully-qualified type or member name.
```
'{0}' is for evaluation purposes only and is subject to change or removal in future updates.
```

The warning is reported for any reference to a type or member that is either:
- marked with the attribute,
- in an assembly or module marked with the attribute,
except when the reference occurs  within `[Experimental]` members (automatic suppression).

The attribute is not inherited from base types or overridden members.

It is also possible to suppress the warning by usual means, such as an explicit compiler option or `#pragma`.
```
[Experimental] enum E { }
[Experimental]
class C
{
    private C(E e) // warning CS08305: 'E' is for evaluation purposes only ...
    {
    }
    internal static C Create() // warning CS08305: 'C' is for evaluation purposes only ...
    {
        return Create(0);
    }
#pragma warning disable 8305
    internal static C Create(E e)
    {
        return new C(e);
    }
}
```

## ObsoleteAttribute and DeprecatedAttribute
`ExperimentalAttribute` is independent of `System.ObsoleteAttribute` or `Windows.Framework.Metadata.DeprecatedAttribute`.

Warnings for `[Experimental]` are reported within `[Obsolete]` or `[Deprecated]` members.
Warnings and errors for `[Obsolete]` and `[Deprecated]` are reported inside `[Experimental]` members.
```
[Obsolete]
class A
{
    static object F() => new C(); // warning CS08305: 'C' is for evaluation purposes only ...
}
[Deprecated(null, DeprecationType.Deprecate, 0)]
class B
{
    static object F() => new C(); // warning CS08305: 'C' is for evaluation purposes only ...
}
[Experimental]
class C
{
    static object F() => new B(); // warning CS0612: 'B' is obsolete
}
```

Warnings and errors for `[Obsolete]` and `[Deprecated]` are reported instead of `[Experimental]` if there are multiple attributes.
```
[Obsolete]
[Experimental]
class A
{
}
[Experimental]
[Deprecated(null, DeprecationType.Deprecate, 0)]
class B
{
}
class C
{
    static A F() => null; // warning CS0612: 'A' is obsolete
    static B G() => null; // warning CS0612: 'B' is obsolete
}
```

## Open questions
- confirm the behavior we want for assembly/module-level attributes
- discuss warning vs. error
