using Famick.HomeManagement.Core.DTOs.Chores;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Validators.Chores;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// A chore's next due date is one of the reminders devices schedule for themselves, so every change
/// that can move it tells the household's devices to refresh them. Reads don't.
/// </summary>
public class ChoresControllerReminderSyncTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Mock<IChoreService> _chores = new();
    private readonly Mock<IReminderSyncPushService> _reminderSyncPush = new();
    private readonly ChoresController _controller;

    public ChoresControllerReminderSyncTests()
    {
        _chores.Setup(c => c.CreateAsync(It.IsAny<CreateChoreRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChoreDto { Id = Guid.NewGuid() });
        _chores.Setup(c => c.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateChoreRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChoreDto());
        _chores.Setup(c => c.ExecuteChoreAsync(It.IsAny<Guid>(), It.IsAny<ExecuteChoreRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChoreLogDto());

        _controller = new ChoresController(
            _chores.Object,
            _reminderSyncPush.Object,
            new CreateChoreRequestValidator(),
            new UpdateChoreRequestValidator(),
            new ExecuteChoreRequestValidator(),
            new SkipChoreRequestValidator(),
            Mock.Of<ITenantProvider>(t => t.TenantId == _tenantId && t.UserId == Guid.NewGuid()),
            NullLogger<ChoresController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    public static TheoryData<string> Changes => new() { "create", "update", "delete", "execute", "skip", "undo" };

    [Theory]
    [MemberData(nameof(Changes))]
    public async Task A_change_refreshes_device_reminders(string change)
    {
        var id = Guid.NewGuid();
        var result = change switch
        {
            "create" => await _controller.Create(new CreateChoreRequest { Name = "Water plants", PeriodType = "daily" }, default),
            "update" => await _controller.Update(id, new UpdateChoreRequest { Name = "Water plants", PeriodType = "daily" }, default),
            "delete" => await _controller.Delete(id, default),
            "execute" => await _controller.Execute(id, new ExecuteChoreRequest(), default),
            "skip" => await _controller.Skip(id, new SkipChoreRequest(), default),
            _ => await _controller.UndoExecution(id, Guid.NewGuid(), default)
        };

        Assert.IsNotType<BadRequestObjectResult>(result);
        _reminderSyncPush.Verify(p => p.NotifyRemindersChangedAsync(_tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_refused_change_does_not()
    {
        await _controller.Create(new CreateChoreRequest { Name = "", PeriodType = "daily" }, default);

        _reminderSyncPush.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_read_does_not()
    {
        _chores.Setup(c => c.GetOverdueChoresAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _controller.GetOverdue(default);

        _reminderSyncPush.VerifyNoOtherCalls();
    }
}
