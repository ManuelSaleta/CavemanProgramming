# Options Pattern in .NET

## Who Is This For?

Don't feel like reading? Jump straight to [Working with IOptions<SomeConfigOpts> (Legacy vs Modern)](#section-i---working-with-ioptionssomeconfigopts-legacy-vs-modern-approach), [The Big Three Comparison](#section-ii---the-lifetime-duel-ioptions-vs-ioptionssnapshot-vs-ioptionsmonitor), or the [Full Working Example](#full-working-example-program).

---

It's for you, dummy; no, wait, *me*. This is for me at 2:00 AM on a Tuesday, staring blankly at a production exception:
```text
System.NullReferenceException: Object reference not set to an instance of an object.
   at CavemanApp.Services.PaymentProcessor.ProcessAsync() in /app/Services/PaymentProcessor.cs:line 42
```
Why? Because someone renamed `"StripeKey"` to `"StripeApiKey"` in `appsettings.json`, and our service was happily doing `_config["StripeKey"]` without a single complaint from the compiler.

By the end of this guide, you will understand the **Options Pattern** in .NET, why raw `IConfiguration` injection is a trap, how to work with `IOptions<SomeConfigOpts>`, how the modern `.ValidateOnStart()` (aka `.ValidateOnStartup()`) approach contrasts with legacy configuration binding, how the three different options interfaces actually work, and how to make invalid configuration crash your app *at startup* instead of when your user is trying to checkout.

Disclaimer: We will use C# and modern .NET, because type safety is our friend and stone tools have evolved.

---

# What Will I Learn?

- Why injecting `IConfiguration` directly across your services is dangerous ("Stringly-Typed" syndrome).
- How to work with `IOptions<SomeConfigOpts>`: contrasting the **Legacy Approach** (`services.Configure<T>`) with the **Modern Approach** (`AddOptions<T>().BindConfiguration(...).ValidateDataAnnotations().ValidateOnStart()`).
- Why `.ValidateOnStart()` (colloquially known as `.ValidateOnStartup()`) is essential for cloud-native / fail-fast architecture.
- The real differences between `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>` (and when your app will silently break if you choose the wrong one).
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

# Section I - Working with IOptions&lt;SomeConfigOpts&gt;: Legacy vs. Modern Approach

The **Options Pattern** replaces magic strings and global `IConfiguration` dictionaries with strongly typed Plain Old CLR Objects (POCOs).

Consider a typical external service configuration:

```csharp
public class SomeConfigOpts
{
    public const string SectionName = "SomeConfig";

    [Required(ErrorMessage = "SomeConfig:ApiKey is required.")]
    public string ApiKey { get; set; } = string.Empty;

    [Url(ErrorMessage = "SomeConfig:Endpoint must be a valid URL.")]
    public string Endpoint { get; set; } = string.Empty;

    [Range(1, 60, ErrorMessage = "SomeConfig:TimeoutSeconds must be between 1 and 60.")]
    public int TimeoutSeconds { get; set; } = 30;
}
```

How you register and consume this options class in your Dependency Injection container dictates whether your application is bulletproof or an accident waiting to happen.

---

### The Legacy Approach: `services.Configure<SomeConfigOpts>(...)`

In older .NET tutorials and legacy codebases, registration usually looks like this:

```csharp
// Legacy / Traditional Approach
builder.Services.Configure<SomeConfigOpts>(
    builder.Configuration.GetSection(SomeConfigOpts.SectionName));
```

#### Why the Legacy Approach is Risky:
* **Lazy Evaluation**: Options binding is executed *lazily*. The JSON section is not bound or checked when the application boots up; it is only parsed when a service first asks for `IOptions<SomeConfigOpts>.Value`.
* **Zero Startup Validation**: If `SomeConfig:ApiKey` is missing or `TimeoutSeconds` is `"invalid_number"`, the application boots without errors, registers as healthy in Kubernetes, and passes readiness probes. Then, the first customer who triggers `OrderProcessingService` gets a 500 internal server error.
* **Stringly Section Lookups**: Requires passing `builder.Configuration.GetSection("SomeConfig")` manually.

---

### The Modern Approach: `AddOptions<T>()` + `.BindConfiguration()` + `.ValidateOnStart()`

Modern .NET (introduced through .NET 6/8+) offers a fluent options builder designed for cloud-native, fail-fast resilience:

```csharp
// Modern .NET Approach (Fluent, Validated, and Fail-Fast)
builder.Services.AddOptions<SomeConfigOpts>()
    .BindConfiguration(SomeConfigOpts.SectionName)
    .ValidateDataAnnotations()
    .Validate(opts => !string.IsNullOrWhiteSpace(opts.ApiKey), "ApiKey cannot be empty or whitespace.")
    .ValidateOnStart(); // <-- The Game Changer: Validate on startup!
```

