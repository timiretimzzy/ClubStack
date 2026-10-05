using ClubStack.Hardware.Fiscalization;

namespace ClubStack.Infrastructure.Fiscalization;

/// <summary>
/// Declares which FDMS environment ClubStack's fiscal device is provisioned for, and
/// whether that device is a test device.
///
/// This is the durable statement of intent. The engine records the environment it
/// derived, but it cannot distinguish "a device provisioned as a test device" from "a
/// device currently pointed at test", so the production-safety guard needs this
/// declaration to enforce the second direction of the rule.
/// </summary>
public sealed class FiscalisationDeviceOptions
{
    public const string SectionName = "Fiscalisation:Device";

    /// <summary>The environment this installation's device is bound to.</summary>
    public FiscalEnvironment Environment { get; set; } = FiscalEnvironment.Production;

    /// <summary>True when the configured device is a test device that must never reach live FDMS.</summary>
    public bool IsTestDevice { get; set; }

    /// <summary>Serial number of the provisioned device, used to detect identity drift.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Device id of the provisioned device, used to detect identity drift.</summary>
    public string? DeviceId { get; set; }
}
