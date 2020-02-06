# Documentation comments {#documentation-comments .Appendix1}

**This annex is informative.**

## General {#general-109 .Appendix2}

C\# provides a mechanism for programmers to document their code using a special comment syntax that contains XML text. In source code files, comments having a certain form can be used to direct a tool to produce XML from those comments and the source code elements, which they precede. Comments using such syntax are called ***documentation comments***. They must immediately precede a user-defined type (such as a class, delegate, or interface) or a member (such as a field, event, property, or method). The XML generation tool is called the ***documentation generator***. (This generator could be, but need not be, the C\# compiler itself.) The output produced by the documentation generator is called the ***documentation file***. A documentation file is used as input to a ***documentation viewer***; a tool intended to produce some sort of visual display of type information and its associated documentation.

A conforming C\# compiler is not required to check the syntax of documentation comments; such comments are simply ordinary comments. A conforming compiler is permitted to do such checking, however.

This specification suggests a set of standard tags to be used in documentation comments, but use of these tags is not required, and other tags may be used if desired, as long the rules of well-formed XML are followed. For C\# implementations targeting the CLI, it also provides information about the documentation generator and the format of the documentation file. No information is provided about the documentation viewer.

## Introduction {#introduction-1 .Appendix2}

Comments having a special form can be used to direct a tool to produce XML from those comments and the source code elements, which they precede. Such comments are single-line comments that start with three slashes (///), or delimited comments that start with a slash and two stars (/\*\*). They must immediately precede a user-defined type (such as a class, delegate, or interface) or a member (such as a field, event, property, or method) that they annotate. Attribute sections (§22.3) are considered part of declarations, so documentation comments must precede attributes applied to a type or member.

**Syntax:**

```csharp
single-line-doc-comment::
	/// input-characters~opt~

delimited-doc-comment::
	/** delimited-comment-text~opt~ */
```

In a *single-line-doc-comment*, if there is a *whitespace* character following the /// characters on each of the *single-line-doc-comments* adjacent to the current *single-line-doc-comment*, then that *whitespace* character is not included in the XML output.

In a *delimited-doc-comment*, if the first non-*whitespace* character on the second line is an *asterisk* and the same pattern of optional *whitespace* characters and an *asterisk* character is repeated at the beginning of each of the lines within the *delimited-doc-comment*, then the characters of the repeated pattern are not included in the XML output. The pattern can include *whitespace* characters after, as well as before, the *asterisk* character.

**Example:**
```csharp
/// <summary>Class <c>Point</c> models a point in a two-dimensional
/// plane.</summary>
///
public class Point
{
	/// <summary>method <c>draw</c> renders the point.</summary>
	void draw() {…}
}
```

The text within documentation comments must be well formed according to the rules of XML (http://www.w3.org/TR/REC-xml). If the XML is ill formed, a warning is generated and the documentation file will contain a comment saying that an error was encountered.

Although developers are free to create their own set of tags, a recommended set is defined in §D.3. Some of the recommended tags have special meanings:

-   The `<param>` tag is used to describe parameters. If such a tag is used, the documentation generator must verify that the specified parameter exists and that all parameters are described in documentation comments. If such verification fails, the documentation generator issues a warning.

-   The cref attribute can be attached to any tag to provide a reference to a code element. The documentation generator must verify that this code element exists. If the verification fails, the documentation generator issues a warning. When looking for a name described in a cref attribute, the documentation generator must respect namespace visibility according to using statements appearing within the source code. For code elements that are generic, the normal generic syntax (e.g.; “List`<T>`”) cannot be used because it produces invalid XML. Braces can be used instead of brackets (e.g.; “List{T}”), or the XML escape syntax can be used (e.g.; “List`<T>`”).

-   The `<summary>` tag is intended to be used by a documentation viewer to display additional information about a type or member.

-   The `<include>` tag includes information from an external XML file.

Note carefully that the documentation file does not provide full information about the type and members (for example, it does not contain any type information). To get such information about a type or member, the documentation file must be used in conjunction with reflection on the type or member.

## Recommended tags {#recommended-tags .Appendix2}

### General {#general-110 .Appendix3}

The documentation generator must accept and process any tag that is valid according to the rules of XML. The following tags provide commonly used functionality in user documentation. (Of course, other tags are possible.)

  ---------------------- --------------- --------------------------------------------------------
  **Tag**                **Reference**   **Purpose**
  &lt;c&gt;              §D.3.2          Set text in a code-like font
  &lt;code&gt;           §D.3.3          Set one or more lines of source code or program output
  &lt;example&gt;        §D.3.4          Indicate an example
  &lt;exception&gt;      §D.3.5          Identifies the exceptions a method can throw
  &lt;list&gt;           §D.3.6          Create a list or table
  &lt;include&gt;        §D.3.6          Includes XML from an external file
  &lt;para&gt;           §D.3.8          Permit structure to be added to text
  &lt;param&gt;          §D.3.9          Describe a parameter for a method or constructor
  &lt;paramref&gt;       §D.3.10         Identify that a word is a parameter name
  &lt;permission&gt;     §D.3.11         Document the security accessibility of a member
  &lt;remarks&gt;        §D.3.12         Describe additional information about a type
  &lt;returns&gt;        §D.3.13         Describe the return value of a method
  &lt;see&gt;            §D.3.14         Specify a link
  &lt;seealso&gt;        §D.3.15         Generate a *See Also* entry
  &lt;summary&gt;        §D.3.16         Describe a type or a member of a type
  &lt;typeparam&gt;      §D.3.17         Describe a type parameter for a generic type or method
  &lt;typeparamref&gt;   §D.3.18         Identify that a word is a type parameter name
  &lt;value&gt;          §D.3.17         Describe a property
  ---------------------- --------------- --------------------------------------------------------

### &lt;c&gt; {#c .Appendix3}

This tag provides a mechanism to indicate that a fragment of text within a description should be set in a special font such as that used for a block of code. For lines of actual code, use `<code>` (§D.3.3).

**Syntax:**
```csharp
<c>text</c>
```
**Example:**

/// &lt;summary&gt;Class &lt;c&gt;Point&lt;/c&gt; models a point in a two-dimensional\
/// plane.&lt;/summary&gt;

public class Point\
{\
	// …\
}


### &lt;code&gt; {#code .Appendix3}

This tag is used to set one or more lines of source code or program output in some special font. For small code fragments in narrative, use `<c>` (§D.3.2).

**Syntax:**
```csharp
<code>source code or program output</code>
```

**Example:**

```csharp
/// <summary>This method changes the point's location by
/// the given x- and y-offsets.
/// <example>For example:
/// <code>
/// Point p = new Point(3,5);
/// p.Translate(-1,3);
/// </code>
/// results in <c>p</c>'s having the value (2,8).
/// </example>
/// </summary>

public void Translate(int xor, int yor) {
	X += xor;
	Y += yor;
}
```

### &lt;example&gt; {#example .Appendix3}

This tag allows example code within a comment, to specify how a method or other library member might be used. Ordinarily, this would also involve use of the tag &lt;code&gt; (§D.3.3) as well.

**Syntax:**
```csharp
<example>description</example>
```

**Example:**

See `<code>` (§D.3.3) for an example.

### &lt;exception&gt; {#exception .Appendix3}

This tag provides a way to document the exceptions a method can throw.

**Syntax:**
```csharp
<exception cref="member">description</exception>
```
where

> cref="*member*"
>
> The name of a member. The documentation generator checks that the given member exists and translates *member* to the canonical element name in the documentation file.
>
> *description *
>
> A description of the circumstances in which the exception is thrown.

**Example:**

```csharp
public class DataBaseOperations
{
	/// <exception cref="MasterFileFormatCorruptException"></exception>
	/// <exception cref="MasterFileLockedOpenException"></exception>
	public static void ReadRecord(int flag) {
		if (flag == 1)
			throw new MasterFileFormatCorruptException();
		else if (flag == 2)
			throw new MasterFileLockedOpenException();
		// …
	}
}
```

### &lt;include&gt; {#include .Appendix3}

This tag allows including information from an XML document that is external to the source code file. The external file must be a well-formed XML document, and an XPath expression is applied to that document to specify what XML from that document to include. The `<include>` tag is then replaced with the selected XML from the external document.

**Syntax**:

```csharp
<include file="filename" path="xpath" />
```

where

> file="filename"
>
> The file name of an external XML file. The file name is interpreted relative to the file that contains the include tag.
>
> path=*"*xpath*"*
>
> An XPath expression that selects some of the XML in the external XML file.

**Example**:

If the source code contained a declaration like:

```csharp
/// <include file="docs.xml" path='extradoc/class[@name="IntList"]/*' />
public class IntList { … }
```

and the external file “docs.xml” had the following contents:
```xml
<?xml version="1.0"?>
<extradoc>
	<class name="IntList">
		<summary>
			Contains a list of integers.
		</summary>
	</class>
	<class name="StringList">
		<summary>
			Contains a list of integers.
		</summary>
	</class>
</extradoc>
```

then the same documentation is output as if the source code contained:

```xml
// <summary>
/// Contains a list of integers.
/// </summary>
public class IntList { … }
```

### &lt;list&gt; {#list .Appendix3}

This tag is used to create a list or table of items. It can contain a `<listheader>` block to define the heading row of either a table or definition list. (When defining a table, only an entry for *term* in the heading need be supplied.)

Each item in the list is specified with an `<item>` block. When creating a definition list, both *term* and *description* must be specified. However, for a table, bulleted list, or numbered list, only *description* need be specified.

**Syntax:**

```csharp
<list type="bullet" | "number" | "table">
	<listheader>
		<term>term</term>
		<description>description</description>
	</listheader>

	<item>
		<term>term</term>
		<description>description</description>
	</item>

	…

	<item>
	<term>term</term>
	<description>description</description>
	</item>
</list>

```
where

> *term*
>
> The term to define, whose definition is in *description*.
>
> *description*
>
> Either an item in a bullet or numbered list, or the definition of a *term*.

**Example:**

```csharp
public class MyClass
{
	/// <summary>Here is an example of a bulleted list:
	/// <list type="bullet">
	/// <item>
	/// <description>Item 1.</description>
	/// </item>
	/// <item>
	/// <description>Item 2.</description>
	/// </item>
	/// </list>
	/// </summary>
	public static void Main () {
		// …
	}
}
```

### &lt;para&gt; {#para .Appendix3}

This tag is for use inside other tags, such as `<summary>` (§D.3.16) or `<returns>` (§D.3.13), and permits structure to be added to text.

**Syntax:**

```csharp
<para>content</para>
```

where

> *content*
>
> The text of the paragraph.

**Example:**

```csharp
/// <summary>This is the entry point of the Point class testing program.
/// <para>This program tests each method and operator, and
/// is intended to be run after any non-trvial maintenance has
/// been performed on the Point class.</para></summary>
public static void Main() {
	// …
}
```

### &lt;param&gt; {#param .Appendix3}

This tag is used to describe a parameter for a method, constructor, or indexer.

**Syntax:**

```csharp
<param name="name">description</param>
```

where

> *name*
>
> The name of the parameter.
>
> *description*
>
> A description of the parameter.

**Example:**

```csharp
/// <summary>This method changes the point's location to
/// the given coordinates.</summary>
/// <param name="xor">the new x-coordinate.</param>
/// <param name="yor">the new y-coordinate.</param>
public void Move(int xor, int yor) {
	X = xor;
	Y = yor;
}
```

### &lt;paramref&gt; {#paramref .Appendix3}

This tag is used to indicate that a word is a parameter. The documentation file can be processed to format this parameter in some distinct way.

**Syntax:**

```csharp
<paramref name="*name*"/>
```

where

> *name*
>
> The name of the parameter.

**Example:**

```csharp
/// <summary>This constructor initializes the new Point to
/// (<paramref name="xor"/>,<paramref name="yor"/>).</summary>
/// <param name="xor">the new Point's x-coordinate.</param>
/// <param name="yor">the new Point's y-coordinate.</param>

public Point(int xor, int yor) {
	X = xor;
	Y = yor;
}
```
### &lt;permission&gt; {#permission .Appendix3}

This tag allows the security accessibility of a member to be documented.

**Syntax:**

```csharp
<permission cref="member">description</permission>
```

where

> *member*
>
> The name of a member. The documentation generator checks that the given code element exists and translates *member* to the canonical element name in the documentation file.
>
> *description*
>
> A description of the access to the member.

**Example:**

```csharp
/// <permission cref="System.Security.PermissionSet">Everyone can
/// access this method.</permission>

public static void Test() {
	// …
}
```

### &lt;remarks&gt; {#remarks .Appendix3}

This tag is used to specify extra information about a type. Use `<summary>` (§D.3.16) to describe the type itself and the members of a type.

**Syntax:**

```csharp
<remarks>description</remarks>
```

where

> *description*
>
> The text of the remark.

**Example:**

```csharp
/// <summary>Class <c>Point</c> models a point in a
/// two-dimensional plane.</summary>
/// <remarks>Uses polar coordinates</remarks>
public class Point
{
	// …
}
```

### &lt;returns&gt; {#returns .Appendix3}

This tag is used to describe the return value of a method.

**Syntax:**

```csharp
<returns>description</returns>
```

where

> *description*
>
> A description of the return value.

**Example:**

```csharp
/// <summary>Report a point's location as a string.</summary>
/// <returns>A string representing a point's location, in the form (x,y),
/// without any leading, trailing, or embedded whitespace.</returns>
public override string ToString() {
	return "(" + X + "," + Y + ")";
}
```

### &lt;see&gt; {#see .Appendix3}

This tag allows a link to be specified within text. Use `<seealso>` (§D.3.15) to indicate text that is to appear in a *See Also* subclause.

**Syntax:**

```csharp
<see cref="member"/>
```

where

> *member*
>
> The name of a member. The documentation generator checks that the given code element exists and changes *member* to the element name in the generated documentation file.

**Example:**

```csharp
/// <summary>This method changes the point's location to
/// the given coordinates. <see cref="Translate"/></summary>

public void Move(int xor, int yor) {
	X = xor;
	Y = yor;
}

/// <summary>This method changes the point's location by
/// the given x- and y-offsets. <see cref="Move"/>
/// </summary>
public void Translate(int xor, int yor) {
	X += xor;
	Y += yor;
}
```

### &lt;seealso&gt; {#seealso .Appendix3}

This tag allows an entry to be generated for the *See Also* subclause. Use `<see>` (§D.3.14) to specify a link from within text.

**Syntax:**

```csharp
<seealso cref="member"/>
```

where

> *member*
>
> The name of a member. The documentation generator checks that the given code element exists and changes *member* to the element name in the generated documentation file.

**Example:**

```csharp
/// <summary>This method determines whether two Points have the same
/// location.</summary>
/// <seealso cref="operator=="/>
/// <seealso cref="operator!="/>
public override bool Equals(object o) {
	// …
}
```

### &lt;summary&gt; {#summary .Appendix3}

This tag can be used to describe a type or a member of a type. Use `<remarks>` (§D.3.12) to describe the type itself.

**Syntax:**

```csharp
<summary>description</summary>
```

where

> *description*
>
> A summary of the type or member.

**Example:**

```csharp
/// <summary>This constructor initializes the new Point to (0,0).</summary>
public Point() : this(0,0) {
}
```

### &lt;typeparam&gt; {#typeparam .Appendix3}

This tag is used to describe a type parameter for a generic type or method.

**Syntax:**

```csharp
<typeparam name="name">description</typeparam>
```

where

> *name*
>
> The name of the type parameter.
>
> *description*
>
> A description of the typeparameter.

**Example:**

```csharp
/// <summary>A generic list class.</summary>
/// <typeparam name="T">The type stored by the list.</typeparam>
public class MyList<T> {
…
}
```

### &lt;typeparamref&gt; {#typeparamref .Appendix3}

This tag is used to indicate that a word is a type parameter. The documentation file can be processed to format this type parameter in some distinct way.

**Syntax:**

```csharp
<typeparamref name="name"/>
```

where

> *name*
>
> The name of the type parameter.

**Example:**

```csharp
/// <summary>This method fetches data and returns a list of <typeparamref name=”T”> ”/>”> .</summary>
/// <param name="string">query to execute</param>

public List<T> FetchData<T>(string query) {
…
}
```

### &lt;value&gt; {#value .Appendix3}

This tag allows a property to be described.

**Syntax:**

```csharp
<value>property description</value>
```

where

> *property description*
>
> A description for the property.

**Example:**

```csharp
/// <value>Property <c>X</c> represents the point's x-coordinate.</value>
public int X
{
	get { return x; }
	set { x = value; }
}
```

## Processing the documentation file {#processing-the-documentation-file .Appendix2}

### General {#general-111 .Appendix3}

The following information is intended for C\# implementations targeting the CLI.

The documentation generator generates an ID string for each element in the source code that is tagged with a documentation comment. This ID string uniquely identifies a source element. A documentation viewer can use an ID string to identify the corresponding item to which the documentation applies.

The documentation file is not a hierarchical representation of the source code; rather, it is a flat list with a generated ID string for each element.

### ID string format {#id-string-format .Appendix3}

The documentation generator observes the following rules when it generates the ID strings:

-   No white space is placed in the string.

-   The first part of the string identifies the kind of member being documented, via a single character followed by a colon. The following kinds of members are defined:

  --------------- ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
  **Character**   **Description**
  E               Event
  F               Field
  M               Method (including constructors, finalizers, and operators)
  N               Namespace
  P               Property (including indexers)
  T               Type (such as class, delegate, enum, interface, and struct)
  !               Error string; the rest of the string provides information about the error. For example, the documentation generator generates error information for links that cannot be resolved.
  --------------- ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

-   The second part of the string is the fully qualified name of the element, starting at the root of the namespace. The name of the element, its enclosing type(s), and namespace are separated by periods. If the name of the item itself has periods, they are replaced by \# (U+0023) characters. (It is assumed that no element has this character in its name.)

-   [#_Toc34995895 .anchor](#_Toc510510576 .anchor) For methods and properties with arguments, the argument list follows, enclosed in parentheses. For those without arguments, the parentheses are omitted. The arguments are separated by commas. The encoding of each argument is the same as a CLI signature, as follows:

<!-- -->

-   Arguments are represented by their documentation name, which is based on their fully qualified name, modified as follows:

<!-- -->

-   Arguments that represent generic types have an appended “’” character followed by the number of type parameters

-   Arguments having the out or ref modifier have an @ following their type name. Arguments passed by value or via params have no special notation.

-   Arguments that are arrays are represented as \[ *lowerbound* : *size* , … , *lowerbound* : *size* \] where the number of commas is the rank less one, and the lower bounds and size of each dimension, if known, are represented in [#_Hlt518052355 .anchor](#_Hlt518052355 .anchor)decimal. If a lower bound or size is not specified, it is omitted. If the lower bound and size for a particular dimension are omitted, the “:” is omitted as well. Jagged arrays are represented by one “\[\]” per level.

-   Arguments that have pointer types other than void are represented using a \* following the type name. A void pointer is represented using a type name of System.Void.

-   Arguments that refer to generic type parameters defined on types are encoded using the “\`” character followed by the zero-based index of the type parameter.

-   Arguments that use generic type parameters defined in methods use a double-backtick “\`\`” instead of the “\`” used for types.

-   Arguments that refer to constructed generic types are encoded using the generic type, followed by “{“, followed by a comma-separated list of type arguments, followed by “}”.

### ID string examples {#id-string-examples .Appendix3}

The following examples each show a fragment of C\# code, along with the ID string produced from each source element capable of having a documentation comment:

-   Types are represented using their fully qualified name, augmented with generic information:

```csharp
enum Color { Red, Blue, Green }

namespace Acme
{
	interface IProcess { … }

	struct ValueType { … }

	class Widget: IProcess
	{
		public class NestedClass { … }

		public interface IMenuItem { … }

		public delegate void Del(int i);

		public enum Direction { North, South, East, West }
	}

	class MyList<T>
	{
		class Helper<U,V>{ … }
	}
}

"T:Color"
"T:Acme.IProcess"
"T:Acme.ValueType"
"T:Acme.Widget"
"T:Acme.Widget.NestedClass"
"T:Acme.Widget.IMenuItem"
"T:Acme.Widget.Del"
"T:Acme.Widget.Direction"
"T:Acme.MyList\`1"
"T:Acme.MyList\`1.Helper\`2"
```

-   Fields are represented by their fully qualified name.

```csharp
namespace Acme
{
	struct ValueType
	{
		private int total;
	}

	class Widget: IProcess
	{
		public class NestedClass
	{
		private int value;
	}

	private string message;
	private static Color defaultColor;
	private const double PI = 3.14159;
	protected readonly double monthlyAverage;
	private long\[\] array1;
	private Widget\[,\] array2;
	private unsafe int \*pCount;
	private unsafe float \*\*ppValues;
	}
}

"F:Acme.ValueType.total"
"F:Acme.Widget.NestedClass.value"
"F:Acme.Widget.message"
"F:Acme.Widget.defaultColor"
"F:Acme.Widget.PI"
"F:Acme.Widget.monthlyAverage"
"F:Acme.Widget.array1"
"F:Acme.Widget.array2"
"F:Acme.Widget.pCount"
"F:Acme.Widget.ppValues"
```

-   Constructors.

```csharp
namespace Acme
{
	class Widget: IProcess
	{
		static Widget() { … }

		public Widget() { … }

		public Widget(string s) { … }
	}
}

"M:Acme.Widget.\#cctor"
"M:Acme.Widget.\#ctor"
"M:Acme.Widget.\#ctor(System.String)"
```

-   Finalizers.

```csharp
namespace Acme
{
	class Widget: IProcess
	{
		~Widget() { … }
	}
}

"M:Acme.Widget.Finalize"
```

-   Methods.

```csharp
namespace Acme
{
	struct ValueType
	{
		public void M(int i) { … }
	}

	class Widget: IProcess
	{
		public class NestedClass
		{
			public void M(int i) { … }
		}

		public static void M0() { … }
		public void M1(char c, out float f, ref ValueType v) { … }
		public void M2(short[] x1, int[,] x2, long[][] x3) { … }
		public void M3(long[][] x3, Widget[][,,] x4) { … }
		public unsafe void M4(char *pc, Color **pf) { … }
		public unsafe void M5(void *pv, double *[][,] pd) { … }
		public void M6(int i, params object[] args) { … }
	}
	class MyList<T>
	{
		public void Test(T t) { … }
	}

	class UseList
	{
		public void Process(MyList<int> list) { … }
		public MyList<T> GetValues<T>(T value) { … } }
}

"M:Acme.ValueType.M(System.Int32)"
"M:Acme.Widget.NestedClass.M(System.Int32)"
"M:Acme.Widget.M0"
"M:Acme.Widget.M1(System.Char,System.Single@,Acme.ValueType@)"
"M:Acme.Widget.M2(System.Int16[],System.Int32[0:,0:],System.Int64[][])"
"M:Acme.Widget.M3(System.Int64[][],Acme.Widget[0:,0:,0:][])"
"M:Acme.Widget.M4(System.Char*,Color**)"
"M:Acme.Widget.M5(System.Void*,System.Double*[0:,0:][])"
"M:Acme.Widget.M6(System.Int32,System.Object[])"
"M:Acme.MyList`1.Test(`0)"
"M:Acme.UseList.Process(Acme.MyList{System.Int32})"
"M:Acme.UseList.GetValues``1(``0)"
```

-   Properties and indexers.

```csharp
namespace Acme
{
	class Widget: IProcess
	{
		public int Width {get { … } set { … }}
		public int this[int i] {get { … } set { … }}
		public int this[string s, int i] {get { … } set { … }}
	}
}

"P:Acme.Widget.Width"
"P:Acme.Widget.Item(System.Int32)"
"P:Acme.Widget.Item(System.String,System.Int32)"
```

-   Events

```csharp
namespace Acme
{
	class Widget: IProcess
	{
		public event Del AnEvent;
	}
}

"E:Acme.Widget.AnEvent"
```

-   Unary operators.

```csharp
namespace Acme
{
	class Widget: IProcess
	{
		public static Widget operator+(Widget x) { … }
	}
}

"M:Acme.Widget.op_UnaryPlus(Acme.Widget)"
```

> The complete set of unary operator function names used is as follows: op\_UnaryPlus, op\_UnaryNegation, op\_LogicalNot, op\_OnesComplement, op\_Increment, op\_Decrement, op\_True, and op\_False.

-   Binary operators.

```csharp
namespace Acme
{
	class Widget: IProcess
	{
		public static Widget operator+(Widget x1, Widget x2) { … }
	}
}

"M:Acme.Widget.op_Addition(Acme.Widget,Acme.Widget)"
```

> The complete set of binary operator function names used is as follows: op\_Addition, op\_Subtraction, op\_Multiply, op\_Division, op\_Modulus, op\_BitwiseAnd, op\_BitwiseOr, op\_ExclusiveOr, op\_LeftShift, op\_RightShift, op\_Equality, op\_Inequality, op\_LessThan, op\_LessThanOrEqual, op\_GreaterThan, and op\_GreaterThanOrEqual.

-   Conversion operators have a trailing “\~” followed by the return type.

```csharp
namespace Acme
{
	class Widget: IProcess
	{
		public static explicit operator int(Widget x) { … }
		public static implicit operator long(Widget x) { … }
	}
}

"M:Acme.Widget.op_Explicit(Acme.Widget)~System.Int32"
"M:Acme.Widget.op_Implicit(Acme.Widget)~System.Int64"
```

## An example {#an-example .Appendix2}

### C\# source code {#c-source-code .Appendix3}

The following example shows the source code of a Point class:

```csharp
namespace Graphics
{

	/// <summary>Class <c>Point</c> models a point in a two-dimensional plane.
	/// </summary>
	public class Point
	{

		/// <summary>Instance variable <c>x</c> represents the point's
		/// x-coordinate.</summary>
		private int x;

		/// <summary>Instance variable <c>y</c> represents the point's
		/// y-coordinate.</summary>
		private int y;

		/// <value>Property <c>X</c> represents the point's x-coordinate.</value>
		public int X
		{
			get { return x; }
			set { x = value; }
		}

		/// <value>Property <c>Y</c> represents the point's y-coordinate.</value>
		public int Y
		{
			get { return y; }
			set { y = value; }
		}

		/// <summary>This constructor initializes the new Point to
		/// (0,0).</summary>
		public Point() : this(0,0) {}

		/// <summary>This constructor initializes the new Point to
		/// (<paramref name="xor"/>,<paramref name="yor"/>).</summary>
		/// <param><c>xor</c> is the new Point's x-coordinate.</param>
		/// <param><c>yor</c> is the new Point's y-coordinate.</param>
		public Point(int xor, int yor) {
			X = xor;
			Y = yor;
		}

		/// <summary>This method changes the point's location to
		/// the given coordinates. <see cref="Translate"/></summary>
		/// <param><c>xor</c> is the new x-coordinate.</param>
		/// <param><c>yor</c> is the new y-coordinate.</param>

		public void Move(int xor, int yor) {
			X = xor;
			Y = yor;
		}

		/// <summary>This method changes the point's location by
		/// the given x- and y-offsets.
		/// <example>For example:
		/// <code>
		/// Point p = new Point(3,5);
		/// p.Translate(-1,3);
		/// </code>
		/// results in <c>p</c>'s having the value (2,8).
		/// <see cref="Move"/></example>
		/// </summary>
		/// <param><c>xor</c> is the relative x-offset.</param>
		/// <param><c>yor</c> is the relative y-offset.</param>

		public void Translate(int xor, int yor) {
			X += xor;
			Y += yor;
		}

		/// <summary>This method determines whether two Points have the same
		/// location.</summary>
		/// <param><c>o</c> is the object to be compared to the current object.
		/// </param>
		/// <returns>True if the Points have the same location and they have
		/// the exact same type; otherwise, false.</returns>
		/// <seealso cref="operator=="/>
		/// <seealso cref="operator!="/>
		public override bool Equals(object o) {
			if (o == null) {
				return false;
			}

			if (this == o) {
				return true;
			}

			if (GetType() == o.GetType()) {
				Point p = (Point)o;
				return (X == p.X) && (Y == p.Y);
			}
			return false;
		}

		/// <summary>Report a point's location as a string.</summary>
		/// <returns>A string representing a point's location, in the form (x,y),
		/// without any leading, training, or embedded whitespace.</returns>
		public override string ToString() {
			return "(" + X + "," + Y + ")";
		}

		/// <summary>This operator determines whether two Points have the same
		/// location.</summary>
		/// <param><c>p1</c> is the first Point to be compared.</param>
		/// <param><c>p2</c> is the second Point to be compared.</param>
		/// <returns>True if the Points have the same location and they have
		/// the exact same type; otherwise, false.</returns>
		/// <seealso cref="Equals"/>
		/// <seealso cref="operator!="/>
		public static bool operator==(Point p1, Point p2) {
		if ((object)p1 == null || (object)p2 == null) {
		return false;
		}

		if (p1.GetType() == p2.GetType()) {
			return (p1.X == p2.X) && (p1.Y == p2.Y);
		}

		return false;
		}

		/// <summary>This operator determines whether two Points have the same
		/// location.</summary>
		/// <param><c>p1</c> is the first Point to be compared.</param>
		/// <param><c>p2</c> is the second Point to be compared.</param>
		/// <returns>True if the Points do not have the same location and the
		/// exact same type; otherwise, false.</returns>
		/// <seealso cref="Equals"/>
		/// <seealso cref="operator=="/>
		public static bool operator!=(Point p1, Point p2) {
			return !(p1 == p2);
		}

	}
}
```

### Resulting XML {#resulting-xml .Appendix3}

Here is the output produced by one documentation generator when given the source code for class Point, shown above:

```xml
<?xml version="1.0"?>
<doc>
	<assembly>
		<name>Point</name>
	</assembly>
	<members>
		<member name="T:Graphics.Point">
			<summary>Class <c>Point</c> models a point in a two-dimensional
			plane.
			</summary>
		</member>

		<member name="F:Graphics.Point.x">
			<summary>Instance variable <c>x</c> represents the point's
			x-coordinate.</summary>
		</member>

		<member name="F:Graphics.Point.y">
			<summary>Instance variable <c>y</c> represents the point's
			y-coordinate.</summary>
		</member>

		<member name="M:Graphics.Point.\#ctor">
			<summary>This constructor initializes the new Point to
			(0,0).</summary>
		</member>

		<member name="M:Graphics.Point.\#ctor(System.Int32,System.Int32)">
			<summary>This constructor initializes the new Point to
			(<paramref name="xor"/>,<paramref name="yor"/>).</summary>
			<param><c>xor</c> is the new Point's x-coordinate.</param>
			<param><c>yor</c> is the new Point's y-coordinate.</param>
		</member>

		<member name="M:Graphics.Point.Move(System.Int32,System.Int32)">
			<summary>This method changes the point's location to
			the given coordinates. <see cref="M:Graphics.Point.Translate(System.Int32,System.Int32)"/></summary>
			<param><c>xor</c> is the new x-coordinate.</param>
			<param><c>yor</c> is the new y-coordinate.</param>

		</member>

		<member
		name="M:Graphics.Point.Translate(System.Int32,System.Int32)">
			<summary>This method changes the point's location by
			the given x- and y-offsets.
			<example>For example:
			<code>
			Point p = new Point(3,5);
			p.Translate(-1,3);
			</code>
			results in <c>p</c>'s having the value (2,8).
			</example>
			<see cref="M:Graphics.Point.Move(System.Int32,System.Int32)"/></summary>
			<param><c>xor</c> is the relative x-offset.</param>
			<param><c>yor</c> is the relative y-offset.</param>

		</member>

		<member name="M:Graphics.Point.Equals(System.Object)">
			<summary>This method determines whether two Points have the same
			location.</summary>
			<param><c>o</c> is the object to be compared to the current
			object.
			</param>
			<returns>True if the Points have the same location and they have
			the exact same type; otherwise, false.</returns>
			<seealso
			cref="M:Graphics.Point.op\_Equality(Graphics.Point,Graphics.Point)"/>
			<seealso
			cref="M:Graphics.Point.op\_Inequality(Graphics.Point,Graphics.Point)"/>
		</member>

		<member name="M:Graphics.Point.ToString">
			<summary>Report a point's location as a string.</summary>
			<returns>A string representing a point's location, in the form
			(x,y),
			without any leading, training, or embedded whitespace.</returns>
		</member>

		<member
		name="M:Graphics.Point.op\_Equality(Graphics.Point,Graphics.Point)">
			<summary>This operator determines whether two Points have the
			same
			location.</summary>
			<param><c>p1</c> is the first Point to be compared.</param>
			<param><c>p2</c> is the second Point to be compared.</param>
			<returns>True if the Points have the same location and they have
			the exact same type; otherwise, false.</returns>
			<seealso cref="M:Graphics.Point.Equals(System.Object)"/>
			<seealso
			cref="M:Graphics.Point.op\_Inequality(Graphics.Point,Graphics.Point)"/>
		</member>

		<member
		name="M:Graphics.Point.op\_Inequality(Graphics.Point,Graphics.Point)">
			<summary>This operator determines whether two Points have the
			same
			location.</summary>
			<param><c>p1</c> is the first Point to be compared.</param>
			<param><c>p2</c> is the second Point to be compared.</param>
			<returns>True if the Points do not have the same location and
			the
			exact same type; otherwise, false.</returns>
			<seealso cref="M:Graphics.Point.Equals(System.Object)"/>
			<seealso
			cref="M:Graphics.Point.op\_Equality(Graphics.Point,Graphics.Point)"/>
		</member>

		<member name="M:Graphics.Point.Main">
			<summary>This is the entry point of the Point class testing
			program.
			<para>This program tests each method and operator, and
			is intended to be run after any non-trvial maintenance has
			been performed on the Point class.</para></summary>
		</member>

		<member name="P:Graphics.Point.X">
			<value>Property <c>X</c> represents the point's
			x-coordinate.</value>
		</member>

		<member name="P:Graphics.Point.Y">
			<value>Property <c>Y</c> represents the point's
			y-coordinate.</value>
		</member>
	</members>
</doc>
```

**End of informative text.**
