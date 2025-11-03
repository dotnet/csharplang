# Relaxed ordering for `partial` and `ref` modifiers

Champion issue: https://github.com/dotnet/csharplang/issues/946

## Summary
[summary]: #summary

- https://github.com/dotnet/csharplang/issues/946
- https://github.com/dotnet/csharplang/blob/main/meetings/2020/LDM-2020-09-09.md#champion-relax-ordering-constraints-around-ref-and-partial-modifiers-on-type-declarations

Allow the `partial` modifier to appear in any position in a modifier list on a type or member declaration.  
Allow the `ref` modifier to appear in any position in a modifier list on a struct declaration.

```cs
// All errors in this sample would be removed by adopting the proposal:
internal partial class C { }
partial internal class C { } // error

internal ref struct RS { }
ref internal struct RS { } // error

internal ref partial struct RS { }
internal partial ref struct RS { } // error
partial ref internal struct RS { } // error

partial class Program
{
    public partial Program();
    partial public Program() { } // error

    public partial int Prop { get; set; }
    partial public int Prop { get => field; set; } // error

    public partial void Method();
    partial public void Method() { } // error

    partial public event Action Event; // error
    public partial event Action Event { add { } remove { } }
}
```

## Motivation
[motivation]: #motivation

Most modifiers in the language can be written in any order. It feels arbitrary that `partial` and `ref` use a more stringent rule, and need to come at the end of the modifier list. This is especially cumbersome in the case of `ref partial` on structs, where even the opposite order `partial ref` is not permitted.

In order to avoid getting in the user's way needlessly, we should allow `partial` and `ref` modifiers to appear in any position in a modifier list, just like the other modifiers.

## Detailed design
[design]: #detailed-design

Note that some of the below grammars are from C# 7 and are missing more recent modifiers such as `required` and `file.`  
Since we have a precedent for contextual keywords `required` and `file` being modifiers, we believe we know how to parse `partial` as a modifier in any valid position it could appear in.

The method grammar [(§15.6.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1561-general) is updated as follows:

```diff
 method_modifiers
-    : method_modifier* 'partial'?
+    : method_modifier*
     ;

 method_modifier
     : ref_method_modifier
     | 'async'
     ;

 ref_method_modifier
     : 'new'
     | 'public'
     | 'protected'
     | 'internal'
     | 'private'
     | 'static'
     | 'virtual'
     | 'sealed'
     | 'override'
     | 'abstract'
     | 'extern'
+    | 'partial'
     | unsafe_modifier   // unsafe code support
     ;
```

The property grammar [(§15.7.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1571-general) is updated as follows:

```diff
property_declaration
-    : attributes? property_modifier* 'partial'? type member_name property_body
+    : attributes? property_modifier* type member_name property_body
    ;

 property_modifier
     : 'new'
     | 'public'
     | 'protected'
     | 'internal'
     | 'private'
     | 'static'
     | 'virtual'
     | 'sealed'
     | 'override'
     | 'abstract'
     | 'extern'
+    | 'partial'
     ;
```

The event grammar [(§15.8.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1581-general) is updated as follows:

```diff
 event_declaration
-    : attributes? event_modifier* 'partial'? 'event' type variable_declarators ';'
+    : attributes? event_modifier* 'event' type variable_declarators ';'
-    | attributes? event_modifier* 'partial'? 'event' type member_name
+    | attributes? event_modifier* 'event' type member_name
         '{' event_accessor_declarations '}'
     ;

 event_modifier
     : 'new'
     | 'public'
     | 'protected'
     | 'internal'
     | 'private'
     | 'static'
     | 'virtual'
     | 'sealed'
     | 'override'
     | 'abstract'
     | 'extern'
+    | 'partial'
     | unsafe_modifier   // unsafe code support
     ;
```

The indexer grammar [(§15.9.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1591-general) is updated as follows:

```diff
indexer_declaration
-    : attributes? indexer_modifier* 'partial'? indexer_declarator indexer_body
+    : attributes? indexer_modifier* indexer_declarator indexer_body
-    | attributes? indexer_modifier* 'partial'? ref_kind indexer_declarator ref_indexer_body
+    | attributes? indexer_modifier* ref_kind indexer_declarator ref_indexer_body
    ;

 indexer_modifier
     : 'new'
     | 'public'
     | 'protected'
     | 'internal'
     | 'private'
     | 'virtual'
     | 'sealed'
     | 'override'
     | 'abstract'
     | 'extern'
+    | 'partial'
     | unsafe_modifier   // unsafe code support
     ;
```

Instance constructor declaration syntax [(§15.11.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#15111-general) is updated as follows:

```diff
 constructor_declaration
-    : attributes? constructor_modifier* 'partial'? constructor_declarator constructor_body
+    : attributes? constructor_modifier* constructor_declarator constructor_body
     ;

 constructor_modifier
     : 'public'
     | 'protected'
     | 'internal'
     | 'private'
     | 'extern'
+    | 'partial'
     | unsafe_modifier   // unsafe code support
     ;
```

The classes grammar [(§15.2.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/classes.md#1521-general) is updated as follows:

```diff
 class_declaration
-    : attributes? class_modifier* 'partial'? 'class' identifier
+    : attributes? class_modifier* 'class' identifier
         type_parameter_list? class_base? type_parameter_constraints_clause*
         class_body ';'?
     ;

 class_modifier
     : 'new'
     | 'public'
     | 'protected'
     | 'internal'
     | 'private'
     | 'abstract'
     | 'sealed'
     | 'static'
+    | 'partial'
     | unsafe_modifier   // unsafe code support
     ;
```

The interfaces grammar [(§18.2.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/interfaces.md#1821-general) is updated as follows:

```diff
 interface_declaration
-    : attributes? interface_modifier* 'partial'? 'interface'
+    : attributes? interface_modifier* 'interface'
       identifier variant_type_parameter_list? interface_base?
       type_parameter_constraints_clause* interface_body ';'?
     ;

 interface_modifier
     : 'new'
     | 'public'
     | 'protected'
     | 'internal'
     | 'private'
+    | 'partial'
     | unsafe_modifier   // unsafe code support
     ;
```

The structs grammar [(§18.2.1)](https://github.com/dotnet/csharpstandard/blob/standard-v7/standard/structs.md#1621-general) is updated as follows:

```diff
 struct_declaration
-    : attributes? struct_modifier* 'ref'? 'partial'? 'struct'
+    : attributes? struct_modifier* 'struct'
       identifier type_parameter_list? struct_interfaces?
       type_parameter_constraints_clause* struct_body ';'?
     ;
```

```diff
 struct_modifier
     : 'new'
     | 'public'
     | 'protected'
     | 'internal'
     | 'private'
     | 'readonly'
+    | 'ref'
+    | 'partial'
     | unsafe_modifier   // unsafe code support
     ;
```

## Drawbacks
[drawbacks]: #drawbacks

N/A

## Alternatives
[alternatives]: #alternatives

We could decide that we don't want to change this. In that case, the next time we spec allowing a new declaration kind to be `partial` or `ref`, we won't have to spend time thinking about whether now is the time to remove the ordering restriction.

## Open questions
[open]: #open-questions

N/A
