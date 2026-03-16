using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace FinanceAPI.Validation;

[AttributeUsage(AttributeTargets.Property)]
public class ValidDateAttribute : ValidationAttribute
{
    public ValidDateAttribute() : base("Date must be a valid calendar date in YYYY-MM-DD format.")
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string s && !DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return new ValidationResult(ErrorMessage);
        }

        return ValidationResult.Success;
    }
}
