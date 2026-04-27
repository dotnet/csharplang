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

The following updates are presented as a diff against the corresponding sections of the C# 7 standard ([expressions.md](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md)), and against the [`with` expression](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#with-expression) subsection of the C# 9 records proposal.

Throughout this section, ~~strikethrough~~ indicates text being removed from the existing specification, and **bold** indicates text being added. Unchanged prose is quoted verbatim for context.

### Assignment operators

A new production *compound_assignment_operator* is introduced in [§12.21.1](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12211-general) alongside the existing *assignment_operator*, and *assignment_operator* is rewired to reference it. This makes the compound subset of the assignment operators namable in the grammar, which other sections (notably [§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers) and the `with` expression below) can then reference by name.

```diff
 assignment_operator
-    : '=' 'ref'? | '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '|=' | '^=' | '<<='
-    | right_shift_assignment
+    : '=' 'ref'?
+    | compound_assignment_operator
     ;

+compound_assignment_operator
+    : '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '|=' | '^=' | '<<='
+    | right_shift_assignment
+    | '??='
+    ;
```

The refactoring recognizes the same set of programs as the previous *assignment_operator*, plus `??=`, which was added to the language in [C# 8](csharp-8.0/null-coalescing-assignment.md) and is grouped here with the other compound assignment operators.

The prose of [§12.21.1](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12211-general) is tightened to name the new production:

~~The assignment operators other than the `=` and `= ref` operators~~ **The assignment operators given by *compound_assignment_operator*** are called the ***compound assignment operators***. These operators perform the indicated operation on the two operands, and then assign the resulting value to the variable, property, or indexer element given by the left operand. The compound assignment operators are described in [§12.21.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12214-compound-assignment).

The identical refactoring is applied to the *assignment_operator* production in the consolidated grammar at [grammar.md](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/grammar.md).

### Object initializer

The following diff is applied to the grammar in [§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers):

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
```

where *compound_assignment_operator* is the production introduced in [§12.21.1](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12211-general) above.

*Note*: A nested *object_initializer* or *collection_initializer* is only reachable through the `=` branch of *member_initializer*. The *compound_assignment_operator* branch admits only *expression*, so forms such as `P += { 1, 2 }` are syntactically ill-formed. *end note*

The prose in [§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers) is updated as follows.

An object initializer consists of a sequence of member initializers, enclosed by `{` and `}` tokens and separated by commas. Each *member_initializer* shall designate a target for the initialization. An *identifier* shall name an accessible field or property **or event** of the object being initialized, whereas an *argument_list* enclosed in square brackets shall specify arguments for an accessible indexer on the object being initialized. **An *identifier* that names an event is a valid target only in combination with the `+=` or `-=` operator ([§12.21.5](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12215-event-assignment)); every other *assignment_operator* on an event target is a compile-time error, because no other assignment operator is valid with an event access as the left operand ([§12.21.1](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12211-general)).** ~~It is an error for an object initializer to include more than one member initializer for the same field or property.~~ **For any given field or property target, at most one member initializer may use the `=` operator. Any number of member initializers using a *compound_assignment_operator* are permitted for the same target. If both are present for the same target, the `=` member initializer shall appear in lexical order before any *compound_assignment_operator* member initializer for that target. No such restriction applies to event or indexer targets.**

**A *member_initializer* whose *initializer_value* is an *object_or_collection_initializer* (the `target = { … }` form) is *exclusive*: it must be the only *member_initializer* in the enclosing *member_initializer_list* whose *initializer_target* designates the same field, property, event, or indexer-with-equivalent-arguments. No other *member_initializer* — whether `=`, another `= { … }`, or a *compound_assignment_operator* — may target the same target.**

*Note*: While an object initializer is not permitted to set the same field or property more than once with `=`, the existing "same indexer arguments multiple times" allowance is preserved for plain assignment and compound forms. The relaxation above for compound operators supports, among other things, subscribing multiple handlers to the same event in a single initializer (`Click += h1, Click += h2`) and accumulating into the same property (`Value = 10, Value += 5`). The `target = { … }` form is exclusive because it configures the nested instance the target already references; combining it with a slot-overwriting `=` or a read-modify-write *compound_assignment_operator* would either discard the nested configuration or read it mid-configuration. *end note*

~~Each *initializer_target* is followed by an equals sign and either an expression, an object initializer or a collection initializer.~~ **When the *member_initializer* uses the `=` operator, the *initializer_target* is followed by either an expression, an object initializer, or a collection initializer. When the *member_initializer* uses a *compound_assignment_operator*, the *initializer_target* is followed by an expression.** ~~It is not possible~~ **When the `=` operator is used, it is not possible** for expressions within the object initializer to refer to the newly created object it is initializing. **When a *compound_assignment_operator* is used, both the read and the write of the target occur on the newly created object. Member initializers are processed in lexical order, so the read performed by a compound member initializer takes place after every preceding member initializer has executed; the read accordingly reflects whatever state that target's get accessor reports at that point.**

~~A member initializer that specifies an expression after the equals sign is processed in the same way as an assignment ([§12.21.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12212-simple-assignment)) to the target.~~ **A *member_initializer* of the form `target op value` in an object initializer is semantically equivalent to the *statement_expression* `x.target op value;`, where `x` is the otherwise invisible and inaccessible temporary variable holding the instance being initialized, and where `x.target` is the field, property, event, or indexer access designated by *initializer_target*. The meaning of that *statement_expression* is given by [§12.21](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1221-assignment-operators): simple assignment ([§12.21.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12212-simple-assignment)) when `op` is `=`, compound assignment ([§12.21.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12214-compound-assignment)) when `op` is a *compound_assignment_operator* and the target is not an event, and event assignment ([§12.21.5](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12215-event-assignment)) when `op` is `+=` or `-=` and the target is an event. In particular, the *statement_expression* context required by [§12.21.5](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12215-event-assignment) is satisfied by this lowering, and the get-and-set requirement on property and indexer targets imposed by [§12.21.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12214-compound-assignment) applies to the target of a *compound_assignment_operator* member initializer.**

The existing [§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers) paragraphs about *nested object initializer* and nested *collection initializer* are unchanged; they remain applicable only to the `=` form, because the grammar does not admit a nested *object_or_collection_initializer* on the *compound_assignment_operator* branch.

*Example*: The following class combines properties and an event:

```csharp
public class Counter
{
    public int Value { get; set; }
    public event EventHandler Changed;
}
```

An instance of `Counter` can be created and initialized using a mixture of `=` and *compound_assignment_operator* member initializers:

```csharp
Counter c = new Counter
{
    Value = 10,
    Value += 5,
    Changed += OnChanged,
    Changed += OnChanged2,
};
```

which has the same effect as

```csharp
Counter __c = new Counter();
__c.Value = 10;
__c.Value += 5;
__c.Changed += OnChanged;
__c.Changed += OnChanged2;
Counter c = __c;
```

where `__c` is an otherwise invisible and inaccessible temporary variable. Each generated line is a *statement_expression*; the first two are a simple assignment and a compound assignment on a property, and the last two are event assignments on an event.

*end example*

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
-    : identifier '=' expression
+    : identifier assignment_operator expression
     ;
```

Because both the previous `=` form and every *compound_assignment_operator* form share the same right-hand side (an *expression*), the two alternatives collapse into a single *assignment_operator* production from [§12.21.1](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12211-general). The `= ref` form of *assignment_operator* is not applicable to a `member_initializer` target (a field, property, or event access is not a reference variable, so [§12.21.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12213-ref-assignment)'s left-operand requirement rejects it at binding time).

The prose of the [`with` expression](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#with-expression) subsection is updated as follows.

On the right hand side of the `with` expression is a `member_initializer_list` with a sequence of assignments to *identifier*, which must be an accessible instance field or property **or event** of the receiver's type. **An *identifier* that names an event is a valid target only in combination with the `+=` or `-=` operator; every other *assignment_operator* on an event target is a compile-time error.**

**For any given field or property target, at most one member initializer may use the `=` operator. Any number of member initializers using a *compound_assignment_operator* are permitted for the same target. If both are present for the same target, the `=` member initializer shall appear in lexical order before any *compound_assignment_operator* member initializer for that target. No such restriction applies to event targets.**

First, receiver's "clone" method (specified above) is invoked and its result is converted to the receiver's type. ~~Then, each `member_initializer` is processed the same way as an assignment to a field or property access of the result of the conversion. Assignments are processed in lexical order.~~ **Then, for each `member_initializer` `target op value` in lexical order, the *statement_expression* `x.target op value;` is executed, where `x` is the otherwise invisible and inaccessible temporary variable holding the converted clone. The meaning of that *statement_expression* is given by [§12.21](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1221-assignment-operators), as for an object initializer. When `op` is a *compound_assignment_operator*, both the read and the write of `target` are performed on the clone `x`; the original receiver of the `with` expression is not read again after its clone method has returned. Member initializers are processed in lexical order, so the read performed by a compound member initializer takes place after every preceding member initializer has executed; the read accordingly reflects whatever state that target's get accessor reports on the clone at that point.**

*Example*: Given

```csharp
public record Counter(int Value)
{
    public event EventHandler Changed;
}

Counter original = ...;
```

the expression

```csharp
Counter c = original with { Value -= 1, Changed += OnChanged };
```

has the same effect as

```csharp
Counter __c = (Counter)original.<Clone>();
__c.Value -= 1;           // both the read and the write are on __c
__c.Changed += OnChanged;
Counter c = __c;
```

where `__c` is an otherwise invisible and inaccessible temporary variable.

*end example*

### Interactions with other features

- **Accessor requirements on property and indexer targets.** A compound *member_initializer* on a property or indexer target inherits the access requirements of [§12.21.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12214-compound-assignment) via the lowering above. In practice, a property or indexer target in either an object initializer or a `with` expression is valid with a *compound_assignment_operator* when it has a `get` accessor together with a `set` or `init` accessor, or when its `get` accessor returns a reference (a ref-returning property or indexer, which classifies the access as a variable per [§12.8.7](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1287-member-access)). An `init` accessor is accepted on the same terms as for a direct `=` member initializer. Plain `=` member initializers continue to use the accessor requirements already specified in their respective sections, unchanged by this proposal.

- **Event targets.** On an event target, `+=` and `-=` dispatch to the event's `add` and `remove` accessors respectively, per [§12.21.5](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12215-event-assignment). A single object initializer or `with` expression may therefore subscribe or unsubscribe multiple handlers in lexical order, including on the same event; see the relaxed uniqueness rule above.

- **Indexer targets.** The grammar of [§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers) continues to permit `'[' argument_list ']'` as an *initializer_target* for both the `=` form and the *compound_assignment_operator* form. The "arguments shall always be evaluated exactly once" rule already specified for indexer initializer targets in [§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers) is unchanged and applies equally to compound member initializers; the get-and-set requirement from [§12.21.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12214-compound-assignment) applies to the selected indexer.

- **Required members.** In an object initializer, a `required` field or property is satisfied only by a `=` *member_initializer*; a *compound_assignment_operator* *member_initializer* does not on its own discharge the requirement. The `required` target may additionally appear under one or more *compound_assignment_operator* *member_initializers* in the same object initializer, subject to the "`=` before any compound" ordering above. In a `with` expression no such restriction applies — the receiver has already been constructed and its `required` members satisfied by that earlier construction, so the `with` clause admits *compound_assignment_operator*-only *member_initializers* on `required` targets. `SetsRequiredMembersAttribute` continues to discharge the obligation in both forms, as in the existing specification.

  *Motivation:* `required` exists to ensure each marked slot is in a valid state before any read of it. A *compound_assignment_operator* reads the slot before writing it; on a freshly constructed object the read sees the slot's default value, which is exactly the state `required` is meant to prevent the object from being observed in. A `with` clone has already been initialized once, so the read is safe.

- **Dynamic.** When the target's instance expression (the temporary `x`) has an accessed member whose container has compile-time type `dynamic`, dynamic binding of the member initializer follows [§12.21.2](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12212-simple-assignment) (for `=`) or [§12.21.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12214-compound-assignment) (for compound), unchanged.

- **Collection initializers are unaffected.** The grammar of [§12.8.16.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128164-collection-initializers) uses *non_assignment_expression* for the unbraced element form, which by definition excludes *assignment* ([§12.22](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1222-expression)). A form such as `a += b` is an *assignment* and therefore remains ill-formed as an element initializer of a collection initializer. No change to [§12.8.16.4](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128164-collection-initializers) is required.

## Back-compat analysis

This is a pure extension. The grammar of [§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers)'s *member_initializer* gains a new alternative via *compound_assignment_operator*, which today is not a valid form of a *member_initializer*; no expression that compiles today changes meaning. The same reasoning applies to the `with` expression's *member_initializer*. Any program that compiled before this feature continues to compile with the same meaning.

## Drawbacks

As with any language feature, the additional specification complexity must be weighed against the clarity and correctness improvements it offers users. The feature is localized to one subsection of [§12.8.16.3](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#128163-object-initializers) and one subsection of the `with` expression, and reuses [§12.21](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1221-assignment-operators)'s existing machinery at every step, so the marginal complexity is small.

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

The single most compelling motivation for this feature is events, and events naturally chain: subscribing two handlers to the same event in one initializer is useful and unsurprising, or unsubscribing and resubscribing. Forbidding this would make the case (`Click -= h1, Click += h2`) illegal in exactly the contexts where the feature is most valuable. The same reasoning extends to compound operators on properties (`Value = 10, Value += 5`), where each step has an observable effect.

At the same time, `=` remains destructive: permitting a second `=` for the same target would make the first assignment dead code. The rule adopted here, *"at most one `=` per target, any number of *compound_assignment_operator* per target after it,"* keeps `=` as unambiguously initializing and lets compound operators compose on top.

### Why specify the semantics as a lowering to *statement_expression*?

Phrasing a *member_initializer* as an equivalent *statement_expression* makes all of the necessary rules fall out of [§12.21](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#1221-assignment-operators) with no further writing. In particular, [§12.21.5](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/expressions.md#12215-event-assignment) already requires event assignment to appear in a *statement_expression* context; the lowering satisfies that requirement by construction, without special-casing events at the initializer level. Each of simple assignment, compound assignment, and event assignment is invoked by the same uniform rule: "the meaning of `x.target op value;`."

## Related discussions

- [Issue #9896: Champion "Compound assignment in object initializer and `with` expression"](https://github.com/dotnet/csharplang/issues/9896).

## Design meetings

TBD
