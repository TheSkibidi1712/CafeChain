//using CafeChain.Application.Constants;
//using CafeChain.Application.DTOs.Admin.InventoryTransfers;
//using CafeChain.Application.DTOs.Admin.RestockRequests;
//using CafeChain.Application.DTOs.Admin.Actor;
//using CafeChain.Application.DTOs.Inventories;
//using CafeChain.Application.DTOs.POS;
//using CafeChain.Application.DTOs.Systems;
//using CafeChain.Application.Interfaces.Admin.Actor;
//using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
//using CafeChain.Application.Interfaces.Inventories;
//using CafeChain.Application.Interfaces.Security;
//using CafeChain.Application.Interfaces.Systems;
//using CafeChain.Application.Results;
//using CafeChain.Application.Services.Admin.InventoryTransfers;
//using CafeChain.Application.Services.Inventories;
//using CafeChain.Application.Services.Security;
//using CafeChain.Application.Services.Systems;
//using CafeChain.Data;
//using CafeChain.Infrastrusture.Interfaces.Systems;
//using CafeChain.Infrastrusture.Repositories.Admin.InventoryTransfers;
//using CafeChain.Infrastrusture.Repositories.Systems;
//using CafeChain.Models.Customers;
//using CafeChain.Models.Enums.Inventory;
//using CafeChain.Models.Enums.Unit;
//using CafeChain.Models.Inventories.Costing;
//using CafeChain.Models.Inventories.Ingredients;
//using CafeChain.Models.Inventories.Stock;
//using CafeChain.Models.Inventories.Transactions;
//using CafeChain.Models.Inventories.Transfers;
//using CafeChain.Models.Permissions;
//using CafeChain.Models.Staffs;
//using CafeChain.Models.Stores;
//using CafeChain.Models.Systems;
//using Microsoft.AspNetCore.Http;
//using Microsoft.Data.SqlClient;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging.Abstractions;
//using Moq;
//using Xunit;

//namespace CafeChain.Tests;

//[Trait("Category", "SqlServerIntegration")]
//public sealed class InventoryTransferDiscrepancySqlServerIssue194Tests : IAsyncLifetime
//{
//    private const string Database = "CafeChain_SC02AcceptanceTests";
//    private static string ConnectionString => SqlServerTestConnection.Create(Database);
//    private static string MasterConnectionString => SqlServerTestConnection.MasterConnectionString();

//    public async Task InitializeAsync()
//    {
//        try
//        {
//            await using (var master = new SqlConnection(MasterConnectionString))
//            {
//                await master.OpenAsync();
//                await using var command = master.CreateCommand();
//                command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
//                await command.ExecuteNonQueryAsync();
//            }

//            await using var context = CreateContext();
//            await context.Database.EnsureDeletedAsync();
//            await context.Database.EnsureCreatedAsync();
//        }
//        catch (Exception ex)
//        {
//            throw new InvalidOperationException(
//                $"SQL Server integration environment unavailable for SC-02. " +
//                $"Set {SqlServerTestConnection.EnvVarName}. Database={Database}. {ex.Message}",
//                ex);
//        }
//    }

//    public Task DisposeAsync() => Task.CompletedTask;

//    [Fact]
//    public async Task SqlServer_ConcurrentDestinationReceipts_DoNotOverReceive()
//    {
//        var transfer = await SeedAndDispatchAsync();
//        var outcomes = await Task.WhenAll(
//            TryReceiveAsync(transfer, 6m, 0m, "sc02-concurrent-receive-a"),
//            TryReceiveAsync(transfer, 6m, 0m, "sc02-concurrent-receive-b"));

//        await using var verify = CreateContext();
//        var detail = await verify.InventoryTransferDetails.SingleAsync(x =>
//            x.InventoryTransferId == transfer.TransferId);
//        var accepted = await verify.BranchReceiptLines
//            .Where(x => x.SourceInventoryTransferDetailId == transfer.DetailId)
//            .SumAsync(x => x.ReceivedBaseQuantity);

