using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace CarRental.WebApi.Infrastructure;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class DecimalRangeInvariantAttribute : ValidationAttribute
{
    private readonly decimal minimum;
    private readonly decimal maximum;
    private readonly string minimumText;
    private readonly string maximumText;

    public DecimalRangeInvariantAttribute(string minimum, string maximum)
    {
        minimumText = minimum;
        maximumText = maximum;
        this.minimum = decimal.Parse(minimum, NumberStyles.Number, CultureInfo.InvariantCulture);
        this.maximum = decimal.Parse(maximum, NumberStyles.Number, CultureInfo.InvariantCulture);

        ErrorMessage = "The field {0} must be between {1} and {2}.";
    }

    public override string FormatErrorMessage(string name)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            ErrorMessageString,
            name,
            minimumText,
            maximumText);
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (!TryConvertToDecimal(value, out var decimalValue))
        {
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
        }

        if (decimalValue < minimum || decimalValue > maximum)
        {
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
        }

        return ValidationResult.Success;
    }

    private static bool TryConvertToDecimal(object value, out decimal decimalValue)
    {
        switch (value)
        {
            case decimal directDecimal:
                decimalValue = directDecimal;
                return true;
            case byte byteValue:
                decimalValue = byteValue;
                return true;
            case sbyte sbyteValue:
                decimalValue = sbyteValue;
                return true;
            case short shortValue:
                decimalValue = shortValue;
                return true;
            case ushort ushortValue:
                decimalValue = ushortValue;
                return true;
            case int intValue:
                decimalValue = intValue;
                return true;
            case uint uintValue:
                decimalValue = uintValue;
                return true;
            case long longValue:
                decimalValue = longValue;
                return true;
            case ulong ulongValue:
                decimalValue = ulongValue;
                return true;
            case float floatValue:
                decimalValue = (decimal)floatValue;
                return true;
            case double doubleValue:
                decimalValue = (decimal)doubleValue;
                return true;
            case string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed):
                decimalValue = parsed;
                return true;
            default:
                decimalValue = default;
                return false;
        }
    }
}
