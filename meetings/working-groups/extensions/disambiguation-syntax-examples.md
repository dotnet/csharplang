## Declarations

``` c#
public static class MyExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        public bool IsEmpty => !source.Any();
        public IEnumerable<T> Where(Func<T, bool> predicate) => ...;
        public T this[int index] => source.ElementAt(index);

        public static IEnumerable<T> Empty => [];
        public static implicit operator ReadOnlySpan<T>(IEnumerable<T> sequence)
            => sequence.ToArray().AsSpan();
    }
    extension(IEnumerable<int> source)
    {
        public IEnumerable(int element, int count) 
            => Enumerable.Repeat(element, count);

        public static IEnumerable<int> Range(int start, int count) 
            => Enumerable.Range(start, count);
        public static IEnumerable<int> operator +(IEnumerable<int> sequence, int value)
            => sequence.Select(i => i + value);
    }
}
```

## Example code

``` c#
// Static extension members
var range = IEnumerable<int>.Range(0, 10);
var empty = IEnumerable<int>.Empty;
range += 5;
ReadOnlySpan<int> span = range;

// Instance extension members
var query = range.Where(i => i < 10);
var isEmpty = query.IsEmpty;
var first = query[0];
var repetition = new IEnumerable<int>(first, 10);
```

## Qualified members

``` c#
// Static extension members
var range = IEnumerable<int>.(MyExtensions.Range)(0, 10);
var empty = IEnumerable<int>.(MyExtensions.Empty);
range (MyExtensions.+=) 5; /* or */ (MyExtensions.+)(range, 5);
ReadOnlySpan<int> span = (MyExtensions.implicit)(range);

// Instance extension members
var query = range.(MyExtensions.Where)(i => i < 10);
var isEmpty = query.(MyExtensions.IsEmpty);
var first = query.(MyExtensions.this)[0];
var repetition = (MyExtensions.new) IEnumerable<int>(first, 10);
```

- Hard to come up with consistent approach to qualifying different members

## Cast-operator

``` c#
// Static extension members
var range = ((MyExtensions)IEnumerable<int>).Range(0, 10);
var empty = ((MyExtensions)IEnumerable<int>).Empty;
(MyExtensions)range += 5;
ReadOnlySpan<int> span = (MyExtensions)range;

// Instance extension members
var query = ((MyExtensions)range).Where(i => i < 10);
var isEmpty = ((MyExtensions)query).IsEmpty;
var first = ((MyExtensions)query)[0];
var repetition = new ((MyExtensions)IEnumerable<int>)(first, 10);
```

- Casting a type
- Casting to a non-type
- Casting LHS of compound assignment

## Invocation syntax

``` c#
// Static extension members
var range = MyExtensions(IEnumerable<int>).Range(0, 10);
var empty = MyExtensions(IEnumerable<int>).Empty;
MyExtensions(range) += 5;
ReadOnlySpan<int> span = MyExtensions(range);

// Instance extension members
var query = MyExtensions(range).Where(i => i < 10);
var isEmpty = MyExtensions(query).IsEmpty;
var first = MyExtensions(query)[0];
var repetition = new MyExtensions(IEnumerable<int>)(first, 10);
```

- Matches the parameter syntax for receiver types
- Invoking a type

## Indexing syntax

``` c#
// Static extension members
var range = MyExtensions[IEnumerable<int>].Range(0, 10);
var empty = MyExtensions[IEnumerable<int>].Empty;
MyExtensions[range] += 5;
ReadOnlySpan<int> span = MyExtensions[range];

// Instance extension members
var query = MyExtensions[range].Where(i => i < 10);
var isEmpty = MyExtensions[query].IsEmpty;
var first = MyExtensions[query][0];
var repetition = new MyExtensions[IEnumerable<int>](first, 10);
```

- Indexing a type

## As-operator

``` c#
// Static extension members
var range = (IEnumerable<int> as MyExtensions).Range(0, 10);
var empty = (IEnumerable<int> as MyExtensions).Empty;
(range as MyExtensions) += 5;
ReadOnlySpan<int> span = range as MyExtensions;

// Instance extension members
var query = (range as MyExtensions).Where(i => i < 10);
var isEmpty = (query as MyExtensions).IsEmpty;
var first = (query as MyExtensions)[0];
var repetition = new (IEnumerable<int> as MyExtensions)(first, 10);
```

- `as` on a type
- `as` a non-type
- `as` on LHS of compound assignment

## At-operator

``` c#
// Static extension members
var range = (IEnumerable<int> at MyExtensions).Range(0, 10);
var empty = (IEnumerable<int> at MyExtensions).Empty;
(range at MyExtensions) += 5;
ReadOnlySpan<int> span = range at MyExtensions;

// Instance extension members
var query = (range at MyExtensions).Where(i => i < 10);
var isEmpty = (query at MyExtensions).IsEmpty;
var first = (query at MyExtensions)[0];
var repetition = new (IEnumerable<int> at MyExtensions)(first, 10);
```

## @-operator

``` c#
// Static extension members
var range = (IEnumerable<int> @ MyExtensions).Range(0, 10);
var empty = (IEnumerable<int> @ MyExtensions).Empty;
range @ MyExtensions += 5;
ReadOnlySpan<int> span = range @ MyExtensions;

// Instance extension members
var query = (range @ MyExtensions).Where(i => i < 10);
var isEmpty = (query @ MyExtensions).IsEmpty;
var first = (query @ MyExtensions)[0];
var repetition = new (IEnumerable<int> @ MyExtensions)(first, 10);
```

