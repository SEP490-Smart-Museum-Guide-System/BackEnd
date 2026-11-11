using SMGS.Application.DTOs.Experience;

namespace SMGS.Application.Interfaces;

public interface IPersonalizedTourService
{
    Task<IReadOnlyList<TourInterest>> Interests(int museumId, CancellationToken ct);
    Task<PersonalTourResult> Recommend(PersonalizedTourRequest request, Guid user, CancellationToken ct);
    Task<PersonalTourResult> Save(Guid recommendationId, Guid user, CancellationToken ct);
    Task<PersonalTourResult> Get(Guid id, Guid user, CancellationToken ct);
    Task<IReadOnlyList<PersonalTourResult>> Mine(Guid user, int page, int pageSize, CancellationToken ct);
    Task<PersonalTourResult> ValidateForStart(Guid id, int museumId, Guid user, CancellationToken ct);
}

public interface IPersonalizedTourModel
{
    Task<TourModelResult> Recommend(TourModelRequest request, CancellationToken ct);
}
