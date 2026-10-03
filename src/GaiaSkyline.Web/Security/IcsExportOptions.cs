namespace GaiaSkyline.Web.Security;

/// <summary>The unguessable token embedded in the public ICS export URL (a credential).</summary>
public sealed class IcsExportOptions
{
    public string Token { get; set; } = string.Empty;
}
