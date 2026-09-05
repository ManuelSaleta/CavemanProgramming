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
