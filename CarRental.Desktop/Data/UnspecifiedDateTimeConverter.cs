using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CarRental.Desktop.Data;

internal sealed class UnspecifiedDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(
        value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
        value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified));

internal sealed class NullableUnspecifiedDateTimeConverter()
    : ValueConverter<DateTime?, DateTime?>(
        value => value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified)
            : value,
        value => value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified)
            : value);

