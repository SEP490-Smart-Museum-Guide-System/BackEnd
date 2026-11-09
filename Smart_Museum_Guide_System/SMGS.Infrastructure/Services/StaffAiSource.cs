using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class StaffAiSourceReader(ApplicationDbContext db, IArtifactContentRepository contents) : IStaffAiSource
{
    public async Task<StaffAiSource> GetAsync(int artifactId, Guid user, bool admin, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var artifact = await ExperienceAccess.Artifact(db, artifactId, user, admin);
        var published = await contents.GetPublishedAsync(artifactId, "vi");
        return new(artifact.Id, artifact.Name, published?.Id, published?.VersionNo,
            published is null ? "" : string.Join("\n", new[] { published.Description, published.NarrationText }.Where(s => !string.IsNullOrWhiteSpace(s))));
    }
}
