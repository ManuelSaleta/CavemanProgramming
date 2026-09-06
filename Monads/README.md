# Monads

## Who Is This For?

Don't feel like reading? Skip straight to [What Is a Monad for Humans](#what-is-a-monad-for-humans) section.

---

It's for you dummy; no, not you, _me_. This is for me, but can also be for you. But mainly... this is for me. Because I have seen every video on Monads, endless tutorials and somehow it never clicked. If you asked me to implement it forget it. Maybe the trick is to start coding by age 5 and sell your first startup by 11. And I am about 20 years too late for that.

By the end of this tutorial you will _understand_ monads, and even you, again... not you silly, me. Potentially know how to implement one...

Disclaimer - I will use C# for this tutorial, because well.. that's what I want to use `:D`

# What Will I Learn?

- You will learn about **monads**.
- You will learn how to implement it in C#. Step-by-step.
- Additionally learn other C# topics, like `Generics`.

Enough mumbling...

# What Even Is a Monad?

## Monads: Mathematical & Functional Definitions

Quick reference definitions for Monads from Category Theory and Functional Programming. (For the ultra nerdy)

---

### 1. Category Theory Definition

A **monad** on a category $\mathcal{C}$ is a triple $(T, \eta, \mu)$, where:

- **$T: \mathcal{C} \to \mathcal{C}$** is an **endofunctor**.
- **$\eta: \text{id}_{\mathcal{C}} \implies T$** is a natural transformation called the **unit** (or _return_).
- **$\mu: T^2 \implies T$** is a natural transformation called the **multiplication** (or _join_).

#### Coherence Laws

These transformations must satisfy the **associativity** and **identity** commutative diagrams:

$$\mu \circ T\mu = \mu \circ \mu T \quad \text{and} \quad \mu \circ T\eta = \mu \circ \eta T = \text{id}_T$$

---

### 2. Functional Programming Definition

In programming, a **monad** is a generic type `M<T>` wrapper equipped with two primary operations used to chain computations while abstracting away side effects, state, or context:

#### Core Operations

| Operation                      | Signature                       | Description                                                              |
| :----------------------------- | :------------------------------ | :----------------------------------------------------------------------- |
| **`return`** (_unit_)          | $T \to M<T>$                    | Lifts a raw value into the monadic context.                              |
| **`bind`** (_flatMap_ / `>>=`) | $(M<T>, (T \to M<U>)) \to M<U>$ | Feeds the inner value into a function returning a new monadic container. |

---

### The Three Monad Laws

To ensure predictable behavior, any implementation must satisfy three algebraic rules:

1. **Left Identity**

```text
bind(return(x), f) == f(x)
```

Wrapping a raw value with Of(x) and then calling FlatMap(f) produces the exact same result as calling f(x) directly.

2. **Right Identity**

Passing your monad's unit/factory function (Container.Of) to FlatMap returns the exact same monad unchanged.

```csharp
var m = Container.Of(42);
// Right side: bind(m, return)
var result = m.FlatMap(x => Container.Of(x));

// Right Identity Law: result.Value == m.Value (both are 42)
// (Note: Mathematically result == m; we compare .Value here since our simple class uses reference equality)
```

3. **Associativity**

When chaining multiple FlatMap operations, the grouping/nesting order of execution doesn't change the final outcome.

```csharp
Func<int, Container<int>> f = x => Container.Of(x + 10);
Func<int, Container<string>> g = x => Container.Of($"Value: {x}");

var m = Container.Of(5);

// Left side: (m >>= f) >>= g
var left = m.FlatMap(f).FlatMap(g);

// Right side: m >>= (\x -> f(x) >>= g)
var right = m.FlatMap(x => f(x).FlatMap(g));

// Associativity Law: left.Value == right.Value ("Value: 15")
```

# Lesson Over

Congratulations, you know all you need about **monads**. Go on, get out of here - there are AI startups to make and sell. But if you would like to stick around and hangout with me that'd be nice too.

Let me try explaining it myself as eloquently as the above segment.

# What Is a Monad for Humans

