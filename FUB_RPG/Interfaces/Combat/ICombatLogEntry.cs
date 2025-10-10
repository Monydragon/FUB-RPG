using System;
using Fub.Enums;

namespace Fub.Interfaces.Combat;

public interface ICombatLogEntry
{
    DateTime Timestamp { get; }
    CombatLogEntryType EntryType { get; }
    string Message { get; }
    Guid? SourceId { get; }
    Guid? TargetId { get; }
    double? Amount { get; }
}

