using System.ComponentModel.DataAnnotations;

namespace GaiaSkyline.Web.Models;

public sealed class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class RegisterViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 12, ErrorMessage = "Use at least 12 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>"Guest" or "Partner" (self-registration is limited to these; never Owner).</summary>
    [Required]
    public string AccountType { get; set; } = "Guest";
}

public sealed class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 12, ErrorMessage = "Use at least 12 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
