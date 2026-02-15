using AgentLoop.Core.Services;
using Xunit;

namespace AgentLoop.Tests;

public class AgentCommandServiceTests
{
    private readonly AgentCommandService _service;

    public AgentCommandServiceTests()
    {
        _service = new AgentCommandService();
    }

    [Fact]
    public void SubstitutePrompt_ReplacesTokens()
    {
        // Arrange
        var agentCommand = "python script.py --prompt \"{prompt}\"";
        var prompt = "Hello {date} {time} {datetime}";

        // Act
        var result = _service.SubstitutePrompt(agentCommand, prompt);

        // Assert
        Assert.Contains("python script.py --prompt", result);
        Assert.DoesNotContain("{date}", result);
        Assert.DoesNotContain("{time}", result);
        Assert.DoesNotContain("{datetime}", result);
        Assert.DoesNotContain("{prompt}", result);
    }

    [Theory]
    [InlineData("python.exe script.py", "python.exe", "script.py")]
    [InlineData("\"C:\\Program Files\\python.exe\" script.py", "C:\\Program Files\\python.exe", "script.py")]
    [InlineData("run", "run", "")]
    [InlineData("  trim  me  ", "trim", "me")]
    public void ParseCommand_ParsesCorrectly(string input, string expectedExe, string expectedArgs)
    {
        // Act
        var (exe, args) = AgentCommandService.ParseCommand(input);

        // Assert
        Assert.Equal(expectedExe, exe);
        Assert.Equal(expectedArgs, args);
    }

    [Fact]
    public void ParseCommand_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => AgentCommandService.ParseCommand(""));
        Assert.Throws<ArgumentException>(() => AgentCommandService.ParseCommand("  "));
    }

    [Fact]
    public void ParseCommand_ThrowsOnUnterminatedQuote()
    {
        Assert.Throws<ArgumentException>(() => AgentCommandService.ParseCommand("\"unterminated"));
    }
}