//        Assert.Equal(1, outcomes.Count(x => x.Succeeded));
//        Assert.Equal(6m, accepted);
//        Assert.InRange(detail.ReceivedBaseQuantity, 0m, 10m);
//        Assert.True(detail.ReceivedBaseQuantity <= detail.DispatchedBaseQuantity);
//    }

//    [Fact]
//    public async Task SqlServer_ReceiveAndWriteOff_OneWinner()
//    {
//        var transfer = await SeedAndDispatchAsync();
//        var outcomes = await Task.WhenAll(
//            TryReceiveAsync(transfer, 8m, 0m, "sc02-receive-vs-writeoff-receive"),
//            TryResolveAsync(transfer, transfer.RowVersion, 10m, InventoryTransferDiscrepancyPostingType.WRITTEN_OFF,
//                "sc02-receive-vs-writeoff-resolve"));

//        await using var verify = CreateContext();
//        var postings = await verify.InventoryTransferDiscrepancyPostings
//            .Where(x => x.InventoryTransferDetailId == transfer.DetailId)
//            .ToListAsync();
//        var accepted = await verify.BranchReceiptLines
//            .Where(x => x.SourceInventoryTransferDetailId == transfer.DetailId)
//            .SumAsync(x => x.ReceivedBaseQuantity);

//        Assert.Equal(1, outcomes.Count(x => x.Succeeded));
//        Assert.True(accepted == 8m || postings.Sum(x => x.Quantity) == 10m);
//        Assert.True(accepted + postings.Sum(x => x.Quantity) <= 10m);
//    }

//    [Fact]
//    public async Task SqlServer_ReturnAndWriteOff_OneWinner()
//    {
//        var transfer = await SeedAndDispatchAsync();
//        var received = await ReceiveAsync(transfer, 8m, 2m, "sc02-return-prepare-receive");
//        var returnRequest = await RequestReturnAsync(transfer, received.RowVersion, 2m,
//            "sc02-return-prepare-request");

//        var outcomes = await Task.WhenAll(
//            TryConfirmReturnAsync(transfer, returnRequest.RowVersion, 2m, "sc02-return-vs-writeoff-return"),
//            TryResolveAsync(transfer, returnRequest.RowVersion, 2m,
//                InventoryTransferDiscrepancyPostingType.WRITTEN_OFF, "sc02-return-vs-writeoff-resolve"));

//        await using var verify = CreateContext();
//        var returned = await verify.InventoryTransferDiscrepancyPostings
//            .Where(x => x.InventoryTransferDetailId == transfer.DetailId
//                && x.PostingType == InventoryTransferDiscrepancyPostingType.RETURNED_TO_SOURCE)
//            .SumAsync(x => x.Quantity);
//        var writtenOff = await verify.InventoryTransferDiscrepancyPostings
//            .Where(x => x.InventoryTransferDetailId == transfer.DetailId
//                && x.PostingType == InventoryTransferDiscrepancyPostingType.WRITTEN_OFF)
//            .SumAsync(x => x.Quantity);

//        Assert.Equal(1, outcomes.Count(x => x.Succeeded));
//        Assert.Equal(2m, returned + writtenOff);
//        Assert.True(returned == 2m || writtenOff == 2m);
//    }

//    [Fact]
//    public async Task SqlServer_RequestKeyReplay_CreatesOnePosting()
//    {
//        var transfer = await SeedAndDispatchAsync();
//        var dto = ReceiveDto(transfer, "sc02-receive-replay", 8m, 2m);

//        var first = await ReceiveAsync(transfer, dto);
//        var replay = await ReceiveAsync(transfer, dto);
//        var conflictingReplay = ReceiveDto(transfer, "sc02-receive-replay", 7m, 3m);

//        await Assert.ThrowsAsync<InvalidOperationException>(() =>
//            ReceiveAsync(transfer, conflictingReplay));

