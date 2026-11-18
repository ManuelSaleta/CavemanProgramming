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

// -------------------------------------------------------------------------------------------------
// 1. Initial In-Memory Configuration
// -------------------------------------------------------------------------------------------------
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

// -------------------------------------------------------------------------------------------------
// 2. Service Registration & Options Setup
// -------------------------------------------------------------------------------------------------
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

// Register a consuming consumer service demonstrating injection
services.AddTransient<OrderProcessingService>();

var provider = services.BuildServiceProvider();

// -------------------------------------------------------------------------------------------------
// Demo 1: Working with IOptions<SomeConfigOpts> (Modern Approach Contrast)
// -------------------------------------------------------------------------------------------------
Console.WriteLine("--- Demo 1: Working with IOptions<SomeConfigOpts> (Modern .ValidateOnStart) ---");
var orderService = provider.GetRequiredService<OrderProcessingService>();
orderService.ProcessOrder();

// -------------------------------------------------------------------------------------------------
// Demo 2: The Lifetime Duel (IOptions vs IOptionsSnapshot vs IOptionsMonitor)
// -------------------------------------------------------------------------------------------------
Console.WriteLine("\n--- Demo 2: The Lifetime Duel (Reloading Config) ---");

var options = provider.GetRequiredService<IOptions<FeatureFlagsOptions>>();
var monitor = provider.GetRequiredService<IOptionsMonitor<FeatureFlagsOptions>>();

// Register an OnChange callback on IOptionsMonitor to react immediately when config changes
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

// Simulating a live configuration reload (e.g. appsettings.json edited in production)
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

// -------------------------------------------------------------------------------------------------
// Demo 3: Named Options (Multi-provider / Multi-tenant configurations)
// -------------------------------------------------------------------------------------------------
Console.WriteLine("--- Demo 3: Named Options ---");
var gatewayMonitor = provider.GetRequiredService<IOptionsMonitor<PaymentGatewayOptions>>();

var stripeOpts = gatewayMonitor.Get("Stripe");
var paypalOpts = gatewayMonitor.Get("PayPal");

Console.WriteLine($"Stripe Config: ApiKey={stripeOpts.ApiKey}, Timeout={stripeOpts.TimeoutSeconds}s");
Console.WriteLine($"PayPal Config: ApiKey={paypalOpts.ApiKey}, Timeout={paypalOpts.TimeoutSeconds}s\n");

// -------------------------------------------------------------------------------------------------
// Demo 4: Options Validation (Fail-Fast with ValidateOnStart)
// -------------------------------------------------------------------------------------------------
Console.WriteLine("--- Demo 4: Options Validation & ValidateOnStart ---");

var invalidConfigData = new Dictionary<string, string?>
{
    ["Database:ConnectionString"] = "", // Invalid: [Required]
    ["Database:TimeoutSeconds"] = "-99" // Invalid: [Range(1, 300)]
};

var invalidConfig = new ConfigurationBuilder()
    .AddInMemoryCollection(invalidConfigData)
    .Build();

var validationServices = new ServiceCollection();

// ValidateDataAnnotations + Custom Delegate + ValidateOnStart
validationServices.AddOptions<DatabaseOptions>()
    .Bind(invalidConfig.GetSection("Database"))
    .ValidateDataAnnotations()
    .Validate(opts => opts.TimeoutSeconds > 0, "Timeout must be greater than zero.")
    .ValidateOnStart();

var validationProvider = validationServices.BuildServiceProvider();

try
{
    // Accessing options or starting host triggers validation
    Console.WriteLine("Attempting to access invalid DatabaseOptions...");
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

Console.WriteLine("\n==================================================");
Console.WriteLine(" All Options Pattern Demos Completed Cleanly!     ");
Console.WriteLine("==================================================");

// -------------------------------------------------------------------------------------------------
// Option Models (POCOs) & Services
// -------------------------------------------------------------------------------------------------
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

public class OrderProcessingService(IOptions<SomeConfigOpts> options)
{
    private readonly SomeConfigOpts _config = options.Value;

    public void ProcessOrder()
    {
        Console.WriteLine($"[OrderProcessingService] Injected IOptions<SomeConfigOpts>:");
        Console.WriteLine($"  Endpoint:       {_config.Endpoint}");
        Console.WriteLine($"  ApiKey:         {_config.ApiKey}");
        Console.WriteLine($"  TimeoutSeconds: {_config.TimeoutSeconds}s");
    }
}

public class DatabaseOptions
{
    [Required(ErrorMessage = "Database:ConnectionString is required and cannot be empty.")]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 300, ErrorMessage = "Database:TimeoutSeconds must be between 1 and 300.")]
    public int TimeoutSeconds { get; set; } = 30;
}

public class FeatureFlagsOptions
{
    public string BetaTheme { get; set; } = "Disabled";
}

public class PaymentGatewayOptions
{
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 15;
}
