# Conditional return and throw statement.

## Summary

Make a nice compact syntax to return a value or throw an exception based upon a condition

## Motivation

Often developers are stuck writing if statement to make exits early on in a method by returning a value or throwing an error.
For example

```cs
public class MyClass
{
  public bool MyValidation(bool var1, bool var2, string someText)
  {
    if(string.IsNullOrEmpty())
    {
      throw new MyException("Missing argument `someText` required for validation");
    }

    if(!var1 && !var2)
    {
      return false;
    }

    if(var1 && var2) 
    {
      return true;
    }

    ... ...  (∩｀-´)⊃━☆ﾟ.*・｡ﾟ Magic
  }
}
```
The language already made this a bit better by onelining these situations
For example
```cs
public class MyClass
{
  public bool MyValidation(bool var1, bool var2, string someText)
  {
    if(string.IsNullOrEmpty()) throw new MyException("Missing argument `someText` required for validation");

    if(!var1 && !var2) return false;

    if(var1 && var2) return true;

    ...  (∩｀-´)⊃━☆ﾟ.*・｡ﾟ Magic

  }
}
```

## Detailed Design

BUT... what is we could do this

```cs
public class MyClass
{
  public bool MyValidation(bool var1, bool var2, string someText)
  {
    throw new MyException("Missing argument `someText` required for validation") when string.IsNullOrEmpty();

    return false when !var1 && !var2;

    return true when (var1 && var2);

    ...  (∩｀-´)⊃━☆ﾟ.*・｡ﾟ Magic

  }
}
```
this is even more intresting when we add pattern matching
for example

```cs
public class MyClass
{
  public bool MyValidation(bool var1, bool var2, string someText)
  {
    return GetSomeValue() when not null;

    return GetSomeOtherValue() when { SomeProperty = 10 };

    var val = GetSomeObject();
    return 20 when val is { SomeThing = true }


    ...  (∩｀-´)⊃━☆ﾟ.*・｡ﾟ MOAR MAGIC

  }
}
```
