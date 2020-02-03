# Standard library

## General

A conforming C# implementation shall provide a minimum set of types having specific semantics. These types and their members are listed here, in alphabetical order by namespace and type. For a formal definition of these types and their members, refer to ISO/IEC 23271:2012 *Common Language Infrastructure (CLI), Partition IV; Base Class Library (BCL), Extended Numerics Library, and Extended Array Library*, which are included by reference in this specification.

> **This text is informative.**
>
> The standard library is intended to be the minimum set of types and members required by a conforming C# implementation. As such, it contains only those members that are explicitly required by the C# language specification.
>
>It is expected that a conforming C# implementation will supply a significantly more extensive library that enables useful programs to be written. For example, a conforming implementation might extend this library by
>
> - Adding namespaces.
> - Adding types.
> - Adding members to non-interface types.
> - Adding intervening base classes or interfaces.
> - Having struct and class types implement additional interfaces.
> - Adding attributes (other than the ConditionalAttribute) to existing types and members.
>
> **End of informative text.**

## Standard Library Types defined in ISO/IEC 23271

```csharp

namespace System
{
    public class ArgumentException : SystemException
    {
        public ArgumentException ();
        public ArgumentException (string message);
        public ArgumentException (string message, Exception innerException);
    }
}

namespace System
{
    public delegate void Action ();
}

namespace System
{
    public class ArithmeticException : Exception
    {
        public ArithmeticException ();
        public ArithmeticException (string message);
        public ArithmeticException (string message, Exception innerException);
    }
}

namespace System
{
    public abstract class Array : IList, ICollection, IEnumerable
    {
        public int Length { get; }
        public int Rank { get; }
        public int GetLength (int dimension);
    }
}

namespace System
{
    public class ArrayTypeMismatchException : Exception
    {
        public ArrayTypeMismatchException ();
        public ArrayTypeMismatchException (string message);
        public ArrayTypeMismatchException (string message,
            Exception innerException);
    }
}

namespace System
{
    [AttributeUsageAttribute (AttributeTargets.All, Inherited = true,
        AllowMultiple = false)]
    public abstract class Attribute
    {
        protected Attribute ();
    }
}

namespace System
{
    public enum AttributeTargets
    {
        Assembly = 0x1,
        Module = 0x2,
        Class = 0x4,
        Struct = 0x8,
        Enum = 0x10,
        Constructor = 0x20,
        Method = 0x40,
        Property = 0x80,
        Field = 0x100,
        Event = 0x200,
        Interface = 0x400,
        Parameter = 0x800,
        Delegate = 0x1000,
        ReturnValue = 0x2000,
        GenericParameter = 0x4000,
        All = 0x7FFF
    }
}

namespace System
{
    [AttributeUsageAttribute (AttributeTargets.Class, Inherited = true)]
    public sealed class AttributeUsageAttribute : Attribute
    {
        public AttributeUsageAttribute (AttributeTargets validOn);
        public bool AllowMultiple { get; set; }
        public bool Inherited { get; set; }
        public AttributeTargets ValidOn { get; }
    }
}

namespace System
{
    public struct Boolean { }
}

namespace System
{
    public struct Byte { }
}

namespace System
{
    public struct Char { }
}

namespace System
{
    public struct Decimal { }
}

namespace System
{
    public abstract class Delegate { }
}

namespace System
{
    public class DivideByZeroException : ArithmeticException
    {
        public DivideByZeroException ();
        public DivideByZeroException (string message);
        public DivideByZeroException (string message, Exception innerException);
    }
}

namespace System
{
    public struct Double { }
}

namespace System
{
    public abstract class Enum : ValueType
    {
        protected Enum ();
    }
}

namespace System
{
    public class Exception
    {
        public Exception ();
        public Exception (string message);
        public Exception (string message, Exception innerException);
        public sealed Exception InnerException { get; }
        public virtual string Message { get; }
    }
}

namespace System
{
    public class GC { }
}

namespace System
{
    public interface IDisposable
    {
        public void Dispose ();
    }
}

namespace System
{
    public sealed class IndexOutOfRangeException : Exception
    {
        public IndexOutOfRangeException ();
        public IndexOutOfRangeException (string message);
        public IndexOutOfRangeException (string message,
            Exception innerException);
    }
}

namespace System
{
    public struct Int16 { }
}

namespace System
{
    public struct Int32 { }
}

namespace System
{
    public struct Int64 { }
}

namespace System
{
    public struct IntPtr { }
}

namespace System.Runtime.CompilerServices
{
    public sealed class IndexerNameAttribute : Attribute
    {
        public IndexerNameAttribute (String indexerName);
    }
}

namespace System.Collections.Generic
{
    public interface IReadOnlyCollection<out T> : IEnumerable<T>
    {
        int Count { get; }
    }
}

namespace System.Collections.Generic
{
    public interface IReadOnlyList<out T> : IReadOnlyCollection<T>
    {
        T this [int index] { get; }
    }
}

namespace System
{
    public class InvalidCastException : Exception
    {
        public InvalidCastException ();
        public InvalidCastException (string message);
        public InvalidCastException (string message, Exception innerException);
    }
}

namespace System
{
    public class InvalidOperationException : Exception
    {
        public InvalidOperationException ();
        public InvalidOperationException (string message);
        public InvalidOperationException (string message,
            Exception innerException);
    }
}

namespace System.Reflection
{
    public abstract class MemberInfo
    {
        protected MemberInfo ();
    }
}

namespace System
{
    public class NotSupportedException : Exception
    {
        public NotSupportedException ();
        public NotSupportedException (string message);
        public NotSupportedException (string message, Exception innerException);
    }
}

namespace System
{
    public struct Nullable<T>
    {
        public bool HasValue { get; }
        public T Value { get; }
    }
}

namespace System
{
    public class NullReferenceException : Exception
    {
        public NullReferenceException ();
        public NullReferenceException (string message);
        public NullReferenceException (string message, Exception innerException);
    }
}

namespace System
{
    public class Object
    {
        public Object ();
        ~Object ();
        public virtual bool Equals (object obj);
        public virtual int GetHashCode ();
        public Type GetType ();
        public virtual string ToString ();
    }
}

namespace System
{
    [AttributeUsageAttribute (AttributeTargets.Class |
        AttributeTargets.Struct |
        AttributeTargets.Enum | AttributeTargets.Interface |
        AttributeTargets.Constructor | AttributeTargets.Method |
        AttributeTargets.Property | AttributeTargets.Field |
        AttributeTargets.Event | AttributeTargets.Delegate,
        Inherited = false)]

    public sealed class ObsoleteAttribute : Attribute
    {
        public ObsoleteAttribute ();
        public ObsoleteAttribute (string message);
        public ObsoleteAttribute (string message, bool error);
        public bool IsError { get; }
        public string Message { get; }
    }
}

namespace System
{
    public class OutOfMemoryException : Exception
    {
        public OutOfMemoryException ();
        public OutOfMemoryException (string message);
        public OutOfMemoryException (string message, Exception innerException);
    }
}

namespace System
{
    public class OverflowException : ArithmeticException
    {
        public OverflowException ();
        public OverflowException (string message);
        public OverflowException (string message, Exception innerException);
    }
}

namespace System
{
    public struct SByte { }
}

namespace System
{
    public struct Single { }
}

namespace System
{
    public sealed class StackOverflowException : Exception
    {
        public StackOverflowException ();
        public StackOverflowException (string message);
        public StackOverflowException (string message, Exception innerException);
    }
}

namespace System
{
    public sealed class String : IEnumerable<Char>, IEnumerable
    {
        public int Length { get; }
        public char this [int index] { get; }
    }
}

namespace System
{
    public abstract class Type : MemberInfo { }
}

namespace System
{
    public sealed class TypeInitializationException : Exception
    {
        public TypeInitializationException (string fullTypeName,
            Exception innerException);
    }
}

namespace System
{
    public struct UInt16 { }
}

namespace System
{
    public struct UInt32 { }
}

namespace System
{
    public struct UInt64 { }
}

namespace System
{
    public struct UIntPtr { }
}

namespace System
{
    public abstract class ValueType
    {
        protected ValueType ();
    }
}

namespace System.Collections
{
    public interface ICollection : IEnumerable
    {
        public int Count { get; }
        public bool IsSynchronized { get; }
        public object SyncRoot { get; }
        public void CopyTo (Array array, int index);
    }
}

namespace System.Collections
{
    public interface IEnumerable
    {
        public IEnumerator GetEnumerator ();
    }
}

namespace System.Collections
{
    public interface IEnumerator
    {
        public object Current { get; }
        public bool MoveNext ();
        public void Reset ();
    }
}

namespace System.Collections
{
    public interface IList : ICollection, IEnumerable
    {
        public bool IsFixedSize { get; }
        public bool IsReadOnly { get; }
        public object this [int index] { get; set; }
        public int Add (object value);
        public void Clear ();
        public bool Contains (object value);
        public int IndexOf (object value);
        public void Insert (int index, object value);
        public void Remove (object value);
        public void RemoveAt (int index);
    }
}

namespace System.Collections.Generic
{
    public interface ICollection<T> : IEnumerable<T>
    {
        public int Count { get; }
        public bool IsReadOnly { get; }
        public void Add (T item);
        public void Clear ();
        public bool Contains (T item);
        public void CopyTo (T[] array, int arrayIndex);
        public bool Remove (T item);
    }
}

namespace System.Collections.Generic
{
    public interface IEnumerable<T> : IEnumerable
    {
        public IEnumerator<T>
            GetEnumerator ();
    }
}

namespace System.Collections.Generic
{
    public interface IEnumerator<T> : IDisposable, IEnumerator
    {
        public T Current { get; }
    }
}

namespace System.Collections.Generic
{
    public interface IList<T> : ICollection<T>
    {
        public T this [int index] { get; set; }
        public int IndexOf (T item);
        public void Insert (int index, T item);
        public void RemoveAt (int index);
    }
}

namespace System.diagnostics
{
    [AttributeUsageAttribute (AttributeTargets.Method |
        AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ConditionalAttribute : Attribute
    {
        public ConditionalAttribute (string conditionString);
        public string ConditionString { get; }
    }
}

namespace System.Threading
{
    public static class Monitor
    {
        public static void Enter (object obj);
        public static void Exit (object obj);
    }
}

```

