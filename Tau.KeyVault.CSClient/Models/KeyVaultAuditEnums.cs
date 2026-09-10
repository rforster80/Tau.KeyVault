namespace Tau.KeyVault.Client.Models;

/// <summary>Action recorded in the access audit log.</summary>
public enum KeyVaultAuditAction
{
    ReadKey,
    ListKeys,
    WriteKey,
    DeleteKey,
    ListEnvironments,
    DeleteEnvironment,
    RenameEnvironment,
    Export,
    Import,
    ReadAudit,
    AuthFailure
}

/// <summary>Result of an audited action.</summary>
public enum KeyVaultAuditOutcome
{
    Success,
    NotFound,
    Denied,
    Error
}

/// <summary>Kind of actor an audit row attributes the action to.</summary>
public enum KeyVaultAuditActorType
{
    Anonymous,
    ApiKey,
    User,
    System
}