Put simply, a **monad** is a design pattern for chaining computations. You wrap a raw value in a context/container, and whenever a transformation wants to return *another* container, the monad automatically flattens it so you don't end up trapped in Russian nesting dolls of types. (If you're just transforming the value inside while keeping the exact same container "shape", that's its simpler sibling, the **Functor**!)
Why would you care? It's a design pattern you've been using all along. `Array` in JS and `IEnumerable<T>` in .NET are classic collection monads. In functional languages (and C# libraries like `LanguageExt`), you'll also see `Option<T>` / `Maybe<T>` (which does what C#'s `Nullable<T>` wishes it could do, minus the struct-only restrictions).
This is a design pattern widely used in functional programming. Okay but _what_ is a monad bro I am getting tired...

- Sorry just stalling. Here it is:

_"A **monad** is a type of container"_ Like a box, a wrapper etc... The important thing to remember is

_Monads always have at least two key operations to them:_

1. A **monad** has a way of putting a raw entity into its wrapper (`return` / `unit`).
2. A **monad** has a way of applying transformations that themselves return wrappers, flattening the result so you don't double-wrap (`bind` / `flatMap`).

Are you an expert now? no? okay let us try once more with a bit more formal context.

_"A **monad** is a generic wrapper that can lift any T into its context, it has a way of binding and applying transformations to T while avoiding side effects and nested wrappers."_

Monads have to have at least these parts to them:

1. A **monad** has a mechanism (`return` / `unit`) to lift a `T` value into its context and resolve to the wrapper.
2. A **monad** has a way to _bind_ `T` (`flatMap`), applying transformations and flattening the result back to the wrapper.

How about now? no, really... okay I really thought I nailed it there. Okay... one last time.

Our Monad example will be: generic wrapper of T

1. Lift `T`, return wrapper (`return` / `unit`).
2. Binds, transforms, flattens, returns wrapper (`bind` / `flatMap`).
3. (Bonus for our specific toy wrapper: a `.Value` property to peek inside. Fun fact though: mathematically, true monads do *not* require an unwrap operation! Haskell's `IO` and async monads deliberately refuse to let you unwrap them in pure code because keeping effects safely locked in the box is the whole point!)

...Still?

...Bro

...Fine, here is the code.

```csharp

// A Monad is a generic wrapper of T.
// Easy enough:

public class Container<T>
{
    //...
}
```

```csharp
// A Monad has a way of lifting T into its context...
//Okay we can do something like:
public class Container<T>
{
    private readonly T _data;

    public Container(T data)
    {
        _data = data;
    }
}

// We can even re-do it using primary-constructor syntax
public class Container<T>(T data)
{
    private readonly T _data = data;
}
```

Now, in functional programming, a monad's `return` / `unit` is simply a function that takes `T` and returns `Container<T>`. In C#, `new Container<int>(9001)` technically fits the bill! But having to type `new` and explicitly spell out `<int>` every single time gets tedious fast and its *yucky*. It would be much nicer to have a static factory function where the compiler infers `T` for us:

```csharp
var myCoolWrapper = new Container<int>(9001); // Works, but our fingers deserve better
```

## Section I - Creating a Truly Static Container

Let's try creating a static method:

```csharp
public class Container<T>(T data)
{
    private readonly T _data = data;

    public static Container<TResult> Of<TResult>(TResult data)
        => new Container<TResult>(data);

}

//...
var sillyContainer = Container<int>.Of(9001);
```

But wait, what is `TResult`? And why did we declare the method as `Of<TResult>(...)`?

If you declared `public static Container<T> Of<T>(T data)` inside `Container<T>`, the C# compiler throws [CS0693](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/generic-type-parameters-errors#type-parameter-declaration-and-naming):

`warning CS0693: Type parameter 'T' has the same name as the type parameter from outer type 'Container<T>'`

Because `T` is already in scope from the class! Renaming it to `TResult` dodges the compiler warning, but it's a bit clumsy because calling `Container<int>.Of("hello")` returns a `Container<string>`. Still, it lets us start creating containers from a static method:

```csharp
Console.WriteLine("Hello Monad Tutorial");

// Getting instance from a static context.
var myWrapper = Container<int>.Of(9001);

Console.WriteLine(myWrapper.GetType());

public class Container<T>(T data)
{
    private readonly T _data = data;

    public static Container<TResult> Of<TResult>(TResult data)
        => new(data);

}
```

We got rid of `new`, but notice something else? - You might be wondering, what good is this generic container malarkey if you have to pass the type signature `int` at compile-time anyways...

You have a much better intuition than me...

I know - bear with me, we are building towards something great.

What if we split this code up a bit!? We use a whole separate static class.

```csharp
Console.WriteLine("Hello Monad Tutorial");

// sticky, requires new keyword and type signature defeating the whole purpose...
var stickyContainer = new Container<int>(9001); //over 9000.

// Our shiny container had no glue,
// The C# compiler uses generic type argument inference to deduce T at compile-time, saving you from <int>.
// Python folks: look at what they have to do to mimic a fraction of our power.jpeg
var sealedContainer = Container.Of(9001);

// pass other Types
var djKhaled = Container.Of("my-love-and-affection");

var anodaOne = Container.Of(new {Stars = 5, Issues = 0});

// Full CLR type Container`1[<>f__AnonymousType0`2[System.Int32,System.Int32]]
Console.WriteLine(anodaOne.GetType());

// Static, but non-generic - sometimes referred to as a Factory Design Pattern
public static class Container
{
    public static Container<T> Of<T>(T data) => new(data);
}

// Non-static, Generic class
public class Container<T>(T data)
{
    private readonly T _data = data;

}
```

We have touched lots of concepts here, some `OOP` some `functional programming`, some other design patterns `Factory` pattern.
Some .NET centric ones like `Generics` and specific features `Primary constructors`.

I know it's a lot and before we continue feel free to brush up on any of them. God knows I've had to, many times, even while literally writing this. Check the [Further Reading Section](#further-reading) for those topics.

But let's keep on trucking!

The keen among you (me not included) might have noticed that I made this container so good, so air tight, that literally nothing can escape.. EVER...
So not super duper useful eh...

Let's rewrite the _Generic Container_ class, clean it and make it more useful via its property or a projection, we can do:

```csharp
public class Container<T>(T value)
{
    public T Value => value;
}

//...

var giftBox = Container.Of(9001);
```

This is pretty modern C# - So a more "traditional" implementation might reveal some of the magic more easily. These two pieces of code are functionally equivalent:

```csharp
public class Container<T>
{
    private readonly T _data;

    public Container(T data)
    {
        _data = data;
    }

    public T Value => _data;
}
```

## Section II - Mapping Our Way to Freedom

Okay but enough of that, so far our **monad** is not very useful, it barely has any value, it can take in a value and it exposes its value... we don't need to use the `new` keyword and we leverage the inference system to figure out the type of T at compile-time.

Let's start making something fun, the second piece we need for our Monad is giving the ability to perform transformations on the wrapped entity. Let's add a `Map` function to our lackluster Container class.

```csharp
public class Container<T>(T value)
{
    public T Value => value;

    public Container<TResult> Map<TResult>(Func<T,TResult> transform)
    {
        return new Container<TResult>(transform(value));
    }
}
```

There is quite a lot going on with this map function. You have actually seen this function, named the same in JavaScript on Array. Or `.Select()` in .NET and many other places.

What our function does:

- Enables us to apply _transformations_ to our (incoming) T type, and produces a new Container of the transformed result. Our second major key to our **monad**. Let's see why shortly.

Anatomy of our `Map` function:

- The method signature matches the return type `Container<TResult>`.
- The `TResult` is used to avoid an overshadowing warning.
- The function takes a `T` as input and returns a `TResult`.
- The class `T value` is passed to the function (as our T input).
- The function resolves to a `TResult`.
- The function is passed to the class constructor.
- Finally a new container is returned.

```csharp
// Now we can do things like:
var giftWrapper = Container
    .Of(9001)                           //We start with int 9001
    .Map(x => x * 2.0)                  //Transform to double 18002.00
    .Map(x => Convert.ToInt32(x + 1))   //Cast back to int, add +1
    .Map(x => $"The value is: {x}")     //Transform to string.
    .Map(x => x.Length);                //Can you get what the result is?

Console.WriteLine($"The type is: {giftWrapper.GetType()}");
Console.WriteLine($"The result is: {giftWrapper.Value}");
// The type is: Container`1[System.Int32]
// The result is: 19
```

## Section III - Containers All the Way Down

Our `wrapper` class is coming along great, we can transform our `T` and mutate it a bunch, we don't worry about its current state or context which is exactly what our goal was.

But if you play around with it for a while you might do something like:

```csharp
//...

var multiWrapped = Container
    .Of(9001)
    .Map(x => Container.Of(x))
    .Map(x => Container.Of(x))
    .Map(x => Container.Of(x));


Console.WriteLine($"mystery wrapper has: {multiWrapped}");
// mystery wrapper has: Container`1[Container`1[Container`1[Container`1[System.Int32]]]]
```

Yes the code snippet is perfectly valid... in that it compiles, but now look at what we've made. _Containers all the way down_. We now have a container that has a container that has a container that has a container with an `int`.

And yes if you wanted to get the actual value of `T` you would do this:

```csharp
//...
Console.WriteLine($"mystery wrapper has: {multiWrapped.Value.Value.Value.Value}");
// mystery wrapper has: 9001
// Call Value once per .Map(...) invocation, + 1 more for the original container. Total of 4 times to unwrap our value, funny gift-giving prank... not very funny in code.
```

This example is humorous, until you have a real-life production scenario you are trying to debug. If you've been around code long enough inevitably you will have run into this scenario (yikes). Things like `response.response` or `data.data` are far more common than you think. So what's the fix?

_We implement a way to flat map the chain_ with our aptly named `.FlatMap()` function.

```csharp
//...
public Container<TResult> FlatMap<TResult>(Func<T, Container<TResult>> transform)
{
    return transform(Value);
}
```

On the surface `.Map(...)` and `.FlatMap(...)` look deceptively similar. But there is one fundamental difference:

1. In `Map(...)`, your transformation returns a plain value: `Func<T, TResult>`. The monad wraps that value in a `Container<TResult>`.
2. In `FlatMap(...)`, your transformation *already* returns a container: `Func<T, Container<TResult>>`. Instead of wrapping it into a `Container<Container<TResult>>`, it yields the container directly—it maps, then **flattens**!

If you already messed up and nested containers with `.Map(x => Container.Of(x))`, you *can* use `FlatMap(x => x)` with the identity function (`x => x`) to peel one layer off—in category theory, this un-nesting is known as `Join` or `Flatten` ($\mu$):

```csharp
// "Peeling" the nested layers with FlatMap as a flattener / join:
var multiWrapped = Container
    .Of(9001)
    .Map(x => Container.Of(x))
    .Map(x => Container.Of(x))
    .Map(x => Container.Of(x))
    .FlatMap(x => x) //Calling FlatMap() w/o lambda raises CS7036
    .FlatMap(x => x)
    .FlatMap(x => x);

Console.WriteLine($"mystery wrapper has: {multiWrapped.Value}");
// mystery wrapper has: 9001
```

Now, don't go calling `Map(...)` followed by `FlatMap(x => x)` like a lunatic. The **real superpower** of `FlatMap` is *prevention*. Whenever you have operations that return new containers, you call `FlatMap` directly instead of `Map`. It guarantees that no matter how many steps you chain, you stay comfortably at a single level of container:

```csharp
// The proper way: chain operations without ever nesting!
var cleanChain = Container
    .Of(9001)
    .FlatMap(x => Container.Of(x + 10))
    .FlatMap(x => Container.Of($"Final value: {x}"));

Console.WriteLine($"clean wrapper has: {cleanChain.Value}");
// clean wrapper has: Final value: 9011
```

# Summary - Putting All Together

Well that was quite a lot of writing. And in the age of `Claude` and `Copilot` - some might be turning their nose saying "why bother?" "what's the point?". And to those people I say, this is _exactly_ the point. It's more important than ever for engineers to dig deeper, really understand the more advanced concepts. As we move more towards being _agentic managers_ where we are spending more time reading and reviewing AI generated code than writing it. Sharpening your skills becomes more and more important.

Regardless of how fast your manager vibe-coded that POC he swears is **_almost production ready_**. Software engineering is still not a solved issue, you still need to know how to code, and how to design systems.

And in my best Youtuber impression, _If you liked this tutorial, leave a comment, feedback or star this repo_ I get absolutely nothing other than knowing maybe someone out there **learnted** something. :D

## Full Working Example Program W/ Extra Goodies

Fundamentally it is all the same code - Here is what changed and got added:

- Adds `.ToString` so we can pass it without calling var.Value.
- Adds .`GetInfo` so we get some nicely formatted output.
- Uses `=>` syntax to remove visual clutter
- Renames `TResult` to `U` to remove visual clutter.
- Renames `Map` to `Select` to enable LINQ syntax.
- Renames `FlatMap` to `SelectMany` to enable LINQ syntax.
- Adds `FlatMap/SelectMany` overload to demonstrate LINQ multi-select.

```csharp
using System;

public class Container
{
    public static Container<T> Of<T>(T value) => new(value);
}

public class Container<T>(T value)
{
    public T Value => value;

    // Map / Functor. Named Select to enable LINQ* syntax
    public Container<U> Select<U>(Func<T, U> transform)
        => Container.Of(transform(value));

    // FlatMap / Bind / Monad. Named SelectMany to enable LINQ* syntax
    public Container<U> SelectMany<U>(Func<T, Container<U>> transform)
        => transform(value);

    // Complete LINQ Query Support - could be a whole README.md on its own
    // Enables multi-from clauses: `from x in c1 from y in c2 select x + y`
    public Container<V> SelectMany<U, V>(
        Func<T, Container<U>> selector,
        Func<T, U, V> resultSelector)
    {
        // Unwraps T, runs selector to get Container<U>, unwraps U, projects to V
        return SelectMany(x => selector(x).Select(y => resultSelector(x, y)));
    }

    public override string ToString() => $"Value of container is: {value}";

    // Reflection method to inspect container state
    public string GetInfo()
    {
        Type containerType = typeof(Container<T>);
        Type innerType = typeof(T);

        return $"Container Type: {containerType.Name}, Inner Type: {innerType.Name}, Value: {value}";
    }
}

public static class Program
{
    public static void Main()
    {
        // 1. Instantiation via static factory
        var wrapper = Container.Of(9001);

        Console.WriteLine("--- Basic Information ---");
        Console.WriteLine(wrapper);           // Calls ToString()
        Console.WriteLine(wrapper.GetInfo()); // Calls GetInfo()

        Console.WriteLine("\n--- Method Chaining (Select & SelectMany) ---");

        // Transform the payload (Map / Select)
        var textWrapper = wrapper.Select(x => $"Power level over {x}!");
        Console.WriteLine(textWrapper);

        // Chain monad operations (FlatMap / SelectMany)
        var multipliedWrapper = wrapper.SelectMany(x => Container.Of(x * 2));
        Console.WriteLine(multipliedWrapper.GetInfo());

        Console.WriteLine("\n--- Idiomatic LINQ Expression ---");

        var c1 = Container.Of(100);
        var c2 = Container.Of(500);

        // Works thanks to the two-parameter SelectMany<U, V> overload
        var combined =
            from x in c1
            from y in c2
            select x + y;

        Console.WriteLine(combined);
        Console.WriteLine(combined.GetInfo());
    }
}
```

# Closing Thoughts

- If you somehow made it here and you thought to yourself... "Cool Container, but why do I care in a real app?" the answer is: welp... this is how all them fancy APIs you've encountered likely work or implement.

The exact same pattern is what powers:

- `Option<T>` / `Maybe<T>` (railway-oriented error handling; what C#'s `Nullable<T>` aspires to be when it grows up)
- `Task<T>` (async sequencing — `async`/`await` is practically C#'s built-in syntax for chaining tasks without callback hell)
- `IEnumerable<T>` (collection traversal via LINQ).

# Further Reading

- [Generics in C#](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/)
- [Primary Constructors in C#](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12#primary-constructors)
- [Projection Methods (LINQ / Select) in C#](https://learn.microsoft.com/en-us/dotnet/csharp/linq/standard-query-operators/projection-operations)
- [Reflection in .NET](https://learn.microsoft.com/en-us/dotnet/framework/reflection-and-codedom/reflection)
- [Factory Design Pattern Concepts in C#](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implemenation#use-factories-for-aggregate-creation)

Happy coding :)
