using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Services.AI;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using UbuhlebethuConnectPro.WebApp.Controllers;
using UbuhlebethuConnectPro.WebApp.Models.DTOs;
using UbuhlebethuConnectPro.WebApp.Services;
using Xunit;

namespace UbuhlebethuConnectPro.Tests
{
    public class MilestoneInvoicingTests
    {
        [Fact]
        public void BuildPrintInvoiceViewModel_WhenJobProvided_CalculatesMilestonesAndTotalsAccurately()
        {
            // Arrange: Project with budget 100,000, progress 45%, and only deposit paid (25,000)
            var job = new Job
            {
                Id = Guid.NewGuid(),
                JobNumber = "PRJ-2026-001",
                Description = "Commercial Office Renovation",
                BuildingType = "Commercial",
                ClientName = "Acme Corp",
                ClientUserName = "acme_client",
                Location = "Sandton, Johannesburg",
                EstimatedCost = 100000m,
                InvoicedAmount = 25000m,
                Progress = 45,
                Variance = 5,
                Status = "In Progress",
                NhbrcCompliant = true,
                CreatedDate = new DateTime(2026, 3, 1)
            };

            // Act
            var vm = DashboardController.BuildPrintInvoiceViewModel(job, null);

            // Assert
            vm.Should().NotBeNull();
            vm.TotalAmount.Should().Be(100000m);
            vm.InvoicedAmount.Should().Be(25000m);
            vm.RemainingAmount.Should().Be(75000m);
            vm.Subtotal.Should().Be(Math.Round(100000m / 1.15m, 2));
            vm.VatAmount.Should().Be(100000m - vm.Subtotal);

            vm.Milestones.Should().HaveCount(3);

            // Milestone 1: 25% Deposit (Paid)
            var m1 = vm.Milestones[0];
            m1.StageNumber.Should().Be(1);
            m1.Percentage.Should().Be(25m);
            m1.Amount.Should().Be(25000m);
            m1.IsPaid.Should().BeTrue();

            // Milestone 2: 50% Progress (Eligible because progress 45% >= 40%)
            var m2 = vm.Milestones[1];
            m2.StageNumber.Should().Be(2);
            m2.Percentage.Should().Be(50m);
            m2.Amount.Should().Be(50000m);
            m2.IsPaid.Should().BeFalse();
            m2.IsEligible.Should().BeTrue();
            m2.Status.Should().Contain("Outstanding");

            // Milestone 3: 25% Handover (Locked because progress 45% < 90%)
            var m3 = vm.Milestones[2];
            m3.StageNumber.Should().Be(3);
            m3.Percentage.Should().Be(25m);
            m3.Amount.Should().Be(25000m);
            m3.IsPaid.Should().BeFalse();
            m3.IsEligible.Should().BeFalse();
            m3.Status.Should().Contain("Locked");

            // Banking Details present for payment simulation
            vm.BankName.Should().Contain("First National Bank");
            vm.AccountNumber.Should().NotBeNullOrWhiteSpace();
            vm.Reference.Should().Be("PRJ-2026-001");
        }

        [Fact]
        public void BuildPrintInvoiceViewModel_WhenProgressReaches90Percent_Milestone3BecomesEligible()
        {
            // Arrange: Project with budget 200,000, 75% invoiced (M1 + M2 settled = 150,000), progress at 92%
            var job = new Job
            {
                Id = Guid.NewGuid(),
                JobNumber = "PRJ-2026-002",
                Description = "Luxury Residential Villa",
                BuildingType = "Residential",
                ClientName = "Jane Doe",
                ClientUserName = "janedoe",
                Location = "Midrand, Gauteng",
                EstimatedCost = 200000m,
                InvoicedAmount = 150000m,
                Progress = 92,
                Status = "In Progress"
            };

            // Act
            var vm = DashboardController.BuildPrintInvoiceViewModel(job, null);

            // Assert
            vm.Milestones[0].IsPaid.Should().BeTrue();
            vm.Milestones[1].IsPaid.Should().BeTrue();

            var m3 = vm.Milestones[2];
            m3.IsPaid.Should().BeFalse();
            m3.IsEligible.Should().BeTrue();
            m3.Status.Should().Contain("Outstanding (Eligible for Payment)");
        }

