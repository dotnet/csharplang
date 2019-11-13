# Fluent Class Definition

* [x] Proposed
* [x] Prototype: [Proof-of-concept, beta version](https://github.com/GalvanizedSoftware/Beethoven)
* [ ] Implementation: _TODO_
* [ ] Specification: _TODO_

## Summary
[summary]: #summary

Fluent style programming applied to class definition.
Similar code is imported into the class multiple times rather than being coded multiple times.

## Motivation
[motivation]: #motivation

Writing code using composition over inheritance will give better code.
Having the possibility to import code into a class without mapping code, will give shorter and more readable code. 

When writing the same code over and over, there is a risk of copy-paste errors.
These errors a difficult to spot, and are normally caught by a person, who has not written the code.

Even though the code is repeatet, and is almost identical,
some people could add there own code to fix a specific bug.
The 'standard' implementation is proven to work, but adding code to it makes the results unpredictable.

In the longer run a runtime version will give a lot of possibilities.
The code can be compiled to fit runtime conditions not know at compile time.

If mapping or implementation is automatic, 
the class does not need to change just because the interface or lower level technology changes.

## Detailed design
[design]: #detailed-design

To introduce fluent code definition, an option to to extent the auto-property system.

A class might be defined as:
```CS
class ClassA : INotifyPropertyChanged
{
  ILogger _logger = Logger.FindLogger(nameof(ClassA));

  int Option
  {
    get;
    set
      .SkipIfSame()
      .Set()
      .NotifyPropertyChanged();
  }

  string GetDiscription(string header)
    .Log(_logger, nameof(GetDiscription), header)
    .SkipIf<string>( () => Option == 0, "Nothing")
    .Return<string>($"{header] is {Option}")

  event PropertyChangedEventHandler PropertyChanged;
}
```

This should be compiled into a full implementation, so the methods (SkipIfSame, Log, etc.) are only used at compile time.

To control the flow inside the property implementation, the imported code is using this signature:
```
bool Foo(ref int returnValue)
```

If this returns ```true```, the execution continues, if ```false``` the execution stops.
At the end, ```returnValue``` is returned.

For the method, the internal signature is:
```CS
bool GetDiscription(string header, ref string returnValue)
```

So the internal code, could look like this:
```CS
class ClassA : INotifyPropertyChanged
{
  ILogger _logger = Logger.FindLogger("ClassA");
  int _option;

  int Option
  {
    get { return _option; }
    set
    {
      if (value.Equals(_option))
        return;
   
      _option = value;

      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Option"));
    }
  }

  string GetDiscription(string header)
  {
    string returnValue = default(string);

    _logger.WriteLog("GetDiscription", header);

    if (Option == 0)
    {
      returnValue = "Nothing";
      return returnValue;
    }

    returnValue = $"{header} is {Option}";
    if (true)
      return returnValue;

    return returnValue;
  }

  event PropertyChangedEventHandler PropertyChanged;
}
```
Alternatively, it might be nessesay to call methods instead of including the code.

### Types

Types for holding the defition are needed:
``` CS
TypeDefinition
GetPropertyDefinition
SetPropertyDefinition
MethodDefinition
EventDefinition
```

The definition types should be used internally in the compiler, but should also be public.
This will enable programmers to define classes runtime.

The dot-notation used in the class definition are extensions to a definition,
this also enables users to add their own. 

For example:
``` CS
SetPropertyDefinition SkipIfEqual(this SetPropertyDefinition previous)
{
  return new SetPropertyDefinition(previous, 
    (oldValue, newValue) => newValue.Equals(newValue))
}
```
(_Simplyfied for readability, this actual code might not work._)

### Default implementation
With the new notation, properties end up having an almost identical code.
So introducing a default implementation would result in less code.

The idea is to only define an interface, and the class just refers to a default implementation. For example:
``` CS
class ClassB : ISomeInterface
{
  import: MyDefaultPropertyImplementation defaultImplementation = new MyDefaultPropertyImplementation();
}
```

This means: Implement all properties in the interface using MyDefaultPropertyImplementation as a template.

I'm not sure this notation is the best way to do this,
but I don't want to mix it up with (_evil_) inheritance by having it one the class definition line,
and the compiler need to know somebody has implemented the interface, so it has to be a new keyword.

For cases not covered by the default implementation, custom implementation can be done inside the class.
``` CS
class ClassB : ISomeInterface
{
  import: MyDefaultPropertyImplementation defaultImplementation = new MyDefaultPropertyImplementation();

  void Foo2()
  {
    // Default implementation ignored, this is used instead
  }
}
```

The import feature could also be used to import partial implementation from another class.

``` CS
class ClassB : ISomeInterface
{
  import ISomeInterface: Foo1Implementation foo1 = new Foo1Implementation(); // Auto-map matching methods from ISomeInterface to class
}

class Foo1Implementation
{
  string Foo1()
  {
    return "1";
  }
}
```

### New posibilities

This tooling will open new possibilies:
* Much easier runtime compilation than existing emit-namespace
* Easier composition of classes where implementation is auto-mapped to good and **SOLID** implementations.
* Auto-mapping enables a form of duck-typing, auto-generated wrapper class.

## Drawbacks
[drawbacks]: #drawbacks

Major change, will have consequences in many places.

A fundametally different way of coding classes may confuse new programmers.

In some cases it might be unclear or illogical what version of the implementation is used.
This should be solve either by compiler errors or very clear rules.

This way of defining classes and code woold be confusing to some,
so the notation should be as self-explanatory as possible.

## Alternatives
[alternatives]: #alternatives

All features can be done with runtime compilation using the Roslyn runtim compilier.
However, performance would be better, if the ode was generated at compile time.

## Unresolved questions
[unresolved]: #unresolved-questions

Notation is a big issue, it will be a big task to find a way to introduce this without adding inconsistency to the language.
