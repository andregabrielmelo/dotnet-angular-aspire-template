namespace AppTemplate.SharedKernel;

/// <summary>
/// Opts an entity into the audit log: every insert, update and delete of it is recorded in the
/// same transaction, but only for the properties its auditing configuration allowlists
/// (<c>AddAuditing</c> in Infrastructure). See ADR 017.
/// </summary>
public interface IAuditable;
