# Compound assignment in object initializer and `with` expression

Champion issue: <https://github.com/dotnet/csharplang/issues/9896>

## Summary

Allow compound assignments in an object initializer:

```cs
var timer = new DispatcherTimer
{
    Interval = TimeSpan.FromSeconds(1d),
    Tick += (_, _) => { /*actual work*/ },
};
```

Or a `with` expression:

```cs
var newCounter = counter with { Value -= 1 };
```

## Motivation

It is not uncommon, especially in UI frameworks, to create objects that both have values assigned and need events hooked up as part of initialization. While object initializers addressed the first part with a nice shorthand syntax, the latter still requires additional statements. This makes it impossible to create these sorts of objects as a simple declaration expression, preventing their use in expression-bodied members or in nested constructs like collection initializers or switch expressions. Spilling the object creation expression out to a variable declaration statement makes things more verbose for such a simple concept.

The declarative UI story can be made much more complete with a small change to the language. Windows Forms in particular can immediately gain a more appetizing story for dynamic or manual creation of UI controls, both in vanilla form and when using third-party vendor frameworks that build on Windows Forms.

The same reasoning applies to more than just events. Newly created objects (and especially objects produced via `with`) may want their initialized values to be relative to a prior or default state, which is exactly what a compound operator expresses.

## Detailed design

The following updates are presented as a diff against the corresponding sections of the C# 6 standard ([expressions.md](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md)), and against the [`with` expression](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#with-expression) subsection of the C# 9 records proposal.

Throughout this section, ~~strikethrough~~ indicates text being removed from the existing specification, and **bold** indicates text being added. Unchanged prose is quoted verbatim for context.

### Object initializer

