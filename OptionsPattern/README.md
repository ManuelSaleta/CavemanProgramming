# Options Pattern in .NET

## Who Is This For?

Don't feel like reading? Jump straight to [The Big Three Comparison](#section-ii---the-lifetime-duel-ioptions-vs-ioptionssnapshot-vs-ioptionsmonitor) or the [Full Working Example](#full-working-example-program).

---

It's for you, dummy; no, wait, *me*. This is for me at 2:00 AM on a Tuesday, staring blankly at a production exception:
```text
System.NullReferenceException: Object reference not set to an instance of an object.
   at CavemanApp.Services.PaymentProcessor.ProcessAsync() in /app/Services/PaymentProcessor.cs:line 42
```
Why? Because someone renamed `"StripeKey"` to `"StripeApiKey"` in `appsettings.json`, and our service was happily doing `_config["StripeKey"]` without a single complaint from the compiler.

By the end of this guide, you will understand the **Options Pattern** in .NET, why raw `IConfiguration` injection is a trap, how the three different options interfaces actually work, and how to make invalid configuration crash your app *at startup* instead of when your user is trying to checkout.

Disclaimer: We will use C# and modern .NET, because type safety is our friend and stone tools have evolved.

---

# What Will I Learn?

- Why injecting `IConfiguration` directly across your services is dangerous ("Stringly-Typed" syndrome).
- How to bind configuration sections to strongly typed POCOs with `IOptions<T>`.
- The real differences between `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>` (and when your app will silently break if you choose the wrong one).
- How to validate configuration with Data Annotations and fail immediately upon app boot with `ValidateOnStart()`.
- How to handle multiple configurations of the same type using **Named Options**.
- How to use `PostConfigure<T>` for normalization and defaults.

---

# The Problem: Stringly-Typed Hell

In early or rushed .NET projects, you often see this caveman classic:

```csharp
public class OrderService
{
    private readonly IConfiguration _configuration;

    public OrderService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ProcessOrder()
    {
        // Hope you didn't misspell "TimeoutSeconds"!
        // Hope someone didn't set it to "thirty" in appsettings.json!
        // Hope you remembered to check for null!
        var timeout = int.Parse(_configuration["Database:TimeoutSeconds"] ?? "30");
        var connStr = _configuration.GetConnectionString("DefaultConnection");
        // ...
    }
}
```

### Why is this bad?
1. **Zero Type Safety**: Everything is a string. If someone types `"true "` with an accidental space or `"30s"`, `int.Parse` or `bool.Parse` throws at runtime.
2. **Scattered Dependencies**: Your service knows about the entire universe of application configuration rather than just what it actually needs (violates the Interface Segregation Principle).
3. **Testing is Painful**: Mocking `IConfiguration` in unit tests is notoriously tedious and verbose.
4. **No Early Feedback**: If a key is missing or corrupted, you only discover it when that specific code path runs in production.

---

# Section I - Binding POCOs & IOptions&lt;T&gt;

The **Options Pattern** replaces magic strings with a Plain Old CLR Object (POCO):

```csharp
public class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}
```

In your `Program.cs` / DI setup:

```csharp
// Reads the "Database" section from appsettings.json and binds it to DatabaseOptions
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));
```

And in your service:

```csharp
public class OrderService
{
    private readonly DatabaseOptions _options;

    // Inject IOptions<T>, not IConfiguration!
    public OrderService(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }

    public void ProcessOrder()
    {
        Console.WriteLine($"Connecting with timeout: {_options.TimeoutSeconds}s");
    }
}
```

Now your unit tests can simply pass `Options.Create(new DatabaseOptions { TimeoutSeconds = 10 })` without mocking `IConfiguration` at all!

---

# Section II - The Lifetime Duel: IOptions vs IOptionsSnapshot vs IOptionsMonitor

Here is where 90% of developers stumble. .NET gives you **three** interfaces for options. If you pick the wrong one, your configuration changes might be ignored, or your application might throw lifetime dependency exceptions.

