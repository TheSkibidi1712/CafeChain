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
using System.Text.Json;
using CafeChain.Application.Results;

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

    public async Task<PosSessionExchangeTicketDto> IssueAsync(PosSessionExchangeContextDto context,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var actorIsActive = await _dbContext.Staffs.AsNoTracking()
            .AnyAsync(x => x.StaffId == context.StaffId && x.AccountId == context.AccountId
                && x.StoreId == context.StoreId && x.Active && x.Account.Active && x.Store.Active,
                cancellationToken);
        if (!actorIsActive)
            throw new UnauthorizedAccessException("Phiên nhân viên hoặc cửa hàng không còn hoạt động.");

        var rawCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = nowUtc.Add(TicketLifetime);
        var serializedContext = JsonSerializer.Serialize(context);
        var entry = new RequestDeduplication
        {
            RequestKey = Hash(rawCode), ActionName = ActionName, StaffId = context.StaffId,
            AccountId = context.AccountId, StoreId = context.StoreId, Status = "PROCESSING",
            PayloadHash = Hash(serializedContext), ResponseBody = serializedContext, CreatedAt = nowUtc,
            ExpiredAt = expiresAtUtc, ProcessingLeaseUntilUtc = expiresAtUtc
        };
        _dbContext.RequestDeduplications.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new PosSessionExchangeTicketDto(rawCode, expiresAtUtc, entry.RequestDeduplicationId);
    }

    public async Task<ServiceResult<PosSessionTokenDto>> ExchangeAsync(string exchangeCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exchangeCode))
            return ServiceResult<PosSessionTokenDto>.Failure(
                "Mã mở POS không hợp lệ.", errorCode: PosSessionExchangeErrorCodes.Invalid);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var codeHash = Hash(exchangeCode.Trim());
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;

        var ticket = await _dbContext.RequestDeduplications.SingleOrDefaultAsync(
            x => x.RequestKey == codeHash && x.ActionName == ActionName, cancellationToken);
        if (ticket == null)
            return ServiceResult<PosSessionTokenDto>.Failure(
                "Mã mở POS không hợp lệ.", errorCode: PosSessionExchangeErrorCodes.Invalid);
        if (ticket.Status == "SUCCESS")
            return ServiceResult<PosSessionTokenDto>.Failure(
                "Mã mở POS đã được sử dụng.", errorCode: PosSessionExchangeErrorCodes.AlreadyUsed);
        if (ticket.ExpiredAt <= nowUtc || ticket.Status == "EXPIRED")
        {
            if (ticket is { Status: "PROCESSING" })
            {
                ticket.Status = "EXPIRED";
                await _dbContext.SaveChangesAsync(cancellationToken);
                if (transaction != null) await transaction.CommitAsync(cancellationToken);
            }
            return ServiceResult<PosSessionTokenDto>.Failure(
                "Mã mở POS đã hết hạn. Vui lòng quay lại StaffHub để thực hiện lại.",
                errorCode: PosSessionExchangeErrorCodes.Expired);
        }
        if (ticket.Status != "PROCESSING")
            return ServiceResult<PosSessionTokenDto>.Failure(
                "Mã mở POS không hợp lệ.", errorCode: PosSessionExchangeErrorCodes.Invalid);

        PosSessionExchangeContextDto? context;
        try
        {
            context = JsonSerializer.Deserialize<PosSessionExchangeContextDto>(ticket.ResponseBody ?? string.Empty);
        }
        catch (JsonException)
        {
            context = null;
        }
        if (context == null || context.AccountId != ticket.AccountId || context.StaffId != ticket.StaffId
            || context.StoreId != ticket.StoreId)
            return ServiceResult<PosSessionTokenDto>.Failure(
                "Ngữ cảnh mở POS không hợp lệ.", errorCode: PosSessionExchangeErrorCodes.Invalid);

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
            return ServiceResult<PosSessionTokenDto>.Failure(
                "Phiên nhân viên hoặc cửa hàng không còn hoạt động.",
                errorCode: PosSessionExchangeErrorCodes.Invalid);
        }

        var token = CreateToken(account, nowUtc, ticket.RequestDeduplicationId, context.Purpose);
        ticket.Status = "SUCCESS";
        ticket.ProcessingLeaseUntilUtc = null;
        ticket.ExpiredAt = token.ExpiresAtUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return ServiceResult<PosSessionTokenDto>.Success(token);
    }

    public async Task<PosSessionExchangeContextDto?> GetContextAsync(
        int contextId,
        int accountId,
        int staffId,
        int storeId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.RequestDeduplications.AsNoTracking().SingleOrDefaultAsync(
            x => x.RequestDeduplicationId == contextId
                && x.ActionName == ActionName
                && x.Status == "SUCCESS"
                && x.AccountId == accountId
                && x.StaffId == staffId
                && x.StoreId == storeId
                && x.ExpiredAt > _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (ticket?.ResponseBody == null) return null;
        try
        {
            return JsonSerializer.Deserialize<PosSessionExchangeContextDto>(ticket.ResponseBody);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private PosSessionTokenDto CreateToken(CafeChain.Models.Customers.Account account, DateTime nowUtc,
        int contextId, string purpose)
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
            new("AvatarUrl", avatarUrl),
            new("PosExchangeContextId", contextId.ToString()),
            new("PosPurpose", purpose)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var expirationHours = double.TryParse(_configuration["Jwt:ExpirationHours"], out var hours) ? hours : 12;
        var expiresAtUtc = nowUtc.AddHours(expirationHours);
        var token = new JwtSecurityToken(_configuration["Jwt:Issuer"] ?? "CafeChain",
            _configuration["Jwt:Audience"] ?? "CafeChain.POS", claims, notBefore: nowUtc,
            expires: expiresAtUtc, signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), SecurityAlgorithms.HmacSha256));
        return new PosSessionTokenDto(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc, contextId, purpose);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