//        await using var verify = CreateContext();
//        var receiptCount = await verify.BranchReceipts.CountAsync(x =>
//            x.SourceInventoryTransferId == transfer.TransferId);
//        var rejectionCount = await verify.InventoryTransferDiscrepancyPostings.CountAsync(x =>
//            x.InventoryTransferDetailId == transfer.DetailId
//            && x.PostingType == InventoryTransferDiscrepancyPostingType.DESTINATION_REJECTED);
//        var transactionCount = await verify.InventoryTransactions.CountAsync(x =>
//            x.InventoryTransferDetailId == transfer.DetailId
//            && x.StoreInventory.StoreId == transfer.DestinationStoreId);
//        var fulfillmentQuantity = await verify.RestockFulfillmentPostings
//            .Where(x => x.RestockRequestId == transfer.RestockRequestId)
//            .SumAsync(x => x.Quantity);

//        Assert.Equal(first.InventoryTransferId, replay.InventoryTransferId);
//        Assert.Equal(InventoryTransferStatus.CANCELLED, replay.Status);
//        Assert.Equal(1, receiptCount);
//        Assert.Equal(1, rejectionCount);
//        Assert.Equal(1, transactionCount);
//        Assert.Equal(8m, fulfillmentQuantity);
//    }

//    [Fact]
//    public async Task SqlServer_TransferAggregateMatchesPostingLedger()
//    {
//        var transfer = await SeedAndDispatchAsync();
//        var firstReceipt = await ReceiveAsync(transfer, 8m, 0m, "sc02-ledger-first-receive");

//        await using (var midway = CreateContext())
//        {
//            var midwayDetail = await midway.InventoryTransferDetails.SingleAsync(x =>
//                x.InventoryTransferDetailId == transfer.DetailId);
//            var midwayPostings = await midway.InventoryTransferDiscrepancyPostings
//                .Where(x => x.InventoryTransferDetailId == transfer.DetailId)
//                .ToListAsync();
//            var midwayAuthority = InventoryTransferQuantityAuthority.Calculate(midwayDetail, midwayPostings);
//            var midwayDestination = await midway.StoreInventories.SingleAsync(x =>
//                x.StoreId == transfer.DestinationStoreId && x.IngredientId == transfer.IngredientId);
//            var midwayFulfilled = await midway.RestockFulfillmentPostings
//                .Where(x => x.RestockRequestId == transfer.RestockRequestId)
//                .SumAsync(x => x.Quantity);

//            Assert.Equal(8m, midwayDestination.AvailableQty);
//            Assert.Equal(8m, midwayFulfilled);
//            Assert.Equal(2m, midwayAuthority.InTransitOpen);
//            Assert.Equal("WAITING_FOR_REMAINDER", midwayAuthority.Status);
//        }

//        var secondDto = ReceiveDto(
//            transfer,
//            "sc02-ledger-second-receive",
//            2m,
//            0m,
//            firstReceipt.RowVersion);
//        var secondReceipt = await ReceiveAsync(transfer, secondDto);

//        await using var verify = CreateContext();
//        var detail = await verify.InventoryTransferDetails.SingleAsync(x =>
//            x.InventoryTransferDetailId == transfer.DetailId);
//        var postings = await verify.InventoryTransferDiscrepancyPostings
//            .Where(x => x.InventoryTransferDetailId == transfer.DetailId)
//            .ToListAsync();
//        var authority = InventoryTransferQuantityAuthority.Calculate(detail, postings);
//        var destination = await verify.StoreInventories.SingleAsync(x =>
//            x.StoreId == transfer.DestinationStoreId && x.IngredientId == transfer.IngredientId);
//        var fulfilled = await verify.RestockFulfillmentPostings
//            .Where(x => x.RestockRequestId == transfer.RestockRequestId)
//            .SumAsync(x => x.Quantity);

//        Assert.Equal(InventoryTransferStatus.COMPLETED, secondReceipt.Status);
//        Assert.Equal(10m, destination.AvailableQty);
//        Assert.Equal(10m, fulfilled);
//        Assert.Equal(10m, authority.DestinationAccepted);
//        Assert.Equal(0m, authority.DestinationRejected);
//        Assert.Equal(0m, authority.ReturnedToSource);
//        Assert.Equal(0m, authority.WrittenOff);
//        Assert.Equal(0m, authority.ClosedShortage);
//        Assert.Equal(0m, authority.InTransitOpen);
//        Assert.Equal("RESOLVED_ACCEPTED", authority.Status);
//        Assert.Empty(postings);
//    }

