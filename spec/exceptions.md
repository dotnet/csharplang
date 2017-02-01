# Exceptions

Exceptions in C# provide a structured, uniform, and type-safe way of handling both system level and application level error conditions. The exception mechanism in C# is quite similar to that of C++, with a few important differences:

*  In C#, all exceptions must be represented by an instance of a class type derived from `System.Exception`. In C++, any value of any type can be used to represent an exception.
*  In C#, a finally block ([The try statement](statements.md#the-try-statement)) can be used to write termination code that executes in both normal execution and exceptional conditions. Such code is difficult to write in C++ without duplicating code.
*  In C#, system-level exceptions such as overflow, divide-by-zero, and null dereferences have well defined exception classes and are on a par with application-level error conditions.

## Causes of exceptions

Exception can be thrown in two different ways.

*  A `throw` statement ([The throw statement](statements.md#the-throw-statement)) throws an exception immediately and unconditionally. Control never reaches the statement immediately following the `throw`.
*  Certain exceptional conditions that arise during the processing of C# statements and expression cause an exception in certain circumstances when the operation cannot be completed normally. For example, an integer division operation ([Division operator](expressions.md#division-operator)) throws a `System.DivideByZeroException` if the denominator is zero. See [Common Exception Classes](exceptions.md#common-exception-classes) for a list of the various exceptions that can occur in this way.

## The System.Exception class

The `System.Exception` class is the base type of all exceptions. This class has a few notable properties that all exceptions share:

*  `Message` is a read-only property of type `string` that contains a human-readable description of the reason for the exception.
*  `InnerException` is a read-only property of type `Exception`. If its value is non-null, it refers to the exception that caused the current exception—that is, the current exception was raised in a catch block handling the `InnerException`. Otherwise, its value is null, indicating that this exception was not caused by another exception. The number of exception objects chained together in this manner can be arbitrary.

The value of these properties can be specified in calls to the instance constructor for `System.Exception`.

## How exceptions are handled

Exceptions are handled by a `try` statement ([The try statement](statements.md#the-try-statement)).

When an exception occurs, the system searches for the nearest `catch` clause that can handle the exception, as determined by the run-time type of the exception. First, the current method is searched for a lexically enclosing `try` statement, and the associated catch clauses of the try statement are considered in order. If that fails, the method that called the current method is searched for a lexically enclosing `try` statement that encloses the point of the call to the current method. This search continues until a `catch` clause is found that can handle the current exception, by naming an exception class that is of the same class, or a base class, of the run-time type of the exception being thrown. A `catch` clause that doesn't name an exception class can handle any exception.

Once a matching catch clause is found, the system prepares to transfer control to the first statement of the catch clause. Before execution of the catch clause begins, the system first executes, in order, any `finally` clauses that were associated with try statements more nested that than the one that caught the exception.

If no matching catch clause is found, one of two things occurs:

*  If the search for a matching catch clause reaches a static constructor ([Static constructors](classes.md#static-constructors)) or static field initializer, then a `System.TypeInitializationException` is thrown at the point that triggered the invocation of the static constructor. The inner exception of the `System.TypeInitializationException` contains the exception that was originally thrown.
*  If the search for matching catch clauses reaches the code that initially started the thread, then execution of the thread is terminated. The impact of such termination is implementation-defined.

Exceptions that occur during destructor execution are worth special mention. If an exception occurs during destructor execution, and that exception is not caught, then the execution of that destructor is terminated and the destructor of the base class (if any) is called. If there is no base class (as in the case of the `object` type) or if there is no base class destructor, then the exception is discarded.

## Common Exception Classes

The following exceptions are thrown by certain C# operations.

|                                      |                |
|--------------------------------------|----------------|
| `System.ArithmeticException`         | A base class for exceptions that occur during arithmetic operations, such as `System.DivideByZeroException` and `System.OverflowException`. | 
| `System.ArrayTypeMismatchException`  | Thrown when a store into an array fails because the actual type of the stored element is incompatible with the actual type of the array. | 
| `System.DivideByZeroException`       | Thrown when an attempt to divide an integral value by zero occurs. | 
| `System.IndexOutOfRangeException`    | Thrown when an attempt to index an array via an index that is less than zero or outside the bounds of the array. | 
| `System.InvalidCastException`        | Thrown when an explicit conversion from a base type or interface to a derived type fails at run time. | 
| `System.NullReferenceException`      | Thrown when a `null` reference is used in a way that causes the referenced object to be required. | 
| `System.OutOfMemoryException`        | Thrown when an attempt to allocate memory (via `new`) fails. | 
| `System.OverflowException`           | Thrown when an arithmetic operation in a `checked` context overflows. | 
| `System.StackOverflowException`      | Thrown when the execution stack is exhausted by having too many pending method calls; typically indicative of very deep or unbounded recursion. | 
| `System.TypeInitializationException` | Thrown when a static constructor throws an exception, and no `catch` clauses exists to catch it. | 
