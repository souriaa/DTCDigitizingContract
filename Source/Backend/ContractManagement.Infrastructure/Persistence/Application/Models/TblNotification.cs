namespace ContractManagement.Infrastructure.Persistence.Application.Models;

public sealed class TblNotification
{
    public long NotificationId { get; set; }
    public string EventKey { get; set; } = null!;
    public string ModuleCode { get; set; } = null!;
    public string EventCode { get; set; } = null!;
    public string SubjectType { get; set; } = null!;
    public int SubjectId { get; set; }
    public int? ActorEmployeeId { get; set; }
    public string Title { get; set; } = null!;
    public string? Body { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? ActionKey { get; set; }
    public ICollection<TblNotificationRecipient> Recipients { get; set; } = [];
    public TblNotificationDispatchOutbox? DispatchOutbox { get; set; }
}

public sealed class TblNotificationRecipient
{
    public long NotificationRecipientId { get; set; }
    public long NotificationId { get; set; }
    public int EmployeeId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public TblNotification Notification { get; set; } = null!;
}

public sealed class TblNotificationDispatchOutbox
{
    // Separate transport delivery state: SignalR success never consumes a Node retry.
    public string NodeStatus { get; set; } = "Disabled";
    public int NodeAttemptCount { get; set; }
    public DateTime? NodeNextAttemptAtUtc { get; set; }
    public Guid? NodeLeaseId { get; set; }
    public DateTime? NodeLeaseUntilUtc { get; set; }
    public DateTime? NodeDispatchedAtUtc { get; set; }
    public string? NodeLastErrorCode { get; set; }
    public long OutboxId { get; set; }
    public long NotificationId { get; set; }
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public TblNotification Notification { get; set; } = null!;
}
