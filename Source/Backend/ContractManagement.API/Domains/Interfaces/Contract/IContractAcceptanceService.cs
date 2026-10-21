using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Contract;

namespace ContractManagement.Domains.Interfaces.Contract;

public interface IContractAcceptanceService
{
    Task<IReadOnlyList<ContractAcceptanceTemplateOptionResponse>> GetTemplatesAsync(
        int contractId, int employeeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContractAcceptanceRecordSummaryResponse>> ListAsync(
        int contractId, int employeeId, CancellationToken cancellationToken = default);
    Task<ContractAcceptanceRecordResponse> GetAsync(
        int contractId, int acceptanceRecordId, int employeeId,
        CancellationToken cancellationToken = default);
    Task<AcceptanceHandoverOptionsResponse> HandoverOptionsAsync(int contractId, int acceptanceRecordId, int employeeId, CancellationToken ct = default);
    Task<ContractAcceptanceRecordResponse> ReplaceHandoverScopeAsync(int contractId, int acceptanceRecordId,
        ReplaceAcceptanceHandoverScopeRequest request, int employeeId, CancellationToken ct = default);
    Task<ContractAcceptanceRecordResponse> CreateAsync(
        int contractId, CreateContractAcceptanceRecordRequest request,
        int employeeId, CancellationToken cancellationToken = default);
    Task<ContractAcceptanceRecordResponse> UpdateAsync(
        int contractId, int acceptanceRecordId,
        UpdateContractAcceptanceRecordRequest request, int employeeId,
        CancellationToken cancellationToken = default);
    Task<ContractAcceptanceRecordResponse> ReplaceReferencesAsync(
        int contractId, int acceptanceRecordId,
        ReplaceContractAcceptanceReferencesRequest request, int employeeId,
        CancellationToken cancellationToken = default);
    Task<ContractAcceptanceRecordResponse> ReplaceSectionsAsync(
        int contractId, int acceptanceRecordId,
        ReplaceContractAcceptanceSectionsRequest request, int employeeId,
        CancellationToken cancellationToken = default);
    Task<ContractAcceptanceRecordResponse> ReplaceMilestonesAsync(
        int contractId, int acceptanceRecordId,
        ReplaceContractAcceptanceMilestonesRequest request, int employeeId,
        CancellationToken cancellationToken = default);
    Task<ContractAcceptanceDocumentContent> PreviewAsync(
        int contractId, int acceptanceRecordId, string format, int employeeId,
        CancellationToken cancellationToken = default);
    Task<ContractAcceptanceRecordResponse> FinalizeAsync(
        int contractId, int acceptanceRecordId,
        FinalizeContractAcceptanceRecordRequest request, int employeeId,
        CancellationToken cancellationToken = default);
    Task<ContractAcceptanceEvidenceResponse> UploadSignedEvidenceAsync(
        int contractId, int acceptanceRecordId,
        UploadContractAcceptanceSignedEvidenceRequest request, int employeeId,
        CancellationToken cancellationToken = default);
    Task<ContractAcceptanceRecordResponse> CancelAsync(
        int contractId, int acceptanceRecordId,
        CancelContractAcceptanceRecordRequest request, int employeeId,
        CancellationToken cancellationToken = default);
    Task<ContractAcceptanceDocumentContent> DownloadArtifactAsync(
        int contractId, int acceptanceRecordId, string format, int employeeId,
        CancellationToken cancellationToken = default);
}
