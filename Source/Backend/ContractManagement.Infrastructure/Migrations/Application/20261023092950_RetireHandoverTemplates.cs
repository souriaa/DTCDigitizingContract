using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class RetireHandoverTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retire only document type 4. Titles containing the same words are not a discriminator.
            // Keep immutable audit rows and file metadata, and never delete a template used by core records.
            migrationBuilder.Sql("""
                DECLARE @Templates TABLE ([Id] int PRIMARY KEY);
                INSERT INTO @Templates SELECT [TemplateId] FROM [tbl_ContractTemplate] WHERE [DocumentType] = 4;
                DECLARE @Versions TABLE ([Id] int PRIMARY KEY);
                INSERT INTO @Versions SELECT [TemplateVersionId] FROM [tbl_ContractTemplateVersion]
                    WHERE [TemplateId] IN (SELECT [Id] FROM @Templates);
                DECLARE @Terms TABLE ([Id] int PRIMARY KEY);
                INSERT INTO @Terms SELECT [TemplateTermId] FROM [tbl_ContractTemplateTerm]
                    WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions);
                DECLARE @Appendices TABLE ([Id] int PRIMARY KEY);
                INSERT INTO @Appendices SELECT [TemplateAppendixId] FROM [tbl_ContractTemplateAppendix]
                    WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions);
                DECLARE @AppendixTerms TABLE ([Id] int PRIMARY KEY);
                INSERT INTO @AppendixTerms SELECT [TemplateAppendixTermId] FROM [tbl_ContractTemplateAppendixTerm]
                    WHERE [TemplateAppendixId] IN (SELECT [Id] FROM @Appendices);
                DECLARE @Milestones TABLE ([Id] int PRIMARY KEY);
                INSERT INTO @Milestones SELECT [TemplatePaymentMilestoneId] FROM [tbl_ContractTemplatePaymentMilestone]
                    WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions);
                DECLARE @Bases TABLE ([Id] int PRIMARY KEY);
                INSERT INTO @Bases SELECT [TemplateLegalBasisId] FROM [tbl_ContractTemplateLegalBasis]
                    WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions);

                IF EXISTS (SELECT 1 FROM [tbl_Contract] WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions))
                    OR EXISTS (SELECT 1 FROM [tbl_ContractVersion] WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions))
                    OR EXISTS (SELECT 1 FROM [tbl_ContractVersionLegalSnapshot] WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions))
                    OR EXISTS (SELECT 1 FROM [tbl_ContractVersionPlaceholderValue] WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions))
                    OR EXISTS (SELECT 1 FROM [tbl_ContractAcceptanceRecord] WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions))
                    OR EXISTS (SELECT 1 FROM [tbl_ContractTerm] WHERE [SourceTemplateTermId] IN (SELECT [Id] FROM @Terms))
                    OR EXISTS (SELECT 1 FROM [tbl_ContractAppendix] WHERE [SourceTemplateAppendixId] IN (SELECT [Id] FROM @Appendices))
                    OR EXISTS (SELECT 1 FROM [tbl_ContractAppendixTerm] WHERE [SourceTemplateAppendixTermId] IN (SELECT [Id] FROM @AppendixTerms))
                    OR EXISTS (SELECT 1 FROM [tbl_ContractPaymentMilestone] WHERE [SourceTemplatePaymentMilestoneId] IN (SELECT [Id] FROM @Milestones))
                    OR EXISTS (SELECT 1 FROM [tbl_ContractVersionPaymentMilestoneSnapshot] WHERE [SourceTemplatePaymentMilestoneId] IN (SELECT [Id] FROM @Milestones))
                    OR EXISTS (SELECT 1 FROM [tbl_ContractLegalBasis] WHERE [SourceTemplateLegalBasisId] IN (SELECT [Id] FROM @Bases))
                    THROW 51000, N'Mẫu bàn giao đang bị hợp đồng/nghiệm thu tham chiếu. Dừng RetireHandoverTemplates để không xóa nhầm dữ liệu nghiệp vụ.', 1;

                DELETE FROM [tbl_ContractTemplateAppendixTerm] WHERE [TemplateAppendixId] IN (SELECT [Id] FROM @Appendices);
                DELETE FROM [tbl_ContractTemplateAppendix] WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions);
                DELETE FROM [tbl_ContractTemplatePaymentMilestone] WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions);
                DELETE FROM [tbl_ContractTemplateLegalBasis] WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions);
                DELETE FROM [tbl_ContractTemplateField] WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions);
                DELETE FROM [tbl_ContractTemplateItemTableColumnLayout] WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions);
                DELETE FROM [tbl_ContractTemplateTerm] WHERE [TemplateVersionId] IN (SELECT [Id] FROM @Versions);
                DELETE FROM [tbl_ContractTemplateVersion] WHERE [TemplateId] IN (SELECT [Id] FROM @Templates);
                DELETE FROM [tbl_ContractTemplate] WHERE [TemplateId] IN (SELECT [Id] FROM @Templates);
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_ContractTemplate_DocumentType",
                table: "tbl_ContractTemplate");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_ContractTemplate_DocumentType",
                table: "tbl_ContractTemplate",
                sql: "[DocumentType] IN (1, 2, 3, 5, 6, 7, 8, 99)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_ContractTemplate_DocumentType",
                table: "tbl_ContractTemplate");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_ContractTemplate_DocumentType",
                table: "tbl_ContractTemplate",
                sql: "[DocumentType] IN (1, 2, 3, 4, 5, 6, 7, 8, 99)");
        }
    }
}
