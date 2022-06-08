**INNATE PROPERTY DELEGATES**

[•]	**Proposed**

	  Prototype: Complete
    
	  Implementation: In Progress
    
	  Specification: Not Started
    

**Summary**
It is a heavily used pattern for users to add a property that would be an auto-property other than their need to set the value on construction (where a reference to this is needed), provide a lazy value on first retrieval, validate new values, or launch events such as refreshing a Blazor state, refreshing a Xamarin/MAUI bound property, or other handling after a property is changed.  Property synchronization between say client and server is another complex problem that many encounter.  I have made these experiences easier using observable data pattern templates that have said functionality, but I believe it would be relatively simple for c# to offer these features offering significant quality of life improvements to numerous coders without significant drawbacks.

**Motivation**
In addition to the use case specified above, this would act similarly to the advent of auto-properties in allowing users to achieve some very common behavior with far less code.  In my own most complex use-case where I synchronize entire structure sets between the client and server (not using GraphQL but a system inspired by it) using reflection heavily to generate structure mappings and automatically set up synchronization, instead of needing to use my own observable classes to wrap items: SessionValue<int>, SessionValueRef<SomeData>, etc., I could use int, string, and “SomeData” properties that I interact with via reflection to observe them and run my synchronization engine via signal with the other client(s).  It would also provide a mechanism for MAUI to use a very simplified model binding system that involves significant saving in source code needed.  Systems like Entity Framework could simplify change tracking using these features as well -- tracking items changed rather than comparing current state versus original state likely leading to optimizations.

  **Sample Situations:**

    **Sample Code WITHOUT the Feature**
  
      using System;
      using System.Collections.Generic;
      using System.Linq;
      using System.Text;
      using System.Threading.Tasks;

      namespace SampleCode;


      // Simple auto property
      public sealed class Sample1
      {
        public int age { get; set; }
      }


      // Simple Scenario with changing / changed handlers
      public delegate void AgeChangingHandler(object sender, Tuple<int, int> ages);
      public delegate void AgeChangeHandler(object sender, int age);



      public sealed class Sample2
      {
        public event AgeChangingHandler? onChangingAge;
        public event AgeChangeHandler? onChangedAge;

        private int _age = 0;
        public int age
        {
          get => this._age;
          set
          {
            if (this._age != value)
            {
              this.onChangingAge?.Invoke(this, new Tuple<int, int>(this._age, value));
              this._age = value;
              this.onChangedAge?.Invoke(this, this._age);
            }
          }
        }
      }



      public class PersonData
      {
        public PersonData(Sample3 _) { }
      };

      // Simple scenario with construction / changing / changed handlers for a class property
      public delegate void PersonChangingHandler(object sender, Tuple<PersonData, PersonData> ages);
      public delegate void PersonChangeHandler(object sender, PersonData age);

      public sealed class Sample3
      {
        public event PersonChangingHandler? onChangingPerson;
        public event PersonChangeHandler? onChangedPerson;

        private PersonData _person;
        // private PersonData _person = PersonData(this); can't use this at this point
        public PersonData person
        {
          get => this._person;
          set
          {
            if (
              !PersonData.Equals(
                this._person,
                value))
            {
              this.onChangingPerson?.Invoke(this, new Tuple<PersonData, PersonData>(this._person, value));
              this._person = value;
              this.onChangedPerson?.Invoke(this, this._person);
            }
          }
        }

        public Sample3()
        {
          this._person = new PersonData(this);
        }
      }




      // Scenario where my syncrhonization system uses automation
      public sealed class Sample4
      {
        public SessionValue<int> age { get; } = new SessionValue<int>(0);
        // In this case, SessionValueRef template class requires IComparable or IStructuralEquatable implementation
        // most constructors take just <PersonData> but for lazy construction, the <PersonData, Sample4> option will
        // be called by the system initializers after all objects are constructed so that PersonData can be instantiated with
        // m which is effectively THIS for Sample4.  This could be done in the constructor but since the rest of the class code
        // is in-line it is nice to keep this inline for consistency to anyone reviewing the code.
        public SessionValueRef<PersonData> person { get; } = new SessionValueRef<PersonData, Sample4>(
          m =>
            new PersonData(m));
        // this class runs an ObservableCollection with numerous helpers to make synchronization work, but
        // I could use ObservableCollection<SomeListItem> here and achieve most of the same functionality with
        // reflection -- so there is less need for such functionality
        public SessionList<SomeListItem> items { get; } = new SessionList<SomeListItem>();

        public void PossibleLogicStuff()
        {
          this.age.valueChanged += this.onAgeChanged;
        }

        public void Deinitialize()
        {
          this.age.valueChanged -= this.onAgeChanged;
        }

        private void onAgeChanged(object sender, int oldAge, int newAge)
        {
          // do whatever maybe changing allowed content displayed

          // this.InvokeAsync(this.StateHasChanged);
          // or this.OnPropertyChanged(BinableAgeProperty.PropertyName);
          // etc.
        }
      }

  
    **Sample Code WITH the Feature (theoretical)**

      using System;
      using System.Collections.Generic;
      using System.Linq;
      using System.Text;
      using System.Threading.Tasks;

      namespace SampleTheoreticalCode;


      // Simple auto property
      public sealed class SampleTheoretical1
      {
        // the implementation of property innate delegates could be that if they are used, IL code is generated to support them,
        // but otherwise, IL code is generated exactly as it used to be.
        // so this simple auto property would generate the same code unless age.changed += for example was referenced.
        public int age { get; set; }

        // another option would be to use 'property' as a key-word to determine that age is a property.
        public property int age;

        // for properties that are retrieval only, readonly could be added
        public readonly property int age => this.CalculateAge();
      }



      // Simple Scenario with changing / changed handlers
      public sealed class SampleTheoretical2
      {
        public event IntChangingHandler? onChangingAge; // assuming each innate system type would have it's own property handlers probably object, args with the values..
        public event IntChangedHandler? onChangedAge;

        public property int age = 0; // equivalent to public int age { get; set; } = 0;

        // a consideration to handling events inline might be feasible
        public property int age = 0
          changing => this.onChangingAge?.Invoke()
          changed => this.onChangedAge?.Invoke(); // equivalent to public int age { get; set; } = 0;

        public SampleTheoretical2()
        {
          // or if not allowed inline, they can be set up in the constructor or via reflection
          this.age.changing += (i1, i2) => this.onChangingAge(i1, i2);
          this.age.changed += (i1, i2) => this.onChangedAge(i1, i2);
        }
      }



      public class PersonData : IStructuralEquatable
      {
        public PersonData(SampleTheoretical3 _) { }
      };

      // Simple scenario with construction / changing / changed handlers for a class property
      public sealed class SampleTheoretical3
      {
        public property PersonData person ==> new PersonData(this)  //  <-- something like '==>' to say invoke this after constructor so THIS can be used.
          changing => this.ValidateUser,
          changed => this.InvokeAsync(this.StateHasChanged);
  
        private bool ValidateUser(object sender, PersonData? old, PersonData newPerson) =>
          newPerson.hasSomerights;
        
        public SampleTheoretical3()
        {
          // this._person = new PersonData(this); // not needed
        }
      }




      // With previous changes, SampleTheoretical4 can be fully automated by my session mapping / modeling code via reflection
      // I will skip it


      // Instead I will include a new sampel #5 that shows code reduction in MAUI
      // I have already achieved this using base model classes that call static bindable property creations for me
      // but the average user uses code simple to the following
      public sealed class SampleTheoretical5Model : INotifyPropertyChanged
      {
        private int _age = 0;
        public int age
        {
          get => this._age;
          set
          {
            if (this._age != value)
            {
              this._age = value;
              this.OnPropertyChanged(AgeProperty.PropertyName);
            }
          }
        }

        public static readonly BindableProperty AgeProperty =
          BindableProperty.CreateAttached(
            nameof(age),
            typeof(string),
            typeof(SampleTheoretical5Model),
            0);

      }
      // this pattern leads to very wordy models in xamarin / MAUI
      // I have gotten around it by using a base model that uses reflection to make most of the happen and my model is just:
      public sealed class SampleTheoretical5ModelB : MyModelHelper
      {
        // IntegerProperty is bindable as an int or as string as it will convert for the form
        // MyModelHelper uses reflection to maintain static list of BindableProperties it creates once so Xamarin /MAUI can work
        // and implements INotifyPropertyChanged and automatically fires on property changes.
        // the only time this is problematic is with two way binding where I have to specify
        // {Bindable name.text}  so it assigns the string to an accessor that will take that since
        // unlike c++, c# does not provide an easy way to override value assignments
        public IntegerProperty age { get; } = CreateIntegerProperty(0);
      }

      // If we have innate property delegates, this could be made a far more convenient feature of MAUI as follows:
      public sealed class SampleTheoretical5ModelC : MAUIModel // standard theoretical MAUI base class
      {
        [AutoBind] // theoretical MAUI attribute to tell MAUI Model class to auto wire the property to INotifyPropertyChanged
        public property int age = 0;
      }

      // the MAUIModel could then check for AutoBind on members, and wire up .changed on each property thus decorated
      // and then fire OnPropertyChanged(...) with the name of each of those properties on changes.  This would reduce typical MAUI
      // model by hundreds of lines.
  
  