> [!NOTE]
> In official .NET BCL terminology, the method is named `.ValidateOnStart()`. In developer discussions and community extensions, it is frequently referred to as **`.ValidateOnStartup()`**. Both describe the same ethos: validating options during host boot rather than deferred execution.

#### Why the Modern Approach is Superior:
1. **`.BindConfiguration(sectionName)`**: Eliminates the boilerplate `builder.Configuration.GetSection(...)`.
2. **`.ValidateDataAnnotations()`**: Automatically evaluates attributes like `[Required]`, `[Range]`, `[Url]`, `[EmailAddress]`, etc.
3. **`.ValidateOnStart()`**: Hooks into the host lifecycle (`IHostedService` startup). Before the HTTP server opens port 80/443 to accept requests, .NET instantiates and validates the options. If the configuration is missing or invalid, an `OptionsValidationException` is thrown immediately at startup, preventing a broken container from ever receiving live traffic.

---

### Side-by-Side Contrast

| Concern | Legacy Approach (`services.Configure<T>`) | Modern Approach (`AddOptions<T>().ValidateOnStart()`) |
| :--- | :--- | :--- |
| **Validation Timing** | Lazy (at point of first consumption) | **Host Boot (Fail-Fast at startup)** |
| **Syntax** | `Configure(config.GetSection("Key"))` | `AddOptions<T>().BindConfiguration("Key")` |
| **Data Annotations** | Ignored by default | Enforced via `.ValidateDataAnnotations()` |
| **Custom Rules** | Manual checks scattered in services | Declarative via `.Validate(opts => ...)` |
| **Deployment Safety** | Corrupt config deploys & crashes during user requests | **Container fails startup probe immediately** |

---

### Consuming `IOptions<SomeConfigOpts>` in Services

Once registered, inject `IOptions<SomeConfigOpts>` directly into your services using modern C# primary constructor syntax:

```csharp
public class OrderProcessingService(
    IOptions<SomeConfigOpts> options, 
    ILogger<OrderProcessingService> logger)
{
    // Extracted once or read as needed; guaranteed valid because ValidateOnStart ran!
    private readonly SomeConfigOpts _config = options.Value;

    public void ProcessOrder()
    {
        logger.LogInformation("Processing with endpoint: {Endpoint} (Timeout: {Timeout}s)",
            _config.Endpoint, _config.TimeoutSeconds);
    }
}
```

In your unit tests, you never have to mock `IConfiguration`:
```csharp
var testOpts = Options.Create(new SomeConfigOpts 
{ 
    ApiKey = "test_key", 
    Endpoint = "https://test.local", 
    TimeoutSeconds = 10 
});
var service = new OrderProcessingService(testOpts, loggerMock.Object);
```

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

# Section III - Advanced Options Validation (Rules & Fail-Fast Mechanics)

Beyond basic Data Annotations, real-world options often require business logic validation or cross-property dependencies.

### ⚠️ Trap Alert: The `required` Keyword vs. `[Required]` / `[Range]` Decorators

A natural question in modern C# is:
*"Can we replace `[Required]` and `[Range]` attributes with C# 11's `required` modifier on the POCO?"*

```csharp
// Can we just replace the attributes with the required keyword?
public class DatabaseOptions
{
    public required string ConnectionString { get; set; }
    public required int TimeoutSeconds { get; set; }
}
```

The short answer: **No, they operate at completely different boundaries.**

1. **Compile-Time vs. Runtime Binding**:
   - The C# `required` keyword is a **compile-time compiler check** enforced when code manually initializes an object using an object initializer (`new DatabaseOptions { ... }`).
   - The .NET Configuration Binder instantiates options dynamically at runtime using reflection (`Activator.CreateInstance`). Reflection completely bypasses the compiler's `required` check!
   - `.ValidateDataAnnotations()` only evaluates `ValidationAttribute` subclasses (like `[RequiredAttribute]`). If `ConnectionString` is omitted from `appsettings.json`, with the `required` keyword alone, `ConnectionString` remains `null` at runtime—and `.ValidateOnStart()` will not catch it!
2. **Value & Range Validation**:
   - The `required` modifier only guarantees that a property was assigned during construction—it has no concept of validation rules (e.g., verifying `1 <= TimeoutSeconds <= 300`, regex matching, or URL validation).
   - For value types like `int`, the default value (`0`) satisfies initialization, but directly violates business constraints.

