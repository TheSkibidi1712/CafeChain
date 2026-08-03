using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CafeChain.Application.Constants.Cloudinaries;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Data;
using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CafeChain.Application.Services.POS;

public sealed class PosSessionExchangeService : IPosSessionExchangeService
{
    private const string ActionName = "POS.SESSION.EXCHANGE";
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromSeconds(60);

    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public PosSessionExchangeService(AppDbContext dbContext, IConfiguration configuration,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public async Task<PosSessionExchangeTicketDto> IssueAsync(int accountId, int staffId, int storeId,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var actorIsActive = await _dbContext.Staffs.AsNoTracking()
            .AnyAsync(x => x.StaffId == staffId && x.AccountId == accountId
                && x.StoreId == storeId && x.Active && x.Account.Active && x.Store.Active,
                cancellationToken);
        if (!actorIsActive)
            throw new UnauthorizedAccessException("Phiên nhân viên hoặc cửa hàng không còn hoạt động.");

        var rawCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = nowUtc.Add(TicketLifetime);
        _dbContext.RequestDeduplications.Add(new RequestDeduplication
        {
            RequestKey = Hash(rawCode), ActionName = ActionName, StaffId = staffId,
            AccountId = accountId, StoreId = storeId, Status = "PROCESSING",
            PayloadHash = Hash($"{accountId}:{staffId}:{storeId}"), CreatedAt = nowUtc,
            ExpiredAt = expiresAtUtc, ProcessingLeaseUntilUtc = expiresAtUtc
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new PosSessionExchangeTicketDto(rawCode, expiresAtUtc);
    }

    public async Task<PosSessionTokenDto?> ExchangeAsync(string exchangeCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exchangeCode)) return null;
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var codeHash = Hash(exchangeCode.Trim());
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;

        var ticket = await _dbContext.RequestDeduplications.SingleOrDefaultAsync(
            x => x.RequestKey == codeHash && x.ActionName == ActionName, cancellationToken);
        if (ticket == null || ticket.Status != "PROCESSING" || ticket.ExpiredAt <= nowUtc)
        {
            if (ticket is { Status: "PROCESSING" })
            {
                ticket.Status = "EXPIRED";
                await _dbContext.SaveChangesAsync(cancellationToken);
                if (transaction != null) await transaction.CommitAsync(cancellationToken);
            }
            return null;
        }

        var account = await _dbContext.Accounts
            .Include(x => x.Staff).ThenInclude(x => x.Store)
            .Include(x => x.AccountRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.AccountId == ticket.AccountId, cancellationToken);
        if (account?.Staff == null || account.Staff.StaffId != ticket.StaffId
            || account.Staff.StoreId != ticket.StoreId || !account.Active
            || !account.Staff.Active || !account.Staff.Store.Active)
        {
            ticket.Status = "FAILED";
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return null;
        }

        ticket.Status = "SUCCESS";
        ticket.ResponseBody = "CONSUMED";
        ticket.ProcessingLeaseUntilUtc = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return CreateToken(account, nowUtc);
    }

    private PosSessionTokenDto CreateToken(CafeChain.Models.Customers.Account account, DateTime nowUtc)
    {
        var jwtKey = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
            throw new InvalidOperationException("Jwt:Key is required. Configure it with User Secrets or a deployment secret.");

        var roles = account.AccountRoles.Where(x => x.Role.Active).Select(x => x.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var avatarUrl = string.IsNullOrWhiteSpace(account.Staff.AvatarUrl)
            ? DefaultImages.StaffAvatarUrl : account.Staff.AvatarUrl;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.AccountId.ToString()),
            new(ClaimTypes.Name, account.Staff.FullName),
            new(ClaimTypes.Email, account.Email),
            new("StaffId", account.Staff.StaffId.ToString()),
            new("StoreId", account.Staff.StoreId.ToString()),
            new("AvatarUrl", avatarUrl)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var expirationHours = double.TryParse(_configuration["Jwt:ExpirationHours"], out var hours) ? hours : 12;
        var expiresAtUtc = nowUtc.AddHours(expirationHours);
        var token = new JwtSecurityToken(_configuration["Jwt:Issuer"] ?? "CafeChain",
            _configuration["Jwt:Audience"] ?? "CafeChain.POS", claims, notBefore: nowUtc,
            expires: expiresAtUtc, signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), SecurityAlgorithms.HmacSha256));
        return new PosSessionTokenDto(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
