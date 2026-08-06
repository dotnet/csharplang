# Null Defaulting operator

C# provides advanced helper operators to handle Null-Conditional (`?.`) and Null-Coalescing (`??`) 
cases, but not a Null-Defaulting operator (`??.`) that could combine the fluent functionality
provided by these operators.

In the example `customer1.Address?.ZipCode?.State == "WA"` can be used to test a value,
the equivalent assignment can be applied using Null-Coalescing operator 
`customer1.Address = customer1.Address ?? new() 
        { 
            ZipCode = customer1.Address?.ZipCode ?? new() 
            { 
                State = "WA" 
            } 
        };`

This pattern cannot be subsequently used to assign `Zone = 98052`, if the `Address?.ZipCode?` has a value, it's also not possible to *subsequently* change the value of `ZipCode.Zone` since `Address.ZipCode` is a 
nullable value.
```C#
public struct ZipCode { public string? State; public int? Zone; }
public struct Address { public ZipCode? ZipCode; }
public struct Customer { public string? Name; public Address? Address; }

public class UnitTest
{
    [Fact]
    public void Test()
    {
        var customer1 = new Customer();
        // null conditional
        Assert.False(customer1.Address?.ZipCode?.State == "WA"); 

        // null coalescing assignment 
        customer1.Address = customer1.Address ?? new() 
        { 
            ZipCode = customer1.Address?.ZipCode ?? new() 
            { 
                State = "WA" 
            } 
        };
        Assert.NotNull(customer1.Address?.ZipCode?.State);

        customer1.Address = customer1.Address ?? new() 
        { 
            ZipCode = customer1.Address?.ZipCode ?? new() 
            { 
                Zone = 98052 
            } 
        };
        // zone not applied, since Address has a value
        Assert.Null(customer1.Address?.ZipCode?.Zone); 
    }
}
```
Adding copy constructors to `Address` and `ZipCode` allows fluent assignment,

```C#
    public ZipCode(ZipCode? copy) { this = copy ?? new(); }
    public Address(Address? copy) { this = copy ?? new(); }
...
    customer2.Address = new Address(customer2.Address) 
    { 
        ZipCode = new ZipCode(customer2.Address?.ZipCode) 
        { 
            State = "WA" 
        } 
    };
    customer2.Address = new Address(customer2.Address) 
    { 
        ZipCode = new ZipCode(customer2.Address?.ZipCode) 
        { 
            Zone = 98052 
        } 
    };
```

---

## Proposal

Add a new Null-defaulting operator `??.` that uses a copy constructor to mirror 
fluent handling of the condition `customer1.Address?.ZipCode?.State == "WA"` for assignment 
`customer1.Address??.ZipCode??.State = "WA";`.

It would also be worth considering whether structs should have a default copy constructor when 
they have a default constructor since they are needed for assignment of `Nullable<>` values, but 
not nullable classes.

## Benefit
Null-defaulting is useful to simplify code generation, but would be especially useful for data-binding 
where a shadow object would no longer be required.

