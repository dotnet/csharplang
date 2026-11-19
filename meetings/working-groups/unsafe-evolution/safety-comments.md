# C/# safety comments

A key realization from studying the [Rust safety story](https://doc.rust-lang.org/book/ch20-01-unsafe-rust.html) is that language keywords describe critical caller/callee safety relationships but are fundamentally a "long game" on getting developers to write safety comments. The Rust model and the new C# model are oriented on delivering actual safety by documenting, reasoning about, and applying guards for safety obligations. It's only possible to apply the correct keywords at scale when you understand the underlying obligations that a given call is taking on. Colocated safety document is the state of the art for achieving that.

The [safety documentation](https://devblogs.microsoft.com/dotnet/improving-csharp-memory-safety/#safety-documentation) section of a recent post goes into depth on safety comments.

## Example

C# safety comments will look like the following:

```csharp
/// <summary>Reads a single byte from unmanaged memory.</summary>
/// <safety>
/// The sum of <paramref name="ptr"/> and <paramref name="ofs"/> must address a byte
/// the caller is permitted to read.
/// </safety>
public static unsafe byte ReadByte(IntPtr ptr, int ofs)
{
    try
    {
        byte* addr = (byte*)ptr;
        unsafe
        {
            // SAFETY: relies on caller obligation.
            return addr[ofs];
        }
    }
    catch (NullReferenceException)
    {
        throw new AccessViolationException();
    }
}
```

There are two forms:

- A formal `/// <safety>` form that describes caller obligations, is enforced by an analyzer, and that can be copied into official documentation.
- An informal `// SAFETY:` form that describes safety assumptions.

The two forms have the same name, "safety", because they both document key aspects of the safety category. There is no `// UNSAFE:` or `// HELLA-DANGEROUS:`. The use of a common "safety" string also makes it very easy to use `grep` to find safety comments.

The example above is very similar to [Rust safety comments](https://std-dev-guide.rust-lang.org/policy/safety-comments.html). Swift follows the same pattern with `@unsafe` declarations and safety documentation.

Real-world examples in official repositories showing both documentation (`///`) and inline (`//`) forms:

**Rust:**
- [const_ptr.rs: safety documentation](https://github.com/rust-lang/rust/blob/fda6d37bb88ee12fd50fa54d15859f1f91b74f55/library/core/src/ptr/const_ptr.rs#L279-L298)
- [rc.rs: safety documentation](https://github.com/rust-lang/rust/blob/fda6d37bb88ee12fd50fa54d15859f1f91b74f55/library/alloc/src/rc.rs#L1261-L1292)

**Swift:**
- [UnsafeRawPointer: @unsafe struct declaration](https://github.com/apple/swift/blob/4ec4ec95c6a1bc56916e9488807e4e28e444fbc7/stdlib/public/core/UnsafeRawPointer.swift#L172)
- [UnsafeRawPointer: loadUnaligned documentation](https://github.com/apple/swift/blob/4ec4ec95c6a1bc56916e9488807e4e28e444fbc7/stdlib/public/core/UnsafeRawPointer.swift#L467-L495)

## Requirements

We need the following:

- Document + standardize the form, in both regular docs and best-practice guidance.
- Add an analyzer that warns if an `unsafe` member is missing a safety comment.
- Decide if the analyzer is enabled when memory safety v2 is enabled.
- Learn docs include the safety comments in a new block for unsafe APIs.
- Note: There is another project to move `///` comments to dotnet/runtime from dotnet/dotnet-api-docs. We need to decide if that work needs to be complete before the safety comments are added.

## Meta

This project is breaking per the C# 1.0 understanding and workflow for unsafe. We've taken the opportunity to align with Rust by breaking from the past approach. In key points, we've decided to learn from Rust in terms of guessing where language design regrets might lay. The key example is where we've decided that `unsafe` methods without inner `unsafe` blocks are an error instead of a warnings. Another place to differentiate is enabling the safety documentation warning by default.

The [Rust crate audit](https://github.com/google/rust-crate-audits/blob/main/auditing_standards.md#ub-risk-1) guidance from Google requires safety documentation as a prominent criteria for the highest rating. This high level of importance is based on the idea that unsafe code without supporting documentation is unreasonable to audit. The lack of documentation makes auditing significantly more expensive and effectively flips the table on who it is vetting the crate. However, safety comment linting, with `clippy` is opt-in.

It's easy to see from Rust that it is very difficult to introduce code errors and warnings mid-stream. Rust has editions as the key mechanism for introducing change. That's both effective and not as a way to introduce breaks since there is a desire to compile existing crates with the newest edition.

We have a one-time opportunity to extend the gold-standard safety workflow, with targeted and well-justified design choices. We should include safety comments as part of C# and on-by-default linting that warn in their absence. We expect that there is far less unsafe code on nuget.org than there is on crates.io, singificantly limiting the adopting concern. These changes will enable us to present C# as a safety focused language for the next era of development.
