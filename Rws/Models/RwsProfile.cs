namespace RaveStudioAI.Rws.Models;

public sealed class RwsTenantProfile
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Dictionary<string, Dictionary<string, List<string>>> Studies { get; set; } = [];
}

public sealed class RwsProfileFile
{
    public string SelectedTenant { get; set; } = string.Empty;
    public Dictionary<string, RwsTenantProfile> Tenants { get; set; } = [];
}
