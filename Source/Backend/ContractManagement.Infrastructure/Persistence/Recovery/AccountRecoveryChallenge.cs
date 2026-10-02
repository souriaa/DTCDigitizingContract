namespace ContractManagement.Infrastructure.Persistence.Recovery;

public enum RecoveryPurpose : byte { PasswordReset = 1, VerifyRecoveryEmail = 2 }
public enum RecoveryEmailKind : byte { PasswordReset = 1, VerifyRecoveryEmail = 2, PasswordChanged = 3 }
public enum RecoveryDeliveryStatus : byte { Pending = 1, Leased = 2, Sent = 3, Failed = 4 }

public sealed class AccountRecoveryChallenge
{
    public long RecoveryChallengeId { get; set; }
    public Guid PublicId { get; set; }
    public int AccountId { get; set; }
    public RecoveryPurpose Purpose { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public byte[] EmailHash { get; set; } = [];
    public int SessionVersionAtIssue { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime LastRequestedAtUtc { get; set; }
    public int RequestCount { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class AccountRecoveryEmailOutbox
{
    public long OutboxId { get; set; }
    public long? RecoveryChallengeId { get; set; }
    public int AccountId { get; set; }
    public RecoveryEmailKind TemplateKind { get; set; }
    public byte[]? RecipientCiphertext { get; set; }
    public byte[]? TokenCiphertext { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public RecoveryDeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string? FailureCode { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
