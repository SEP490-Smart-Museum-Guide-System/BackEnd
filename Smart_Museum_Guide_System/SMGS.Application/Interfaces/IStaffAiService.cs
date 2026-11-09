using System.Text.Json;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Guide;

namespace SMGS.Application.Interfaces;

public interface IStaffAiService
{
    Task<ResponseDto<StaffAiResult>> GenerateAsync(int artifactId, Guid user, bool admin, StaffAiRequest request, CancellationToken cancellationToken = default);
}
public interface IStaffAiSource
{
    Task<StaffAiSource> GetAsync(int artifactId, Guid user, bool admin, CancellationToken cancellationToken = default);
}
public interface IStaffAiModel
{
    Task<JsonElement> GenerateAsync(StaffAiModelRequest request, CancellationToken cancellationToken = default);
}