### Quick Comparison Table

| Feature | `IOptions<T>` | `IOptionsSnapshot<T>` | `IOptionsMonitor<T>` |
| :--- | :--- | :--- | :--- |
| **DI Lifetime** | **Singleton** | **Scoped** | **Singleton** |
| **Reloads on Config Change?** | ❌ Never (Frozen at startup) | ✔️ Yes (Per request/scope) | ✔️ Yes (Instantly in real-time) |
| **Safe to inject in Singletons?** | ✔️ Yes | ❌ **NO!** (Captive dependency runtime error) | ✔️ Yes |
| **Named Options Support?** | ❌ No | ✔️ Yes (`.Get("name")`) | ✔️ Yes (`.Get("name")`) |
| **Change Event Notification?** | ❌ No | ❌ No | ✔️ Yes (`.OnChange(...)`) |
| **Access Method** | `.Value` | `.Value` | `.CurrentValue` |

---

### When to use which?

#### 1. `IOptions<T>`
- **Use when:** Your configuration never changes while the application is running, or changes require a pod/service restart anyway (e.g. database connection strings, encryption keys).
- **Pros:** Lowest overhead, thread-safe, can be injected anywhere (Singleton, Scoped, Transient).
- **Cons:** If `appsettings.json` is edited on disk or updated via a config provider, `IOptions<T>.Value` stays frozen with the startup values forever.

#### 2. `IOptionsSnapshot<T>`
- **Use when:** You are in a web request or scoped service (e.g., ASP.NET Core Controller, Minimal API endpoint), and you want the options to reflect any updates made to configuration on that specific request.
- **Guarantee:** Consistent values throughout a single request. If config reloads halfway through a web request, your request still sees the same consistent snapshot it started with.
- ⚠️ **CAUTION:** Never inject `IOptionsSnapshot<T>` into a Singleton service! Because `IOptionsSnapshot` is Scoped, ASP.NET Core will throw an `InvalidOperationException` (Cannot consume scoped service from singleton).

#### 3. `IOptionsMonitor<T>`
- **Use when:** You need real-time, hot-reloaded configuration updates inside a **Singleton** service (like background workers, `IHostedService`, messaging consumers, or caches).
- **Bonus:** Provides an `OnChange(opt => { ... })` event listener so you can trigger cache invalidation or reconfigure connections on the fly without restarting.
- **Access:** Read via `.CurrentValue` (not `.Value`).

---

# Section III - Don't Trust, Verify: Options Validation

What happens if someone puts an empty string for the database connection string?
By default, the options pattern happily binds an empty string, and your app explodes later when the first query executes.

We want **fail-fast** architecture: if configuration is invalid, the app should refuse to boot.

### 1. Data Annotations Validation

Add validation attributes to your options POCO:

```csharp
public class DatabaseOptions
{
    [Required(ErrorMessage = "ConnectionString cannot be empty!")]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 300, ErrorMessage = "Timeout must be between 1 and 300 seconds.")]
    public int TimeoutSeconds { get; set; } = 30;
}
```

### 2. ValidateDataAnnotations and ValidateOnStart

Use `AddOptions<T>()` fluent builder:

```csharp
builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection("Database"))
    .ValidateDataAnnotations()
    .Validate(opts => opts.ConnectionString.Contains("Server"), "Must specify a Server.")
    .ValidateOnStart(); // <-- The magic: fails during Host startup!
```

> [!IMPORTANT]
> Without `ValidateOnStart()`, validation only runs the first time `IOptions.Value` is accessed. With `ValidateOnStart()`, if the config is invalid, the application terminates immediately on startup with a descriptive `OptionsValidationException`.

---

# Section IV - Named Options & Post-Configuration

### Named Options
What if your application talks to multiple payment gateways, or multiple Redis instances, each having the same configuration shape?

Instead of creating `StripeOptions`, `PayPalOptions`, `SquareOptions` classes with identical properties, use **Named Options**:

```csharp
// Register multiple named instances of PaymentGatewayOptions
services.Configure<PaymentGatewayOptions>("Stripe", configuration.GetSection("Gateways:Stripe"));
services.Configure<PaymentGatewayOptions>("PayPal", configuration.GetSection("Gateways:PayPal"));

// Retrieve them via IOptionsSnapshot or IOptionsMonitor
public class PaymentService(IOptionsMonitor<PaymentGatewayOptions> gateways)
{
    public void ChargeWithStripe()
    {
        var stripe = gateways.Get("Stripe");
        Console.WriteLine($"Using key: {stripe.ApiKey}");
    }
}
```

### Post-Configuration
`PostConfigure<T>` allows you to modify or sanitize options after all binding has occurred (e.g., ensuring a trailing slash on URLs or setting computed defaults):

```csharp
services.PostConfigure<DatabaseOptions>(opts =>
{
    if (!opts.ConnectionString.EndsWith(";"))
    {
        opts.ConnectionString += ";";
    }
});
```

---

# Full Working Example Program

The accompanying [LearningOptions.cs](file:///home/gman/Projects/CavemanProgramming/OptionsPattern/LearningOptions.cs) file is fully runnable.

You can execute it directly from the repository root:

```bash
dotnet run --project OptionsPattern
```

Here is the complete implementation demonstrating `IOptions`, hot reloading with `IOptionsMonitor` and `IOptionsSnapshot`, Named Options, and `ValidateOnStart`:

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

Console.WriteLine("==================================================");
Console.WriteLine(" Caveman Programming: Options Pattern in .NET     ");
Console.WriteLine("==================================================\n");

// 1. Initial In-Memory Configuration
var initialConfig = new Dictionary<string, string?>
{
    ["Database:ConnectionString"] = "Server=sql.caveman.local;Database=RocksDb;User=thog;Password=secret",
    ["Database:TimeoutSeconds"] = "30",
    ["Features:BetaTheme"] = "Disabled",
    ["PaymentGateways:Stripe:ApiKey"] = "sk_stripe_thog_123",
    ["PaymentGateways:Stripe:TimeoutSeconds"] = "5",
    ["PaymentGateways:PayPal:ApiKey"] = "pp_sec_caveman_456",
    ["PaymentGateways:PayPal:TimeoutSeconds"] = "10"
};

var memorySource = new MemoryConfigurationSource { InitialData = initialConfig };
var configBuilder = new ConfigurationBuilder();
configBuilder.Add(memorySource);
var configuration = configBuilder.Build();

// 2. Service Registration & Options Setup
var services = new ServiceCollection();

services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
services.Configure<FeatureFlagsOptions>(configuration.GetSection("Features"));

services.Configure<PaymentGatewayOptions>("Stripe", configuration.GetSection("PaymentGateways:Stripe"));
services.Configure<PaymentGatewayOptions>("PayPal", configuration.GetSection("PaymentGateways:PayPal"));

services.PostConfigure<DatabaseOptions>(opts =>
{
    if (!opts.ConnectionString.EndsWith(";"))
    {
        opts.ConnectionString += ";";
    }
});

var provider = services.BuildServiceProvider();

// Demo 1: Basic IOptions<T>
Console.WriteLine("--- Demo 1: Basic IOptions<T> (Strongly Typed POCO) ---");
var dbOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
Console.WriteLine($"Connection String: {dbOptions.ConnectionString}");
Console.WriteLine($"Timeout:           {dbOptions.TimeoutSeconds}s\n");

// Demo 2: The Lifetime Duel (Reloading Config)
Console.WriteLine("--- Demo 2: The Lifetime Duel (Reloading Config) ---");
var options = provider.GetRequiredService<IOptions<FeatureFlagsOptions>>();
var monitor = provider.GetRequiredService<IOptionsMonitor<FeatureFlagsOptions>>();

using var changeListener = monitor.OnChange(updated =>
{
    Console.WriteLine($"  [Event] IOptionsMonitor detected change! New BetaTheme: {updated.BetaTheme}");
});

Console.WriteLine($"Initial State:");
Console.WriteLine($"  IOptions.Value:          {options.Value.BetaTheme}");
Console.WriteLine($"  IOptionsMonitor.Current: {monitor.CurrentValue.BetaTheme}");

using (var scope1 = provider.CreateScope())
{
    var snapshot1 = scope1.ServiceProvider.GetRequiredService<IOptionsSnapshot<FeatureFlagsOptions>>();
    Console.WriteLine($"  IOptionsSnapshot (Scope 1): {snapshot1.Value.BetaTheme}");
}

// Simulating live configuration update
Console.WriteLine("\n--> Modifying configuration and reloading provider...");
configuration["Features:BetaTheme"] = "Enabled";
configuration.Reload();

Console.WriteLine($"\nAfter Config Reload:");
Console.WriteLine($"  IOptions.Value:          {options.Value.BetaTheme} (Unchanged! Stays frozen forever)");
Console.WriteLine($"  IOptionsMonitor.Current: {monitor.CurrentValue.BetaTheme} (Updated immediately!)");

using (var scope2 = provider.CreateScope())
{
    var snapshot2 = scope2.ServiceProvider.GetRequiredService<IOptionsSnapshot<FeatureFlagsOptions>>();
    Console.WriteLine($"  IOptionsSnapshot (Scope 2): {snapshot2.Value.BetaTheme} (Updated in new scope!)\n");
}

// Demo 3: Named Options
Console.WriteLine("--- Demo 3: Named Options ---");
var gatewayMonitor = provider.GetRequiredService<IOptionsMonitor<PaymentGatewayOptions>>();
var stripeOpts = gatewayMonitor.Get("Stripe");
var paypalOpts = gatewayMonitor.Get("PayPal");
Console.WriteLine($"Stripe Config: ApiKey={stripeOpts.ApiKey}, Timeout={stripeOpts.TimeoutSeconds}s");
Console.WriteLine($"PayPal Config: ApiKey={paypalOpts.ApiKey}, Timeout={paypalOpts.TimeoutSeconds}s\n");

// Demo 4: Options Validation
Console.WriteLine("--- Demo 4: Options Validation & ValidateOnStart ---");
var invalidConfig = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Database:ConnectionString"] = "",
        ["Database:TimeoutSeconds"] = "-99"
    })
    .Build();