//    [Fact]
//    public async Task SqlServer_ReturnPreservesOriginalFifoCost()
//    {
//        var transfer = await SeedAndDispatchAsync(unitCost: 17.25m);
//        var received = await ReceiveAsync(transfer, 8m, 2m, "sc02-fifo-receive");
//        var request = await RequestReturnAsync(transfer, received.RowVersion, 2m, "sc02-fifo-return-request");

//        await using (var beforeConfirmation = CreateContext())
//        {
//            var sourceBefore = await beforeConfirmation.StoreInventories.SingleAsync(x =>
//                x.StoreId == transfer.SourceStoreId && x.IngredientId == transfer.IngredientId);
//            Assert.Equal(0m, sourceBefore.AvailableQty);
//        }

//        await ConfirmReturnAsync(transfer, request.RowVersion, 2m, "sc02-fifo-return-confirm");

//        await using var verify = CreateContext();
//        var returnLayer = await verify.InventoryCostLayers.SingleAsync(x =>
//            x.SourceTransferDiscrepancyPostingId != null
//            && x.StoreId == transfer.SourceStoreId);
//        var source = await verify.StoreInventories.SingleAsync(x =>
//            x.StoreId == transfer.SourceStoreId && x.IngredientId == transfer.IngredientId);
//        var destinationFulfilled = await verify.RestockFulfillmentPostings
//            .Where(x => x.RestockRequestId == transfer.RestockRequestId)
//            .SumAsync(x => x.Quantity);

//        Assert.Equal(2m, returnLayer.Quantity);
//        Assert.Equal(2m, returnLayer.RemainingQuantity);
//        Assert.Equal(17.25m, returnLayer.UnitCost);
//        Assert.Equal(2m, source.AvailableQty);
//        Assert.Equal(8m, destinationFulfilled);
//    }

//    [Fact]
//    public async Task SqlServer_CloseShortageReplay_IsIdempotent()
//    {
//        var transfer = await SeedAndDispatchAsync();
//        var received = await ReceiveAsync(transfer, 8m, 0m, "sc02-close-receive");
//        var dto = ResolutionDto(transfer, received.RowVersion, 2m,
//            InventoryTransferDiscrepancyPostingType.CLOSED_SHORTAGE, "sc02-close-replay");

//        var first = await ResolveAsync(transfer, dto);
//        var replay = await ResolveAsync(transfer, dto);

//        await using var verify = CreateContext();
//        var closeCount = await verify.InventoryTransferDiscrepancyPostings.CountAsync(x =>
//            x.InventoryTransferDetailId == transfer.DetailId
//            && x.PostingType == InventoryTransferDiscrepancyPostingType.CLOSED_SHORTAGE);
//        var transactions = await verify.InventoryTransactions.CountAsync(x =>
//            x.InventoryTransferDetailId == transfer.DetailId
//            && x.StoreInventory.StoreId == transfer.DestinationStoreId);
//        var fulfilled = await verify.RestockFulfillmentPostings
//            .Where(x => x.RestockRequestId == transfer.RestockRequestId)
//            .SumAsync(x => x.Quantity);

//        Assert.Equal(first.InventoryTransferId, replay.InventoryTransferId);
//        Assert.Equal(InventoryTransferStatus.COMPLETED, replay.Status);
//        Assert.Equal(1, closeCount);
//        Assert.Equal(1, transactions);
//        Assert.Equal(8m, fulfilled);
//    }

//    [Fact]
//    public async Task SqlServer_WriteOffLeavesRestockShort_RuntimeSmoke()
//    {
//        var transfer = await SeedAndDispatchAsync(unitCost: 19.5m);
//        var received = await ReceiveAsync(transfer, 8m, 0m, "sc02-writeoff-receive");

