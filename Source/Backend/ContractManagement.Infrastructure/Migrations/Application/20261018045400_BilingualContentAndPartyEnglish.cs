using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class BilingualContentAndPartyEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressEn",
                table: "tbl_TenantLegalProfile",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankNameEn",
                table: "tbl_TenantLegalProfile",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalEntityNameEn",
                table: "tbl_TenantLegalProfile",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeTitleEn",
                table: "tbl_TenantLegalProfile",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerAddressEn",
                table: "tbl_Customers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerBankNameEn",
                table: "tbl_Customers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCityEn",
                table: "tbl_Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCompanyEn",
                table: "tbl_Customers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerContactPersonTitleEn",
                table: "tbl_Customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCountryEn",
                table: "tbl_Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerRepresentativeTitleEn",
                table: "tbl_Customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressEn",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankNameEn",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CityEn",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactTitleEn",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryEn",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalNameEn",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeTitleEn",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "BilingualContentMode",
                table: "tbl_ContractTerm",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<byte>(
                name: "BilingualContentMode",
                table: "tbl_ContractTemplateTerm",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<byte>(
                name: "BilingualContentMode",
                table: "tbl_ContractTemplateAppendixTerm",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<byte>(
                name: "BilingualContentMode",
                table: "tbl_ContractAppendixTerm",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<byte>(
                name: "BilingualContentMode",
                table: "tbl_ContractAcceptanceSection",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<string>(
                name: "LocationEn",
                table: "tbl_ContractAcceptanceRecord",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressEn",
                table: "tbl_ContractAcceptanceParty",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalNameEn",
                table: "tbl_ContractAcceptanceParty",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeTitleEn",
                table: "tbl_ContractAcceptanceParty",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantLegalProfile_AddressEn",
                table: "tbl_TenantLegalProfile",
                sql: "LEN(LTRIM(RTRIM([AddressEn]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantLegalProfile_BankNameEn",
                table: "tbl_TenantLegalProfile",
                sql: "LEN(LTRIM(RTRIM([BankNameEn]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantLegalProfile_LegalEntityNameEn",
                table: "tbl_TenantLegalProfile",
                sql: "LEN(LTRIM(RTRIM([LegalEntityNameEn]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantLegalProfile_RepresentativeTitleEn",
                table: "tbl_TenantLegalProfile",
                sql: "LEN(LTRIM(RTRIM([RepresentativeTitleEn]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TblContractTerm_BilingualContentMode",
                table: "tbl_ContractTerm",
                sql: "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TblContractTemplateTerm_BilingualContentMode",
                table: "tbl_ContractTemplateTerm",
                sql: "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TblContractTemplateAppendixTerm_BilingualContentMode",
                table: "tbl_ContractTemplateAppendixTerm",
                sql: "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TblContractAppendixTerm_BilingualContentMode",
                table: "tbl_ContractAppendixTerm",
                sql: "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TblContractAcceptanceSection_BilingualContentMode",
                table: "tbl_ContractAcceptanceSection",
                sql: "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [ContentEn] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantLegalProfile_AddressEn",
                table: "tbl_TenantLegalProfile");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantLegalProfile_BankNameEn",
                table: "tbl_TenantLegalProfile");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantLegalProfile_LegalEntityNameEn",
                table: "tbl_TenantLegalProfile");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantLegalProfile_RepresentativeTitleEn",
                table: "tbl_TenantLegalProfile");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TblContractTerm_BilingualContentMode",
                table: "tbl_ContractTerm");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TblContractTemplateTerm_BilingualContentMode",
                table: "tbl_ContractTemplateTerm");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TblContractTemplateAppendixTerm_BilingualContentMode",
                table: "tbl_ContractTemplateAppendixTerm");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TblContractAppendixTerm_BilingualContentMode",
                table: "tbl_ContractAppendixTerm");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TblContractAcceptanceSection_BilingualContentMode",
                table: "tbl_ContractAcceptanceSection");

            migrationBuilder.DropColumn(
                name: "AddressEn",
                table: "tbl_TenantLegalProfile");

            migrationBuilder.DropColumn(
                name: "BankNameEn",
                table: "tbl_TenantLegalProfile");

            migrationBuilder.DropColumn(
                name: "LegalEntityNameEn",
                table: "tbl_TenantLegalProfile");

            migrationBuilder.DropColumn(
                name: "RepresentativeTitleEn",
                table: "tbl_TenantLegalProfile");

            migrationBuilder.DropColumn(
                name: "CustomerAddressEn",
                table: "tbl_Customers");

            migrationBuilder.DropColumn(
                name: "CustomerBankNameEn",
                table: "tbl_Customers");

            migrationBuilder.DropColumn(
                name: "CustomerCityEn",
                table: "tbl_Customers");

            migrationBuilder.DropColumn(
                name: "CustomerCompanyEn",
                table: "tbl_Customers");

            migrationBuilder.DropColumn(
                name: "CustomerContactPersonTitleEn",
                table: "tbl_Customers");

            migrationBuilder.DropColumn(
                name: "CustomerCountryEn",
                table: "tbl_Customers");

            migrationBuilder.DropColumn(
                name: "CustomerRepresentativeTitleEn",
                table: "tbl_Customers");

            migrationBuilder.DropColumn(
                name: "AddressEn",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "BankNameEn",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "CityEn",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "ContactTitleEn",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "CountryEn",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "LegalNameEn",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "RepresentativeTitleEn",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "BilingualContentMode",
                table: "tbl_ContractTerm");

            migrationBuilder.DropColumn(
                name: "BilingualContentMode",
                table: "tbl_ContractTemplateTerm");

            migrationBuilder.DropColumn(
                name: "BilingualContentMode",
                table: "tbl_ContractTemplateAppendixTerm");

            migrationBuilder.DropColumn(
                name: "BilingualContentMode",
                table: "tbl_ContractAppendixTerm");

            migrationBuilder.DropColumn(
                name: "BilingualContentMode",
                table: "tbl_ContractAcceptanceSection");

            migrationBuilder.DropColumn(
                name: "LocationEn",
                table: "tbl_ContractAcceptanceRecord");

            migrationBuilder.DropColumn(
                name: "AddressEn",
                table: "tbl_ContractAcceptanceParty");

            migrationBuilder.DropColumn(
                name: "LegalNameEn",
                table: "tbl_ContractAcceptanceParty");

            migrationBuilder.DropColumn(
                name: "RepresentativeTitleEn",
                table: "tbl_ContractAcceptanceParty");
        }
    }
}