## In-operator

``` c#
// Static extension members
var range = (IEnumerable<int> in MyExtensions).Range(0, 10);
var empty = (IEnumerable<int> in MyExtensions).Empty;
(range in MyExtensions) += 5;
ReadOnlySpan<int> span = range in MyExtensions;

// Instance extension members
var query = (range in MyExtensions).Where(i => i < 10);
var isEmpty = (query in MyExtensions).IsEmpty;
var first = (query in MyExtensions)[0];
var repetition = new (IEnumerable<int> in MyExtensions)(first, 10);
```

## Using-operator

``` c#
// Static extension members
var range = (IEnumerable<int> using MyExtensions).Range(0, 10);
var empty = (IEnumerable<int> using MyExtensions).Empty;
(range using MyExtensions) += 5;
ReadOnlySpan<int> span = range using MyExtensions;

// Instance extension members
var query = (range using MyExtensions).Where(i => i < 10);
var isEmpty = (query using MyExtensions).IsEmpty;
var first = (query using MyExtensions)[0];
var repetition = new (IEnumerable<int> using MyExtensions)(first, 10);
```

## Speakable lowering

``` c#
// Static extension members
var range = MyExtensions.Range(0, 10);
var empty = MyExtensions.Empty;
range = MyExtensions.op_addition(range, 5);
ReadOnlySpan<int> span = MyExtensions.op_implicit(range); // Need target typing?

// Instance extension members
var query = MyExtensions.Where(range, i => i < 10);
var isEmpty = MyExtensions.get_IsEmpty(query);
var first = MyExtensions.get_Item(query, 0);
var repetition = MyExtensions.__ctor_IEnumerable<int>(first, 10);
```

## Using clauses as statements

``` c#
using static MyExtensions 
{
    // Static extension members
    var range = IEnumerable<int>.Range(0, 10);
    var empty = IEnumerable<int>.Empty;
    range += 5;
    ReadOnlySpan<int> span = range;

    // Instance extension members
    var query = range.Where(i => i < 10);
    var isEmpty = query.IsEmpty;
    var first = query[0];
    var repetition = new IEnumerable<int>(first, 10);
}
```

- Doesn't address bypassing instance member


``` c#
// Static extension members
var range = MyExtensions.(IEnumerable<int>).Range(0, 10);
var empty = MyExtensions.(IEnumerable<int>).Empty;
MyExtensions.(range) += 5;
ReadOnlySpan<int> span = MyExtensions.(range);

// Instance extension members
var query = MyExtensions.(range).Where(i => i < 10);
var isEmpty = MyExtensions.(query).IsEmpty;
var first = MyExtensions.(query)[0];
var repetition = new MyExtensions.(IEnumerable<int>)(first, 10);
```


``` c#
// Static extension members
var range = IEnumerable<int>.(MyExtensions).Range(0, 10);
var empty = IEnumerable<int>.(MyExtensions).Empty;
range.(MyExtensions) += 5;
ReadOnlySpan<int> span = range.(MyExtensions);

// Instance extension members
var query = range.(MyExtensions).Where(i => i < 10);
var isEmpty = query.(MyExtensions).IsEmpty;
var first = query.(MyExtensions)[0];
var repetition = new IEnumerable<int>.(MyExtensions)(first, 10);
```


``` c#
// Static extension members
var range = IEnumerable<int>.as(MyExtensions).Range(0, 10);
var empty = IEnumerable<int>.as(MyExtensions).Empty;
range.as(MyExtensions) += 5;
ReadOnlySpan<int> span = range.as(MyExtensions);

// Instance extension members
var query = range.as(MyExtensions).Where(i => i < 10);
var isEmpty = query.as(MyExtensions).IsEmpty;
var first = query.as(MyExtensions)[0];
var repetition = new IEnumerable<int>.as(MyExtensions)(first, 10);
```


``` c#
// Static extension members
var range = IEnumerable<int>.MyExtensions.Range(0, 10);
var empty = IEnumerable<int>.MyExtensions.Empty;
range.MyExtensions += 5;
ReadOnlySpan<int> span = range.MyExtensions;

// Instance extension members
var query = range.MyExtensions.Where(i => i < 10);
var isEmpty = query.MyExtensions.IsEmpty;
var first = query.MyExtensions[0];
var repetition = new IEnumerable<int>.MyExtensions(first, 10);
```
- Doesn't allow giving the full name of `MyExtensions`


``` c#
// Static extension members
var range = IEnumerable<int>.in(MyExtensions).Range(0, 10);
var empty = IEnumerable<int>.in(MyExtensions).Empty;
range.in(MyExtensions) += 5;
ReadOnlySpan<int> span = range.in(MyExtensions);

// Instance extension members
var query = range.in(MyExtensions).Where(i => i < 10);
var isEmpty = query.in(MyExtensions).IsEmpty;
var first = query.in(MyExtensions)[0];
var repetition = new IEnumerable<int>.in(MyExtensions)(first, 10);
```



``` c#
// Static extension members
var range = IEnumerable<int>.at(MyExtensions).Range(0, 10);
var empty = IEnumerable<int>.at(MyExtensions).Empty;
range.at(MyExtensions) += 5;
ReadOnlySpan<int> span = range.at(MyExtensions);

// Instance extension members
var query = range.at(MyExtensions).Where(i => i < 10);
var isEmpty = query.at(MyExtensions).IsEmpty;
var first = query.at(MyExtensions)[0];
var repetition = new IEnumerable<int>.at(MyExtensions)(first, 10);
```

