using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Operations;

/// <summary>Revocable POS access session. It is not a WorkShift and never owns cash.</summary>
public class PosAccessSession
{
    public long PosAccessSessionId { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string JwtId { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public int StaffId { get; set; }
    public int StoreId { get; set; }
    public string TerminalId { get; set; } = string.Empty;
    public int? WorkShiftId { get; set; }
    public int ExchangeContextId { get; set; }
    public string Status { get; set; } = PosAccessSessionStatuses.Active;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public int? EndedByStaffId { get; set; }
    public string? EndReason { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public virtual Account Account { get; set; } = null!;
    public virtual Staff Staff { get; set; } = null!;
    public virtual Store Store { get; set; } = null!;
    public virtual PosTerminal Terminal { get; set; } = null!;
    public virtual WorkShift? WorkShift { get; set; }
    public virtual Staff? EndedByStaff { get; set; }
}

public static class PosAccessSessionStatuses
{
    public const string Active = "ACTIVE";
    public const string LoggedOut = "LOGGED_OUT";
    public const string Replaced = "REPLACED";
    public const string Revoked = "REVOKED";
    public const string AdminEnded = "ADMIN_ENDED";
    public const string Expired = "EXPIRED";
    public const string TerminalLocked = "TERMINAL_LOCKED";
}
