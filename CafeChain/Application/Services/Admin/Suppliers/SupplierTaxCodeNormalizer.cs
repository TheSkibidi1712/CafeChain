using CafeChain.Application.Constants;
using CafeChain.Application.Exceptions;
using System.Text.RegularExpressions;

namespace CafeChain.Application.Services.Admin.Suppliers
{
    public static partial class SupplierTaxCodeNormalizer
    {
        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var compact = WhitespaceRegex().Replace(value.Trim(), "")
                .Replace('\u2010', '-')
                .Replace('\u2011', '-')
                .Replace('\u2012', '-')
                .Replace('\u2013', '-')
                .Replace('\u2212', '-');

            if (DigitsOnlyRegex().IsMatch(compact) && compact.Length == 13)
                compact = $"{compact[..10]}-{compact[10..]}";

            if (!CanonicalRegex().IsMatch(compact))
            {
                throw new SupplierDomainException(
                    SupplierIdentityConstants.TaxCodeInvalid,
                    "Mã số thuế phải gồm 10 chữ số hoặc 10 chữ số, dấu gạch ngang và 3 chữ số.");
            }

            return compact;
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
        private static partial Regex DigitsOnlyRegex();

        [GeneratedRegex(@"^\d{10}(-\d{3})?$", RegexOptions.CultureInvariant)]
        private static partial Regex CanonicalRegex();
    }
}