The following diff is applied to the grammar in [§11.7.15.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#117153-object-initializers):

```diff
 object_initializer
     : '{' member_initializer_list? '}'
     | '{' member_initializer_list ',' '}'
     ;

 member_initializer_list
     : member_initializer (',' member_initializer)*
     ;

 member_initializer
     : initializer_target '=' initializer_value
+    | initializer_target compound_assignment_operator expression
     ;

 initializer_target
     : identifier
     | '[' argument_list ']'
     ;

 initializer_value
     : expression
     | object_or_collection_initializer
     ;

+compound_assignment_operator
+    : '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '|=' | '^=' | '<<='
+    | right_shift_assignment
+    ;
```

> *Note*: A nested *object_initializer* or *collection_initializer* is only reachable through the `=` branch of *member_initializer*. The *compound_assignment_operator* branch admits only *expression*, so forms such as `P += { 1, 2 }` are syntactically ill-formed. *end note*

The prose in [§11.7.15.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#117153-object-initializers) is updated as follows.

> An object initializer consists of a sequence of member initializers, enclosed by `{` and `}` tokens and separated by commas. Each *member_initializer* shall designate a target for the initialization. An *identifier* shall name an accessible field**,** ~~or~~ property**, or event** of the object being initialized, whereas an *argument_list* enclosed in square brackets shall specify arguments for an accessible indexer on the object being initialized. **An *identifier* that names an event is a valid target only in combination with the `+=` or `-=` operator ([§11.18.4](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11184-event-assignment)); every other *assignment_operator* on an event target is a compile-time error, because no other assignment operator is valid with an event access as the left operand ([§11.18.1](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11181-general)).** ~~It is an error for an object initializer to include more than one member initializer for the same field or property.~~ **For any given field or property target, at most one member initializer may use the `=` operator. Any number of member initializers using a *compound_assignment_operator* are permitted for the same target. If both are present for the same target, the `=` member initializer shall appear in lexical order before any *compound_assignment_operator* member initializer for that target. No such restriction applies to event or indexer targets.**
>
> *Note*: While an object initializer is not permitted to set the same field or property more than once with `=`, the existing "same indexer arguments multiple times" allowance is preserved. The relaxation above for compound operators supports, among other things, subscribing multiple handlers to the same event in a single initializer (`Click += h1, Click += h2`) and accumulating into the same property (`Value = 10, Value += 5`). *end note*

> ~~Each *initializer_target* is followed by an equals sign and either an expression, an object initializer or a collection initializer.~~ **When the *member_initializer* uses the `=` operator, the *initializer_target* is followed by either an expression, an object initializer, or a collection initializer. When the *member_initializer* uses a *compound_assignment_operator*, the *initializer_target* is followed by an expression.** ~~It is not possible~~ **When the `=` operator is used, it is not possible** for expressions within the object initializer to refer to the newly created object it is initializing. **When a *compound_assignment_operator* is used, both the read and the write of the target occur on the newly created object. Member initializers are processed in lexical order, so the read side of a compound member initializer observes the value established by any preceding member initializer for the same target.**

> ~~A member initializer that specifies an expression after the equals sign is processed in the same way as an assignment ([§11.18.2](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11182-simple-assignment)) to the target.~~ **A *member_initializer* of the form `target op value` in an object initializer is semantically equivalent to the *statement_expression* `x.target op value;`, where `x` is the otherwise invisible and inaccessible temporary variable holding the instance being initialized, and where `x.target` is the field, property, event, or indexer access designated by *initializer_target*. The meaning of that *statement_expression* is given by [§11.18](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1118-assignment-operators): simple assignment ([§11.18.2](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11182-simple-assignment)) when `op` is `=`, compound assignment ([§11.18.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11183-compound-assignment)) when `op` is a *compound_assignment_operator* and the target is not an event, and event assignment ([§11.18.4](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11184-event-assignment)) when `op` is `+=` or `-=` and the target is an event. In particular, the *statement_expression* context required by [§11.18.4](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11184-event-assignment) is satisfied by this lowering, and the get-and-set requirement on property and indexer targets imposed by [§11.18.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11183-compound-assignment) applies to the target of a *compound_assignment_operator* member initializer.**

The existing [§11.7.15.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#117153-object-initializers) paragraphs about *nested object initializer* and nested *collection initializer* are unchanged; they remain applicable only to the `=` form, because the grammar does not admit a nested *object_or_collection_initializer* on the *compound_assignment_operator* branch.

> *Example*: The following class combines properties and an event:
>
> ```csharp
> public class Counter
> {
>     public int Value { get; set; }
>     public event EventHandler Changed;
> }
> ```
>
> An instance of `Counter` can be created and initialized using a mixture of `=` and *compound_assignment_operator* member initializers:
>
> ```csharp
> Counter c = new Counter
> {
>     Value = 10,
>     Value += 5,
>     Changed += OnChanged,
>     Changed += OnChanged2,
> };
> ```
>
> which has the same effect as
>
> ```csharp
> Counter __c = new Counter();
> __c.Value = 10;
> __c.Value += 5;
> __c.Changed += OnChanged;
> __c.Changed += OnChanged2;
> Counter c = __c;
> ```
>
> where `__c` is an otherwise invisible and inaccessible temporary variable. Each generated line is a *statement_expression*; the first two are a simple assignment and a compound assignment on a property, and the last two are event assignments on an event.
>
> *end example*

### `with` expression

The following diff is applied to the grammar of the [`with` expression](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#with-expression):

```diff
 with_expression
     : switch_expression
     | switch_expression 'with' '{' member_initializer_list? '}'
     ;

 member_initializer_list
     : member_initializer (',' member_initializer)*
     ;

 member_initializer
     : identifier '=' expression
+    | identifier compound_assignment_operator expression
     ;
```

where *compound_assignment_operator* is the production defined in [§11.7.15.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#117153-object-initializers).

The prose of the [`with` expression](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#with-expression) subsection is updated as follows.

> On the right hand side of the `with` expression is a `member_initializer_list` with a sequence of assignments to *identifier*, which must be an accessible instance field**,** ~~or~~ property**, or event** of the receiver's type. **An *identifier* that names an event is a valid target only in combination with the `+=` or `-=` operator; every other *assignment_operator* on an event target is a compile-time error.**
>
> **For any given field or property target, at most one member initializer may use the `=` operator. Any number of member initializers using a *compound_assignment_operator* are permitted for the same target. If both are present for the same target, the `=` member initializer shall appear in lexical order before any *compound_assignment_operator* member initializer for that target. No such restriction applies to event targets.**
>
> First, receiver's "clone" method (specified above) is invoked and its result is converted to the receiver's type. ~~Then, each `member_initializer` is processed the same way as an assignment to a field or property access of the result of the conversion. Assignments are processed in lexical order.~~ **Then, for each `member_initializer` `target op value` in lexical order, the *statement_expression* `x.target op value;` is executed, where `x` is the otherwise invisible and inaccessible temporary variable holding the converted clone. The meaning of that *statement_expression* is given by [§11.18](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1118-assignment-operators), as for an object initializer. When `op` is a *compound_assignment_operator*, both the read and the write of `target` are performed on the clone `x`; the original receiver of the `with` expression is not read again after its clone method has returned.**

> *Example*: Given
>
> ```csharp
> public record Counter(int Value)
> {
>     public event EventHandler Changed;
> }
>
> Counter original = ...;
> ```
>
> the expression
>
> ```csharp
> Counter c = original with { Value -= 1, Changed += OnChanged };
> ```
>
> has the same effect as
>
> ```csharp
> Counter __c = (Counter)original.<Clone>();
> __c.Value -= 1;           // both the read and the write are on __c
> __c.Changed += OnChanged;
> Counter c = __c;
> ```
>
> where `__c` is an otherwise invisible and inaccessible temporary variable.
>
> *end example*

### Interactions with other features

- **Accessor requirements on property and indexer targets.** Because a *member_initializer* `target op value` with `op` a *compound_assignment_operator* is specified by lowering to the *statement_expression* `x.target op value;`, the access requirements of [§11.18.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11183-compound-assignment) apply unchanged. In practice this means:

  - In an object initializer, a property or indexer target is valid with a *compound_assignment_operator* when it has both a `get` accessor and a `set` accessor, or when its `get` accessor returns a reference (a ref-returning property or indexer, which classifies the access as a variable per [§11.7.6](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1176-member-access)). A property whose write side is an `init`-only accessor is not valid as a compound target here: object initializers treat the brace block as an init-allowed context for the direct `=` form only, and the compound form's lowering to a *statement_expression* is outside that init-allowed context.

  - In a `with` expression, a property target is valid with a *compound_assignment_operator* when it has a `get` accessor together with either a `set` or an `init` accessor, or when its `get` accessor returns a reference. `init` is permitted here because a `with` expression is an initialization context for the clone; `with { P = v }` is legal for an `init`-only `P`, and so is `with { P += v }`.

  Plain `=` member initializers continue to use the accessor requirements already specified in their respective sections, unchanged by this proposal.

- **Event targets.** On an event target, `+=` and `-=` dispatch to the event's `add` and `remove` accessors respectively, per [§11.18.4](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11184-event-assignment). A single object initializer or `with` expression may therefore subscribe or unsubscribe multiple handlers in lexical order, including on the same event; see the relaxed uniqueness rule above.

- **Indexer targets.** The grammar of [§11.7.15.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#117153-object-initializers) continues to permit `'[' argument_list ']'` as an *initializer_target* for both the `=` form and the *compound_assignment_operator* form. The "arguments shall always be evaluated exactly once" rule already specified for indexer initializer targets in [§11.7.15.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#117153-object-initializers) is unchanged and applies equally to compound member initializers; the get-and-set requirement from [§11.18.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11183-compound-assignment) applies to the selected indexer.

- **Dynamic.** When the target's instance expression (the temporary `x`) has an accessed member whose container has compile-time type `dynamic`, dynamic binding of the member initializer follows [§11.18.2](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11182-simple-assignment) (for `=`) or [§11.18.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11183-compound-assignment) (for compound), unchanged.

- **Collection initializers are unaffected.** The grammar of [§11.7.15.4](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#117154-collection-initializers) uses *non_assignment_expression* for the unbraced element form, which by definition excludes *assignment* ([§11.19](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1119-expression)). A form such as `a += b` is an *assignment* and therefore remains ill-formed as an element initializer of a collection initializer. No change to [§11.7.15.4](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#117154-collection-initializers) is required.

## Back-compat analysis

This is a pure extension. The grammar of [§11.7.15.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#117153-object-initializers)'s *member_initializer* gains a new alternative via *compound_assignment_operator*, which today is not a valid form of a *member_initializer*; no expression that compiles today changes meaning. The same reasoning applies to the `with` expression's *member_initializer*. Any program that compiled before this feature continues to compile with the same meaning.

## Drawbacks

As with any language feature, the additional specification complexity must be weighed against the clarity and correctness improvements it offers users. The feature is localized to one subsection of [§11.7.15.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#117153-object-initializers) and one subsection of the `with` expression, and reuses [§11.18](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1118-assignment-operators)'s existing machinery at every step, so the marginal complexity is small.

## Alternatives

- **Do nothing.** Users continue to use patterns like an extension method that takes a receiver and a configuration lambda:

  ```csharp
  var timer = new DispatcherTimer
  {
      Interval = TimeSpan.FromSeconds(1d),
  }.Init(t => t.Tick += (_, _) => { /*actual work*/ });
  ```

  This works for non-`init` members, but not for `init`-only properties, and loses the first-class expression shape.

- **Restrict to events only.** A narrower feature that would permit `Event += handler` in an object initializer but not `Property += 1`. This addresses the headline UI scenario but excludes the accumulation-into-property and accumulation-into-cloned-value scenarios that motivate the `with` case.

## Design decisions

### Why allow multiple member initializers for the same target?

The single most compelling motivation for this feature is events, and events naturally chain: subscribing two handlers to the same event in one initializer is useful and unsurprising. Forbidding this would make the common case (`Click += h1, Click += h2`) illegal in exactly the contexts where the feature is most valuable. The same reasoning extends to compound operators on properties (`Value = 10, Value += 5`), where each step has an observable effect.

At the same time, `=` remains destructive: permitting a second `=` for the same target would make the first assignment dead code. The rule adopted here, *"at most one `=` per target, any number of *compound_assignment_operator* per target after it,"* keeps `=` as unambiguously initializing and lets compound operators compose on top.

### Why include events in `with` expressions?

A `with` expression produces a clone of the receiver, and the clone inherits the original's event subscriptions (via the clone method). Subscribing or unsubscribing handlers on the clone via `+=` / `-=` is a coherent operation on the clone alone, and the original receiver is unaffected. Although `with` has historically been associated with "non-destructive mutation" of data-shape members, there is no principled reason to disallow event targets here; the `with` expression simply delegates to the same event assignment rules as an object initializer.

### Why require `get` on a compound target, and why does `init` behave differently between object initializers and `with`?

Compound assignment is defined by [§11.18.3](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11183-compound-assignment) as a read-modify-write: the target must be both readable and writable. A compound *member_initializer* therefore requires a readable target, whereas a plain-`=` member initializer requires only a writable target. A ref-returning `get` produces a variable-classified access and is sufficient for both the read and the write, as it is in any other compound assignment.

On the write side, object initializers and `with` expressions differ with respect to `init` accessors. A plain-`=` object initializer already permits an `init` accessor because the object-initializer brace block is an init-allowed context for the direct assignment form only. A *compound_assignment_operator* member initializer is not a direct assignment; it is specified by lowering to a *statement_expression* outside that init-allowed context, so `init` is not callable as the write side. A `with` expression is different: the whole `{ ... }` is an init-allowed context for the clone, and that context applies to both direct assignment and the compound form, so `init` is permitted there.

### Why specify the semantics as a lowering to *statement_expression*?

Phrasing a *member_initializer* as an equivalent *statement_expression* makes all of the necessary rules fall out of [§11.18](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1118-assignment-operators) with no further writing. In particular, [§11.18.4](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#11184-event-assignment) already requires event assignment to appear in a *statement_expression* context; the lowering satisfies that requirement by construction, without special-casing events at the initializer level. Each of simple assignment, compound assignment, and event assignment is invoked by the same uniform rule: "the meaning of `x.target op value;`."

## Related discussions

- [Issue #9896: Champion "Compound assignment in object initializer and `with` expression"](https://github.com/dotnet/csharplang/issues/9896).

## Design meetings

TBD
