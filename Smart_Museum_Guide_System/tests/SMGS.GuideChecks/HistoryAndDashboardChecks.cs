using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.ArtifactContent.Request;
using SMGS.Application.DTOs.ArtifactContent.Response;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Dashboard;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;
using SMGS.Application.Services;
using SMGS.Domain.Constants;

internal static class HistoryAndDashboardChecks
{
    public static async Task RunAsync()
    {
        var checks = 0;
        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new Exception("FAIL: " + message);
        }

        var visitorId = Guid.NewGuid();
        var historyStore = new InMemoryGuideHistoryFake();
        var contentStub = new MultiArtifactContentStub();
        var modelStub = new EchoGuideModelStub();
        var guideService = new ArtifactGuideService(contentStub, modelStub, historyStore);

        // 1. First question records history
        var ask1 = await guideService.AskAsync(17, visitorId, new GuideMessageRequest
        {
            Question = "Trống đồng có hoa văn gì?"
        });
        Check(ask1.StatusCode == 200 && ask1.Data?.Supported == true && historyStore.Records.Count == 1,
            "First question recorded in history store.");

        // 2. Follow-up question with UseSavedHistory = true loads previous exchange automatically
        var ask2 = await guideService.AskAsync(17, visitorId, new GuideMessageRequest
        {
            Question = "Nó tìm thấy ở đâu?",
            UseSavedHistory = true
        });
        Check(ask2.StatusCode == 200
            && modelStub.LastHistory.Count == 2
            && modelStub.LastHistory[0].Role == "user"
            && modelStub.LastHistory[0].Text == "Trống đồng có hoa văn gì?"
            && historyStore.Records.Count == 2,
            "UseSavedHistory loaded previous turns from history store.");

        // 3. Unsupported question recorded with Supported = false
        modelStub.NextSupported = false;
        var ask3 = await guideService.AskAsync(20, visitorId, new GuideMessageRequest
        {
            Question = "Giá bán hiện vật này bao nhiêu?"
        });
        Check(ask3.StatusCode == 200 && ask3.Data?.Supported == false && historyStore.Records.Count == 3 && !historyStore.Records.Last().Supported,
            "Unsupported answer recorded with Supported = false.");