//        decimal sourceBefore;
//        decimal destinationBefore;
//        int layersBefore;
//        await using (var before = CreateContext())
//        {
//            sourceBefore = await before.StoreInventories
//                .Where(x => x.StoreId == transfer.SourceStoreId && x.IngredientId == transfer.IngredientId)
//                .Select(x => x.AvailableQty)
//                .SingleAsync();
//            destinationBefore = await before.StoreInventories
//                .Where(x => x.StoreId == transfer.DestinationStoreId && x.IngredientId == transfer.IngredientId)
//                .Select(x => x.AvailableQty)
//                .SingleAsync();
//            layersBefore = await before.InventoryCostLayers.CountAsync(x =>
//                x.IngredientId == transfer.IngredientId);
//        }

//        var result = await ResolveAsync(
//            transfer,
//            ResolutionDto(
//                transfer,
//                received.RowVersion,
//                2m,
//                InventoryTransferDiscrepancyPostingType.WRITTEN_OFF,
//                "sc02-writeoff-runtime"));

//        await using var verify = CreateContext();
//        var posting = await verify.InventoryTransferDiscrepancyPostings.SingleAsync(x =>
//            x.InventoryTransferDetailId == transfer.DetailId
//            && x.PostingType == InventoryTransferDiscrepancyPostingType.WRITTEN_OFF);
//        var sourceAfter = await verify.StoreInventories
//            .Where(x => x.StoreId == transfer.SourceStoreId && x.IngredientId == transfer.IngredientId)
//            .Select(x => x.AvailableQty)
//            .SingleAsync();
//        var destinationAfter = await verify.StoreInventories
//            .Where(x => x.StoreId == transfer.DestinationStoreId && x.IngredientId == transfer.IngredientId)
//            .Select(x => x.AvailableQty)
//            .SingleAsync();
//        var fulfilled = await verify.RestockFulfillmentPostings
//            .Where(x => x.RestockRequestId == transfer.RestockRequestId)
//            .SumAsync(x => x.Quantity);

//        Assert.True(posting.InventoryTransferDiscrepancyPostingId > 0);
//        Assert.Equal(InventoryTransferStatus.COMPLETED, result.Status);
//        Assert.Equal(sourceBefore, sourceAfter);
//        Assert.Equal(destinationBefore, destinationAfter);
//        Assert.Equal(layersBefore, await verify.InventoryCostLayers.CountAsync(x =>
//            x.IngredientId == transfer.IngredientId));
//        Assert.Equal(8m, fulfilled);
//    }

//    [Fact]
//    public async Task SqlServer_OtherStoreCannotResolveDiscrepancy()
//    {
//        var transfer = await SeedAndDispatchAsync();
//        var received = await ReceiveAsync(transfer, 8m, 0m, "sc02-scope-receive");
//        var dto = ResolutionDto(transfer, received.RowVersion, 2m,
//            InventoryTransferDiscrepancyPostingType.CLOSED_SHORTAGE, "sc02-scope-resolve");

//        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
//            ResolveAsync(transfer, dto, allowScope: false));

//        await using var verify = CreateContext();
//        Assert.Empty(await verify.InventoryTransferDiscrepancyPostings
//            .Where(x => x.InventoryTransferDetailId == transfer.DetailId)
//            .ToListAsync());
//    }

//    private async Task<TransferSeed> SeedAndDispatchAsync(decimal unitCost = 12m)
//    {
//        await using var seed = CreateContext();
//        var stores = await seed.Stores.OrderBy(x => x.StoreId).Take(2).ToListAsync();
//        var unit = await seed.Units.OrderBy(x => x.UnitId).FirstAsync();
//        var role = await EnsureRoleAsync(seed, RoleConstants.BusinessOwner);
//        var account = new Account
//        {
//            Email = $"sc02-{Guid.NewGuid():N}@test.local",
//            PasswordHash = "test",
//            Active = true,
//            CreatedAt = DateTime.UtcNow
//        };
//        seed.Accounts.Add(account);
//        await seed.SaveChangesAsync();
//        seed.AccountRoles.Add(new AccountRole { AccountId = account.AccountId, RoleId = role.RoleId });

