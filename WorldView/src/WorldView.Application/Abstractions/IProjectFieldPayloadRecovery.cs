using System;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Fields;

namespace WorldView.Application.Abstractions;

/// <summary>
/// Startup hook for reconciling a package's authoritative Field Payload metadata with its
/// managed files after the Core Domain database has been opened and the Project identity read.
/// </summary>
public interface IProjectFieldPayloadRecovery
{
    Task<FieldPayloadIntegrityReport> ReconcileAsync(
        IPackageDatabase database,
        string packageRoot,
        Guid projectId,
        CancellationToken cancellationToken = default);
}
