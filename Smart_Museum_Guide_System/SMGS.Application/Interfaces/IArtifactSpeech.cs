namespace SMGS.Application.Interfaces;

public record SpeechAudio(byte[] Bytes);

public interface IArtifactSpeech
{
    Task<SpeechAudio> GenerateAsync(string text, CancellationToken cancellationToken = default);
}
