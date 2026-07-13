namespace CafeChain.Application.Interfaces.POS
{
    public interface IOtpCodeGenerator
    {
        /// <summary>Generate a 6-character OTP from the allowed alphabet using cryptographic RNG.</summary>
        string Generate();

        /// <summary>Normalize and validate user input. Returns null if invalid.</summary>
        string? NormalizeAndValidate(string? rawCode);
    }
}
