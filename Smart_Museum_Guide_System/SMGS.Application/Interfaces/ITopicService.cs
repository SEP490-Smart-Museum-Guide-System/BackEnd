using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Topic;

namespace SMGS.Application.Interfaces;

public interface ITopicService
{
    Task<ResponseDto<PaginatedResponseDto<TopicResponseDto>>> GetAllAsync(
        int? museumId, string? keyword, bool? isActive, int page, int pageSize);

    Task<ResponseDto<TopicResponseDto>> GetByIdAsync(int id);

    Task<ResponseDto<TopicResponseDto>> CreateAsync(CreateTopicRequestDto dto, Guid userId, bool isAdmin);

    Task<ResponseDto<TopicResponseDto>> UpdateAsync(int id, UpdateTopicRequestDto dto, Guid userId, bool isAdmin);

    Task<ResponseDto<object>> DeleteAsync(int id, Guid userId, bool isAdmin);

    Task<ResponseDto<List<ArtifactTopicTagDto>>> GetArtifactTopicsAsync(int artifactId);

    Task<ResponseDto<List<ArtifactTopicTagDto>>> AssignTopicsAsync(
        int artifactId, IReadOnlyCollection<int> topicIds, Guid userId, bool isAdmin);

    Task<ResponseDto<List<ArtifactTopicTagDto>>> SetArtifactTopicsAsync(
        int artifactId, IReadOnlyCollection<int> topicIds, Guid userId, bool isAdmin);

    Task<ResponseDto<object>> RemoveTopicFromArtifactAsync(
        int artifactId, int topicId, Guid userId, bool isAdmin);
}

