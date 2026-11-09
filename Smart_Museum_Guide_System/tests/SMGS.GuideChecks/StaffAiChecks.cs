using System.Text.Json;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;
using SMGS.Application.Services;

internal static class StaffAiChecks
{
    public static async Task RunAsync()
    {
        var source = new Source(); var model = new Model(); var service = new StaffAiService(source, model); var user = Guid.NewGuid();
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        var request = new StaffAiRequest { Mode = "narration" };
        Check((await service.GenerateAsync(17, Guid.Empty, false, request)).StatusCode == 401 && model.Calls == 0, "Anonymous draft reached model.");
        source.Deny = true;
        try { await service.GenerateAsync(17, user, false, request); throw new Exception("Scope denied expected."); }
        catch (ExperienceException e) { Check(e.StatusCode == 403 && model.Calls == 0, "Unauthorized source reached model."); }
        source.Deny = false;
        Check((await service.GenerateAsync(17, user, false, request)).StatusCode == 422 && model.Calls == 0, "Name-only artifact invented a history.");
        request.SourceText = "Bình gốm được dùng để đựng nước.";
        var result = await service.GenerateAsync(17, user, false, request);
        Check(result.StatusCode == 200 && result.Data!.IsDraft && result.Data.LanguageCode == "vi" && model.Last!.ArtifactName == "Tên từ DB"
            && model.Last.SourceText.Contains(request.SourceText) && source.LastUser == user && !source.LastAdmin, "Draft identity/source/access missing.");
        source.SavedText = "Nguồn đã xuất bản của bảo tàng.";
        await service.GenerateAsync(17, user, true, request);
        Check(source.LastAdmin && model.Last!.SourceText.Contains(source.SavedText), "Admin source scope or saved content missing.");
        model.Fail = true;
        Check((await service.GenerateAsync(17, user, false, request)).StatusCode == 503, "Model failure became mock output.");
        model.Fail = false; model.Body = "{\"title\":\"T\",\"narrationText\":\"\",\"evidenceQuotes\":[]}";
        Check((await service.GenerateAsync(17, user, false, request)).StatusCode == 502, "Malformed model draft accepted.");
        model.Body = "{\"question\":\"Bình gốm dùng làm gì?\",\"options\":[\"Đựng nước\",\"Dệt vải\"],\"correctIndex\":0,\"explanation\":\"Tư liệu ghi dùng đựng nước.\",\"evidenceQuote\":\"Bình gốm được dùng để đựng nước.\"}";
        request.Mode = "quiz";
        Check((await service.GenerateAsync(17, user, false, request)).Data!.Draft.GetProperty("correctIndex").GetInt32() == 0, "Quiz draft contract lost.");
        model.Body = model.Body.Replace("\"correctIndex\":0", "\"correctIndex\":4");
        Check((await service.GenerateAsync(17, user, false, request)).StatusCode == 502, "Invalid quiz correct index accepted.");
        request.ExcludeQuestions = [new string('x', 1001)];
        Check((await service.GenerateAsync(17, user, false, request)).StatusCode == 400, "Unbounded excluded question accepted.");
        Console.WriteLine("PASS: staff AI source/scope/draft checks; no DB/model calls.");
    }
    private class Source : IStaffAiSource
    {
        public bool Deny; public string SavedText = ""; public Guid LastUser; public bool LastAdmin;
        public Task<StaffAiSource> GetAsync(int id, Guid user, bool admin, CancellationToken token = default)
        {
            if (Deny) throw new ExperienceException("Không được phân công.", 403);
            LastUser = user; LastAdmin = admin;
            return Task.FromResult(new StaffAiSource(id, "Tên từ DB", SavedText.Length > 0 ? 91 : null, SavedText.Length > 0 ? 2 : null, SavedText));
        }
    }
    private class Model : IStaffAiModel
    {
        public int Calls; public bool Fail; public StaffAiModelRequest? Last;
        public string Body = "{\"title\":\"Bình gốm\",\"narrationText\":\"Bình gốm được dùng để đựng nước.\",\"evidenceQuotes\":[\"Bình gốm được dùng để đựng nước.\"]}";
        public Task<JsonElement> GenerateAsync(StaffAiModelRequest data, CancellationToken token = default)
        {
            Calls++; Last = data; if (Fail) throw new GuideException("Model chưa sẵn sàng.");
            using var doc = JsonDocument.Parse(Body); return Task.FromResult(doc.RootElement.Clone());
        }
    }
}
