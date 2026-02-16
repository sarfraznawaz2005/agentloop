using AgentLoop.Core.Interfaces;
using AgentLoop.Data.Models;
using AgentLoop.UI.ViewModels;
using Moq;
using Xunit;

namespace AgentLoop.Tests;

public class MainViewModelTests
{
    private readonly Mock<ITaskSchedulerService> _taskSchedulerMock;
    private readonly Mock<ILogService> _logServiceMock;
    private readonly Mock<IAppLogService> _appLogServiceMock;

    public MainViewModelTests()
    {
        _taskSchedulerMock = new Mock<ITaskSchedulerService>();
        _logServiceMock = new Mock<ILogService>();
        _appLogServiceMock = new Mock<IAppLogService>();

        // Set up default returns to avoid null ref in InitializeAsync
        _taskSchedulerMock.Setup(s => s.IsServiceRunning()).Returns(true);
        _taskSchedulerMock.Setup(s => s.GetAllJobs()).Returns(new List<JobModel>());
        _logServiceMock.Setup(s => s.GetRecentLogsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<JobStatus?>(), It.IsAny<bool?>()))
            .ReturnsAsync(new List<LogEntry>());
        _logServiceMock.Setup(s => s.GetRecentLogCountAsync(It.IsAny<JobStatus?>(), It.IsAny<bool?>()))
            .ReturnsAsync(0);
    }

    [Fact]
    public void JobFilter_FiltersCorrectly()
    {
        // Arrange
        var vm = new MainViewModel(_taskSchedulerMock.Object, _logServiceMock.Object, _appLogServiceMock.Object);
        var job1 = new JobModel { Name = "Clean Logs" };
        var job2 = new JobModel { Name = "Daily Backup" };

        vm.Jobs.Add(job1);
        vm.Jobs.Add(job2);

        // Act
        vm.JobFilter = "Clean";

        // Assert
        Assert.Single(vm.FilteredJobs);
        Assert.Equal("Clean Logs", vm.FilteredJobs[0].Name);
    }

    [Fact]
    public void JobFilter_Empty_ShowsAll()
    {
        // Arrange
        var vm = new MainViewModel(_taskSchedulerMock.Object, _logServiceMock.Object, _appLogServiceMock.Object);
        vm.Jobs.Add(new JobModel { Name = "Job1" });
        vm.Jobs.Add(new JobModel { Name = "Job2" });

        // Act
        vm.JobFilter = "InitialValue"; // Trigger filter once
        vm.JobFilter = ""; // Set back to empty to show all

        // Assert
        Assert.Equal(2, vm.FilteredJobs.Count);
    }

    [Fact]
    public void SelectedJob_TriggersPropertyChange()
    {
        // Arrange
        var vm = new MainViewModel(_taskSchedulerMock.Object, _logServiceMock.Object, _appLogServiceMock.Object);
        var job = new JobModel { Name = "Test" };
        bool triggered = false;
        vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.SelectedJob)) triggered = true; };

        // Act
        vm.SelectedJob = job;

        // Assert
        Assert.True(triggered);
        Assert.Equal(job, vm.SelectedJob);
    }
}
