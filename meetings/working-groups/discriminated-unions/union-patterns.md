# Union Patterning Matching

## Motivation

The current approach where all patterns apply to the value of the union works well for new uses of unions, whether they be via the union syntax, one of the standard unions or a even custom unions.
After all, we are trying to make the union *wrapper* feel like its not really there.

```csharp
union Pet(Cat, Dog);

Pet p = new Cat("Fido");

if (p is Cat) { ... }         // succeeds: since p.Value is Cat
```

Yet, we are also wanting to encourage authors of existing union-like types to adopt the `Union` attribute, opting into the new language behaviors to benefit their users.  But this backfires, when the existing type already has valid meaning
associated with some patterns.

Suppose a third-party `Result<T>` type opts in to language integration via the `Union` attribute, and an existing user is already pattern-matching against the type's own properties:

```csharp
struct MyResult<T> { public bool IsOk => ...; public T OkValue => ...; }

var result = new MyResult<int>(10);

if (result is { IsOk: true, OkValue: var v })
    Use(v);
```

Under the "always defer to `Value`" approach, this breaks the build during compilation: the property pattern is redirected to the union's `Value` property (not shown), which is typed as `object` and has no `IsOk` property. 

The end user must now rewrite their code the moment the adapter author opts in. 
This is what we want to avoid.

## Proposal

Change pattern matching rules to support scenarios that don't work given current approach.

To avoid this issue, the compiler must have a strategy to know when to apply a pattern to the union instance and when to apply the pattern to its value. 

The options/approaches are:

- **Value always** - current implementation (does not solve the problem)
- **Syntax Decides** - syntax decides which target (union or value) the pattern is applied to
- **Try both** - both targets are tried at runtime

## Syntax Decides

The **Syntax Decides** approach is simple in definition. It only has two general rules.

- Non-null Constant Patterns and any pattern with a type test are targeted against the union's value.
- Unconditional _ and var patterns as well as *unguarded* positional and property patterns are applied to the union instance only.
  
**Null patterns** are treated specially and may be checked against both the union and its value when the union type is a reference type. This is already the existing behavior, and is similar to the **Try Both** approach below, and is not expected to be changed. All three options include this behavior.

<br/>

```csharp
union Pet(Cat, Dog) { public bool IsFuzzy => ...; }
Pet p = new Cat("Fido");

p is { IsFuzzy: true }          // Succeeds, since pattern applies to p
p is Pet { IsFuzy: true }       // Fails, since p.Value is not a Pet
p is Cat                        // Succeeds, since p.Value is a Cat
```

The benefit of this approach is that it is easy to explain, and it solves the motivating scenario.

The downsides are that you cannot do type tests of the union instance itself, for any reason.

```csharp
union Pet(Cat, Dog) : IPet { ... }
Pet p = new Cat("Fido");

p is Pet                        // always fails
p is IPet                       // always fails, unless case types also implement interface

bool Is<T>(Pet p) => p is T;
Is<Pet>()                       // always fails, since type test for T will never apply to p, only p.Value
Is<IPet>()                      // always fails
```

## Try Both

The **Try Both** approach is nearly the same as the **Syntax Decides** approach, but has a different rule for patterns with type tests and causes us to be more explicit about the behavior of and patterns.

#### Upsides
- Matches user's intuition; both kinds of scenarios the user cares about work.  
- Current code keeps working when union type authors add `Union` attribute.
- Compiler is right place to absorb complexity.
- We already do **Try Both** for null.

#### Downsides
- There can be esoteric edge cases that are still not captured
- May be more difficult to implement.


### Type Tests
When a pattern match against a union type includes a **type test**, that test is done against both the union instance and the union's value. If either of the tests succeed, then the type test portion of the pattern succeeds.

```csharp
union Pet(Cat, Dog);
Pet p = new Cat(Name="Fido");

p is Pet                      // true, since p is a Pet
p is Cat                      // true, since p.Value is a Cat
```

If the test declares a named variable that variable contains the value of the target that succeeded first. 
The union instance is always tested first and then the union value only if the union test failed.

```csharp
record Cat(string Name) : ICat;
union Pet(Cat, Dog) : IPet;
Pet p = new Cat(Name="Fido");

p is IPet         // true, since Pet implement IPet
p is IPet ip      // true, since Pet implements IPet, ip is the union boxed
p is ICat c       // true, since p.Value is a Cat and Cat implements ICat
```

**note:** If the compiler can determine that one of the targets is incapable of matching the type it may remove that portion of the match from the emitted code.

### Constant Pattern (other than null pattern)

When the **constant pattern** is used, the match is always done against the value of the union.
This the same as the **Syntax Decides** approach.

```csharp
union StringOrInt(string, int);
StringOrInt s = 10;

s is 10           // true : s.Value is an int and the int has the value 10
```

*Note: you can think of a constant pattern as a type test (of the constants natural type) and value match. So you can think of it as following the **try both** type test rule, but the union type never matches the constants natural type so its can be elided.*

### Null Pattern

When the **null pattern** is used, the pattern uses the same **Try Both** approach that the type test uses. 
This is already the plan (existing behavior) for nulls, and is the same behavior with the **Syntax Decides** approach.
If the union is a reference type, it too may have a null state beyond the null state of the union value.

