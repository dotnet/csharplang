# Capability-safe code

Champion issue: https://github.com/dotnet/csharplang/issues/10217

## Summary
[summary]: #summary

In a system with strict encapsulation, memory safety and no ambient authority, the object graph determines which references are available (directly or indirectly) to an object.
Handing out an object reference therefore confers a capability, so **references** and **capabilities** become interchangeable terms.

This follows the principle of least authority, where objects have no capabilities by default and are sandboxed by the design of the object graph.
This contrasts with current C# programs where any misbehaving object has as much access as the process.

This proposal allows types or static members to be declared **capability-safe**, a contract enforced by the runtime and the compiler.
Capability-safe code is limited to a subset of C#.
It may only perform operations through explicit object references it already has, such as parameters, fields reachable from `this` or objects returned by those references.
It cannot use escape hatches that might violate encapsulation or memory safety (pointers, native interop, reflection, `dynamic`) or static APIs not certified capability-safe.

### Illustration

As the following (valid) code illustrates, a capability-safe method may exercise arbitrarily powerful capabilities such as reading or writing a file through a reference that it is given:
```csharp
public interface IFile
{
    string ReadAllText();
    void WriteAllText(string contents);
}

[CapabilitySafe]
public static string ReadConfig(IFile configFile)
{
    return configFile.ReadAllText(); // valid
}
```

This capability-safe method can perform I/O through `configFile`.

If the caller doesn't directly or indirectly expose a reference for a certain capability, it is not possible for the capability-safe code to get it. For instance, an implementation that uses ambient authority, such as a static method that isn't capability-safe, causes a compiler error and a runtime type load error:

```csharp
[CapabilitySafe]
public static string ReadConfig()
{
    return File.ReadAllText("config.json"); // error
}
```

It is the responsibility of the caller to hand out capabilities that are restrained (not more powerful than necessary):
```csharp
public interface IFileSystem
{
    string ReadAllText(string path);
    void WriteAllText(string path, string contents);
    void Delete(string path);
}

[CapabilitySafe]
public static string ReadConfig(IFileSystem fileSystem)
{
    ...
}

IFileSystem fileSystem = ...;
ReadConfig(fileSystem); // the capability is too broad as it exposes arbitrary path access
```

The most direct way to restrain a capability is to use encapsulation to attenuate a more powerful capability:

```csharp
public sealed class ReadOnlyFile : IFile
{
    private readonly IFile _inner;

    public ReadOnlyFile(IFile inner)
    {
        _inner = inner;
    }

    public string ReadAllText()
    {
        return _inner.ReadAllText();
    }

    public void WriteAllText(string contents)
    {
        throw new NotSupportedException();
    }
}

IFile file = GetFile();
ReadOnlyFile readOnlyFile = new ReadOnlyFile(file);
ReadConfig(readOnlyFile); // implementation of ReadOnlyFile restricts write access and encapsulates underlying read/write file capability
```

Such a `ReadOnlyFile` wrapper attenuates an `IFile` capability by preserving read access and refusing write access. This provides fine-grained control over what capabilities `ReadConfig` can exercise.

A caller may require that an API be capability-safe. This can be achieved by using the capability-safe contract within trusted code:
```csharp
[CapabilitySafe]
static string ReadConfig(IFile file)
{
    return External.ReadConfig(file);
}
```

Similarly, the capability-safe contract can be used within trusted code to require that dynamically-loaded code is capability-safe. Consider a host application that defines the extension contract:
```csharp
[CapabilitySafe]
public interface IExtension
{
    int CountLines(IFile input);
}
```

The caller can dynamically load an extension implementing this interface with the guarantee that it has access to no capability except for the file it is given a reference to:

```csharp
IExtension extension = LoadExtension(path); // note: the runtime ensures that the loaded extension types satisfy the required capability-safe contract
if (extension != null)
{
    ReadOnlyFile readOnlyFile = GetReadOnlyFile();
    int count = extension.CountLines(readOnlyFile); // note: untrusted extension is limited in the damage it can do. Unable to overwrite or exfiltrate the file through the network, for example.
}
```

In summary, code that can be verified to be capability-safe starts with no capabilities and the caller controls the extent of capabilities exposed.  

## Motivation
[motivation]: #motivation

Currently, any part of the program executes with the full authority of the process.
This makes software composition dramatically riskier and increases the impact of supply-chain attacks, confused agents and compromised plugins.
Mitigations are complex and add significant overhead.

Object-capability design gives a useful discipline: authority should flow through explicit and unforgeable object references only.
This allows granular flexibility, where parts of the program are entrusted only with limited and specific capabilities.  For instance, one object can have read-only access to a file while another object has write access to a directory.
This also allows auditing of exposure. If a portion of the object graph is never directly or indirectly handed a reference to a specific object-capability, then we know that capability cannot be reached.

## Limitations
[limitations]: #limitations

With all the security problems that come with ambient access to files, network, and other resources through static APIs and configured permissions, such API design is undeniably convenient.
The object-capability discipline creates a burden of passing references around and attenuating capabilities in order to create a program that is both robust and useful.
Further language features, such as easier wrapper generation, could mitigate this burden.

Another downside is that existing APIs were not designed with object-capability discipline in mind. This means that new usage patterns and APIs may have to be developed.
Even if we simply annotate existing APIs conservatively, the contract can be adopted incrementally. For example, only dynamically-loaded extensions might be subject to the object-capability discipline, while helpers can be developed locally (application-specific `Directory` or `File` abstractions, for example) or in dedicated packages or namespaces where shared implementations are useful.

This proposal does not address build-time supply-chain attacks.

Finally, it remains possible for a misbehaving object to exhaust resources by consuming CPU, allocating memory in a loop, blocking indefinitely or exhausting the stack.

## Detailed design
[design]: #detailed-design

## Open questions
[open]: #open-questions

- Delegates
- Expression trees

## References

- [Robust Composition: Towards a Unified Approach to Access Control and Concurrency Control (Mark Miller's thesis)](https://papers.agoric.com/assets/pdf/papers/robust-composition.pdf)
- [Midori: Objects as Secure Capabilities](https://joeduffyblog.com/2015/11/10/objects-as-secure-capabilities/): Midori eliminated ambient authority and access control in favor of capabilities represented by object references.
- [Capability-based security](https://en.wikipedia.org/wiki/Capability-based_security)
- [.NET Framework Code Access Security (CAS)](https://learn.microsoft.com/en-us/dotnet/framework/misc/code-access-security): CAS attempted to limit partially trusted code through permission sets and stack-walk demands.
- [Secure ECMAScript (SES) (2016)](https://github.com/tc39/proposal-ses) and related ecosystem ([Google Caja (2008)](https://en.wikipedia.org/wiki/Caja_project), [Hardened JavaScript (2021)](https://hardenedjs.org/), [Endo (2020)](https://endojs.github.io/endo/))
- [Joe-E (2004)](https://people.eecs.berkeley.edu/~daw/papers/joe-e-ndss10.pdf)
- [E language (1997)](https://en.wikipedia.org/wiki/E_%28programming_language%29)
