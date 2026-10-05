namespace ClubStack.Domain.Credentials;

/// <summary>
/// Credential lifecycle states (Phase 3 §16). The canonical list is kept exactly
/// as specified; after a replacement the superseded credential reaches
/// <see cref="Replaced"/> (Phase 8 §7.4), which Phase 3 also describes as the
/// retired terminal state. A credential has exactly one authoritative current
/// state at any time.
/// </summary>
public enum CredentialStatus
{
    Issued,
    Active,
    Suspended,
    Lost,
    Stolen,
    Expired,
    Replaced,
}

/// <summary>
/// Credential medium (Phase 3 §15). The initial product must support magnetic
/// stripe (CRED-002); barcode/QR/RFID/NFC and digital credentials are explicitly
/// future types that map onto the same credential model (Phase 0 §9.3) and are
/// therefore part of the type catalogue now.
/// </summary>
public enum CredentialType
{
    MagneticStripe,
    Barcode,
    QrCode,
    Rfid,
    NearField,
    Digital,
}