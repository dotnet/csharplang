# `left join` and `right join` query expression clauses

Champion issue: https://github.com/dotnet/csharplang/issues/8947

## Summary
[summary]: #summary

Introduce `left join` and `right join` clauses into LINQ query expression syntax, e.g.:

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

https://github.com/dotnet/runtime/issues/110292 is an approved API proposal for introducing LeftJoin and RightJoin operators into the System.Linq for .NET 10; see that proposal for additional performance information and benchmarks. Aside from allowing users to more easily express SQL LEFT/RIGHT JOIN when using a LINQ provider (such as EF Core), it would also allow both easier and more efficient in-memory left/right joins, exactly in the way that inner joins are already supported.

Along with the introduction of the operators into System.Linq, the C# query expression syntax can be extended with new `left join` and `right join` clauses which would translate to these new operators, allowing for simpler and more efficient code where C# query syntax is used.

## Detailed design
[design]: #detailed-design

Proposed grammar change ([§11.7.1](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/expressions.md#1117-query-expressions)):

### Grammar

```diff
join_clause
-    : 'join' type? identifier 'in' expression 'on' expression
+    : ('left' | 'right')? 'join' type? identifier 'in' expression 'on' expression
      'equals' expression
    ;
```

As per the grammar proposal, the syntax for the proposed `left join` and `right join` expression clauses would be identical to the existing `join` clause. For example:

```c#
from student in students
left join department in departments on student.DepartmentID equals department.ID
select new { student.Name, department?.Name }
```

### Left join translation

A `left join` clause immediately followed by a `select` clause

```csharp
from «x1» in «e1»  
left join «x2» in «e2» on «k1» equals «k2»  
select «v»
```

is translated into

```csharp
( «e1» ) . LeftJoin( «e2» , «x1» => «k1» , «x2» => «k2» , ( «x1» , «x2» ) => «v» )
```

> *Example*: The example
>
> ```csharp
> from c in customersh
> left join o in orders on c.CustomerID equals o.CustomerID
> select new { c.Name, o?.OrderDate, o?.Total }
> ```
>
> is translated into
>
> ```csharp
> (customers).LeftJoin(
>    orders,
>    c => c.CustomerID, o => o.CustomerID,
>    (c, o) => new { c.Name, o?.OrderDate, o?.Total })
> ```
>
> *end example*

A `left join` clause followed by a query body clause:

```csharp
from «x1» in «e1»  
left join «x2» in «e2» on «k1» equals «k2»  
...
```

is translated into

```csharp
from * in ( «e1» ) . LeftJoin(  
«e2» , «x1» => «k1» , «x2» => «k2» ,
( «x1» , «x2» ) => new { «x1» , «x2» })  
...
```

### Right join translation

A `right join` clause immediately followed by a `select` clause

```csharp
from «x1» in «e1»  
right join «x2» in «e2» on «k1» equals «k2»  
select «v»
```

is translated into

```csharp
( «e1» ) . RightJoin( «e2» , «x1» => «k1» , «x2» => «k2» , ( «x1» , «x2» ) => «v» )
```

> *Example*: The example
>
> ```csharp
> from o in orders
> right join c in customersh on o.CustomerID equals c.CustomerID
> select new { o?.OrderDate, o?.Total, c.Name }
> ```
>
> is translated into
>
> ```csharp
> (orders).RightJoin(
>    customers,
>    o => o.CustomerID, c => c.CustomerID,
>    (c, o) => new { o?.OrderDate, o?.Total, c.Name })
> ```
>
> *end example*

A `right join` clause followed by a query body clause:

```csharp
from «x1» in «e1»  
right join «x2» in «e2» on «k1» equals «k2»  
...
```

is translated into

```csharp
from * in ( «e1» ) . RightJoin(  
«e2» , «x1» => «k1» , «x2» => «k2» ,
( «x1» , «x2» ) => new { «x1» , «x2» })  
...
```

### Nullability of range variables

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

## Drawbacks
[drawbacks]: #drawbacks

There are no specific drawbacks to introduce `left join`/`right join` AFAICT. It's worth noting that C# query expression support for LINQ hasn't evolved in a long time - this would be the first change in quite a while. At the same time, AFAIK there hasn't been any formal deprecation/archiving of this area of the language.

## Alternatives
[alternatives]: #alternatives

If support isn't added to C#, users can still use the `LeftJoin`/`RightJoin` methods being introduced into .NET 10, although this would force them to drop out of C# query syntax to method syntax. In order to keep using query syntax, users will more likely continue using the existing way of expressing left joins via `join into` + `DefaultIfEmpty()` ([docs](https://learn.microsoft.com/en-us/dotnet/csharp/linq/standard-query-operators/join-operations#perform-left-outer-joins)), which is both inefficient and more verbose.

Also, since first-class left/right join is being introduced into .NET and EF 10, not including support in C# would mean a partial feature that's implemented only in some parts of the stack and not in others.

## Open questions
[open]: #open-questions

As noted above, this is the first proposal for evolving C#'s support around LINQ in a long while; there are quite a few other gaps in this area: additional C# query expression clauses (distinct, aggregates, set operations...), as well as evolving expression tree support to allow for newer C# constructs ([discussion](https://github.com/dotnet/csharplang/issues/2545)). We should consider our strategy going forward in this area.

