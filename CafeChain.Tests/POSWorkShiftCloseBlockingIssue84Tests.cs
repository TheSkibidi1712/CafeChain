using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Infrastructure.Repositories.Admin.POS;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace CafeChain.Tests.POS
{
    public class POSWorkShiftCloseBlockingIssue84Tests : IntegrationTestBase
    {
        [Fact]
        public async Task CloseShiftAsync_WithBackendAwaitingPaymentPosOrder_ReturnsFailureAndKeepsShiftOpen()
        {
            using var context = CreateDbContext();
            context.WorkShifts.Add(CreateOpenShift());
            context.Orders.Add(new Order
            {
                StoreId = 3,
                StaffId = 17,
                WorkShiftId = 84,
                OrderStatusId = SystemConstants.OrderStatuses.AwaitingPayment,
                PaymentStatusId = SystemConstants.PaymentStatuses.Unpaid,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Source = "POS",
                SubTotal = 45000m,
                Total = 45000m,
                CreatedAt = DateTime.Now
            });
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.CloseShiftAsync(17, 3, new CloseShiftRequestDto
            {
                ActualEndingCash = 500000m
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("Đang có giao dịch thanh toán chưa hoàn tất", result.Message);

            var shift = await context.WorkShifts.SingleAsync(workShift => workShift.ShiftId == 84);
            Assert.Equal("Open", shift.Status);
            Assert.Null(shift.EndTime);
            Assert.Null(shift.ActualEndingCash);
        }

        [Fact]
        public async Task CloseShiftAsync_WithoutBackendAwaitingPaymentPosOrder_ClosesAsBefore()
        {
            var shift = CreateOpenShift();
            var repository = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            repository
                .Setup(repo => repo.GetActiveShiftAsync(17, 3))
                .ReturnsAsync(shift);
            repository
                .Setup(repo => repo.HasOpenPosPaymentAsync(84, 3))
                .ReturnsAsync(false);
            repository
                .Setup(repo => repo.GetTotalCashSalesAsync(84))
                .ReturnsAsync(0m);
            repository
                .Setup(repo => repo.UpdateShiftAsync(It.IsAny<WorkShift>()))
                .Returns(Task.CompletedTask);
            var service = CreateService(repository.Object);

            var result = await service.CloseShiftAsync(17, 3, new CloseShiftRequestDto
            {
                ActualEndingCash = 500000m
            });

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("Closed", shift.Status);
            Assert.NotNull(shift.EndTime);
            Assert.Equal(500000m, shift.ActualEndingCash);
            Assert.Equal(0m, shift.CashDiscrepancy);
            repository.Verify(repo => repo.UpdateShiftAsync(It.IsAny<WorkShift>()), Times.Once);
        }

        [Fact]
        public async Task CloseShiftAsync_WithCashDiscrepancy_PersistsWorkShiftFieldsWithoutInvoiceAuditLog()
        {
            var shift = CreateOpenShift();
            var repository = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            repository
                .Setup(repo => repo.GetActiveShiftAsync(17, 3))
                .ReturnsAsync(shift);
            repository
                .Setup(repo => repo.HasOpenPosPaymentAsync(84, 3))
                .ReturnsAsync(false);
            repository
                .Setup(repo => repo.GetTotalCashSalesAsync(84))
                .ReturnsAsync(0m);
            repository
                .Setup(repo => repo.UpdateShiftAsync(It.IsAny<WorkShift>()))
                .Returns(Task.CompletedTask);
            var service = CreateService(repository.Object);

            var result = await service.CloseShiftAsync(17, 3, new CloseShiftRequestDto
            {
                ActualEndingCash = 490000m,
                DiscrepancyReason = "Thiếu tiền mặt khi kiểm két."
            });

            Assert.True(result.IsSuccess, result.Message);

            Assert.Equal("Closed", shift.Status);
            Assert.Equal(500000m, shift.ExpectedEndingCash);
            Assert.Equal(490000m, shift.ActualEndingCash);
            Assert.Equal(-10000m, shift.CashDiscrepancy);
            Assert.Equal("Thiếu tiền mặt khi kiểm két.", shift.DiscrepancyReason);
            repository.Verify(repo => repo.UpdateShiftAsync(shift), Times.Once);
        }

        private static WorkShiftService CreateService(AppDbContext context)
        {
            return CreateService(new WorkShiftRepository(context));
        }

        private static WorkShiftService CreateService(IWorkShiftRepository repository)
        {
            return new WorkShiftService(
                repository,
                Mock.Of<IHrAttendanceService>(),
                Mock.Of<IPOSOrderRepository>(),
                Mock.Of<ISupervisorAuthService>(),
                Mock.Of<IOtpChallengeRepository>(),
                Mock.Of<ILogger<WorkShiftService>>());
        }

        private static WorkShift CreateOpenShift()
        {
            return new WorkShift
            {
                ShiftId = 84,
                StoreId = 3,
                UserId = 17,
                Status = "Open",
                StartTime = DateTime.Now,
                StartingCash = 500000m,
                ExpectedEndingCash = 500000m
            };
        }
    }
}
