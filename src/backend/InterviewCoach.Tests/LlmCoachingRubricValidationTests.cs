using FluentAssertions;
using InterviewCoach.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterviewCoach.Tests;

public class LlmCoachingRubricValidationTests
{
    private readonly LlmCoachingService _service = new(new NoopLlmClient(), NullLogger<LlmCoachingService>.Instance);

    private static string BuildJson(string rubricJson) => $$"""
        {
          "rubric": {{rubricJson}},
          "overall": 72,
          "feedback": [
            { "category": "audio", "severity": 2, "title": "Pacing", "evidence": "e", "time_range_ms": [0, 1000], "suggestion": "s", "example_phrase": "x" },
            { "category": "content", "severity": 3, "title": "Depth", "evidence": "e", "time_range_ms": [0, 1000], "suggestion": "s", "example_phrase": "x" }
          ],
          "drills": [ { "title": "d", "steps": ["a"], "duration_min": 5 } ]
        }
        """;

    [Fact]
    public void CompleteRubric_IsValid_AndKeepsValues()
    {
        var json = BuildJson("""{ "technical_correctness": 4, "depth": 3, "structure": 4, "clarity": 5, "confidence": 3 }""");

        var ok = _service.TryParseAndValidate(json, out var response, out var errors);

        ok.Should().BeTrue(string.Join("; ", errors));
        response!.Rubric.Depth.Should().Be(3);
        response.Rubric.Clarity.Should().Be(5);
    }

    [Fact]
    public void ExplicitZeroScore_IsStillValid()
    {
        var json = BuildJson("""{ "technical_correctness": 0, "depth": 0, "structure": 1, "clarity": 0, "confidence": 0 }""");

        _service.TryParseAndValidate(json, out _, out var errors).Should().BeTrue(string.Join("; ", errors));
    }

    [Fact]
    public void MissingRubricField_IsRejected_InsteadOfDefaultingToZero()
    {
        var json = BuildJson("""{ "technical_correctness": 4, "structure": 4, "clarity": 5, "confidence": 3 }""");

        var ok = _service.TryParseAndValidate(json, out _, out var errors);

        ok.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("rubric.depth"));
    }

    [Fact]
    public void WronglyNamedRubricField_IsRejected()
    {
        var json = BuildJson("""{ "technicalCorrectness": 4, "depth": 3, "structure": 4, "clarity": 5, "confidence": 3 }""");

        var ok = _service.TryParseAndValidate(json, out _, out var errors);

        ok.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("rubric.technical_correctness"));
    }

    [Fact]
    public void MissingRubricObject_IsRejected_WithoutThrowing()
    {
        var json = """
            { "overall": 72, "feedback": [], "drills": [] }
            """;

        var act = () => _service.TryParseAndValidate(json, out _, out _);

        act.Should().NotThrow();
        _service.TryParseAndValidate(json, out _, out var errors).Should().BeFalse();
        errors.Should().Contain(e => e.Contains("rubric"));
    }

    private sealed class NoopLlmClient : ILlmClient
    {
        public Task<LlmJsonResponse> GenerateJsonAsync(LlmJsonRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
