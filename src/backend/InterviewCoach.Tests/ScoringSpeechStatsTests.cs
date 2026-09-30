using FluentAssertions;
using InterviewCoach.Api.Services;
using InterviewCoach.Domain;

namespace InterviewCoach.Tests;

public class ScoringSpeechStatsTests
{
    private readonly ScoringService _scoring = new();

    private static Session SessionWithTranscript(params (long startMs, long endMs, string text)[] segments)
    {
        var session = new Session { Id = Guid.NewGuid() };
        foreach (var (startMs, endMs, text) in segments)
        {
            session.TranscriptSegments.Add(new TranscriptSegment
            {
                SessionId = session.Id,
                StartMs = startMs,
                EndMs = endMs,
                Text = text
            });
        }

        return session;
    }

    private static string Words(int count) => string.Join(' ', Enumerable.Repeat("kelime", count));

    [Fact]
    public void SpeakingRate_WithoutStats_IsDerivedFromTranscript()
    {
        // 140 kelime / 1 dakika konusma: ideal aralikta
        var session = SessionWithTranscript((0, 30_000, Words(70)), (40_000, 70_000, Words(70)));

        var result = _scoring.ComputeScoreCard(session, [], null);

        result.SpeakingRateScore.Should().Be(100);
    }

    [Fact]
    public void SpeakingRate_SlowSpeechFromTranscript_ReturnsLowerScore()
    {
        var session = SessionWithTranscript((0, 60_000, Words(60)));

        var result = _scoring.ComputeScoreCard(session, [], null);

        result.SpeakingRateScore.Should().BeInRange(1, 99);
    }

    [Fact]
    public void FillerScore_WithoutStats_CountsFillerWordsInTranscript()
    {
        var clean = SessionWithTranscript((0, 60_000, Words(130)));
        var withFillers = SessionWithTranscript((0, 60_000,
            "ee şey yani ııı hmm " + Words(60) + " ee yani şey ıı um uh " + Words(60)));

        var cleanResult = _scoring.ComputeScoreCard(clean, [], null);
        var fillerResult = _scoring.ComputeScoreCard(withFillers, [], null);

        cleanResult.FillerScore.Should().Be(100);
        fillerResult.FillerScore.Should().BeLessThan(cleanResult.FillerScore);
    }

    [Fact]
    public void FillerWords_AreNotMatchedInsideOtherWords()
    {
        // "şeyler", "yanıt", "eee" icermeyen kelimeler dolgu sayilmamali
        var session = SessionWithTranscript((0, 60_000, "şeyler yanıt yanında erken " + Words(130)));

        var result = _scoring.ComputeScoreCard(session, [], null);

        result.FillerScore.Should().Be(100);
    }

    [Fact]
    public void ExplicitStats_TakePrecedenceOverTranscript()
    {
        var session = SessionWithTranscript((0, 60_000, Words(140)));
        var stats = new Dictionary<string, object> { ["wpm"] = 60 };

        var result = _scoring.ComputeScoreCard(session, [], stats);

        result.SpeakingRateScore.Should().BeLessThan(100);
    }

    [Fact]
    public void SpeechScores_WithoutStatsOrTranscript_KeepNeutralDefault()
    {
        var result = _scoring.ComputeScoreCard(new Session { Id = Guid.NewGuid() }, [], null);

        result.SpeakingRateScore.Should().Be(50);
        result.FillerScore.Should().Be(50);
    }

    [Fact]
    public void Feedback_WithoutStats_FlagsSlowSpeechFromTranscript()
    {
        var session = SessionWithTranscript((0, 60_000, Words(60)));
        var scoreCard = _scoring.ComputeScoreCard(session, [], null);

        var feedback = _scoring.GenerateFeedback(session, scoreCard, [], null);

        feedback.Should().Contain(f => f.Category == "SpeakingRate");
    }
}
