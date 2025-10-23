# Conditional operator/access syntax refinement

## Summary

Introduce whitespace-sensitive parsing rules to eliminate ambiguity between the conditional operator (`?:`)
and null-conditional access operators (`?.` and `?[`). This change requires that tokens in null-conditional 
operators must be adjacent with no intervening characters, while conditional operators with certain operand
patterns must have whitespace or comments between the `?` and the subsequent token.

## Motivation

The C# language currently has potential ambiguities when parsing the `?` token followed by `[` or `.`, which
can be difficult to interpret both visually and syntactically.

### Existing ambiguities

Consider the sequence `a?[b`. This could be interpreted as:
- A null-conditional indexing operation: `a?[b]` (index into `a` with `b` if `a` is not null)
- The start of a conditional expression: `a ? [b] : expr` (if `a` is true, evaluate to collection expression
  `[b]`, otherwise `expr`)

While the parser can disambiguate by looking ahead for a `:` token, this creates visual ambiguity for readers.

A true syntactic ambiguity exists with: `A ? [ B ] ? [ C ] : D`

This could be parsed as:
- `(A?[B]) ? [C] : D` - conditional expression producing a collection, then null-conditional indexing
- `A ? ([B]?[C]) : D` - conditional expression with nested conditional in the true branch

The language currently interprets this as the former, which is reasonable since the latter would involve
creating a collection expression solely to perform null-conditional indexing on it—a pattern with no practical use.

### Additional motivation from target-typed static member access

This issue also arises in the context of [target-typed static member access](https://github.com/dotnet/csharplang/blob/main/proposals/target-typed-static-member-access.md). The sequence `a?.b` could be:
- A null-conditional member access: `a?.b`
- The start of a conditional expression with static member access: `a ? .b : expr`

By establishing whitespace-sensitivity rules, we eliminate these ambiguities entirely.

### Precedent

The C# language already employs whitespace-sensitive parsing for the `>>` token sequence.
As stated in the specification:

> `right_shift` is made up of the two tokens `>` and `>`. Similarly, `right_shift_assignment` is
  made up of the two tokens `>` and `>=`. Unlike other productions in the syntactic grammar, no
  characters of any kind (not even whitespace) are allowed between the two tokens in each of these
  productions. These productions are treated specially in order to enable the correct handling of
  type_parameter_lists.

This proposal extends the same concept to null-conditional operators.

## Detailed design

### Parsing rules

The following multi-character operators are modified to require that their constituent tokens be adjacent with no intervening characters:

- **Null-conditional member access (`?.`)**: The `?` and `.` tokens must be adjacent
- **Null-conditional indexing (`?[`)**: The `?` and `[` tokens must be adjacent

Similar to the `>>` production, no characters of any kind (not even whitespace, comments, or preprocessor directives) are allowed between these tokens.

Conversely, when the `?` token is followed by `[` or `.` in a conditional expression context, there **must** be at
least one intervening character (whitespace, comment, or preprocessor directive) between the `?` and the subsequent
token.

### Grammar changes

The grammar productions for null-conditional operators now explicitly require token adjacency:

```g4
null_conditional_member_access
    : primary_expression '?' '.' identifier type_argument_list?
      (null_forgiving_operator? dependent_access)*
    ;

null_conditional_projection_initializer
    : primary_expression '?' '.' identifier type_argument_list?
    ;
    
null_conditional_element_access
    : primary_expression '?' '[' argument_list ']'
      (null_forgiving_operator? dependent_access)*
    ;
```

With explicit text stating:

> `?` `.` and `?` `[` are made up of the two tokens `?` followed by either `.` or `[`. Unlike other productions in the syntactic grammar, no
characters of any kind (not even whitespace) are allowed between the two tokens in each of these
productions.

The [Conditional Operator](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1115-conditional-operator) section is updated to include the following rule:

> A conditional expression of the form `b ? x : y` is not allowed to have `?` and `x` touch if `x` starts with a `.` or `[` token.  If `x` starts with either of those tokens, the expression will instead be interpreted as a  `null_conditional_member_access` or `null_conditional_element_access` respectively.


### Examples

**Valid null-conditional access (no space):**
```csharp
var x = obj?[index];        // Null-conditional indexing
var y = obj?.Member;        // Null-conditional member access
```

**Valid conditional expressions (with space/comment):**
```csharp
var x = condition ? [1, 2, 3] : [4, 5, 6];    // Conditional with collection expressions
var y = flag ? .StaticMember : expr;          // Conditional with static member access (possible future feature)
var z = test ?/*comment*/[x] : [y];      // Comment between tokens
```

**Invalid (violates adjacency requirement):**
```csharp
var x = obj? [index];       // Error: space between ? and [
var y = obj? .Member;       // Error: space between ? and .
```

## Drawbacks

### Breaking change

This is a breaking change. Code that currently uses whitespace between `?` and `[` for null-conditional indexing will no longer compile.  Similarly, code like `a?[b]:[c]` would break as well.  

Real-world examples from existing codebases include:
- `var aux_data = ctx? ["auxData"] as JObject;`
- `var baseScheme = SchemeManager.GetHardCodedSchemes ()? ["Base"];`

These patterns would need to be updated to remove the whitespace: `ctx?["auxData"]` and `SchemeManager.GetHardCodedSchemes()?["Base"]`.
Similarly,  if `a?[b]:` was encountered, it would need to be updated to have explicit spaces to
preserve parsing behavior.

### Migration burden

Developers will need to update their code when upgrading to the language version that includes this feature.

When the compiler encounters code that would have been valid under the old rules but is now invalid (e.g., `ctx? ["auxData"]`), it should:

1. Attempt to parse using the new rules (requiring adjacency for null-conditional access)
2. If this fails, attempt to parse using the old interpretation
3. If the old interpretation succeeds as a null-conditional access, provide a diagnostic suggesting removal of the whitespace:
   - **Error**: "Whitespace is not allowed between '?' and '[' in null-conditional indexing. Remove the whitespace to fix this error."
   - **Suggested fix**: `ctx?["auxData"]`

This approach provides clear guidance for migration, also ensuring that the breaking change is manageable.

## Design meetings

Link to design notes that affect this proposal, and describe in one sentence for each what changes they led to.

## Unresolved questions


## Language version

This feature will be enabled only for the C# language version in which it ships, allowing existing code to
continue compiling under previous language versions.


