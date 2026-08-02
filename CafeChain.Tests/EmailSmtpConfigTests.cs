using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.Accounts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>
    /// Fresh-clone SMTP config: DeliveryMode=Smtp without password must fail clearly
    /// (no silent success, no host crash).
    /// </summary>
    public class EmailSmtpConfigTests
    {
        [Fact]
        public async Task SendAsync_SmtpMode_WithoutPassword_ThrowsStructuredConfigError()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:DeliveryMode"] = "Smtp",
                    ["Email:SmtpHost"] = "smtp.gmail.com",
                    ["Email:SmtpPort"] = "587",
                    ["Email:Address"] = "cafechain8386@gmail.com",
                    // Password intentionally missing — simulates fresh clone
                })
                .Build();

            var service = new EmailService(config, NullLogger<EmailService>.Instance);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SendAsync("approver@example.com", "subj", "<html>body</html>"));

            Assert.Contains(
                OtpConstants.ErrorCodes.EmailSmtpPasswordNotConfigured,
                ex.Message,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-canary-value", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SendAsync_LogMode_DoesNotRequirePassword()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:DeliveryMode"] = "Log",
                    ["Email:Address"] = "cafechain8386@gmail.com",
                })
                .Build();

            var service = new EmailService(config, NullLogger<EmailService>.Instance);

            await service.SendAsync("approver@example.com", "subj", "<html>body</html>");
        }

        [Fact]
        public async Task SendAsync_SmtpMode_EmptyPassword_ThrowsStructuredConfigError()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:DeliveryMode"] = "Smtp",
                    ["Email:SmtpHost"] = "smtp.gmail.com",
                    ["Email:SmtpPort"] = "587",
                    ["Email:Address"] = "cafechain8386@gmail.com",
                    ["Email:Password"] = "",
                })
                .Build();

            var service = new EmailService(config, NullLogger<EmailService>.Instance);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SendAsync("approver@example.com", "subj", "<html>body</html>"));

            Assert.Contains(
                OtpConstants.ErrorCodes.EmailSmtpPasswordNotConfigured,
                ex.Message,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
