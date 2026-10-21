using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Application;

public partial class DbDtctechContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TblContract>(entity =>
        {
            entity.Property(x => x.ContractDirection).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("CK_tbl_Contract_ContractDirection", "[ContractDirection] IN (1, 2)"));
            entity.HasIndex(x => new { x.ContractDirection, x.Status });
        });
        modelBuilder.Entity<TblContractTemplateVersion>(entity =>
        {
            entity.Property(x => x.ContractDirection).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("CK_tbl_ContractTemplateVersion_ContractDirection", "[ContractDirection] IN (1, 2)"));
            entity.HasIndex(x => new { x.ContractDirection, x.Status });
        });
        modelBuilder.Entity<TblContractVersionLegalSnapshot>(entity =>
        {
            entity.Property(x => x.ContractDirection).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("CK_tbl_ContractVersionLegalSnapshot_ContractDirection", "[ContractDirection] IN (1, 2)"));
        });
        modelBuilder.Entity<TblProduct>(entity =>
        {
            entity.Property(x => x.ProductNameEn).HasMaxLength(500).IsUnicode().IsRequired(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_Products_ProductNameEn",
                "LEN(LTRIM(RTRIM([ProductNameEn]))) > 0"));
        });
        modelBuilder.Entity<TblService>(entity =>
        {
            entity.Property(x => x.ServiceNameEn).HasMaxLength(500).IsUnicode().IsRequired(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_Services_ServiceNameEn",
                "LEN(LTRIM(RTRIM([ServiceNameEn]))) > 0"));
        });
        modelBuilder.Entity<TblContractTemplateTerm>(entity =>
        {
            entity.Property(x => x.BilingualContentMode).HasDefaultValue((byte)1);
            entity.ToTable(table => table.HasCheckConstraint("CK_TblContractTemplateTerm_BilingualContentMode",
                "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)"));
        });
        modelBuilder.Entity<TblContractTemplateAppendixTerm>(entity =>
        {
            entity.Property(x => x.BilingualContentMode).HasDefaultValue((byte)1);
            entity.ToTable(table => table.HasCheckConstraint("CK_TblContractTemplateAppendixTerm_BilingualContentMode",
                "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)"));
        });
        modelBuilder.Entity<TblContractTerm>(entity =>
        {
            entity.Property(x => x.BilingualContentMode).HasDefaultValue((byte)1);
            entity.ToTable(table => table.HasCheckConstraint("CK_TblContractTerm_BilingualContentMode",
                "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)"));
        });
        modelBuilder.Entity<TblContractAppendixTerm>(entity =>
        {
            entity.Property(x => x.BilingualContentMode).HasDefaultValue((byte)1);
            entity.ToTable(table => table.HasCheckConstraint("CK_TblContractAppendixTerm_BilingualContentMode",
                "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)"));
        });
        modelBuilder.Entity<TblContractAcceptanceSection>(entity =>
        {
            entity.Property(x => x.BilingualContentMode).HasDefaultValue((byte)1);
            entity.ToTable(table => table.HasCheckConstraint("CK_TblContractAcceptanceSection_BilingualContentMode",
                "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [ContentEn] IS NULL)"));
        });
        modelBuilder.Entity<TblTenantLegalProfile>(entity =>
        {
            entity.Property(x => x.LegalEntityNameEn).HasMaxLength(500).IsUnicode().IsRequired(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_TenantLegalProfile_LegalEntityNameEn",
                "LEN(LTRIM(RTRIM([LegalEntityNameEn]))) > 0"));
            entity.Property(x => x.AddressEn).HasMaxLength(2000).IsUnicode().IsRequired(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_TenantLegalProfile_AddressEn",
                "LEN(LTRIM(RTRIM([AddressEn]))) > 0"));
            entity.Property(x => x.RepresentativeTitleEn).HasMaxLength(200).IsUnicode().IsRequired(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_TenantLegalProfile_RepresentativeTitleEn",
                "LEN(LTRIM(RTRIM([RepresentativeTitleEn]))) > 0"));
            entity.Property(x => x.BankNameEn).HasMaxLength(500).IsUnicode().IsRequired(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_TenantLegalProfile_BankNameEn",
                "LEN(LTRIM(RTRIM([BankNameEn]))) > 0"));
        });
        modelBuilder.Entity<TblCustomer>(entity =>
        {
            entity.Property(x => x.CustomerCompanyEn).HasMaxLength(500).IsUnicode();
            entity.Property(x => x.CustomerAddressEn).HasMaxLength(500).IsUnicode();
            entity.Property(x => x.CustomerBankNameEn).HasMaxLength(500).IsUnicode();
            entity.Property(x => x.CustomerCityEn).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.CustomerCountryEn).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.CustomerContactPersonTitleEn).HasMaxLength(200).IsUnicode();
            entity.Property(x => x.CustomerRepresentativeTitleEn).HasMaxLength(200).IsUnicode();
        });
        modelBuilder.Entity<TblContractVersionPartySnapshot>(entity =>
        {
            entity.Property(x => x.SubjectCode).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.ContactName).HasMaxLength(300).IsUnicode();
            entity.Property(x => x.ContactTitle).HasMaxLength(300).IsUnicode();
            entity.Property(x => x.ContactPhone).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.FullName).HasMaxLength(300).IsUnicode();
            entity.Property(x => x.Email).HasMaxLength(255).IsUnicode();
            entity.Property(x => x.Website).HasMaxLength(500).IsUnicode();
            entity.Property(x => x.City).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.Country).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.Mobile).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.ZipCode).HasMaxLength(50).IsUnicode();
            entity.Property(x => x.LegalNameEn).HasMaxLength(1000).IsUnicode();
            entity.Property(x => x.AddressEn).HasMaxLength(2000).IsUnicode();
            entity.Property(x => x.RepresentativeTitleEn).HasMaxLength(500).IsUnicode();
            entity.Property(x => x.ContactTitleEn).HasMaxLength(500).IsUnicode();
            entity.Property(x => x.BankNameEn).HasMaxLength(1000).IsUnicode();
            entity.Property(x => x.CityEn).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.CountryEn).HasMaxLength(100).IsUnicode();
        });
        modelBuilder.Entity<TblContractAcceptanceParty>(entity =>
        {
            entity.Property(x => x.LegalNameEn).HasMaxLength(500).IsUnicode();
            entity.Property(x => x.AddressEn).HasMaxLength(2000).IsUnicode();
            entity.Property(x => x.RepresentativeTitleEn).HasMaxLength(300).IsUnicode();
        });
        modelBuilder.Entity<TblContractAcceptanceRecord>().Property(x => x.ContractSignDate).HasColumnType("date");
        modelBuilder.Entity<TblContractAcceptanceRecord>().Property(x => x.LocationEn)
            .HasMaxLength(500).IsUnicode();
    }
}

