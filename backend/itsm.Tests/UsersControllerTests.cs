using System.Security.Claims;
using FluentValidation;
using FluentAssertions;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Persistence.Services;
using itsm.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace itsm.Tests;

public class UsersControllerTests
{
    [Fact]
    public async Task Update_TenantOwnerByLocalAdmin_ReturnsForbidden()
    {
        var options = new DbContextOptionsBuilder<ItsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetCurrentTenantId(1);
        await using var db = new ItsmDbContext(options, tenantContext);
        await db.Database.EnsureCreatedAsync();

        var admin = new User { Username = "admin", Email = "admin@example.test", TenantId = 1 };
        admin.RoleMappings.Add(new UserRoleMapping { RoleId = 2 });
        var owner = new User { Username = "owner", Email = "owner@example.test", TenantId = 1 };
        owner.RoleMappings.Add(new UserRoleMapping { RoleId = 6 });
        db.Users.AddRange(admin, owner);
        await db.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        var service = new UserAdminService(db, hasher.Object);
        var controller = new UsersController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim("Id", admin.Id.ToString())
                    }, "test"))
                }
            }
        };
        var validator = new InlineValidator<UpdateUserRequest>();

        var result = await controller.Update(owner.Id, new UpdateUserRequest
        {
            Username = owner.Username,
            Email = owner.Email,
            FullName = "Changed owner"
        }, validator, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