**Detailed design**
There are two manners in which it has occurred to me to implement this feature.  Either require a keyword such as ‘property’ or ‘bindable’ to the property to add in the bindable events, or base the existence of the extra code for delegates if there is any code using them.  Link-time code inclusion / omission creates problems for users that dynamically load modules or generate code, so I think it would be less problematic to use a keyword.
A current property:  int age { get; set; }
Could become:  property int age;
                    Or: bindable int age;
(I am suggesting remove the requirement for { get; set; } since the keyword differentiates it from a field already.)
I will use the word property in sample code, but bindable, or possibly another more appropriate keyword would work as well.
Once that property is thus decorated, it will have innate delegates:
Possible delegates / names could be initialized, reading, changing, changed.  Any such property that is a class should be required to implement IComparable or IStructuralEquality so that changes can be detected by the underlying .net core code.
These could be bound in typical fashion from a method:
		property int age { get; set; }

		public void SomeLogicCode()
                      {
                           this.age.changing += this.ValidateAgeRange(…
		                       this.age.changed += this.UpdateContentSuitability(…

I would allow assignment of the value during construction as is now the case
  
    		property int age = 0;
  
and also support lazy construction with ‘this’ after construction is finished:
  
	    	property PersonData person ==> new PersonData(this);
  
allowing combinations:
  
	    	property int age = 0 ==> this.CalculateAge();
  
The property could also, optionally support simpler inline-use of the delegates with something like this: (all optional uses)
  
	    	property int age = 0 ==> this.CalculateAge()
                          initialized => this.UpdateContentSuitability(),
                          changing => this.ValidateAgeRange(…),
                          changed => this.UpdateContentSuitability(),
                          reading => this.CheckForAgeChanges();

If the property is declared with the readonly keyword, then changing / changed should be omitted from the available delegates.
  
        [AutoBind] // <--suggest a AutoBindAttribute for properties that is used by component models
                   //    such as in MAUI where it causes binding to the property to automatically update with changes

**Drawbacks**
Adding this as an option for ALL properties would have the downside of adding extra code / slowing performance unless the IL avoided generating code if the delegates were not used.  IL might omit code later used by reflection when not desired.  Hence, I was suggesting a special property such as ‘property’ or ‘bindable’ or similar be used.  I think this would also avoid confusing new users.
                         
**Alternatives**
I currently use observable template value holders that achieve the same functionality with little effort, but initial development was complex – this would bring the same ease of use to the general masses.
While my MAUI model helpers have vastly simplified my MAUI development, certain binding situations do not lend as well to having a converter involved -- I would prefer to bind directly to the simple type as would be possible with this proposed change.
                         
**Unresolved questions**
Question of compile-time code generation decision or to use a distinct keyword with properties that feature this functionality.  I think a distinct keyword would not only ensure avoiding degrading performance on existing properties and avoid confusing new users, though I think compiler experts might have more informed input to provide on that.

**Design meetings**
None that I am aware of.
