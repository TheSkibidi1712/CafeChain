using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.POS;

public sealed class PosSessionExchangeRequestDto
{
    [Required, StringLength(200, MinimumLength = 32)]
    public string ExchangeCode { get; set; } = string.Empty;
}

public sealed record PosSessionExchangeTicketDto(string ExchangeCode, DateTime ExpiresAtUtc);

public sealed record PosSessionTokenDto(string Token, DateTime ExpiresAtUtc);