#### The Pro-Move: Combine Both!
```csharp
public class DatabaseOptions
{
    // 'required' enforces compile-time initialization in your C# unit tests
    // '[Required]' enforces runtime validation by the .NET configuration binder & ValidateOnStart
    [Required(ErrorMessage = "ConnectionString cannot be empty.")]
    public required string ConnectionString { get; set; }

    // Range cannot be expressed with the required keyword
    [Range(1, 300, ErrorMessage = "TimeoutSeconds must be between 1 and 300.")]
    public int TimeoutSeconds { get; set; } = 30;
}
```

### Fluent Custom Validation

```csharp
builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection("Database"))
    .ValidateDataAnnotations()
    // Custom inline validation predicate:
    .Validate(opts => opts.ConnectionString.Contains("Server"), "ConnectionString must specify a 'Server' property.")
    .Validate(opts => opts.TimeoutSeconds > 0, "TimeoutSeconds must be strictly positive.")
    .ValidateOnStart();
```

### Implementing `IValidateOptions<T>`

For complex validation logic (e.g., regex, external verification, or extensive multi-property rules), create a dedicated validator class:

```csharp
public class SomeConfigOptsValidator : IValidateOptions<SomeConfigOpts>
{
    public ValidateOptionsResult Validate(string? name, SomeConfigOpts options)
    {
        if (options.TimeoutSeconds > 30 && options.Endpoint.Contains("sandbox"))
        {
            return ValidateOptionsResult.Fail("Sandbox endpoints cannot have a timeout greater than 30s.");
        }

        return ValidateOptionsResult.Success;
    }
}

// In DI registration:
services.AddSingleton<IValidateOptions<SomeConfigOpts>, SomeConfigOptsValidator>();
```

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

Here is the complete implementation demonstrating `IOptions<SomeConfigOpts>`, modern `.ValidateOnStart()`, live reloading with `IOptionsMonitor` and `IOptionsSnapshot`, Named Options, and startup exception handling:

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
    ["SomeConfig:ApiKey"] = "caveman_live_api_key_12345",
    ["SomeConfig:Endpoint"] = "https://api.caveman.local/v1",
    ["SomeConfig:TimeoutSeconds"] = "15",
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

// Register IConfiguration into DI (enables .BindConfiguration(...))
services.AddSingleton<IConfiguration>(configuration);

// --- Modern Approach: AddOptions + BindConfiguration + ValidateDataAnnotations + ValidateOnStart ---
services.AddOptions<SomeConfigOpts>()
    .BindConfiguration(SomeConfigOpts.SectionName)
    .ValidateDataAnnotations()
    .Validate(opts => !string.IsNullOrWhiteSpace(opts.ApiKey), "ApiKey cannot be empty.")
    .ValidateOnStart(); // Fail-fast at startup!

// Register DatabaseOptions bound to the "Database" section
services.Configure<DatabaseOptions>(configuration.GetSection("Database"));

// Register FeatureFlagsOptions bound to the "Features" section
services.Configure<FeatureFlagsOptions>(configuration.GetSection("Features"));

// Register Named Options: multiple configurations of the same type under unique names
services.Configure<PaymentGatewayOptions>("Stripe", configuration.GetSection("PaymentGateways:Stripe"));
services.Configure<PaymentGatewayOptions>("PayPal", configuration.GetSection("PaymentGateways:PayPal"));

// Post-Configuration: normalize values or apply defaults after binding
services.PostConfigure<DatabaseOptions>(opts =>
{
    if (!opts.ConnectionString.EndsWith(";"))
    {
        opts.ConnectionString += ";";
    }
});

// Register consumer service
services.AddTransient<OrderProcessingService>();

var provider = services.BuildServiceProvider();

// Demo 1: Working with IOptions<SomeConfigOpts> (Modern Approach Contrast)
Console.WriteLine("--- Demo 1: Working with IOptions<SomeConfigOpts> (Modern .ValidateOnStart) ---");
var orderService = provider.GetRequiredService<OrderProcessingService>();
orderService.ProcessOrder();

// Demo 2: The Lifetime Duel (Reloading Config)
Console.WriteLine("\n--- Demo 2: The Lifetime Duel (Reloading Config) ---");
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
3. **Resilience & Fail-Fast**: Embracing the modern `.AddOptions<T>().ValidateOnStart()` approach guarantees that invalid configurations fail immediately at boot time rather than blowing up during production traffic.

As software development moves faster and AI assistants generate more boilerplate, understanding these foundational mechanics ensures you spot subtle configuration lifetime bugs before they hit production.

---

# Further Reading

- [Options pattern in .NET (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [Options validation in .NET (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/core/extensions/options-validation)
- [Configuration in .NET (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