var validationServices = new ServiceCollection();
validationServices.AddOptions<DatabaseOptions>()
    .Bind(invalidConfig.GetSection("Database"))
    .ValidateDataAnnotations()
    .Validate(opts => opts.TimeoutSeconds > 0, "Timeout must be greater than zero.")
    .ValidateOnStart();

var validationProvider = validationServices.BuildServiceProvider();

try
{
    var faultyOptions = validationProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
}
catch (OptionsValidationException ex)
{
    Console.WriteLine("SUCCESS: Caught expected OptionsValidationException at startup!");
    foreach (var failure in ex.Failures)
    {
        Console.WriteLine($"  - {failure}");
    }
}
```

---

# Summary & Closing Thoughts

The Options Pattern is more than just avoiding `_config["Key"]`. It is about:
1. **Encapsulation**: Services ask only for the options they need.
2. **Predictability**: Choosing the right lifetime (`IOptions` vs `IOptionsSnapshot` vs `IOptionsMonitor`) ensures your app behaves correctly under load and during config updates.
3. **Resilience**: Enforcing fail-fast validation (`ValidateOnStart`) so configuration bugs never reach production silently.

As software development moves faster and AI assistants generate more boilerplate, understanding these foundational mechanics ensures you spot subtle configuration lifetime bugs before they hit production.

---

# Further Reading

- [Options pattern in .NET (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [Options validation in .NET (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/core/extensions/options-validation)
- [Configuration in .NET (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
