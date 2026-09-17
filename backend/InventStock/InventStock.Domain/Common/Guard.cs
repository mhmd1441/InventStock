using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Common;

internal static class Guard
{
    internal static string AgainstNullOrWhiteSpace(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{fieldName} must not exceed {maxLength} characters.");
        }

        return trimmed;
    }

    internal static string? AgainstTooLong(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{fieldName} must not exceed {maxLength} characters.");
        }

        return trimmed;
    }

    internal static decimal AgainstNegative(decimal value, string fieldName)
    {
        if (value < 0)
        {
            throw new DomainException($"{fieldName} must not be negative.");
        }

        return value;
    }

    internal static int AgainstNegative(int value, string fieldName)
    {
        if (value < 0)
        {
            throw new DomainException($"{fieldName} must not be negative.");
        }

        return value;
    }
}
