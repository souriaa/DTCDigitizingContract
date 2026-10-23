using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.Contract;

public sealed partial class ContractAcceptanceService
{
    private async Task LockContractAsync(int contractId, CancellationToken ct)
    {
        // Serialize acceptance mutations with other closing operations on the same contract.
        if (!UsesInMemoryProvider())
            await _db.TblContracts.FromSqlInterpolated(
                $"SELECT * FROM [tbl_Contract] WITH (UPDLOCK,HOLDLOCK) WHERE [ContractId] = {contractId}").LoadAsync(ct);
    }

    private static void ValidateAcceptanceDate(DateTime date, DateTime contractSignDate)
    {
        if (date.Date < contractSignDate.Date)
            throw Rule("AcceptanceDateInvalid",
                $"Ngày nghiệm thu không được trước ngày ký hợp đồng ({contractSignDate:dd/MM/yyyy}).");
    }
}
