using CarRental.Shared.ReferenceData;

namespace CarRental.WebApi.Models;

public sealed class RentalStatusHistory
{
    public int Id { get; set; }

    public int RentalId { get; set; }

    public RentalStatus? FromStatusId { get; set; }

    public RentalStatus ToStatusId { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public int? ChangedByEmployeeId { get; set; }

    public string ChangedBySource { get; set; } = ChangeSources.System;

    public Rental? Rental { get; set; }

    public Employee? ChangedByEmployee { get; set; }
}
