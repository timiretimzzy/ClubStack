using ClubStack.Hardware.Fiscalization;
using ClubStack.Infrastructure.Fiscalization;

namespace ClubStack.Application.Tests;

/// <summary>
/// Binding a device to an FDMS environment is done by writing two keys into the engine's
/// own <c>config.ini</c>. The engine owns that file and rewrites it through its own save
/// routine, so these updates must be surgical.
///
/// The risk this covers is not a failed write but a successful destructive one: a
/// whole-file rewrite would silently drop the DPAPI-protected values and any key
/// ClubStack does not model, and the device would only fail much later, at fiscalisation.
/// </summary>
public sealed class FiscalStackDeviceConfigFileTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"d55-config-{Guid.NewGuid():N}.ini");

    private const string Original = """
        [Device]
        SERIALNUMBER=FSTK-00000001
        DEVICEID=18084
        COMPANYNAME=Legacy Dev
        ZIMRASERVER=https://fdmsapi.zimra.co.zw
        VERIFICATIONSERVER=https://fdms.zimra.co.zw
        QRURL=https://fdms.zimra.co.zw/vy
        LICENSEKEY=@PROTECTED-BY-DPAPI-OPAQUE-VALUE
        UNMODELLEDKEY=keep-me-too
        """;

    public FiscalStackDeviceConfigFileTests() => File.WriteAllText(_path, Original);

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static Dictionary<string, string?> Bind(FiscalEnvironment environment)
    {
        var (zimraServer, verificationServer) = FiscalEndpoints.For(environment);
        return new Dictionary<string, string?>
        {
            ["ZIMRASERVER"] = zimraServer,
            ["VERIFICATIONSERVER"] = verificationServer,
        };
    }

    [Fact]
    public void Reads_a_value()
    {
        var config = new FiscalStackDeviceConfigFile(_path);

        Assert.Equal("FSTK-00000001", config.Read("SERIALNUMBER"));
    }

    [Fact]
    public void Returns_null_for_an_absent_key()
    {
        var config = new FiscalStackDeviceConfigFile(_path);

        Assert.Null(config.Read("NO_SUCH_KEY"));
    }

    /// <summary>
    /// The whole point of binding an environment: the engine derives its environment,
    /// tax identifiers and endpoint behaviour from these two values.
    /// </summary>
    [Fact]
    public void Binds_the_device_to_the_test_environment()
    {
        var config = new FiscalStackDeviceConfigFile(_path);

        config.SetValues(Bind(FiscalEnvironment.Test));

        Assert.Equal("https://fdmsapitest.zimra.co.zw", config.Read("ZIMRASERVER"));
        Assert.Equal("https://fdmstest.zimra.co.zw", config.Read("VERIFICATIONSERVER"));
    }

    [Fact]
    public void Binds_the_device_to_the_production_environment()
    {
        var config = new FiscalStackDeviceConfigFile(_path);

        config.SetValues(Bind(FiscalEnvironment.Production));

        Assert.Equal("https://fdmsapi.zimra.co.zw", config.Read("ZIMRASERVER"));
        Assert.Equal("https://fdms.zimra.co.zw", config.Read("VERIFICATIONSERVER"));
    }

    [Fact]
    public void Preserves_keys_it_does_not_model()
    {
        var config = new FiscalStackDeviceConfigFile(_path);

        config.SetValues(Bind(FiscalEnvironment.Test));

        var content = File.ReadAllText(_path);

        Assert.Contains("LICENSEKEY=@PROTECTED-BY-DPAPI-OPAQUE-VALUE", content, StringComparison.Ordinal);
        Assert.Contains("UNMODELLEDKEY=keep-me-too", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Preserves_the_device_identity_while_binding_an_environment()
    {
        var config = new FiscalStackDeviceConfigFile(_path);

        config.SetValues(Bind(FiscalEnvironment.Test));

        Assert.Equal("FSTK-00000001", config.Read("SERIALNUMBER"));
        Assert.Equal("18084", config.Read("DEVICEID"));
        Assert.Equal("Legacy Dev", config.Read("COMPANYNAME"));
    }

    [Fact]
    public void Replaces_an_existing_value_rather_than_appending_a_duplicate()
    {
        var config = new FiscalStackDeviceConfigFile(_path);

        config.SetValues(Bind(FiscalEnvironment.Test));

        var occurrences = File.ReadAllLines(_path)
            .Count(l => l.TrimStart().StartsWith("ZIMRASERVER", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(1, occurrences);
    }

    /// <summary>
    /// Provisioning is specified as repeatable: running it twice must reach the same
    /// state, not accumulate configuration drift.
    /// </summary>
    [Fact]
    public void Round_trips_a_rebinding_so_provisioning_is_repeatable()
    {
        var config = new FiscalStackDeviceConfigFile(_path);

        config.SetValues(Bind(FiscalEnvironment.Test));
        var afterFirst = File.ReadAllText(_path);

        config.SetValues(Bind(FiscalEnvironment.Test));

        Assert.Equal(afterFirst, File.ReadAllText(_path));
    }

    /// <summary>
    /// A write that changes nothing must not disturb anything. The writer normalises the
    /// trailing newline, so the comparison is over the significant lines.
    /// </summary>
    [Fact]
    public void Leaves_an_unrelated_file_untouched_when_no_keys_are_supplied()
    {
        var config = new FiscalStackDeviceConfigFile(_path);

        config.SetValues(new Dictionary<string, string?>());

        var expected = Original.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var actual = File.ReadAllText(_path)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(expected, actual);
    }
}
