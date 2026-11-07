using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenConquer.Client.Diagnostics;

/// <summary>Emits fixed messages with allowlisted numeric context. Never pass exception objects to the logger.</summary>
internal sealed partial class ClientDiagnostics(ILogger logger)
{
    internal static ClientDiagnostics Silent { get; } = new(NullLogger.Instance);

    [LoggerMessage(2100, LogLevel.Error, "Startup arguments rejected (code {ArgumentCode}). Usage: OpenConquer.Client [--content-root <path>] [--window-size <WIDTHxHEIGHT>] [--window-mode <resizable|fixed|fullscreen>] [--presentation <fit|integer|stretch>]")]
    internal partial void StartupArgumentsRejected(int argumentCode);

    [LoggerMessage(2101, LogLevel.Error, "Client stopped (failure {FailureCode}).")]
    internal partial void ClientStopped(int failureCode);

    [LoggerMessage(2102, LogLevel.Error, "Client cleanup failed (failure {FailureCode}).")]
    internal partial void ClientCleanupFailed(int failureCode);

    [LoggerMessage(2103, LogLevel.Warning, "Startup logo unavailable. Continuing without the splash.")]
    internal partial void StartupLogoUnavailable();

    [LoggerMessage(2104, LogLevel.Warning, "Package declaration {DeclarationIndex} was skipped (outcome {OutcomeCode}).")]
    internal partial void PackageSkipped(int declarationIndex, int outcomeCode);

    [LoggerMessage(2200, LogLevel.Warning, "Revoked GameServer session cleanup failed (failure {FailureCode}).")]
    internal partial void RevokedSessionCleanupFailed(int failureCode);

    [LoggerMessage(2201, LogLevel.Warning, "Role creation content unavailable (failure {FailureCode}).")]
    internal partial void RoleContentUnavailable(int failureCode);

    [LoggerMessage(2202, LogLevel.Warning, "User-local account history unavailable (failure {FailureCode}). Continuing without persistence.")]
    internal partial void HistoryUnavailable(int failureCode);

    [LoggerMessage(2203, LogLevel.Warning, "User-local account history could not be validated (failure {FailureCode}). Updates disabled.")]
    internal partial void HistoryRejected(int failureCode);

    [LoggerMessage(2204, LogLevel.Warning, "User-local account history could not be saved (failure {FailureCode}). Updates disabled.")]
    internal partial void HistorySaveFailed(int failureCode);

    [LoggerMessage(2205, LogLevel.Warning, "Startup audio unavailable (failure {FailureCode}). Continuing silently.")]
    internal partial void AudioUnavailable(int failureCode);

    [LoggerMessage(2206, LogLevel.Warning, "Desktop clipboard unavailable.")]
    internal partial void ClipboardUnavailable();

    [LoggerMessage(2207, LogLevel.Warning, "Realm catalog authority is unconfigured. No servers are available.")]
    internal partial void CatalogUnconfigured();

    [LoggerMessage(2208, LogLevel.Warning, "Authenticated realm discovery is unavailable or rejected.")]
    internal partial void CatalogUnavailable();

    [LoggerMessage(2300, LogLevel.Warning, "World connection closed (failure {FailureCode}).")]
    internal partial void WorldConnectionClosed(int failureCode);

    [LoggerMessage(2301, LogLevel.Error, "Required world entry content unavailable (failure {FailureCode}).")]
    internal partial void WorldEntryContentUnavailable(int failureCode);

    [LoggerMessage(2302, LogLevel.Warning, "Character mesh resources are missing. Continuing without a role renderer.")]
    internal partial void WorldRoleMissing();

    [LoggerMessage(2303, LogLevel.Error, "Character artwork unavailable (failure {FailureCode}).")]
    internal partial void WorldRoleUnavailable(int failureCode);

    [LoggerMessage(2304, LogLevel.Error, "World presentation content unavailable (failure {FailureCode}).")]
    internal partial void WorldPresentationUnavailable(int failureCode);

    [LoggerMessage(2305, LogLevel.Warning, "Character model {ModelId} for equipment slot {EquipmentSlot} is missing.")]
    internal partial void WorldRoleModelMissing(uint modelId, int equipmentSlot);

    [LoggerMessage(2306, LogLevel.Warning, "World packet {PacketType} or action {ActionCode} is not implemented.")]
    internal partial void UnhandledWorldPacket(ushort packetType, ushort actionCode);

    [LoggerMessage(2307, LogLevel.Warning, "World attribute group {AttributeCode} is not implemented.")]
    internal partial void UnhandledWorldAttribute(uint attributeCode);

    [LoggerMessage(2308, LogLevel.Warning, "World reached its {PacketLimit}-entry unsupported packet diagnostic limit.")]
    internal partial void WorldPacketLimitReached(int packetLimit);
}
