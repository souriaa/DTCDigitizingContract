using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Recovery;

public static class AccountRecoveryMapping
{
    public static void Configure(ModelBuilder model, string challengeTable, string outboxTable, string accountColumn)
    {
        model.Entity<AccountRecoveryChallenge>(entity =>
        {
            entity.ToTable(challengeTable, table =>
            {
                table.HasCheckConstraint($"CK_{challengeTable}_Purpose", "[Purpose] IN (1, 2)");
                table.HasCheckConstraint($"CK_{challengeTable}_Account", $"[{accountColumn}] > 0 AND [SessionVersionAtIssue] > 0 AND [RequestCount] > 0");
                table.HasCheckConstraint($"CK_{challengeTable}_Dates", "[ExpiresAtUtc] > [CreatedAtUtc] AND [LastRequestedAtUtc] >= [CreatedAtUtc]");
            });
            entity.HasKey(x => x.RecoveryChallengeId);
            entity.Property(x => x.AccountId).HasColumnName(accountColumn);
            entity.Property(x => x.TokenHash).HasColumnType("binary(32)").IsRequired();
            entity.Property(x => x.EmailHash).HasColumnType("binary(32)").IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => new { x.AccountId, x.Purpose }).IsUnique().HasFilter("[UsedAtUtc] IS NULL AND [RevokedAtUtc] IS NULL");
            entity.HasIndex(x => new { x.AccountId, x.CreatedAtUtc });
        });
        model.Entity<AccountRecoveryEmailOutbox>(entity =>
        {
            entity.ToTable(outboxTable, table =>
            {
                table.HasCheckConstraint($"CK_{outboxTable}_Values", $"[{accountColumn}] > 0 AND [TemplateKind] IN (1, 2, 3) AND [Status] IN (1, 2, 3, 4) AND [AttemptCount] >= 0");
                table.HasCheckConstraint($"CK_{outboxTable}_Lease", "([Status] = 2 AND [LeaseId] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL) OR ([Status] <> 2 AND [LeaseId] IS NULL AND [LeaseUntilUtc] IS NULL)");
            });
            entity.HasKey(x => x.OutboxId);
            entity.Property(x => x.AccountId).HasColumnName(accountColumn);
            entity.Property(x => x.RecipientCiphertext).HasMaxLength(1024);
            entity.Property(x => x.TokenCiphertext).HasMaxLength(512);
            entity.Property(x => x.FailureCode).HasMaxLength(64).IsUnicode(false);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
            entity.HasIndex(x => x.RecoveryChallengeId).IsUnique().HasFilter("[RecoveryChallengeId] IS NOT NULL AND [Status] IN (1, 2)");
        });
    }
}
