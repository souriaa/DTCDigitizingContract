using System.Text.Json;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Controllers.Notifications;
using ContractManagement.API.Domains.Notifications;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Notifications;

public sealed class NotificationInboxTests
{
    [Fact]
    public async Task Stage_IsAtomicUntilCallerSaves_AndDeduplicatesEventAndRecipients()
    {
        await using var db = CreateDb();
        db.TblEmployees.AddRange(Employee(11), Employee(12), Employee(13, status: 0));
        db.TblContracts.Add(new TblContract {
            ContractDirection = (byte)1, ContractId = 10, EmployeeId = 12, ContractName = "Test" });
        await db.SaveChangesAsync();
        var publisher = Publisher(db);
        var evt = Event(actor: 11);

        Assert.True(await publisher.StageAsync(evt, [11, 12, 12, 13]));
        Assert.Empty(await db.TblNotifications.AsNoTracking().ToListAsync());
        Assert.False(await publisher.StageAsync(evt, [12]));
        await db.SaveChangesAsync();

        var notification = Assert.Single(await db.TblNotifications.Include(x => x.Recipients)
            .Include(x => x.DispatchOutbox).ToListAsync());
        Assert.Equal(12, Assert.Single(notification.Recipients).EmployeeId);
        Assert.NotNull(notification.DispatchOutbox);
        Assert.Equal("Pending", notification.DispatchOutbox.Status);
    }

    [Fact]
    public async Task Inbox_FiltersByEmployeeAndCurrentContractPermission()
    {
        await using var db = CreateDb();
        db.TblEmployees.AddRange(Employee(11), Employee(12));
        db.TblContracts.Add(new TblContract {
            ContractDirection = (byte)1, ContractId = 10, EmployeeId = 12, ContractName = "Test" });
        await db.SaveChangesAsync();
        await Publisher(db).StageAsync(Event(actor: null), [12]);
        await db.SaveChangesAsync();
        var policy = new ContractNotificationResourcePolicy(db, new ContractResourceAuthorizationService(db));

        var other = Controller(db, policy, 11);
        Assert.Equal(0, Count(await other.UnreadCount(default)));
        var owner = Controller(db, policy, 12);
        Assert.Equal(1, Count(await owner.UnreadCount(default)));

        db.TblContracts.Single().EmployeeId = 11;
        await db.SaveChangesAsync();
        Assert.Equal(0, Count(await owner.UnreadCount(default)));
    }

    [Fact]
    public async Task Inbox_InvalidCursorAndUnregisteredActionAreRejected()
    {
        await using var db = CreateDb();
        db.TblEmployees.Add(Employee(12));
        db.TblContracts.Add(new TblContract {
            ContractDirection = (byte)1, ContractId = 10, EmployeeId = 12, ContractName = "Test" });
        await db.SaveChangesAsync();
        var controller = Controller(db,
            new ContractNotificationResourcePolicy(db, new ContractResourceAuthorizationService(db)), 12);
        Assert.IsType<BadRequestObjectResult>(await controller.List("not-base64", 20));
        Assert.IsType<BadRequestObjectResult>(await controller.List(null, 51));

        var invalidEvent = Event(null) with { EventKey = "contract:10:bad", ActionKey = "https://wrong.example" };
        await Assert.ThrowsAsync<ArgumentException>(() => Publisher(db).StageAsync(invalidEvent, [12]));
    }

