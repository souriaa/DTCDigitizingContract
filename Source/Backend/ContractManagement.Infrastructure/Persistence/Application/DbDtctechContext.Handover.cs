using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
namespace ContractManagement.Infrastructure.Persistence.Application;
public partial class DbDtctechContext
{
    public DbSet<TblContractHandoverBaseline> TblContractHandoverBaselines => Set<TblContractHandoverBaseline>();
    public DbSet<TblContractHandoverScopeItem> TblContractHandoverScopeItems => Set<TblContractHandoverScopeItem>();
    public DbSet<TblContractHandoverRecord> TblContractHandoverRecords => Set<TblContractHandoverRecord>();
    public DbSet<TblContractHandoverLine> TblContractHandoverLines => Set<TblContractHandoverLine>();
    public DbSet<TblContractHandoverParty> TblContractHandoverParties => Set<TblContractHandoverParty>();
    public DbSet<TblContractHandoverReference> TblContractHandoverReferences => Set<TblContractHandoverReference>();
    public DbSet<TblContractHandoverSection> TblContractHandoverSections => Set<TblContractHandoverSection>();
    public DbSet<TblContractHandoverEvidence> TblContractHandoverEvidences => Set<TblContractHandoverEvidence>();
    public DbSet<TblContractHandoverPlaceholderValue> TblContractHandoverPlaceholderValues => Set<TblContractHandoverPlaceholderValue>();
    public DbSet<TblContractAcceptanceScopeAllocation> TblContractAcceptanceScopeAllocations => Set<TblContractAcceptanceScopeAllocation>();
    private static void ConfigureHandover(ModelBuilder model)
    {
        model.Entity<TblContractHandoverBaseline>(e => {
            e.ToTable("tbl_ContractHandoverBaseline"); e.HasKey(x => x.HandoverBaselineId);
            e.HasIndex(x => new { x.ContractId, x.ContractVersionId }).IsUnique();
        });
        model.Entity<TblContractHandoverScopeItem>(e => {
            e.ToTable("tbl_ContractHandoverScopeItem", t => {
                t.HasCheckConstraint("CK_HandoverScope_Mode", "[ScopeMode] IN (1,2)");
                t.HasCheckConstraint("CK_HandoverScope_Quantity", "[SourceQuantity] > 0 AND [RequiredQuantity] > 0 AND (([ScopeMode] = 1 AND [RequiredQuantity] = [SourceQuantity]) OR ([ScopeMode] = 2 AND [RequiredQuantity] = 1))");
            }); e.HasKey(x => x.HandoverScopeItemId);
            e.HasIndex(x => new { x.HandoverBaselineId, x.SourceContractItemId }).IsUnique();
            e.HasIndex(x => new { x.HandoverBaselineId, x.DisplayOrder }).IsUnique();
            e.Property(x => x.ItemCode).HasMaxLength(100);
            e.Property(x => x.NameVi).HasMaxLength(2000); e.Property(x => x.NameEn).HasMaxLength(2000);
            e.Property(x => x.UnitVi).HasMaxLength(100); e.Property(x => x.UnitEn).HasMaxLength(100);
            e.Property(x => x.SourceQuantity).HasPrecision(18,4); e.Property(x => x.RequiredQuantity).HasPrecision(18,4);
        });
        model.Entity<TblContractHandoverRecord>(e => {
            e.ToTable("tbl_ContractHandoverRecord", t => {
                t.HasCheckConstraint("CK_HandoverRecord_Status", "[Status] IN (0,1,2,3)");
                t.HasCheckConstraint("CK_HandoverRecord_Hash", "([SnapshotHash] IS NULL OR LEN([SnapshotHash]) = 64) AND ([Status] NOT IN (1,2) OR [SnapshotHash] IS NOT NULL)");
            });
            e.HasKey(x => x.HandoverRecordId);
            e.HasIndex(x => new { x.ContractId, x.HandoverCode }).IsUnique();
            e.HasIndex(x => new { x.ContractId, x.ContractVersionId }).IsUnique().HasFilter("[Status] IN (0,1)").HasDatabaseName("UX_HandoverRecord_ActiveVersion");
            e.HasIndex(x => new { x.ContractVersionId, x.Status });
            e.Property(x => x.HandoverCode).HasMaxLength(100);
            e.Property(x => x.Location).HasMaxLength(500); e.Property(x => x.LocationEn).HasMaxLength(500);
            e.Property(x => x.CancellationReason).HasMaxLength(1000);
            e.Property(x => x.SnapshotHash).HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.ContractSignDate).HasColumnType("date"); e.Property(x => x.HandoverDate).HasColumnType("date");
            e.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        });
        model.Entity<TblContractHandoverLine>(e => {
            e.ToTable("tbl_ContractHandoverLine", t => t.HasCheckConstraint("CK_HandoverLine_Quantity", "[Quantity] > 0"));
            e.HasKey(x => x.HandoverLineId);
            e.HasIndex(x => new { x.HandoverRecordId, x.HandoverScopeItemId }).IsUnique();
            e.HasIndex(x => x.HandoverScopeItemId);
            e.HasIndex(x => new { x.HandoverRecordId, x.DisplayOrder }).IsUnique();
            e.Property(x => x.Quantity).HasPrecision(18,4);
            e.Property(x => x.NotesVi).HasMaxLength(2000); e.Property(x => x.NotesEn).HasMaxLength(2000);
        });
        model.Entity<TblContractHandoverParty>(e => {
            e.ToTable("tbl_ContractHandoverParty", t => t.HasCheckConstraint("CK_HandoverParty_Subject", "[SubjectKind] IN (1,2)"));
            e.HasKey(x => x.HandoverPartyId); e.HasIndex(x => new { x.HandoverRecordId, x.SubjectKind }).IsUnique();
            e.Property(x => x.LegalName).HasMaxLength(1000); e.Property(x => x.LegalNameEn).HasMaxLength(1000);
            e.Property(x => x.Address).HasMaxLength(2000); e.Property(x => x.AddressEn).HasMaxLength(2000);
            e.Property(x => x.RepresentativeName).HasMaxLength(300);
            e.Property(x => x.RepresentativeTitle).HasMaxLength(500); e.Property(x => x.RepresentativeTitleEn).HasMaxLength(500);
            e.Property(x => x.Phone).HasMaxLength(100); e.Property(x => x.Fax).HasMaxLength(100); e.Property(x => x.TaxCode).HasMaxLength(50);
        });
        model.Entity<TblContractHandoverReference>(e => {
            e.ToTable("tbl_ContractHandoverReference"); e.HasKey(x => x.HandoverReferenceId);
            e.HasIndex(x => new { x.HandoverRecordId, x.DisplayOrder }).IsUnique();
        });
        model.Entity<TblContractHandoverSection>(e => {
            e.ToTable("tbl_ContractHandoverSection", t => t.HasCheckConstraint("CK_HandoverSection_Mode", "[BilingualContentMode] IN (1,2)"));
            e.HasKey(x => x.HandoverSectionId);
            e.HasIndex(x => new { x.HandoverRecordId, x.SectionCode }).IsUnique();
            e.HasIndex(x => new { x.HandoverRecordId, x.DisplayOrder }).IsUnique();
            e.Property(x => x.SectionCode).HasMaxLength(100);
            e.Property(x => x.TitleVi).HasMaxLength(1000); e.Property(x => x.TitleEn).HasMaxLength(1000);
            e.Property(x => x.ContentVi).HasColumnType("nvarchar(max)"); e.Property(x => x.ContentEn).HasColumnType("nvarchar(max)");
        });
        model.Entity<TblContractHandoverEvidence>(e => {
            e.ToTable("tbl_ContractHandoverEvidence"); e.HasKey(x => x.HandoverEvidenceId);
            e.HasIndex(x => x.HandoverRecordId).IsUnique(); e.HasIndex(x => x.FileId).IsUnique();
            e.Property(x => x.SignDate).HasColumnType("date");
        });
        model.Entity<TblContractHandoverPlaceholderValue>(e => {
            e.ToTable("tbl_ContractHandoverPlaceholderValue"); e.HasKey(x => x.HandoverPlaceholderValueId);
            e.HasIndex(x => new { x.HandoverRecordId, x.PlaceholderKey }).IsUnique();
            e.Property(x => x.PlaceholderKey).HasMaxLength(100); e.Property(x => x.SourceFieldKey).HasMaxLength(250);
            e.Property(x => x.RenderedValue).HasColumnType("nvarchar(max)");
        });
        model.Entity<TblContractAcceptanceScopeAllocation>(e => {
            e.ToTable("tbl_ContractAcceptanceScopeAllocation", t => t.HasCheckConstraint("CK_AcceptanceScopeAllocation_Quantity", "[Quantity] > 0"));
            e.HasKey(x => x.AcceptanceScopeAllocationId);
            e.HasIndex(x => new { x.AcceptanceRecordId, x.HandoverLineId }).IsUnique();
            e.HasIndex(x => x.HandoverLineId); e.Property(x => x.Quantity).HasPrecision(18,4);
        });
    }
}
