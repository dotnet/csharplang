# Sample Extensions and API reference

These notes guide our conversation on how to extend the current API docs tooling so that we provide a great experience for customers when they want to learn about APIs represented by new `extension` members.

The first section describes how existing extension methods are displayed. These are `static` methods whose first parameter has the `this` modifier. They can be called either as instance methods on the *extended* type, or as static methods of the enclosing class.

The second section provides a template containing the different kinds of extensions enabled by the new `extension` members.Then, it shows a mockup of how those would be presented on learn.

Finally, a short discussion on how the disambiguation syntax, still being designed, could impact the final layout.

Scope for this is only for API reference for new extensions. Other docs tasks are out of scope.

## Open questions

- This assumes that two distinct `extension` containers declared in the same class can't have the same overload. For example, this should be illegal:
  ```csharp
  public static class Extensions
  {
      extension(IEnumerable<T> sequence)
      {
          public bool IsEmpty => ...;
      }
      extension(IEnumerable<T> sequence)
      {
          // Error duplicate member.
          public bool IsEmpty => ...;
      }
  }

## Existing Extension members

The experience for extension methods on docs provides a template for how we can approach the new extensions.

### Declaration

Existing extension methods are declared in a `static` class. The method must be `static`, and the first parameter has the `this` modifier:

```csharp
public class SomeExtensionsAndStuff
{
    public bool IsEmpty<T>(this IEnumerable<T> source) => source.Any() == false;
}
```

### Consumption

Extension methods can be consumed using "instance" syntax:

```csharp
bool empty = sequence.IsEmpty();
```

Or using a static call syntax:

```csharp
bool empty = SomeExtensionsAndStuff.IsEmpty(sequence);
```

### Docs presentation

We'll use the example of `System.Linq.Enumerable`:

- The page for the [class](https://learn.microsoft.com/dotnet/api/system.linq.enumerable) lists all the public extension methods defined in this class. The prototypes show the `this` modifier in the parameter. Each overload has an entry in the list. This includes generic specializations (e.g. `Sum(IEnumerable<int>)`, `Sum(IEnumerable<double>)` and so on).
- The TOC under that class lists all the method groups (overloads are combined into one entry).
- The extended type (`System.Collections.Generics.IEnumerable<T>`) includes a section that lists all [extension methods](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1#extension-methods). All overloads are listed here. The extension method signatures don't list the containing class (*should they?*). They are grouped by containing class, but other than the link URL, there's no indication of the containing class.

Can we assume that the lack of feedback regarding no way to see which class declares a given extension method indicates that ambiguities are rare?

### XML Comments

XML comments on the class and its methods provides the input for the current presentation:

```csharp
/// <summary>
/// Notes about the class...
/// </summary>
/// <remarks>
/// All kinds of details...
/// </remarks>
public static class SomeExtensionsAndStuff
{
    /// <summary>
    /// Determines if a collection is empty.
    /// </summary>
    /// <typeparam name="T">The element type for the sequence</typeparam>
    /// <param name="source">The source sequence</param>
    /// <returns>true if the sequence is empty</returns>
    /// <remarks>
    /// More details...
    /// </remarks>
    public static bool IsEmpty<T>(this IEnumerable<T> source) => source.Any() == false;
}
```

## C# 14 Extension Example

This section provides examples of all the different kinds of extension members proposed in C# 14 / .NET 10. For each one, we show how it would be called, and how we'll want the docs presentation for these types.

### Declaration

```csharp
// Static class containing extensions
public static class NewAgeExtensions
{
   // An extension container for a type: IEnumerable<T>.
   // Multiple extension containers can be defined in the 
   // same class, either for the same type or different types.
   extension<T>(IEnumerable<T> receiver) // "receiver" becomes the name of the `this` parameter.
   {
       // (currently supported) extension method
       public bool IsEmpty() => receiver.Any() == false;

       // "Instance" extension property
       public bool IsEmpty { get; } // How to display this member? `bool extension(IEnumerable<T>).IsEmpty { get; }`

       // "Instance" extension indexer:
       public T this[int index]=> receiver.Skip(index).First(); 

        // static extension operator 
        // "receiver" is not part of the signature, 
        // The parameter names are "left" and "right"
       public static IEnumerable<T> operator+(IEnumerable<T> left, IEnumerable<T> right) {}
   } 

    // Another extension container, for a closed generic type
   extension(IEnumerable<int> intSequence)
   {
       // "instance" extension method
       public int Sum();
   } 

   // Another extension container. Generic type parameter, with constraint(s)
   extension<T>(IEnumerable<T> structSequence) where T : struct
   {
        // "instance" extension method
       public int Method();
   }
   
   // An extension that contains only static method, so the 
   // receiver isn't needed:
   extension(IEnumerable<int>)
   {
        public static IEnumerable<int> operator+ (IEnumerable<int> left, IEnumerable<int> right) {}
   }
}
```

### Consumption

Each of these examples can be consumed using different syntax:

All extensions can be called as though they are instance members on the extended type:

```csharp
IEnumerable<string> allMessages = GetMessages();
IEnumerable<string> allErrors = GetErrorList();


// method:
bool empty = allMessages.IsEmpty();

// property:
bool empty2 = allMessages.IsEmpty;

// Indexer:
string atFive = allMessages[5];

// operator:
var allNotes = allMessages + allErrors;

// Closed generic type: (method shown, same for property and operators)
int sum = sequenceOfInts.Sum();

// Generic with constraints:
int result = sequenceOfPoints.Method(); // assume Point satisfies `struct` constraint.

