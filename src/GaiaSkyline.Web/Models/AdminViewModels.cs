using System.ComponentModel.DataAnnotations;

namespace GaiaSkyline.Web.Models;

public sealed class AdminLoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public sealed class TwoFactorViewModel
{
    [Required]
    [Display(Name = "Code")]
    public string Code { get; set; } = string.Empty;

    public bool IsRecoveryCode { get; set; }
}

public sealed class EnrollTwoFactorViewModel
{
    /// <summary>The TOTP secret, formatted in groups for manual entry.</summary>
    public string SharedKey { get; set; } = string.Empty;

    /// <summary>Inline SVG QR code for the otpauth URI.</summary>
    public string QrSvg { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Verification code")]
    public string Code { get; set; } = string.Empty;
}