    [Fact]
    public async Task ApprovalNotification_GoesOnlyToAssignedActiveManager()
    {
        await using var db = CreateDb();
        db.TblEmployees.AddRange(Employee(11), Employee(21, role: EmployeeType.Manager),
            Employee(22, role: EmployeeType.Manager), Employee(23, status: 0, role: EmployeeType.Manager));
        db.TblContracts.Add(new TblContract {
            ContractDirection = (byte)1, ContractId = 10, EmployeeId = 11, ContractName = "Test" });
        db.TblApprovalWorkflows.Add(new TblApprovalWorkflow
        {
            WorkflowId = 7, WorkflowName = "Manager 21", ObjectType = "Contract",
            StepNo = 1, ApproverEmployeeId = 21, IsActive = true
        });
        await db.SaveChangesAsync();
        var producer = new ContractNotificationProducer(db, Publisher(db));

        Assert.True(await producer.StageApprovalRequestedAsync(10, 42, 11, 7, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var notification = Assert.Single(await db.TblNotifications.Include(x => x.Recipients).ToListAsync());
        Assert.Equal("contract:10:approval-request:42:submitted", notification.EventKey);
        Assert.Equal(new[] { 21, 22 }, notification.Recipients.Select(x => x.EmployeeId).Order());
    }

    [Fact]
    public async Task Withdrawal_NotifiesOriginalApprover_WhenWorkflowAssignmentChanges()
    {
        await using var db = CreateDb();
        db.TblEmployees.AddRange(Employee(11), Employee(21, role: EmployeeType.Manager),
            Employee(22, role: EmployeeType.Manager));
        db.TblContracts.Add(new TblContract {
            ContractDirection = (byte)1, ContractId = 10, EmployeeId = 11, ContractName = "Test" });
        var workflow = new TblApprovalWorkflow
        {
            WorkflowId = 7, WorkflowName = "Manager 21", ObjectType = "Contract",
            StepNo = 1, ApproverEmployeeId = 21, IsActive = true
        };
        db.TblApprovalWorkflows.Add(workflow);
        await db.SaveChangesAsync();
        var producer = new ContractNotificationProducer(db, Publisher(db));
        await producer.StageApprovalRequestedAsync(10, 42, 11, 7, DateTime.UtcNow);
        await db.SaveChangesAsync();

        workflow.ApproverEmployeeId = 22;
        await db.SaveChangesAsync();
        Assert.True(await producer.StageApprovalWithdrawnAsync(10, 42, 11, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var withdrawn = await db.TblNotifications.Include(x => x.Recipients)
            .SingleAsync(x => x.EventCode == "ApprovalWithdrawn");
        Assert.Equal(new[] { 21, 22 }, withdrawn.Recipients.Select(x => x.EmployeeId).Order());
    }

    [Fact]
    public async Task OwnerNotification_DoesNotNotifyTheActor()
    {
        await using var db = CreateDb();
        db.TblEmployees.Add(Employee(12));
        db.TblContracts.Add(new TblContract {
            ContractDirection = (byte)1, ContractId = 10, EmployeeId = 12, ContractName = "Test" });
        await db.SaveChangesAsync();
        var producer = new ContractNotificationProducer(db, Publisher(db));

        Assert.False(await producer.StageCurrentOwnerAsync(10, 12,
            "contract:10:completed:1", "ContractCompleted", "Completed", "overview", DateTime.UtcNow));
        Assert.Empty(await db.TblNotifications.ToListAsync());
    }

    private static int Count(IActionResult action)
    {
        var ok = Assert.IsType<OkObjectResult>(action);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        return document.RootElement.GetProperty("count").GetInt32();
    }

    private static NotificationsController Controller(DbDtctechContext db,
        INotificationResourcePolicy policy, int employeeId)
    {
        var http = new DefaultHttpContext();
        http.Items[EmployeeAuthorizationContext.HttpContextItemKey] = new AuthenticatedEmployee(
            employeeId, "test", "Test", EmployeeType.Sale, []);
        return new NotificationsController(db, [policy])
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static INotificationPublisher Publisher(DbDtctechContext db) =>
        new NotificationPublisher(db,
            [new ContractNotificationResourcePolicy(db, new ContractResourceAuthorizationService(db))]);

    private static NotificationEvent Event(int? actor) =>
        new("contract:10:test:1", "Contract", "Test", NotificationSubjects.Contract,
            10, actor, "Test", null, DateTime.UtcNow, "overview");

    private static TblEmployee Employee(int id, byte status = 1, EmployeeType role = EmployeeType.Sale) => new()
    {
        EmployeeId = id, EmployeeAccount = $"employee{id}", EmployeeFullName = $"Employee {id}",
        EmployeeType = (byte)role, Status = status, SessionVersion = 1
    };

    private static DbDtctechContext CreateDb() => new(
        new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
