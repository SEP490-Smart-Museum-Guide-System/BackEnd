namespace SMGS.Infrastructure.Services;

public class GuideModelOptions
{
    public const string SectionName = "ArtifactGuide";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:8003/";
    public string ServiceKey { get; set; } = "";
}
