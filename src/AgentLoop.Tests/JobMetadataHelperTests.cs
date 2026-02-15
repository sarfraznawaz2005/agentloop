using AgentLoop.Data.Models;
using AgentLoop.Data.Services;
using Xunit;

namespace AgentLoop.Tests;

public class JobMetadataHelperTests
{
    [Fact]
    public void Encode_ProducesValidHeader()
    {
        // Arrange
        var job = new JobModel { Prompt = "Test Prompt", Silent = true };

        // Act
        var encoded = JobMetadataHelper.Encode(job);

        // Assert
        Assert.StartsWith("AgentLoop Job v1", encoded);
        Assert.Contains("SILENT:true", encoded);
        Assert.Contains("PROMPT:", encoded);
    }

    [Fact]
    public void Decode_RoundTrip_Works()
    {
        // Arrange
        var original = new JobModel
        {
            Prompt = "Hello World!",
            Silent = false,
            AgentOverride = "custom-agent",
            HexColor = "#FF0000",
            Icon = "Star"
        };
        var encoded = JobMetadataHelper.Encode(original);

        // Act
        var decoded = JobMetadataHelper.Decode(encoded);

        // Assert
        Assert.NotNull(decoded);
        Assert.Equal(original.Prompt, decoded.Value.Prompt);
        Assert.Equal(original.Silent, decoded.Value.Silent);
        Assert.Equal(original.AgentOverride, decoded.Value.AgentOverride);
        Assert.Equal(original.HexColor, decoded.Value.HexColor);
        Assert.Equal(original.Icon, decoded.Value.Icon);
    }

    [Fact]
    public void Decode_ReturnsNull_ForInvalidInput()
    {
        Assert.Null(JobMetadataHelper.Decode(null));
        Assert.Null(JobMetadataHelper.Decode(""));
        Assert.Null(JobMetadataHelper.Decode("Invalid Header"));
    }

    [Theory]
    [InlineData("AgentLoop Job v1\nsomething", true)]
    [InlineData("Something Else", false)]
    public void IsAgentLoopTask_DetectsHeader(string input, bool expected)
    {
        Assert.Equal(expected, JobMetadataHelper.IsAgentLoopTask(input));
    }
}
