using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Navigation;

namespace SMGS.Application.Interfaces;

public interface IOutdoorRoutingService
{
    Task<ResponseDto<OutdoorRouteResponseDto>> GetRouteAsync(CoordinateDto origin, CoordinateDto destination,
        CancellationToken cancellationToken = default);
}