        [Fact]
        public void BuildPrintInvoiceViewModel_WhenProgressUnder40Percent_Milestone2RemainsLocked()
        {
            // Arrange: Deposit paid (25%), but site progress is only 30% (< 40%)
            var job = new Job
            {
                Id = Guid.NewGuid(),
                JobNumber = "PRJ-2026-003",
                Description = "Warehouse Extension",
                BuildingType = "Industrial",
                ClientName = "Logistics Ltd",
                EstimatedCost = 80000m,
                InvoicedAmount = 20000m, // 25%
                Progress = 30
            };

            // Act
            var vm = DashboardController.BuildPrintInvoiceViewModel(job, null);

            // Assert
            vm.Milestones[0].IsPaid.Should().BeTrue();

            var m2 = vm.Milestones[1];
            m2.IsPaid.Should().BeFalse();
            m2.IsEligible.Should().BeFalse();
            m2.Status.Should().Contain("Locked");
            m2.Status.Should().Contain("40%");
        }

        [Fact]
        public async Task SimulateMilestonePayment_CallsApiClientAndRedirectsToClientPortal()
        {
            // Arrange
            var mockApi = new Mock<IConnectProApiClient>();
            var mockAi = new Mock<IAIService>();
            var mockLogger = new Mock<ILogger<DashboardController>>();

            var jobId = Guid.NewGuid();
            mockApi.Setup(x => x.PayMilestoneAsync(jobId, 1))
                   .ReturnsAsync(new Job
                   {
                       Id = jobId,
                       EstimatedCost = 100000m,
                       InvoicedAmount = 25000m
                   });

            var controller = new DashboardController(mockApi.Object, mockAi.Object, mockLogger.Object);
            var tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
            controller.TempData = tempData;

            // Act
            var result = await controller.SimulateMilestonePayment(jobId, 1) as RedirectToActionResult;

            // Assert
            result.Should().NotBeNull();
            result!.ActionName.Should().Be("ClientPortal");
            mockApi.Verify(x => x.PayMilestoneAsync(jobId, 1), Times.Once);
            controller.TempData["SuccessMessage"].Should().NotBeNull();
        }

        [Fact]
        public async Task PrintInvoice_WhenJobNotFound_ReturnsNotFound()
        {
            // Arrange
            var mockApi = new Mock<IConnectProApiClient>();
            var mockAi = new Mock<IAIService>();
            var mockLogger = new Mock<ILogger<DashboardController>>();

            mockApi.Setup(x => x.GetJobsAsync()).ReturnsAsync(new List<Job>());
            mockApi.Setup(x => x.GetQuoteAsync(It.IsAny<Guid>())).ReturnsAsync((QuoteDto)null!);

            var controller = new DashboardController(mockApi.Object, mockAi.Object, mockLogger.Object);

            // Act
            var result = await controller.PrintInvoice(Guid.NewGuid(), null);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task QuotationController_PrintInvoice_ReturnsPrintViewWithCorrectModel()
        {
            // Arrange
            var mockApi = new Mock<IConnectProApiClient>();
            var mockCurrency = new Mock<ICurrencyService>();

            var quoteId = Guid.NewGuid();
            var quote = new QuoteDto
            {
                Id = quoteId,
                Reference = "UB-QT-9999",
                ClientName = "Test Client",
                CreatedBy = "test_client",
                ProjectType = "Civil Works",
                SiteLocation = "Pretoria",
                ProjectDescription = "Road rehabilitation",
                EstimatedBudget = 500000m,
                Status = "Approved",
                RequestDate = DateTime.UtcNow
            };

            mockApi.Setup(x => x.GetQuoteAsync(quoteId)).ReturnsAsync(quote);
            mockApi.Setup(x => x.GetJobsAsync()).ReturnsAsync(new List<Job>());

            var controller = new QuotationController(mockApi.Object, mockCurrency.Object);
            
            // Set User as test_client
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "test_client"),
                new Claim(ClaimTypes.Role, "Client")
            }, "TestAuth"));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            // Act
            var result = await controller.PrintInvoice(quoteId) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.ViewName.Should().Be("PrintInvoice");
            var model = result.Model as PrintInvoiceViewModel;
            model.Should().NotBeNull();
            model!.Reference.Should().Be("UB-QT-9999");
            model.TotalAmount.Should().Be(500000m);
            model.Milestones.Should().HaveCount(3);
            model.Milestones[0].Amount.Should().Be(125000m); // 25% Deposit
            model.Milestones[1].Amount.Should().Be(250000m); // 50% Progress
            model.Milestones[2].Amount.Should().Be(125000m); // 25% Handover
        }
    }
}
