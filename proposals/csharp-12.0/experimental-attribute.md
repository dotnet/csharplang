ExperimentalAttribute
=====================

[!INCLUDE[Specletdisclaimer](../speclet-disclaimer.md)]

Report warnings for references to types and members marked with `System.Diagnostics.CodeAnalysis.ExperimentalAttribute`.
```cs
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
        public string? Message { get; set; }
    }
}
```

## Reported diagnostic

Although the diagnostic is technically a warning (so that the compiler allows suppressing it),
it is treated as an error for purpose of reporting. This causes the build to fail if the diagnostic
is not suppressed.  

The diagnostic is reported for any reference to a type or member that is either:
- marked with the attribute,
- in an assembly or module marked with the attribute,

except when the reference occurs within `[Experimental]` members (automatic suppression).

It is also possible to suppress the diagnostic by usual means, such as an explicit compiler option or `#pragma`.  
For example, if the API is marked with `[Experimental("DiagID")]` or `[Experimental("DiagID", UrlFormat = "https://example.org/{0}")]`, 
the diagnostic can be suppressed with `#pragma warning disable DiagID`.

An error is produced if the diagnostic ID given to the experimental attribute is not a [valid C# identifier](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/lexical-structure.md#643-identifiers).  

If value for `Message` property is not provided, the diagnostic message is a specific message, where `'{0}'` is the fully-qualified type or member name.
```
'{0}' is for evaluation purposes only and is subject to change or removal in future updates.
```

If value for `Message` property is provided, the diagnostic message is a specific message, where `'{0}'` is the fully-qualified type or member name
and `'{1}'` is the `Message`.
```
'{0}' is for evaluation purposes only and is subject to change or removal in future updates: '{1}'.
```

The attribute is not inherited from base types or overridden members.

## ObsoleteAttribute and DeprecatedAttribute

Warnings for `[Experimental]` are reported within `[Obsolete]` or `[Deprecated]` members.  
Warnings and errors for `[Obsolete]` and `[Deprecated]` are reported inside `[Experimental]` members.  
But warnings and errors for `[Obsolete]` and `[Deprecated]` are reported instead of `[Experimental]` if there are multiple attributes.  