//        var staff = new Staff
//        {
//            AccountId = account.AccountId,
//            StoreId = stores[0].StoreId,
//            FullName = "SC-02 SQL actor",
//            Active = true,
//            CreatedAt = DateTime.UtcNow,
//        };
//        var ingredient = new Ingredient
//        {
//            Code = "SC02-ING-" + Guid.NewGuid().ToString("N")[..8],
//            Name = "SC-02 ingredient",
//            BaseUnitId = unit.UnitId,
//            Active = true
//        };
//        seed.Staffs.Add(staff);
//        seed.Ingredients.Add(ingredient);
//        await seed.SaveChangesAsync();

//        var sourceInventory = new StoreInventory
//        {
//            StoreId = stores[0].StoreId,
//            IngredientId = ingredient.IngredientId,
//            AvailableQty = 10m,
//            ReservedQty = 0m,
//            LastUpdated = DateTime.UtcNow
//        };
//        var sourceLayer = new InventoryCostLayer
//        {
//            StoreId = stores[0].StoreId,
//            IngredientId = ingredient.IngredientId,
//            Quantity = 10m,
//            RemainingQuantity = 10m,
//            UnitCost = unitCost,
//            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
//        };
//        var request = new RestockRequest
//        {
//            StoreId = stores[1].StoreId,
//            IngredientId = ingredient.IngredientId,
//            RequestedQuantity = 10m,
//            Status = RestockRequestStatuses.Processing,
//            Priority = RestockRequestPriorities.Normal,
//            CreatedByStaffId = staff.StaffId,
//            CreatedAt = DateTime.UtcNow,
//            UpdatedAt = DateTime.UtcNow
//        };
//        seed.StoreInventories.Add(sourceInventory);
//        seed.InventoryCostLayers.Add(sourceLayer);
//        seed.RestockRequests.Add(request);
//        await seed.SaveChangesAsync();

//        var transfer = new InventoryTransfer
//        {
//            Code = "SC02-" + Guid.NewGuid().ToString("N")[..8],
//            RequestKey = "SC02-DRAFT-" + Guid.NewGuid().ToString("N"),
//            FromStoreId = stores[0].StoreId,
//            ToStoreId = stores[1].StoreId,
//            Type = InventoryTransferType.STORE_TO_STORE,
//            Purpose = InventoryTransferPurpose.REPLENISHMENT,
//            Status = InventoryTransferStatus.PENDING,
//            DocumentDate = DateTime.Today,
//            CreatedByStaffId = staff.StaffId,
//            CreatedAt = DateTime.UtcNow,
//            Details =
//            [
//                new InventoryTransferDetail
//                {
//                    IngredientId = ingredient.IngredientId,
//                    RestockRequestId = request.RestockRequestId,
//                    UnitId = unit.UnitId,
//                    Quantity = 10m,
//                    BaseQuantity = 10m,
//                    UnitPrice = unitCost
//                }
//            ]
//        };
//        seed.InventoryTransfers.Add(transfer);
//        await seed.SaveChangesAsync();

//        var detail = transfer.Details.Single();
//        await using var dispatchContext = CreateContext();
//        var dispatched = await CreateService(dispatchContext, staff.StaffId).DispatchAsync(
//            transfer.InventoryTransferId, "SC02-DISPATCH-" + Guid.NewGuid().ToString("N"));

//        return new TransferSeed(
//            transfer.InventoryTransferId,
//            detail.InventoryTransferDetailId,
//            request.RestockRequestId,
//            stores[0].StoreId,
//            stores[1].StoreId,
//            staff.StaffId,
//            ingredient.IngredientId,
//            dispatched.RowVersion);
//    }

//    private async Task<InventoryTransferMutationResultDTO> ReceiveAsync(
//        TransferSeed transfer,
//        decimal accepted,
//        decimal rejected,
//        string requestKey) =>
//        await ReceiveAsync(transfer, ReceiveDto(transfer, requestKey, accepted, rejected));

