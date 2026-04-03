using CarRental.Shared.ReferenceData;

namespace CarRental.Desktop.Models;

public sealed class DamageStatusHistory
{
    public int Id { get; set; }

    public int DamageId { get; set; }

    public DamageStatus? FromStatusId { get; set; }

    public DamageStatus ToStatusId { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public int? ChangedByEmployeeId { get; set; }

    public string ChangedBySource { get; set; } = ChangeSources.System;

    public Damage? Damage { get; set; }

    public Employee? ChangedByEmployee { get; set; }
}

