# Introduce `left` and `right` modifiers to the `join` query expression clauses

Champion issue: <https://github.com/dotnet/csharplang/issues/8947>

## Summary
[summary]: #summary

Introduce `left` and `right` modifiers to the LINQ query expression syntax `join` clause, e.g.:

```c#
from student in students
left join department in departments on student.DepartmentID equals department.ID
select new { student.Name, department?.Name }
```

## Motivation
[motivation]: #motivation

LINQ has a [Join operator](https://learn.microsoft.com/dotnet/api/system.linq.enumerable.join), which, like its SQL INNER JOIN counterpart, correlates elements of two sequences based on matching keys; the C# language has a corresponding `join` clause which translates to this operator ([section 12.20.3.5 of the C# specs](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#122035-from-let-where-join-and-orderby-clauses)).

In addition to INNER JOIN, SQL also has LEFT JOIN, which returns outer elements even if there's no corresponding inner ones; LINQ and C#, in contrast, lack this operator. [The LINQ conceptual documentation shows how to combine existing operators to achieve a left join](https://learn.microsoft.com/en-us/dotnet/csharp/linq/standard-query-operators/join-operations#perform-left-outer-joins):

```c#
var query =
    from student in students
    join department in departments on student.DepartmentID equals department.ID into gj
    from subgroup in gj.DefaultIfEmpty()
    select new
    {
        student.FirstName,
        student.LastName,
        Department = subgroup?.Name ?? string.Empty
    };
```

Or using method syntax:

```c#
var query = students
    .GroupJoin(departments, student => student.DepartmentID, department => department.ID, (student, departmentList) => new { student, subgroup = departmentList })
    .SelectMany(
        joinedSet => joinedSet.subgroup.DefaultIfEmpty(),
        (student, department) => new
        {
            student.student.FirstName,
            student.student.LastName,
            Department = department.Name
        });
```

Although functionality sufficient for expressing a left join operation, this combining approach has the following drawbacks:

- It's complicated, requiring combining multiple different LINQ operators in a specific way to form a complex construct, and is easy to accidentally get wrong. Many EF users have complained about the complexity of this construct for expressing a simple SQL LEFT JOIN.
- It is inefficient - the combination of operators adds significant overhead compared to a single operator using an internal lookup table (or "hash join", as Join is implemented).

In .NET 10, new `LeftJoin()` and `RightJoin()` methods have been introduced into System.LINQ; see https://github.com/dotnet/runtime/issues/110292 for the API proposal, discussion, and performance information and benchmarks. Following are the relevant API signatures and examples:

```c#
// API signatures:
public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(
    this IEnumerable<TOuter> outer,
    IEnumerable<TInner> inner,
    Func<TOuter, TKey> outerKeySelector,
    Func<TInner, TKey> innerKeySelector,
    Func<TOuter, TInner, TResult> resultSelector);

public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(
    this IEnumerable<TOuter> outer,
    IEnumerable<TInner> inner,
    Func<TOuter, TKey> outerKeySelector,
    Func<TInner, TKey> innerKeySelector,
    Func<TOuter, TInner?, TResult> resultSelector);

public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(
    this IEnumerable<TOuter> outer,
    IEnumerable<TInner> inner,
    Func<TOuter, TKey> outerKeySelector,
    Func<TInner, TKey> innerKeySelector,
    Func<TOuter?, TInner, TResult> resultSelector);

// Usage examples:

// Existing (inner) join operator: students are only returned if correlated departments are found
var query = students.Join(
    departments,
    student => s.DepartmentID,
    department => department.ID,
    (student, department) => new { student.FirstName, student.LastName, Department = department.Name });

// New left (outer) join operator: all students are returned, even those without any correlated department
// Departments without a correlated student are not returned.
var query = students.LeftJoin(
    departments,
    student => s.DepartmentID,
    department => department.ID,
    (student, department) => new { student.FirstName, student.LastName, Department = department?.Name });

// New right (outer) join operator: all departments are returned, even those without any correlated student.
// Students without a correlated department are not returned.
var query = students.RightJoin(
    departments,
    student => s.DepartmentID,
    department => department.ID,
    (student, department) => new { student?.FirstName, student?.LastName, Department = department.Name });
```

Aside from allowing users to more easily express SQL LEFT/RIGHT JOIN when using a LINQ provider (such as EF Core), the new APIs also allow both easier and more efficient in-memory left/right joins, exactly in the way that inner joins are already supported.

Along with the introduction of the operators into System.Linq, the C# query expression syntax can be extended with new `left` and `right` modifiers for the `join` clauses, which would translate to these new methods, allowing for simpler and more efficient code where C# query syntax is used.

## Detailed design
[design]: #detailed-design

### Grammar

Proposed grammar change ([§11.7.1](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1117-query-expressions)):

```diff
join_clause
-    : 'join' type? identifier 'in' expression 'on' expression 'equals' expression
+    : ('left' | 'right')? 'join' type? identifier 'in' expression 'on' expression 'equals' expression
    ;
```

The `join` clause is thus extended via new, optional `left` and `right` modifiers; the clause stays otherwise the same, with the modifiers only changing which LINQ method is translated to (see below):

```c#
from student in students
left join department in departments on student.DepartmentID equals department.ID
select new { student.Name, department?.Name }
```

### `join` clause specification

This proposes removing the `join` clause from the [From, let, where, join and orderby clauses (§11.7.1)](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1117-query-expressions), and adding the following dedicated section for the `join` clause, including the new proposed modifiers.

<details>
<summary>Removal of `join` clause specs from [§11.7.1](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1117-query-expressions)</summary>

A `join` clause immediately followed by a `select` clause

```csharp
from «x1» in «e1»  
join «x2» in «e2» on «k1» equals «k2»  
select «v»
```

... remainder of existing join specification up through ...

> the final translation of which is
>
> ```csharp
> customers
>     .GroupJoin(
>         orders,
>         c => c.CustomerID,
>         o => o.CustomerID,
>         (c, co) => new { c, co })
>     .Select(x => new { x, n = x.co.Count() })
>     .Where(y => y.n >= 10)
>     .Select(y => new { y.x.c.Name, OrderCount = y.n })
> ```
>
> where `x` and `y` are compiler generated identifiers that are otherwise invisible and inaccessible.
>
> *end example*

</details>

Following is the new proposed dedicated section for the `join` clauses. This section would be numbered as 11.17.3.6 (pushing down the existing sections), or appended at the end. Note that although the `join into` clause is moved into the new section, it is otherwise completely unaffected by this proposal; `join into` translates to `GroupJoin()`, which already performs a conceptual left join (where an outer element has no correlated inner element, it is returned with an empty inner group).

#### 11.17.3.6 Join clauses

##### 11.17.3.6.1 Join clause

**PREVIOUS SECTION ON JOIN REMOVED ABOVE - NOT INCLUDING JOIN INTO - MOVED HERE VERBATIM**

The `left` and `right` modifiers of the `join` clause change the translation to the `LeftJoin()` and `RightJoin()` methods respectively, instead of `Join()`. Every other aspect of the translation remains the same.

##### 11.17.3.6.2 Join range variable nullability

A regular (inner) join introduces a range variable whose nullability follows from its source sequence. In other words, given the following code:

```csharp
from c in customersh
join o in orders on c.CustomerID equals o.CustomerID
select new { c.Name, o.OrderDate, o.Total }
```

... the nullability of the range variable `o` flows from `orders` (`o` is an `Order?` if `orders` is `List<Order?>`).

In contrast, since `left join` returns outer elements which don't have correlated inner elements, the inner range variable it introduces is always nullable:

```csharp
from c in customersh
left join o in orders on c.CustomerID equals o.CustomerID
select new { c.Name, o?.OrderDate, o?.Total }
```

... in this example, `o` is an `Order?` even if `orders` is a `List<Order>`. Note that "nullable" in this context means `T?` in an unconstrained generic context; in other words, if `orders` is a sequence of value types (e.g. `List<int>`), then `o` has the same type as the elements of that sequence (`int`, not `int?`).

`right join` operates in a similar way, with one important difference: the already-existing, outer range variable is made nullable, rather than the inner range variable:

```csharp
from o in orders
right join c in customersh on o.CustomerID equals c.CustomerID
select new { o?.OrderDate, o?.Total, c.Name }
```

In other words, if `o` was non-nullable before the `right join` operation (since `orders` is a sequence of non-nullable elements), the `right join` operation makes it nullable.

##### 11.17.3.6.3 Join into clause

<details>
<summary>Moved `join into` section, as-is</summary>

A `join`-`into` clause immediately followed by a `select` clause

```csharp
from «x1» in «e1»  
join «x2» in «e2» on «k1» equals «k2» into «g»  
select «v»
```

is translated into

```csharp
( «e1» ) . GroupJoin( «e2» , «x1» => «k1» , «x2» => «k2» ,
                     ( «x1» , «g» ) => «v» )
```

A `join into` clause followed by a query body clause

```csharp
from «x1» in «e1»  
join «x2» in «e2» on «k1» equals «k2» into *g»  
...
```

is translated into

```csharp
from * in ( «e1» ) . GroupJoin(  
   «e2» , «x1» => «k1» , «x2» => «k2» , ( «x1» , «g» ) => new { «x1» , «g» })
...
```

> *Example*: The example
>
> ```csharp
> from c in customers
> join o in orders on c.CustomerID equals o.CustomerID into co
> let n = co.Count()
> where n >= 10
> select new { c.Name, OrderCount = n }
> ```
>
> is translated into
>
> ```csharp
> from * in (customers).GroupJoin(
>     orders,
>     c => c.CustomerID,
>     o => o.CustomerID,
>     (c, co) => new { c, co })
> let n = co.Count()
> where n >= 10
> select new { c.Name, OrderCount = n }
> ```
>
> the final translation of which is
>
> ```csharp
> customers
>     .GroupJoin(
>         orders,
>         c => c.CustomerID,
>         o => o.CustomerID,
>         (c, co) => new { c, co })
>     .Select(x => new { x, n = x.co.Count() })
>     .Where(y => y.n >= 10)
>     .Select(y => new { y.x.c.Name, OrderCount = y.n })
> ```
>
> where `x` and `y` are compiler generated identifiers that are otherwise invisible and inaccessible.
>
> *end example*

</details>

## Drawbacks
[drawbacks]: #drawbacks

There are no specific drawbacks to introduce `left join`/`right join` AFAICT. It's worth noting that C# query expression support for LINQ hasn't evolved in a long time - this would be the first change in quite a while. At the same time, AFAIK there hasn't been any formal deprecation/archiving of this area of the language.

Note that the proposed `join` modifiers do not require any LINQ expression tree changes, as they're represented via existing MethodCallExpression's which reference the new `LeftJoin()` and `RightJoin()` methods. There is thus nothing blocking supporting them from LINQ providers (such as EF Core).

## Alternatives
[alternatives]: #alternatives

If support isn't added to C#, users can still use the `LeftJoin`/`RightJoin` methods being introduced into .NET 10, although this would force them to drop out of C# query syntax to method syntax. In order to keep using query syntax, users will more likely continue using the existing way of expressing left joins via `join into` + `DefaultIfEmpty()` ([docs](https://learn.microsoft.com/en-us/dotnet/csharp/linq/standard-query-operators/join-operations#perform-left-outer-joins)), which is both inefficient and more verbose.

Also, since first-class left/right join is being introduced into .NET and EF 10, not including support in C# would mean a partial feature that's implemented only in some parts of the stack and not in others.

## Open questions
[open]: #open-questions

As noted above, this is the first proposal for evolving C#'s support around LINQ in a long while; there are quite a few other gaps in this area: additional C# query expression clauses (distinct, aggregates, set operations...), as well as evolving expression tree support to allow for newer C# constructs ([discussion](https://github.com/dotnet/csharplang/issues/2545)). We should consider our strategy going forward in this area.

