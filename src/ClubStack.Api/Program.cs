using ClubStack.Api;

var application = await ApiBootstrapper.CreateApplicationAsync(args);

await application.RunAsync();

/// <summary>Entry-point type used by <c>WebApplicationFactory&lt;Program&gt;</c> in API tests.</summary>
public partial class Program
{
}