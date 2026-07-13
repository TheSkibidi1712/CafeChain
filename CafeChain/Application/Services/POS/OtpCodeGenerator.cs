using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.POS;

namespace CafeChain.Application.Services.POS
{
    public sealed class OtpCodeGenerator : IOtpCodeGenerator
    {
        private static readonly Regex AllowedPattern = new(
            $"^[{Regex.Escape(OtpConstants.Alphabet)}]{{{OtpConstants.CodeLength}}}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string Generate()
        {
            var alphabet = OtpConstants.Alphabet;
            var chars = new char[OtpConstants.CodeLength];
            for (var i = 0; i < chars.Length; i++)
            {
                // Unbiased index — no manual modulo on raw bytes.
                var idx = RandomNumberGenerator.GetInt32(alphabet.Length);
                chars[i] = alphabet[idx];
            }

            return new string(chars);
        }

        public string? NormalizeAndValidate(string? rawCode)
        {
            if (string.IsNullOrWhiteSpace(rawCode))
                return null;

            // Trim ends first; reject internal whitespace (do not map ambiguous chars).
            var trimmed = rawCode.Trim();
            if (trimmed.Any(char.IsWhiteSpace))
                return null;

            var normalized = trimmed.ToUpperInvariant();
            if (normalized.Length != OtpConstants.CodeLength)
                return null;

            if (!AllowedPattern.IsMatch(normalized))
                return null;

            return normalized;
        }
    }
}
