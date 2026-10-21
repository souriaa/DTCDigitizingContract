using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
namespace ContractManagement.Domains.Interfaces.Contract;
public interface IContractHandoverService
{
    Task<IReadOnlyList<HandoverRecordSummary>> ListAsync(int contractId, int employeeId, CancellationToken ct = default);
    Task<HandoverDetailResponse> GetAsync(int contractId, int recordId, int employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<HandoverTemplateOption>> TemplatesAsync(int contractId, int employeeId, CancellationToken ct = default);
    Task<HandoverReadinessResponse> ReadinessAsync(int contractId, int employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<HandoverScopeCandidateResponse>> ScopeCandidatesAsync(int contractId, int employeeId, CancellationToken ct = default);
    Task<HandoverReadinessResponse> ConfirmScopeAsync(int contractId, ConfirmHandoverScopeRequest request, int employeeId, CancellationToken ct = default);
    Task<HandoverDetailResponse> CreateAsync(int contractId, CreateContractHandoverRequest request, int employeeId, CancellationToken ct = default);
    Task<HandoverDetailResponse> UpdateAsync(int contractId, int recordId, UpdateContractHandoverRequest request, int employeeId, CancellationToken ct = default);
    Task<ContractAcceptanceDocumentContent> PreviewAsync(int contractId, int recordId, string format, int employeeId, CancellationToken ct = default);
    Task<HandoverDetailResponse> FinalizeAsync(int contractId, int recordId, HandoverRowVersionRequest request, int employeeId, CancellationToken ct = default);
    Task<HandoverDetailResponse> SignAsync(int contractId, int recordId, UploadHandoverSignedRequest request, int employeeId, CancellationToken ct = default);
    Task<HandoverDetailResponse> CancelAsync(int contractId, int recordId, CancelContractHandoverRequest request, int employeeId, CancellationToken ct = default);
    Task<ContractAcceptanceDocumentContent> DownloadAsync(int contractId, int recordId, string format, bool evidence, int employeeId, CancellationToken ct = default);
}
