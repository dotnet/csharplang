# Unsafe Design Priorities

**Author:** [Richard Lander](https://github.com/richlander)

Safety is the primary value proposition of C#. This makes it a critical aspect to improve constantly but rarely disrupt. We're on the verge of deploying the first significant and disruptive change to safety in about 25 years. A lot has changed during that time. 

> **Defining goal:** C# code, after adopting new memory safety rules, meets the guarantees that we want to provide. The rules were derived from analysis of our own CVEs and are in service of reducing and removing the possiblity of writing those CVEs in C#.

It's a good reminder that the initial safety design came out of nothing. There was not much industry context to pull from. Most of what came out of that design exercise 25+ years ago was sound and remains part of our architecture. Ideas like strong name assemblies perhaps made more sense then but are no longer relevant. This time around, we have many more examples to look at, with Rust and Swift being the most obvious. It's a much easier task this time around because of that, enabling us to define a safety model that will be meaningful and durable for many years into the future.

It's important to rank our macro priorities to help inform the micro design choices that follow.

Priorities (stack ranked):

- **Descriptive:** The safety state of each part of the program is easy to apply (for production) and determine (for consumption); the language itself enables effective auditing workflows.
- **Simple:** The overall model is as simple as we can make it and the markings we apply to program members are as minimal as we can make them and can be understood without additional context or inference.
- **Enforcement:** C# is primarily deployed as binaries, which makes build errors the only workable signal for adoption of the new memory safety model.
- **Industry-aligned:** Developers coming from other languages will implicitly understand our approach, while C# developers can read Rust and Swift documentation (in addition to our own) to better understand the intent and semantics of new/updated C# language syntax.
- **Compatible:** Existing code needs to change to conform to the new rules, however, we will limit change where we can.


A consequence of these priorities is that our design will be familiar and approachable to AI agents that self-drive auditing and application of the model. They will be able to use a combination of raw text, sed/awk/grep, and language tools, whatever fits their preference. The strong enforcement model will handcuff the agents such that our users will have high confidence in the results, which is critical given the C# bias to binary distribution. These priorities result in a model that can be deployed and trusted at scale.

