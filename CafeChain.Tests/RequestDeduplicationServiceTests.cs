using CafeChain.Application.Services.Systems;
using CafeChain.Infrastrusture.Interfaces.Systems;
using CafeChain.Models.Systems;
using Moq;

namespace CafeChain.Tests;

public sealed class RequestDeduplicationServiceTests
{
    [Fact]
    public async Task Same_key_and_canonical_payload_replays_but_changed_payload_conflicts()
    {
        RequestDeduplication? stored = null;
        var repository = new Mock<IRequestDeduplicationRepository>();
        repository
            .Setup(x => x.GetAsync("key-1", "Inventory.Submit", 7))
            .ReturnsAsync(() => stored);
        repository
            .Setup(x => x.AddAsync(It.IsAny<RequestDeduplication>()))
            .Callback<RequestDeduplication>(x => stored = x)
            .Returns(Task.CompletedTask);
        repository.Setup(x => x.SaveChangesAsync()).Returns(Task.CompletedTask);
        var service = new RequestDeduplicationService(repository.Object);

        var first = await service.BeginAsync(
            "key-1", "Inventory.Submit", 7,
            new Dictionary<string, object> { ["quantity"] = 2, ["storeId"] = 1 });
        var processing = await service.BeginAsync(
            "key-1", "Inventory.Submit", 7,
            new Dictionary<string, object> { ["storeId"] = 1, ["quantity"] = 2 });
        stored!.Status = "SUCCESS";
        stored.ReferenceId = 42;
        stored.ResponseBody = "{}";

        var replay = await service.BeginAsync(
            "key-1", "Inventory.Submit", 7,
            new Dictionary<string, object> { ["storeId"] = 1, ["quantity"] = 2 });
        var conflict = await service.BeginAsync(
            "key-1", "Inventory.Submit", 7,
            new Dictionary<string, object> { ["storeId"] = 1, ["quantity"] = 3 });

        Assert.True(first.CanProcess);
        Assert.Equal("REQUEST_IN_PROGRESS", processing.ErrorCode);
        Assert.False(replay.CanProcess);
        Assert.Equal(42, replay.ReferenceId);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", conflict.ErrorCode);
    }

    [Fact]
    public async Task Failed_business_transaction_detaches_key_for_retry()
    {
        var repository = new Mock<IRequestDeduplicationRepository>();
        var entry = new RequestDeduplication();
        var service = new RequestDeduplicationService(repository.Object);

        await service.MarkFailedAsync(entry, new { error = "rollback" });

        repository.Verify(x => x.Detach(entry), Times.Once);
        repository.Verify(x => x.Update(It.IsAny<RequestDeduplication>()), Times.Never);
        repository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }
}
