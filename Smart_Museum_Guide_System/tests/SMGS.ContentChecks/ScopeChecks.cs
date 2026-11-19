using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.ArtifactContent.Response;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;

internal static class ScopeChecks
{
    public static async Task RunAsync()
    {
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        var create = typeof(MuseumsController).GetMethod("Create")!;
        Check(create.GetCustomAttribute<AuthorizeAttribute>()?.Roles == RoleNames.Admin
            && !create.IsDefined(typeof(AllowAnonymousAttribute)), "Museum creation must require Admin.");
        var userId = Guid.NewGuid();
        bool allowed = false;
        int writes = 0, historyReads = 0, feedbackReads = 0;
        Guid? listScope = null;
        var accounts = Stub<IAccountAdminService>((method, args) => method.Name switch
        {
            "CanManageArtifactAsync" or "CanManageMuseumAsync" => Task.FromResult(allowed || (bool)args![1]!),
            _ => throw new NotSupportedException(method.Name)
        });
        var contents = Stub<IArtifactContentService>((method, args) =>
        {
            var data = ResponseDto<ArtifactContentResponseDto>.Success(new() { Id = 21, ArtifactId = 7, MuseumId = 1 });
            switch (method.Name)
            {
                case "GetByIdAsync": return Task.FromResult(data);
                case "ReviewAsync": case "SubmitAsync": writes++; return Task.FromResult(data);
                case "GetVersionHistoryAsync": historyReads++; return Task.FromResult(ResponseDto<List<ArtifactContentResponseDto>>.Success([]));
                case "GetAllAsync": listScope = (Guid?)args![5]; return Task.FromResult(ResponseDto<PaginatedResponseDto<ArtifactContentResponseDto>>.Success(new()));
                default: throw new NotSupportedException(method.Name);
            }
        });
        T AsUser<T>(T controller, string role) where T : ControllerBase
        {
            controller.ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
                new ClaimsIdentity([new(ClaimTypes.NameIdentifier, userId.ToString()), new(ClaimTypes.Role, role)], "test")) } };
            return controller;
        }
        var review = AsUser(new ArtifactContentsController(contents, accounts), RoleNames.Curator);
        var submissions = AsUser(new ArtifactContentSubmissionsController(contents, accounts), RoleNames.MuseumStaff);
        Check(await review.GetById(21) is ForbidResult, "Unassigned curator must not read a draft by ID.");
        Check(await review.Approve(21, new()) is ForbidResult && writes == 0, "Unassigned curator must not approve.");
        Check(await review.Reject(21, new()) is ForbidResult && writes == 0, "Unassigned curator must not reject.");
        Check(await submissions.Submit(7, new()) is ForbidResult && writes == 0, "Unassigned staff must not submit.");
        Check(await submissions.SubmitManual(7, new()) is ForbidResult && writes == 0, "Manual submission must enforce scope.");
        Check(await submissions.GetVersionHistory(7) is ForbidResult && historyReads == 0, "Unassigned staff must not read version history.");
        await review.GetAll(null, null);
        Check(listScope == userId, "Review list must forward the authenticated identity for database filtering.");
        allowed = true;
        Check(await review.Approve(21, new()) is ObjectResult { StatusCode: 200 } && writes == 1, "Assigned curator can approve.");
        Check(await submissions.Submit(7, new()) is ObjectResult { StatusCode: 200 } && writes == 2, "Assigned staff can submit.");
        allowed = false;
        AsUser(submissions, RoleNames.Admin);
        Check(await submissions.Submit(7, new()) is ObjectResult { StatusCode: 200 } && writes == 3, "Admin can submit across museum assignments.");
        var feedbackService = Stub<IFeedbackService>((method, args) => { feedbackReads++; throw new Exception("Unassigned feedback request reached service."); });
        var feedbackRepo = Stub<IFeedbackRepository>((method, args) => method.Name == "GetByIdAsync"
            ? Task.FromResult<VisitFeedback?>(new() { Visit = new Visit { MuseumId = 2 } })
            : throw new NotSupportedException(method.Name));
        var feedback = AsUser(new FeedbacksController(feedbackService, accounts, feedbackRepo), RoleNames.MuseumStaff);
        Check(await feedback.GetByMuseum(2) is ForbidResult && feedbackReads == 0, "Staff cannot list feedback from an unassigned museum.");
        Check(await feedback.Respond(9, new()) is ForbidResult && feedbackReads == 0, "Staff cannot respond to an unassigned museum feedback.");
        Console.WriteLine("PASS scope regressions: draft disclosure/mutation, scoped pagination identity, Admin submission, feedback access, museum creation.");
    }

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> call) where T : class
    {
        var proxy = DispatchProxy.Create<T, ScopeProxy>();
        ((ScopeProxy)(object)proxy).Call = call;
        return proxy;
    }
}

public class ScopeProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Call = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Call(method!, args);
}
