Building a large internal repo with `this.field/value` style of breaking changes had 100+ errors. Hit every variation of the diagnostic:
- `field` as field
- `value` as field
- `this.that.value` in accessor

Example:

```csharp
public int Value
{
  get { return this.value; }
  set
  {
    this.value = value;
    this.valuePtr = &value;
  }
}
```

Random repos found with GH queries
- [this.value](https://github.com/search?q=%22this.value%22+language%3AC%23&type=repositories&l=C%23) 209K results, 20% hit rate of real bug through 2 pages
    - https://github.com/DotNetAnalyzers/StyleCopAnalyzers
    - https://github.com/VodeoGames/VodeoECS
    - https://github.com/NeoforceControls/Neoforce-Mono
    - https://github.com/askeladdk/aiedit
    - https://github.com/YuhangGe/DLX-System
    - https://github.com/GameDiffs/TheForest
    - https://github.com/alexsteb/GuitarPro-to-Midi
- [this.that.value](https://github.com/search?q=%2Fthis%5C.%5Ba-z%5D%2B%5C.value%2F+language%3AC%23&type=code) 106K results, 8% hit rate of real bug through 4 pages
    - https://github.com/Real-Serious-Games/Unity-Editor-UI
    - https://github.com/VeeamHub/SuperEdit
    - https://github.com/moto2002/mobahero_src
- [this.field](https://github.com/search?q=%22this.field%22+language%3AC%23&type=code) 41K results, 8% hit rate of real bugs through 3 pages
    - https://github.com/HaloMods/Halo1AnimationEditor
    - https://github.com/Team-COCONUT/Minotaur
    - https://github.com/moto2002/mobahero_src
    - https://github.com/Toskyuu/TPW
    - https://github.com/graehu/SON

I only dug in the first 4-5 pages for each query as I felt it was representative at that point. Possible I'm wrong but I doubt the numbers would change if we dug any deeper.

More GH queries:

- [\_field](https://github.com/search?q=%2F%28%5E%7C%5CW%29_field%28%24%7C%5CW%29%2F+language%3AC%23&type=code) as a field name. 6K results, near 100% hit rate
- [\_value](https://github.com/search?q=%2F%28%5E%7C%5CW%29_value%28%24%7C%5CW%29%2F+language%3AC%23&type=code) as a field name. 137K results, near 100% hit rate

Largest intentional breaking change we've in last ~5 years was lambda inference and overload changes:
    - Had a very long lead and got plenty of preview feedback about it. 
    - In total we had ~20-30 bugs filed against it.-
    - That is number of customers who did not self correct. Let's assume for sake of argument that total number of users that hit this problem is 10x reported. So 200-300

My conclusions from data:

1. Generated code is a challenge for our fixer
2. `field` and `_field` are uncommon but not rare field names
    a. The use of `field/_field` is a style decision
    b. Breaking change is about the style in which it is declared and accessed: `this.field =` vs. `field =`
3. `value` and `_value` are common field names
    a. The use of `value/_value` is a style decision
    b. Breaking change is about the style in which it is declared and accessed: `this.value` vs. `value =`
4. Ratio of field:value virtually every query is 1:8-1:12
5. Breaks on `this.value` or `this.field` are not viable 
    a. In the range of 20K and 10K impacted GH samples
    b. That is roughly 2 orders of magnitude greater than the upper bound of our highest breaking change
6. It is very hard to quantify what the breaks on naked field / value would be. 
    a. Hard to construct a GH query to narrow down to uses of field / value without keywords. 
    b. Metadata queries can't distinguish between this.field and simply field in an accessor 
 