//    private async Task<InventoryTransferMutationResultDTO> ReceiveAsync(
//        TransferSeed transfer,
//        InventoryTransferReceiveDTO dto)
//    {
//        await using var context = CreateContext();
//        return await CreateService(context, transfer.StaffId).ReceiveAsync(transfer.TransferId, dto);
//    }

//    private async Task<InventoryTransferMutationResultDTO> RequestReturnAsync(
//        TransferSeed transfer,
//        string rowVersion,
//        decimal quantity,
//        string requestKey)
//    {
//        await using var context = CreateContext();
//        return await CreateService(context, transfer.StaffId).RequestReturnAsync(
//            transfer.TransferId,
//            ResolutionDto(transfer, rowVersion, quantity,
//                InventoryTransferDiscrepancyPostingType.RETURN_REQUESTED, requestKey));
//    }

//    private async Task<InventoryTransferMutationResultDTO> ConfirmReturnAsync(
//        TransferSeed transfer,
//        string rowVersion,
//        decimal quantity,
//        string requestKey)
//    {
//        await using var context = CreateContext();
//        return await CreateService(context, transfer.StaffId).ConfirmReturnAsync(
//            transfer.TransferId,
//            ResolutionDto(transfer, rowVersion, quantity,
//                InventoryTransferDiscrepancyPostingType.RETURNED_TO_SOURCE, requestKey));
//    }

//    private async Task<InventoryTransferMutationResultDTO> ResolveAsync(
//        TransferSeed transfer,
//        InventoryTransferResolutionDTO dto,
//        bool allowScope = true)
//    {
//        await using var context = CreateContext();
//        return await CreateService(context, transfer.StaffId, allowScope).ResolveShortageAsync(
//            transfer.TransferId, dto);
//    }

//    private async Task<Outcome> TryReceiveAsync(
//        TransferSeed transfer,
//        decimal accepted,
//        decimal rejected,
//        string requestKey)
//    {
//        try
//        {
//            await ReceiveAsync(transfer, accepted, rejected, requestKey);
//            return Outcome.Success();
//        }
//        catch (Exception ex)
//        {
//            return Outcome.Failure(ex);
//        }
//    }

//    private async Task<Outcome> TryConfirmReturnAsync(
//        TransferSeed transfer,
//        string rowVersion,
//        decimal quantity,
//        string requestKey)
//    {
//        try
//        {
//            await ConfirmReturnAsync(transfer, rowVersion, quantity, requestKey);
//            return Outcome.Success();
//        }
//        catch (Exception ex)
//        {
//            return Outcome.Failure(ex);
//        }
//    }

//    private async Task<Outcome> TryResolveAsync(
//        TransferSeed transfer,
//        string rowVersion,
//        decimal quantity,
//        InventoryTransferDiscrepancyPostingType type,
//        string requestKey)
//    {
//        try
//        {
//            await ResolveAsync(
//                transfer,
//                ResolutionDto(transfer, rowVersion, quantity, type, requestKey));
//            return Outcome.Success();
//        }
//        catch (Exception ex)
//        {
//            return Outcome.Failure(ex);
//        }
//    }

//    private static InventoryTransferReceiveDTO ReceiveDto(
//        TransferSeed transfer,
//        string requestKey,
//        decimal accepted,
//        decimal rejected,
//        string? rowVersion = null) => new()
//    {
//        RowVersion = rowVersion ?? transfer.RowVersion,
//        RequestKey = requestKey,
//        ReceivedAt = DateTime.UtcNow,
//        Lines =
//        [
//            new InventoryTransferReceiveLineDTO
//            {
//                InventoryTransferDetailId = transfer.DetailId,
//                ReceivedBaseQuantity = accepted,
//                RejectedBaseQuantity = rejected,
//                RejectionIssueType = rejected > 0 ? "DAMAGED" : null,
//                RejectionReason = rejected > 0 ? "SC-02 test rejection" : null
//            }
//        ]
//    };

