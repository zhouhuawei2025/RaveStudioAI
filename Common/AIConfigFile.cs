namespace RaveStudioAI.Common;

public sealed class AIConfigFile
{
    public string SelectedProfile { get; set; } = string.Empty;
    public List<AIConfigProfile> Profiles { get; set; } = [];
}