        // 4. Controller action checks for ArtifactGuideController, GuideConversationsController & DashboardController
        var guideController = new ArtifactGuideController(guideService, historyStore)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = BuildHttpContext(visitorId, RoleNames.Visitor)
            }
        };

        var getHistoryResult = await guideController.GetHistory(17) as ObjectResult;
        var historyResponse = getHistoryResult?.Value as ResponseDto<IReadOnlyList<GuideChatMessageDto>>;
        Check(getHistoryResult?.StatusCode == 200 && historyResponse?.Data?.Count == 2,
            "ArtifactGuideController.GetHistory returns saved messages for artifact 17.");

        var convController = new GuideConversationsController(historyStore)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = BuildHttpContext(visitorId, RoleNames.Visitor)
            }
        };

        var convResult = await convController.GetMyConversations() as ObjectResult;
        var convResponse = convResult?.Value as ResponseDto<PaginatedResponseDto<GuideConversationSummaryDto>>;
        Check(convResult?.StatusCode == 200 && convResponse?.Data?.TotalCount == 2,
            "GuideConversationsController.GetMyConversations returns 2 artifact conversations.");

        var feedResult = await convController.GetMyMessagesFeed() as ObjectResult;
        var feedResponse = feedResult?.Value as ResponseDto<PaginatedResponseDto<GuideChatMessageDto>>;
        Check(feedResult?.StatusCode == 200 && feedResponse?.Data?.TotalCount == 3,
            "GuideConversationsController.GetMyMessagesFeed returns all 3 messages across artifacts.");

        var clearResult = await guideController.ClearHistory(17) as ObjectResult;
        var clearResponse = clearResult?.Value as ResponseDto<int>;
        Check(clearResult?.StatusCode == 200 && clearResponse?.Data == 2,
            "ArtifactGuideController.ClearHistory clears 2 messages for artifact 17.");

        var dashFake = new DashboardServiceFake();
        var dashController = new DashboardController(dashFake)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = BuildHttpContext(Guid.NewGuid(), RoleNames.Admin)
            }
        };
        Check((await dashController.GetAdminDashboard() as ObjectResult)?.StatusCode == 200, "DashboardController.GetAdminDashboard returns 200.");
        Check((await dashController.GetSystemStaffDashboard() as ObjectResult)?.StatusCode == 200, "DashboardController.GetSystemStaffDashboard returns 200.");
        Check((await dashController.GetMuseumDashboard(1) as ObjectResult)?.StatusCode == 200, "DashboardController.GetMuseumDashboard returns 200.");
    }

    private static HttpContext BuildHttpContext(Guid userId, string role)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "test"));
        return ctx;
    }

    private sealed class InMemoryGuideHistoryFake : IGuideHistoryService
    {
        public readonly List<GuideChatMessageDto> Records = [];

        public Task RecordExchangeAsync(Guid userId, int artifactId, int museumId, string question, string answer, bool supported, GuideSource? source, CancellationToken cancellationToken = default)
        {
            Records.Add(new GuideChatMessageDto(
                Records.Count + 1,
                artifactId,
                "Hiện vật #" + artifactId,
                museumId,
                "Bảo tàng #" + museumId,
                question,
                answer,
                supported,
                supported && source is not null ? [source] : [],
                DateTime.UtcNow));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<GuideTurn>> GetRecentTurnsAsync(Guid userId, int artifactId, int maxExchanges = 4, CancellationToken cancellationToken = default)
        {
            var turns = new List<GuideTurn>();
            foreach (var r in Records.Where(x => x.ArtifactId == artifactId).TakeLast(maxExchanges))
            {
                turns.Add(new GuideTurn("user", r.Question));
                turns.Add(new GuideTurn("assistant", r.Answer));
            }
            return Task.FromResult<IReadOnlyList<GuideTurn>>(turns);
        }

        public Task<ResponseDto<IReadOnlyList<GuideChatMessageDto>>> GetArtifactHistoryAsync(int artifactId, Guid userId, int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult(ResponseDto<IReadOnlyList<GuideChatMessageDto>>.Success(Records.Where(x => x.ArtifactId == artifactId).ToList()));

        public Task<ResponseDto<int>> ClearArtifactHistoryAsync(int artifactId, Guid userId, CancellationToken cancellationToken = default)
        {
            var removed = Records.RemoveAll(x => x.ArtifactId == artifactId);
            return Task.FromResult(ResponseDto<int>.Success(removed));
        }

        public Task<ResponseDto<PaginatedResponseDto<GuideConversationSummaryDto>>> GetMyConversationsAsync(Guid userId, int? museumId = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        {
            var items = Records
                .GroupBy(x => x.ArtifactId)
                .Select(g =>
                {
                    var last = g.Last();
                    return new GuideConversationSummaryDto(g.Key, last.ArtifactName, last.MuseumId, last.MuseumName, g.Count(), last.Question, last.Answer, last.Supported, last.CreatedAt);
                })
                .ToList();
            return Task.FromResult(ResponseDto<PaginatedResponseDto<GuideConversationSummaryDto>>.Success(new()
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = items.Count
            }));
        }

        public Task<ResponseDto<PaginatedResponseDto<GuideChatMessageDto>>> GetMyMessagesFeedAsync(Guid userId, int? museumId = null, int? artifactId = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
            => Task.FromResult(ResponseDto<PaginatedResponseDto<GuideChatMessageDto>>.Success(new()
            {
                Items = Records.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = Records.Count
            }));

        public Task<ResponseDto<PaginatedResponseDto<GuideAdminLogDto>>> GetLogsAsync(int? museumId, int? artifactId, bool? supported, int page, int pageSize, Guid callerId, bool isAdminOrSystemStaff, CancellationToken cancellationToken = default)
            => Task.FromResult(ResponseDto<PaginatedResponseDto<GuideAdminLogDto>>.Success(new()));
    }

    private sealed class MultiArtifactContentStub : IArtifactContentService
    {
        public Task<ResponseDto<ArtifactContentResponseDto>> GetPublishedAsync(int artifactId, string languageCode, Guid? userId = null) =>
            Task.FromResult(ResponseDto<ArtifactContentResponseDto>.Success(new ArtifactContentResponseDto
            {
                Id = 100 + artifactId,
                ArtifactId = artifactId,
                MuseumId = artifactId == 20 ? 2 : 1,
                ArtifactName = "Hiện vật #" + artifactId,
                Title = "Nội dung hiện vật #" + artifactId,
                Description = "Mô tả chi tiết hiện vật đã được Curator phê duyệt.",
                NarrationText = "Bài thuyết minh hiện vật.",
                VersionNo = 1,
                LanguageCode = "vi",
                Status = ContentStatuses.Published,
                IsCurrent = true
            }));

        public Task<ResponseDto<PaginatedResponseDto<ArtifactContentResponseDto>>> GetAllAsync(int? m, int? a, string? s, int p, int n, Guid? scopeUserId = null) => throw new NotSupportedException();
        public Task<ResponseDto<ArtifactContentResponseDto>> GetByIdAsync(int id) => throw new NotSupportedException();
        public Task<ResponseDto<List<ArtifactContentResponseDto>>> GetAllPublishedByArtifactAsync(int a, Guid? u = null) => throw new NotSupportedException();
        public Task<ResponseDto<List<ArtifactContentResponseDto>>> GetVersionHistoryAsync(int a, string? l = null) => throw new NotSupportedException();
        public Task<ResponseDto<ArtifactContentResponseDto>> SubmitAsync(int a, SubmitArtifactContentRequestDto d, Guid u) => throw new NotSupportedException();
        public Task<ResponseDto<ArtifactContentResponseDto>> ReviewAsync(int a, ReviewArtifactContentRequestDto d, Guid u, bool approve) => throw new NotSupportedException();
    }

    private sealed class EchoGuideModelStub : IArtifactGuideModel
    {
        public bool NextSupported = true;
        public IReadOnlyList<GuideTurn> LastHistory = [];

        public Task<GuideModelAnswer> AnswerAsync(GuideContext context, string question, IReadOnlyList<GuideTurn> history, CancellationToken cancellationToken = default)
        {
            LastHistory = history;
            return Task.FromResult(new GuideModelAnswer("Câu trả lời cho: " + question, NextSupported));
        }
    }

    private sealed class DashboardServiceFake : IDashboardService
    {
        public Task<ResponseDto<AdminDashboardDto>> GetAdminDashboardAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default)
            => Task.FromResult(ResponseDto<AdminDashboardDto>.Success(null!));

        public Task<ResponseDto<SystemStaffDashboardDto>> GetSystemStaffDashboardAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default)
            => Task.FromResult(ResponseDto<SystemStaffDashboardDto>.Success(null!));

        public Task<ResponseDto<MuseumDashboardDto>> GetMuseumDashboardAsync(int museumId, Guid callerId, bool isAdmin, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default)
            => Task.FromResult(ResponseDto<MuseumDashboardDto>.Success(null!));
    }
}
