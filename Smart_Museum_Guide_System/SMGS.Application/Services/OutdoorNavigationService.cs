using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Navigation;
using SMGS.Application.Interfaces;
using SMGS.Domain.Interfaces;

namespace SMGS.Application.Services;

public class OutdoorNavigationService(IMuseumRepository museums, IOutdoorRoutingService routing)
{
    public async Task<ResponseDto<OutdoorRouteResponseDto>> GetRouteAsync(OutdoorRouteRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.MuseumId < 1 || request.TravelMode != "DRIVE" || request.Origin?.IsValid() != true)
            return ResponseDto<OutdoorRouteResponseDto>.Fail("Chỉ hỗ trợ DRIVE và tọa độ xuất phát hợp lệ.");
        cancellationToken.ThrowIfCancellationRequested();
        var museum = await museums.GetByIdAsync(request.MuseumId, cancellationToken);
        if (museum is null || !museum.IsActive)
            return ResponseDto<OutdoorRouteResponseDto>.Fail("Không tìm thấy bảo tàng.", 404);
        var destination = new CoordinateDto
        {
            Latitude = (double?)museum.Latitude, Longitude = (double?)museum.Longitude
        };
        if (!destination.IsValid())
            return ResponseDto<OutdoorRouteResponseDto>.Fail("Bảo tàng chưa có tọa độ hợp lệ.");
        var response = await routing.GetRouteAsync(request.Origin, destination, cancellationToken);
        if (response.Data is not null) response.Data.MuseumId = museum.Id;
        return response;
    }
}