## Standard Library Types not defined in ISO/IEC 23271:2012

The following types, including the members listed, must be defined in a conforming standard library. (These types might be defined in a future edition of ISO/IEC 23271.) It is expected that many of these types will have more members available than are listed.

A conforming implementation may provide `Task.GetAwaiter()` and `Task<T>.GetAwaiter()` as extension methods.

```csharp

namespace System.Runtime.CompilerServices
{
    [AttributeUsage (AttributeTargets.Parameter, Inherited = false)]
    public sealed class CallerFilePathAttribute : Attribute
    {
        public CallerFilePathAttribute () { }
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage (AttributeTargets.Parameter, Inherited = false)]
    public sealed class CallerLineNumberAttribute : Attribute
    {
        public CallerLineNumberAttribute () { }
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage (AttributeTargets.Parameter, Inherited = false)]
    public sealed class CallerMemberNameAttribute : Attribute
    {
        public CallerMemberNameAttribute () { }
    }
}

namespace System.Linq.Expressions
{
    public sealed class Expression<TDelegate>
    {
        // See Section 12.7.3 for details on what
        // Delegate types (TDelegate) must be supported,
        // and which may be omitted.
        public TDelegate Compile ();
    }
}

namespace System.Runtime.CompilerServices
{
    public interface INotifyCompletion
    {
        void OnCompleted (Action continuation);
    }
}

namespace System.Runtime.CompilerServices
{
    public interface ICriticalNotifyCompletion : INotifyCompletion
    {
        void UnsafeOnCompleted (Action continuation);
    }
}

namespace System.Threading.Tasks
{
    public class Task
    {
        public System.Runtime.CompilerServices.TaskAwaiter GetAwaiter ();
    }
}

namespace System.Threading.Tasks
{
    public class Task<TResult> : System.Threading.Tasks.Task
    {
        public new System.Runtime.CompilerServices.TaskAwaiter<T>
            GetAwaiter ();
    }
}

namespace System.Runtime.CompilerServices
{
    public struct TaskAwaiter : ICriticalNotifyCompletion,
        INotifyCompletion
        {
            public bool IsCompleted { get; }
            public void GetResult ();
        }
}

namespace System.Runtime.CompilerServices
{
    public struct TaskAwaiter<T> : ICriticalNotifyCompletion,
        INotifyCompletion
        {
            public bool IsCompleted { get; }
            public T GetResult ();
        }
}

```