//    private static InventoryTransferResolutionDTO ResolutionDto(
//        TransferSeed transfer,
//        string rowVersion,
//        decimal quantity,
//        InventoryTransferDiscrepancyPostingType type,
//        string requestKey) => new()
//    {
//        RowVersion = rowVersion,
//        RequestKey = requestKey,
//        Reason = "SC-02 SQL acceptance",
//        ResolutionType = type,
//        Lines =
//        [
//            new InventoryTransferResolutionLineDTO
//            {
//                InventoryTransferDetailId = transfer.DetailId,
//                BaseQuantity = quantity
//            }
//        ]
//    };

//    private AppDbContext CreateContext() => new(
//        new DbContextOptionsBuilder<AppDbContext>()
//            .UseSqlServer(ConnectionString)
//            .Options);

//    private AdminInventoryTransferService CreateService(
//        AppDbContext context,
//        int staffId,
//        bool allowScope = true)
//    {
//        var dedupRepository = new RequestDeduplicationRepository(context);
//        var deduplication = new RequestDeduplicationService(dedupRepository);
//        var issuePolicy = new Mock<IInventoryIssuePolicy>();
//        issuePolicy.Setup(x => x.EvaluateAsync(It.IsAny<InventoryIssueRequest>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync((InventoryIssueRequest request, CancellationToken _) => new InventoryIssueDecision(
//                InventoryIssueOutcome.Allowed,
//                InventoryIssueReasonCodes.NonNegativeIssueAllowed,
//                request.BeforeAvailableQty,
//                request.IssueQty,
//                request.BeforeAvailableQty - request.IssueQty,
//                0,
//                0,
//                false,
//                false,
//                string.Empty));
//        var alerts = new Mock<IStockAlertService>();
//        alerts.Setup(x => x.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
//            .ReturnsAsync(ServiceResult<StockAlertEvaluationResultDto>.Success(new()));
//        var actor = new Mock<IAdminActorContextAccessor>();
//        actor.Setup(x => x.Get(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
//            .Returns(new AdminActorContext
//            {
//                StaffId = staffId,
//                RoleNames = [RoleConstants.BusinessOwner]
//            });
//        var scope = new Mock<IScopeAuthorizationService>();
//        scope.Setup(x => x.CanAccessStoreAsync(staffId, It.IsAny<int>()))
//            .ReturnsAsync(allowScope);
//        var allocations = new Mock<IRestockAllocationService>();
//        allocations.Setup(x => x.ValidateAllocationAsync(It.IsAny<RestockAllocationValidationRequest>()))
//            .ReturnsAsync(ServiceResult<RestockAllocationSummaryDto>.Success(new()));

//        return new AdminInventoryTransferService(
//            new AdminInventoryTransferRepository(context),
//            deduplication,
//            issuePolicy.Object,
//            new InventoryCostLayerConsumptionService(context),
//            new RestockFulfillmentPostingService(context),
//            alerts.Object,
//            new FixedUserContext(staffId),
//            actor.Object,
//            scope.Object,
//            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
//            allocations.Object);
//    }

//    private static async Task<Role> EnsureRoleAsync(AppDbContext context, string name)
//    {
//        var role = await context.Roles.FirstOrDefaultAsync(x => x.Name == name);
//        if (role != null)
//            return role;

//        role = new Role
//        {
//            Name = name,
//            Active = true,
//            IsStoreLevel = false,
//            CreatedAt = DateTime.UtcNow
//        };
//        context.Roles.Add(role);
//        await context.SaveChangesAsync();
//        return role;
//    }

//    private sealed record TransferSeed(
//        int TransferId,
//        int DetailId,
//        int RestockRequestId,
//        int SourceStoreId,
//        int DestinationStoreId,
//        int StaffId,
//        int IngredientId,
//        string RowVersion);

//    private sealed record Outcome(bool Succeeded, Exception? Error)
//    {
//        public static Outcome Success() => new(true, null);
//        public static Outcome Failure(Exception error) => new(false, error);
//    }

//    private sealed class FixedUserContext(int staffId) : IUserContext
//    {
//        public int StaffId { get; } = staffId;
//        public string StaffName => "SC-02 SQL actor";
//    }
//}
