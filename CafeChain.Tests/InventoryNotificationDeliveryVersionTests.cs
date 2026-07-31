using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Options;
using CafeChain.Application.Services.Operations;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests;

public sealed class InventoryNotificationDeliveryVersionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Same_version_before_cooldown_is_a_no_op()
    {
        var existing = ExistingNotification(
            meaningfulVersion: "version-1",
            updatedAt: Now.UtcDateTime.AddMinutes(-30));
        var harness = CreateHarness(existing);

        var result = await harness.Service.DeliverAsync(Request("version-1"));

        Assert.Equal(0, result.UpdatedCount);
        Assert.False(result.Published);
        Assert.True(existing.IsRead);
        Assert.Equal(Now.UtcDateTime.AddMinutes(-30), existing.UpdatedAt);
        harness.Repository.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        harness.Publisher.Verify(
            x => x.PublishAsync(
                It.IsAny<InventoryNotificationChangedDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Same_version_after_cooldown_is_an_unread_reminder()
    {
        var existing = ExistingNotification(
            meaningfulVersion: "version-1",
            updatedAt: Now.UtcDateTime.AddMinutes(-241));
        var harness = CreateHarness(existing);

        var result = await harness.Service.DeliverAsync(Request("version-1"));

        Assert.Equal(1, result.UpdatedCount);
        Assert.True(result.Published);
        Assert.False(existing.IsRead);
        Assert.Null(existing.ReadAt);
        Assert.Equal(Now.UtcDateTime, existing.UpdatedAt);
        Assert.NotNull(harness.Published);
        Assert.True(harness.Published!.ShouldToast);
    }

    [Fact]
    public async Task Changed_version_bypasses_cooldown_and_publishes_immediately()
    {
        var existing = ExistingNotification(
            meaningfulVersion: "version-1",
            updatedAt: Now.UtcDateTime.AddMinutes(-5));
        var harness = CreateHarness(existing);

        var result = await harness.Service.DeliverAsync(Request("version-2"));

        Assert.Equal(1, result.UpdatedCount);
        Assert.True(result.Published);
        Assert.Equal("version-2", existing.MeaningfulVersion);
        Assert.False(existing.IsRead);
        Assert.Null(existing.ReadAt);
        Assert.Equal(Now.UtcDateTime, existing.UpdatedAt);
        Assert.NotNull(harness.Published);
        Assert.True(harness.Published!.ShouldToast);
    }

    [Fact]
    public async Task Recurrence_reactivates_the_resolved_row_instead_of_inserting_a_duplicate()
    {
        var existing = ExistingNotification(
            meaningfulVersion: "version-1",
            updatedAt: Now.UtcDateTime.AddMinutes(-5));
        existing.ResolvedAt = Now.UtcDateTime.AddMinutes(-5);
        var harness = CreateHarness(existing);

        var result = await harness.Service.DeliverAsync(Request("version-1"));

        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.True(result.Published);
        Assert.Null(existing.ResolvedAt);
        Assert.False(existing.IsRead);
        Assert.Null(existing.ReadAt);
        Assert.NotNull(harness.Published);
        Assert.True(harness.Published!.ShouldToast);
        harness.Repository.Verify(
            x => x.Add(It.IsAny<StaffNotification>()),
            Times.Never);
    }

    [Fact]
    public async Task New_notification_stores_a_trimmed_version_with_the_configured_maximum_length()
    {
        var harness = CreateHarness(existing: null);
        var version = new string('v', 70);

        var result = await harness.Service.DeliverAsync(Request(version));

        Assert.Equal(1, result.CreatedCount);
        Assert.NotNull(harness.Added);
        Assert.Equal(64, harness.Added!.MeaningfulVersion!.Length);
        Assert.Equal(version[..64], harness.Added.MeaningfulVersion);
    }

    [Fact]
    public async Task Generic_content_change_before_cooldown_preserves_existing_no_toast_behavior()
    {
        var existing = ExistingNotification(
            meaningfulVersion: null,
            updatedAt: Now.UtcDateTime.AddMinutes(-5));
        var harness = CreateHarness(existing);
        var request = Request(meaningfulVersion: null) with { Body = "Updated body" };

        var result = await harness.Service.DeliverAsync(request);

        Assert.Equal(1, result.UpdatedCount);
        Assert.True(result.Published);
        Assert.Equal("Updated body", existing.Body);
        Assert.True(existing.IsRead);
        Assert.NotNull(existing.ReadAt);
        Assert.NotNull(harness.Published);
        Assert.False(harness.Published!.ShouldToast);
    }

    [Fact]
    public void Reorder_reminder_cooldown_defaults_to_four_hours()
    {
        Assert.Equal(
            240,
            new InventoryReorderNotificationOptions().ReorderReminderCooldownMinutes);
    }

    private static InventoryNotificationDeliveryRequest Request(string? meaningfulVersion) =>
        new(
            StoreId: 7,
            Type: "INVENTORY_REORDER_ALERT",
            Title: "Reorder ingredient",
            Body: "Current body",
            Severity: "WARNING",
            EntityType: "InventoryReorder",
            EntityId: 15,
            ChangeKind: InventoryNotificationChangeKinds.Updated,
            CooldownMinutes: 240,
            MeaningfulVersion: meaningfulVersion);

    private static StaffNotification ExistingNotification(
        string? meaningfulVersion,
        DateTime updatedAt) =>
        new()
        {
            StaffNotificationId = 99,
            StoreId = 7,
            RecipientStaffId = 1,
            Type = "INVENTORY_REORDER_ALERT",
            Title = "Reorder ingredient",
            Body = "Current body",
            Severity = "WARNING",
            DeduplicationKey = "1:7:INVENTORY_REORDER_ALERT:InventoryReorder:15",
            MeaningfulVersion = meaningfulVersion,
            EntityType = "InventoryReorder",
            EntityId = 15,
            IsRead = true,
            ReadAt = updatedAt.AddMinutes(-1),
            CreatedAt = Now.UtcDateTime.AddDays(-1),
            UpdatedAt = updatedAt
        };

    private static TestHarness CreateHarness(StaffNotification? existing)
    {
        var audience = new Mock<IInventoryNotificationAudienceResolver>();
        audience
            .Setup(x => x.ResolveAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new InventoryNotificationRecipient(
                    StaffId: 1,
                    AccountId: 101,
                    Email: null,
                    FullName: "Store Manager")
            ]);

        var repository = new Mock<IStaffNotificationRepository>();
        repository
            .Setup(x => x.GetByDeduplicationKeyAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var publisher = new Mock<IInventoryNotificationPublisher>();
        var harness = new TestHarness(repository, publisher);
        repository
            .Setup(x => x.Add(It.IsAny<StaffNotification>()))
            .Callback<StaffNotification>(notification => harness.Added = notification);
        publisher
            .Setup(x => x.PublishAsync(
                It.IsAny<InventoryNotificationChangedDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<InventoryNotificationChangedDto, CancellationToken>(
                (notification, _) => harness.Published = notification)
            .Returns(Task.CompletedTask);

        harness.Service = new InventoryNotificationDeliveryService(
            audience.Object,
            repository.Object,
            publisher.Object,
            Options.Create(new InventoryNotificationOptions
            {
                InventoryCooldownMinutes = 15
            }),
            new FixedTimeProvider(Now));
        return harness;
    }

    private sealed class TestHarness(
        Mock<IStaffNotificationRepository> repository,
        Mock<IInventoryNotificationPublisher> publisher)
    {
        public Mock<IStaffNotificationRepository> Repository { get; } = repository;
        public Mock<IInventoryNotificationPublisher> Publisher { get; } = publisher;
        public InventoryNotificationDeliveryService Service { get; set; } = null!;
        public InventoryNotificationChangedDto? Published { get; set; }
        public StaffNotification? Added { get; set; }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
