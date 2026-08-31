# Monads 

## Who is this for?
 It's for you dummy; no, not you, _me_. This is for me, but can also be for you. But mainly... this is for me. Because I have seen every video on Monads, endless tutorials and somehow it never clicked. If you asked me to implement it forget it. Maybe the trick is to start coding by age 5 and sell your first startup by 11. And I am about 20 years too late for that. 

By the end of this tutorial you will *understand* monads, and even you,again... not you silly, me. Potentially know how to implement one...

If you are human, skip to the [What Is A Monad For Humans](#what-is-a-monad-for-humans) section.

Disclaimer - I will use C# for this tutorial, because well.. thats what I want to use `:D`

Enough mumbling...

## What Even Is a Monad? 

# Monads: Mathematical & Functional Definitions

Quick reference definitions for Monads from Category Theory and Functional Programming.

---

## 1. Category Theory Definition

A **monad** on a category $\mathcal{C}$ is a triple $(T, \eta, \mu)$, where:

* **$T: \mathcal{C} \to \mathcal{C}$** is an **endofunctor**.
* **$\eta: \text{id}_{\mathcal{C}} \implies T$** is a natural transformation called the **unit** (or *return*).
* **$\mu: T^2 \implies T$** is a natural transformation called the **multiplication** (or *join*).

### Coherence Laws
These transformations must satisfy the **associativity** and **identity** commutative diagrams:

$$\mu \circ T\mu = \mu \circ \mu T \quad \text{and} \quad \mu \circ T\eta = \mu \circ \eta T = \text{id}_T$$

---

## 2. Functional Programming Definition

In programming, a **monad** is a generic type `M<T>` wrapper equipped with two primary operations used to chain computations while abstracting away side effects, state, or context:

### Core Operations

| Operation | Signature | Description |
| :--- | :--- | :--- |
| **`return`** (*unit*) | $T \to M<T>$ | Lifts a raw value into the monadic context. |
| **`bind`** (*flatMap* / `>>=`) | $(M<T>, (T \to M<U>)) \to M<U>$ | Feeds the inner value into a function returning a new monadic container. |

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


## Lesson Over
Congratulations, you know all you need about **monads**. Go on, get out of here - there are AI startups to make and sell. But if you would like to stick around and hangout with me that'd be nice too.

Le me try explaining it myself as eloquently as the above segment.

# What is a Monad For Humans

Put simply, a **monad** is a type of container that can take something in have it applied changes to while retaining the same shape, it doesn't care about the state of the thing it contains. This is a design pattern widely used in functional programming. Okay but *what* is a monad bro I am getting tired...

- Sorry just stalling. Here it is

*"A **monad** is a type of container"* Like a box, a wrapper etc... The important thing to remember is

*Monads always have at least three key aspects to them:*

1. A **monad** has a way of putting an entity into its wrapper
2. A **monad** has a way of applying any number of transformations to the wrapped entity, while't being able to return the updated entity in the wrapper.
3. A **monad** has a way of unwrapping its entity regardless of the amount of transformationed applied to it.


Are you an expert now? no? okay let us try once more with a bit more formal context.

*"A **monad** is a generic wrapper that can lift any T into its context, it has a way of binding and applying transformations to T while avoiding side effects."* 

Monads at least have three parts to them:

1. A **monad** has a static mechanism to lift T value into its context and resolves to the wrapper.
2. A **monad** has a way to *bind* T, it *applies* transformations, and resolves to the wrepper.
3. A **monad** has a way to *return* T unwrapped regardless of how many times transformations you applied to T.


How about now? no, really... okay I really thought I nailed it there. Okay... one last time.

Monad: generic wrapper of T

1. Static context, lift T, return wrapper.
2. Binds, transforms, return wrapper.
3. Unwraps T, returns T no matter how many wrappers.


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

    public class Container<T>(T data)
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

Earlier I mentioned that that for a class to be a true **monad** it needs a **static** way to lift T into its context. And while our wrapper is a damn fine wrapper, it doesn't have a way of directly satysfying that condition so far; since you would need to create a *new* instace of `Container`. As such:

```csharp 
var myCoolWrapper = new Container<int>(9001);
```
## Section I - Creating a Trully Static Container

Lets create a static method:

```csharp
public class Container<T>(T data)
{
    private readonly T _data = data;

    public static Container<TResult> Of<TResult>(TResult data) 
        => new Container<TResult>.Of(data)
    
}
```

But wait, what is TResult? and why do you declare function `Of` as `Of<TResult>(...)`
But when you call it you simply called `.Of(data)`

1. `TResult` is different from `T` to avoid an overshadowing warning, otherwise you get [CS0693](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/generic-type-parameters-errors#type-parameter-declaration-and-naming).

`warning CS0693: Type parameter 'T' has the same name as the type parameter from outer type 'Container<T>'`

It seems like a whole lot of work to write that whole function just to avoid a warning, but it does smoething else entirely huge for us. We are off the hook now and can make containers from a static context - yay us!

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

// Our shiny container had no glue, compiler can infer the Type of T at runtime
// Python folks: look at what they have to do to mimick a fraction of our power.jpeg
var sealedContainer = Container.Of(9001);

// pass other Types
var djkhaled = Container.Of("my-love-and-affection");

var anodaone = Container.Of(new {Stars = 5, Issues = 0});

// Full CLR type Container`1[<>f__AnonymousType0`2[System.Int32,System.Int32]]
Console.WriteLine(anodaone.GetType());

// Static, but none-generic - sometimes reffered to as a Factory Design Pattern
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

I know its a lot and before we continue feel free to brush up on any of them. God knows I've had to many times, even while literally writing this. Check the [Further Reading Section](#further-reading) for those topics.

But let's keep on trucking!

The keen among you (me not included) might have noticed that I made this container so good, so air tight, that literally nothing can escape.. EVER...
So not super duper useful eh... 

Let's rewrite the *Generic Container* class, clean it and make it more useful via its property or a projection, we can do:
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

    public T Value = _data;
}
```
## Section II - Adding The Mapping Function
Okay but enough of that, so far our **monad** is not very useful, it barely has any value, it can take in a value and it eposes its value... we dont need to use the `new` keyword and it we levearage the inferance system to figure out the type at runtime.

Let's start making something fun, the second the piece we need for our Monad is giving the abilty to perform trans formations on the wrapped entity. Let's add a `Map` function to our lackluster Container class.

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
- Enables us to apply *transformations* to our (incoming) T type, and produces a new Container of the transformed result. our second major key to our **monad**. Lets see why shortly.

Anatomy of our `Map` function:
- The method signature matches the return type `Container<TResult>`.
- The `TResult` is used to avoid an overshadowing warning. 
- The function takes a `T` as input and returns a `TResult`.
- The class `T value` is passed to the function (as our T input).
- The function resolves to a `TResult`.
- The function is passed to the class consutrctor. 
- Finally a new container is returned.

```csharp
// Now we can do things like:
var giftWrapper = Container
    .Of(9001)                           //We start with int 9001
    .Map(x => x * 2.0)                  //Transform to float 18002.00
    .Map(x => Convert.ToInt32(x + 1))   //Cast back to int, add +1
    .Map(x => $"The value is: {x}")     //Transform to string.
    .Map(x => x.Length);                //Can you get what the result is?

Console.WriteLine($"The type is: {giftWrapper.GetType()}");
Console.WriteLine($"The result is: {giftWrapper}");

// The type is: Container`1[System.Int32]
// The result is: 19
```






# Further Reading
- [Generics C#]()
- [Primmary Constructors C#]()
- [Projection Methods C#]()
- [Reflection C#]()
- [Factory Design Pattern C#]()