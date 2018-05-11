# Singleton Keyword

## Summary

Add a keyword for declaring singletons.

## Motivation

Write less and less code.

## Detailed design

The language declare a new keyword named "singleton" which allow to declare a Singleton object directly.

```C#
public singleton MyObject Obj => new MyObject();
```

or directly with a compile-time type inference

```C#
public singleton Obj => new MyObject();
```


## Alternative

It's already possible to declare singleton. It's a simple but a heavy and repetitive task. 

**For example in classical way :**

```C#
private static MyObject _obj;

public static MyObject Obj
{
  get 
  {
    if (_obj == null)
      _obj = new MyObject();
    
    return _obj;
  }
}
```

**It's better with null propagation way, but less understandable :**

```C#
private static MyObject _obj;
public static MyObject Obj => _obj ?? (_obj = new MyObject());
```

See https://github.com/csblo/csharp-tricks