```csharp
union class StringOrInt(string, int?);  // not actual syntax
StringOrInt? n1 = null;
StringOrInt n2 = (int?)null;

n1 is null           // true : n1 *is* null
n2 is null           // true : n2.Value *is* null
```

Note: this also applies to `Nullable<Union>`.  See [Nullable Unions](#nullable-unions) below.

### Property Pattern

This just an example of the application of the type test rule, and not new rule.

- Property patterns w/o a type test apply to the union instance only. This is the same as the **Syntax Decides** approach.

- Property patterns with a type test apply to the target of the matching type test rule. This is different than the **Syntax Decides** approach.


```csharp
record Cat(string Name);
union Pet(Cat, Dog);
Pet p = new Cat(Name="Fido");

p is { Name: "Fido" }                      // error: Pet has no 'Name'; applied to p
p is { Value: Cat { Name: "Fido" } }       // true; applied to p
p is Pet { Value: Cat { Name: "Fido" } }   // true; applied to p
p is Cat { Name: "Fido" }                  // true; applied to p.Value
p is {}                                    // true; applied to p and always true for struct union
```

### Positional Pattern

This just an example of the application of the type test rule, and not new rule.

- Positional patterns w/o a type test apply the union instance only. Same as **Syntax Decides**.

- Positional patterns with a type test apply to the target of the matching type test rule. Different than **Syntax Decides**


```csharp
record Cat(string Name);
// a union type with its own deconstruct
union Pet(Cat, Dog) { public void Deconstruct(out object value) { value = this.Value; }}}

Pet p = new Cat(Name="Fido");

p is ("Fido")           // false: p.Value is not "Fido"
p is (Cat("Fido"))      // true: p's deconstruct is a Cat and Cat's deconstruct is "Fido"
p is Pet(Cat("Fido"))   // true: p's deconstruct is a Cat and Cat's deconstruct is "Fido"
p is Cat("Fido")        // true: cat.Name is "Fido"
```

### List Pattern

List patterns do not have a type test. They only apply to the union instance.

There is no new rule here. This is existing behavior.

For fun and discussion; an example of a union type that implements the list pattern.

```csharp
union OneOrMore(IReadOnlyList<string>, string) 
{ 
    public int Count => 
        this.Value is IReadOnlyList<<string> list ? list.Count 
        : this.Value is string s : 1
        : 0;

    public object? this[int index] => 
        this.Value is IReadOnlyList<string> list ? list[index]
        : this.Value is string s && index == 0 : s
        : throw new ArgumentOutOfRangeException();
}

OneOrMore ulist = new List<string> { "abc" };
OneOrMore ustr = "abc";
OneOrMore uempty = default;

ulist is ["abc"]    // true
ustr is ["abc"]     // true
uempty is []        // true 
```

### Or Pattern

The operands of an "or" pattern act independently of each other.

This is not a new rule, and there is no change in behavior of the "or" pattern.  
It is just provided to aid discussion.

```csharp
record Cat(string Name);
union Pet(Cat, Dog);
Pet p = new Cat(Name="Fido");

p is Cat or Dog;      // true; same as: p is Cat || p is Dog
```
The or pattern does not narrow the input type for the right-side operand.  
Both patterns are applied to the original source `p`, and follow the **Try Both** rules independently.
The outcome of the or pattern does not narrow the type or adjust the target of the successive patterns.

### And Pattern

The operands of the and pattern do not act independently. The right side of the and pattern depends on the outcome of the left side. 

This pattern has a subtle new rule.

When the left side has a type test, the target that wins the test determines the instance that flows into the right side operand. This is new behavior because before there was never a question about what instance passed on to the right side.

```csharp
record Cat(string Name);
union Pet(Cat, Dog);
Pet p = new Cat(Name="Fido");

p is Cat and { Name: "Fido" };    // true; same as: p is Cat c && c is { Name: "Fido" }
```
The outcome of the left-side narrows the type given to the right-side.
The outcome of the left-side determines the target that is used on the right-side.

### Not Pattern

The not pattern behaves normally. It negates the outcome of the pattern supplied to it, after the rules specified here are applied.

There is no new behavior here, and this is provided for discussion.

```csharp
record Cat(string Name) : ICat;
union Pet(Cat, Dog) : IPet;

Pet pc = new Cat("Fido");
Pet pd = new Dog("Felix");

pc is not ICat           // false; !(pc is ICat) -- Cat implements ICat
pc is not IPet           // false; !(pc is IPet) -- Pet implements IPet
pd is not ICat           // true; !(pd is ICat)  -- neither Pet nor Dog implements ICat
```
The outcome of the not pattern does not narrow the type or change the target for successive patterns.


## Nullable Unions

This is a related discussion, but not explicitly about patterns applied to Union types.

It already has a current behavior of applying some rules to both the Nullable<T> type and the Nullable's Value, similar to the **Try Both** approach.

### Null Pattern

When the null pattern is applied to a Nullable<Union> it behaves similarly to the **Try Both** approach;
both the `Nullable<Union>` and its value are checked for null.

### Other Patterns

All other patterns apply to the value of the `Nullable<Union>`, when the value is not null.
When the value is a union type, the other rules in this proposal apply.
