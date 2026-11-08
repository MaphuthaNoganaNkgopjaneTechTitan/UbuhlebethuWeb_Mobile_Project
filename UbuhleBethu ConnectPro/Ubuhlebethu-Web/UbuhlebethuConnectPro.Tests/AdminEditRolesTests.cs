using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using UbuhlebethuConnectPro.WebApp.Controllers;

namespace UbuhlebethuConnectPro.Tests;

public class AdminEditRolesTests
{
    [Fact]
    public async Task EditRolesGet_UsesApiRolesAndReportsStaleRoles()
    {
        var client = new Mock<IConnectProApiClient>();
        client.Setup(x => x.GetUserAsync("user-1")).ReturnsAsync(new UserDto
        {
            Id = "user-1",
            UserName = "user@example.com"
        });
        client.Setup(x => x.GetUserRolesAsync("user-1")).ReturnsAsync(new UserRolesDto
        {
            CurrentRoles = new List<string> { "Admin", "LegacyRole" },
            AvailableRoles = new List<string> { "Admin", "FieldWorker" }
        });

        var controller = new AdminController(client.Object);
        var result = await controller.EditRoles("user-1");

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<EditRolesViewModel>().Subject;
        model.Roles.Select(role => role.RoleName).Should().BeEquivalentTo("Admin", "FieldWorker");
        model.Roles.Single(role => role.RoleName == "Admin").Selected.Should().BeTrue();
        model.StaleRoles.Should().ContainSingle().Which.Should().Be("LegacyRole");
    }

    [Fact]
    public async Task EditRolesPost_DoesNotSubmitRolesThatAreNoLongerAvailable()
    {
        var client = new Mock<IConnectProApiClient>();
        client.Setup(x => x.GetUserAsync("user-1")).ReturnsAsync(new UserDto
        {
            Id = "user-1",
            UserName = "user@example.com"
        });
        client.Setup(x => x.GetUserRolesAsync("user-1")).ReturnsAsync(new UserRolesDto
        {
            CurrentRoles = new List<string> { "Admin" },
            AvailableRoles = new List<string> { "Admin" }
        });

        var controller = new AdminController(client.Object);
        var result = await controller.EditRoles(new EditRolesViewModel
        {
            UserId = "user-1",
            Roles = new List<RoleCheckbox>
            {
                new() { RoleName = "LegacyRole", Selected = true }
            }
        });

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewData.ModelState.IsValid.Should().BeFalse();
        client.Verify(x => x.UpdateUserRolesAsync(It.IsAny<string>(), It.IsAny<UpdateUserRolesRequest>()), Times.Never);
    }
}
