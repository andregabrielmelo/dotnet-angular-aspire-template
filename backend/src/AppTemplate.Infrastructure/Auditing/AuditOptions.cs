using System.Linq.Expressions;
using System.Reflection;

namespace AppTemplate.Infrastructure.Auditing;

public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>False turns auditing off entirely (nothing is recorded); see "Removing it" in ADR 017.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Entries older than this are deleted daily by <see cref="AuditRetentionJob"/>.</summary>
    public int RetentionDays { get; set; } = 365;

    internal Dictionary<Type, HashSet<string>> AuditedProperties { get; } = [];

    /// <summary>
    /// Audits <typeparamref name="TEntity"/>, recording changes to exactly these properties.
    /// Anything not listed is never written to the log; that keeps secrets and noise out by
    /// default. Properties (or their types) marked <c>[PersonalData]</c>/<c>[SecretData]</c>
    /// are recorded as changed, with their values masked.
    /// </summary>
    public AuditOptions Audit<TEntity>(params Expression<Func<TEntity, object?>>[] properties)
        where TEntity : IAuditable
    {
        AuditedProperties[typeof(TEntity)] = properties
            .Select(property => MemberOf(property.Body).Name)
            .ToHashSet(StringComparer.Ordinal);
        return this;
    }

    private static MemberInfo MemberOf(Expression expression) =>
        expression switch
        {
            MemberExpression member => member.Member,
            UnaryExpression { Operand: MemberExpression member } => member.Member,
            _ => throw new ArgumentException($"'{expression}' isn't a property of the entity."),
        };
}
