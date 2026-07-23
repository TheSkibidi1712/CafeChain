using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading.Tasks;
using Xunit;

namespace CafeChain.Tests.POS
{
    public class POSWorkShiftCashValidationTests
    {
        [Theory]
        [InlineData(-1000)]
        [InlineData(500000.5)]
        [InlineData(500222)]
        public async Task CloseShiftAsync_WithInvalidActualEndingCash_RejectsBeforeReadingShift(
            decimal actualEndingCash)
        {
            var shiftRepository = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            var service = CreateService(shiftRepository.Object);

            var result = await service.CloseShiftAsync(17, 3, new CloseShiftRequestDto
            {
                ActualEndingCash = actualEndingCash
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("Tiền mặt thực tế trong két không hợp lệ", result.Message);
            shiftRepository.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CloseShiftByExceptionAsync_WithInvalidActualEndingCash_RejectsBeforeOtpOrShiftLookup()
        {
            var shiftRepository = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            var service = CreateService(shiftRepository.Object);

            var result = await service.CloseShiftByExceptionAsync(
                17,
                3,
                84,
                new CloseShiftExceptionRequestDto
                {
                    ActualEndingCash = 500222m,
                    ExceptionReason = "Mất mạng kéo dài."
                });

            Assert.False(result.IsSuccess);
            Assert.Contains("bội số của 1.000đ", result.Message);
            shiftRepository.VerifyNoOtherCalls();
        }

        private static WorkShiftService CreateService(IWorkShiftRepository shiftRepository)
        {
            return new WorkShiftService(
                shiftRepository,
                Mock.Of<IPOSOrderRepository>(),
                Mock.Of<IOtpChallengeRepository>(),
                Mock.Of<IOtpPayloadFingerprintService>(),
                Mock.Of<ILogger<WorkShiftService>>());
        }
    }
}