// static operator on closed generic type:
IEnumerable<int> sumSequence = seqeunceOfInts + anotherSequenceOfInts;
```

The new extensions can also be called using the `static` member access. Using the `static` syntax will require some form of disambiguation syntax. This design decision has not been finalized yet. Only one example is shown:

```csharp
IEnumerable<string> allMessages = GetMessages();
IEnumerable<string> allErrors = GetErrorList();


// method:
bool empty = NewAgeExtensions.IsEmpty(allMessages);

// property:
bool empty2 = (allMessages as NewAgeExtensions).IsEmpty;

// indexer:
bool atFive = (allMessages as NewAgeExtensions)[5];

// operator:
var allNotes = (allMessages as NewAgeExtensions) + allErrors;

// Closed generic type: (method shown, same for property and operators)
int sum = (sequenceOfInts as NewAgeExtensions).Sum();

// Generic with constraints:
int result = (sequenceOfPoints as NewAgeExtensions).Method(); // assume Point satisfies `struct` constraint.

// static operator on closed generic type:
IEnumerable<int> sumSequence = (seqeunceOfInts as NewAgeExtensions) + anotherSequenceOfInts;
```

### Docs presentation

Using the presentation of the existing extension methods, we propose:

- The page for the `NewAgeExtensions` class lists all the public extension members defined in that class. There should be four lists:
  - The prototypes for "instance" methods show the `this` modifier in the receiver parameter. Each overload has an entry in the list. This includes generic specializations (e.g. `Sum(IEnumerable<int>)`, `Sum(IEnumerable<double>)` and so on).
  - The prototypes for properties show property syntax. A summary column should include the type being extended. (That may need to be enforced as part of the `///` comments). The "properties" node includes indexers. Indexers are named "Item[]" in the TOC.
  - The prototypes for static methods include the class name, `NewAgeExtensions` as part of the prototype.
  - The prototypes for operators shows the operator syntax. A summary column should include the class name, `NewAgeExtensions`.
- The TOC under that class lists all members defined in the class:
  - The **Methods** node lists all the method groups (overloads are combined into one entry). This includes all static methods.
  - The **Properties** node lists all properties and indexers.
  - The **operators** node lists all extension operators. The naming convention for an operator follows current operator pages (e.g. "Addition", "Multiplication", "Implicit", "Explicit").
- The extended type (`System.Collections.Generics.IEnumerable<T>`) includes a section that lists all extension members. All overloads are listed here. Following the TOC for classes, there are three sub-sections:  **Methods**, **Properties** and **operators**. The extension member signatures don't list the containing class (*should they?*). They are grouped by containing class, but other than the link URL, there's no indication of the containing class.

This presentation suggests possible changes to the existing extension methods. These should be considered:

- For the `static class` that declares `extensions`, members aren't grouped by the type extended, such as generic specialization. Should they?
- Where a type lists its extensions, should they be grouped by the declaring `static class`? They aren't now.

### XML comments

The `param` tag on the `extension` container is copied to the output for each relevant extension member in that extension. For that reason, it's not repeated as an element on each contained member.

```csharp
/// <Summary>
/// This class contains a number of extensions for sequences.
/// </Summary>
/// <remarks>
/// This would have a lot of new information about these new
/// extensions.
/// </remarks>
public static class NewAgeExtensions
{
   /// <param name="receiver">
   /// This is the input sequence.
   /// </param>
   extension<T>(IEnumerable<T> receiver)
   {
       /// <Summary>
       /// Determine if a sequence is empty.
       /// </Summary>
       /// <returns>`true` if the sequence is empty</returns>
       /// <remarks>yada, yada, yada</remarks>
       public bool IsEmpty() => receiver.Any() == false;

       /// <Summary>
       /// Determine if the source sequence is empty.
       /// <Summary>
       public bool IsEmpty { get; }

       /// <summary>
       /// ...
       /// </summary>
       /// <param name="index">The 0-based index</param>
       /// <returns>The item at index `index`.</returns>
       /// <remarks>This is O(n), where n is the index.</remarks>
       public T this[int index]=> receiver.Skip(index).First(); 

        /// <summary>
        /// Adds each corresponding element in the two sequences
        /// </summary>
        /// <param name="left">The left sequence</param>
        /// <param name="right">The right sequence</param>
        /// <returns>The sequence of sums</returns>
        /// <remarks>
        /// This operator adds the corresponding elements in the left and 
        /// right sequences. If the sequences are different lengths, the
        /// result sequence is the length of the shorter input sequence.
        /// </remarks>
        public static IEnumerable<T> operator+(IEnumerable<T> left, IEnumerable<T> right) {}
   } 

   /// <param name="intSequence>The source sequence of integers</param>
   extension(IEnumerable<int> intSequence)
   {
       /// <summary>...</summary>
       /// <remarks>...</remarks>
       public int Sum();
   } 

   /// <param name="structSequence>...</param>
   extension<T>(IEnumerable<T> structSequence) where T : struct
   {
       /// <summary>...</summary>
       /// <returns>...</returns>
       /// <remarks>...</remarks>
       public int Method();
   }
   
   extension(IEnumerable<int>)
   {
        /// <summary> ... </summary>
        /// <param name="left">...</param>
        /// <param name="right">...</param>
        /// <returns> ... </returns>
        /// <remarks> ... </remarks>
        public static IEnumerable<int> operator+ (IEnumerable<int> left, IEnumerable<int> right) {}
   }
}
```

## Disambiguation and API docs

A disambiguation syntax is required when more than one `class` declares extension members with the same signature. Consumers must use a static syntax to specify which method should be called. We believe this is the less common case. However, it is common enough that our docs presentation should clearly display the class where an extension member is declared.

The C# LDM hasn't finalized the [disambiguation syntax](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/extensions/disambiguation-syntax-examples.md).
