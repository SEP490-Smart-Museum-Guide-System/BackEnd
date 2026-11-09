using System.Text.Json;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;

namespace SMGS.Application.Services;

public class StaffAiService(IStaffAiSource source, IStaffAiModel model) : IStaffAiService
{
    public async Task<ResponseDto<StaffAiResult>> GenerateAsync(int artifactId, Guid user, bool admin, StaffAiRequest r, CancellationToken cancellationToken = default)
    {
        if (user == Guid.Empty) return ResponseDto<StaffAiResult>.Fail("Vui lòng đăng nhập.", 401);
        if (artifactId <= 0 || r.Mode is not ("narration" or "quiz") || r.SourceText is null || r.SourceText.Length > 6000
            || r.Audience is not ("children" or "general" or "scholar") || r.Tone is not ("vivid" or "inspiring" or "academic")
            || r.Difficulty is not ("easy" or "medium" or "hard") || r.ExcludeQuestions is null || r.ExcludeQuestions.Length > 5
            || r.ExcludeQuestions.Any(q => string.IsNullOrWhiteSpace(q) || q.Length > 1000))
            return ResponseDto<StaffAiResult>.Fail("Cấu hình tạo bản nháp không hợp lệ.");
        cancellationToken.ThrowIfCancellationRequested();
        // Access is checked before reading any unpublished/private source or contacting the model.
        var saved = await source.GetAsync(artifactId, user, admin, cancellationToken);
        if (saved.ArtifactId != artifactId) return ResponseDto<StaffAiResult>.Fail("Không tìm thấy hiện vật.", 404);
        var text = string.Join("\n\n", new[] { saved.Text, r.SourceText.Trim() }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (text.Length < 20) return ResponseDto<StaffAiResult>.Fail("Cần nội dung đã xuất bản hoặc tư liệu nguồn ít nhất 20 ký tự. Ảnh và tên hiện vật chưa đủ để viết lịch sử.", 422);
        if (text.Length > 12000) return ResponseDto<StaffAiResult>.Fail("Tư liệu quá dài. Hãy chọn đoạn nguồn ngắn hơn để tạo bản nháp.", 422);
        var used = saved with { Text = text };
        try
        {
            var draft = await model.GenerateAsync(new(artifactId, saved.ArtifactName, text, r.Mode, r.Audience, r.Tone, r.Difficulty, r.ExcludeQuestions), cancellationToken);
            ValidateDraft(draft, r.Mode);
            return ResponseDto<StaffAiResult>.Success(new(artifactId, r.Mode, "vi", true, used, draft), "Đã tạo bản nháp AI. Cần kiểm tra và thẩm định trước khi xuất bản.");
        }
        catch (GuideException e) { return ResponseDto<StaffAiResult>.Fail(e.Message, e.StatusCode); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { return ResponseDto<StaffAiResult>.Fail("Bản nháp AI không hợp lệ. Hãy kiểm tra tư liệu và tạo lại.", 502); }
    }

    public static void ValidateDraft(JsonElement draft, string mode)
    {
        var expected = mode == "narration" ? new[] { "title", "narrationText", "evidenceQuotes" }
            : new[] { "question", "options", "correctIndex", "explanation", "evidenceQuote" };
        var keys = draft.EnumerateObject().Select(p => p.Name).ToArray();
        if (keys.Length != expected.Length || !new HashSet<string>(keys).SetEquals(expected)) throw new JsonException();
        static string Text(JsonElement parent, string key, int min, int max)
        {
            var value = parent.GetProperty(key).GetString();
            if (value is null || value.Trim().Length < min || value.Length > max) throw new JsonException();
            return value;
        }
        if (mode == "narration")
        {
            Text(draft, "title", 1, 200); Text(draft, "narrationText", 20, 3000);
            var quotes = draft.GetProperty("evidenceQuotes");
            if (quotes.GetArrayLength() is < 1 or > 3 || quotes.EnumerateArray().Any(q => q.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(q.GetString()))) throw new JsonException();
        }
        else
        {
            Text(draft, "question", 5, 1000); Text(draft, "explanation", 5, 1500); Text(draft, "evidenceQuote", 6, 1000);
            var options = draft.GetProperty("options"); var index = draft.GetProperty("correctIndex").GetInt32();
            if (options.GetArrayLength() is < 2 or > 4 || index < 0 || index >= options.GetArrayLength()) throw new JsonException();
            var values = options.EnumerateArray().Select(o => o.GetString()?.Trim()).ToArray();
            if (values.Any(o => string.IsNullOrWhiteSpace(o) || o.Length > 300) || values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length) throw new JsonException();
        }
    }
}
