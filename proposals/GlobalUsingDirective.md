# Global Using Directive

Syntax for a using directive is extended with an optional `global` keyword that can precede the `using` keyword:
```antlr
using_directive
  : 'global'? 'using' ('static' | name_equals)? name ';'
  ;
```

- Global Using Directives are allowed only on the Compilation Unit level (cannot be used inside a namespace declaration).
- Global Using directives, if any, must precede any non-global using directives. 
- The scope of a Global Using Directive extends over the namespace member declarations and non-global using directives of all compilation units within the program.
The scope of a Global Using Directive specifically does not include other Global Using Directives. Thus, peer Global Using Directives or those from a different
compilation unit do not affect each other, and the order in which they are written is insignificant.
