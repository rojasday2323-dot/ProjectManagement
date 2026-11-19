using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectManagement.Api.Data;
using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Tests.Endpoints;

public class UserEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetUsers_ReturnsOkWithData()
    {
        var response = await _client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        Assert.NotNull(users);
        Assert.True(users.Count >= 9);
    }

    [Fact]
    public async Task GetDevelopers_ReturnsOkWithData()
    {
        var response = await _client.GetAsync("/api/developers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var developers = await response.Content.ReadFromJsonAsync<List<DeveloperResponse>>();
        Assert.NotNull(developers);
        Assert.True(developers.Count >= 5);
    }

    [Fact]
    public async Task SeedRequestedDevelopers_IsIdempotentAndDoesNotInventEmails()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DbSeeder.SeedAsync(db);

        var names = new[]
        {
            "Lozano Velázquez Oscar Javier",
            "Pacheco Luna Ana Karina",
            "Honorato Mejía Alberto",
            "Martínez Delgado Roberto Valentín",
            "Velázquez López Juan Carlos",
            "Dorantes Martínez Alejandra",
            "Peña Pacheco Lisbet"
        };
        var developers = await _client.GetFromJsonAsync<List<DeveloperResponse>>(
            "/api/developers?includeInactive=true");

        Assert.NotNull(developers);
        foreach (var name in names)
        {
            var matches = developers.Where(developer => developer.DisplayName == name).ToArray();
            Assert.Single(matches);
            Assert.Null(matches[0].Email);
        }
    }

    [Fact]
    public async Task Developer_CanBeCreatedEditedAndDeactivatedWithoutDeletingHistory()
    {
        var uniqueName = $"Developer API test {Guid.NewGuid():N}";
        var createdResponse = await _client.PostAsJsonAsync(
            "/api/developers",
            new CreateDeveloperRequest(uniqueName, null, null));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<DeveloperResponse>();
        Assert.NotNull(created);
        Assert.Null(created.Email);
        Assert.True(created.IsActive);

        var duplicateResponse = await _client.PostAsJsonAsync(
            "/api/developers",
            new CreateDeveloperRequest(uniqueName.ToUpperInvariant(), null, null));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/developers/{created.Id}",
            new UpdateDeveloperRequest($"{uniqueName} editado", "QA", "Senior"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<DeveloperResponse>();
        Assert.NotNull(updated);
        Assert.Equal("QA", updated.Specialty);

        var deactivateResponse = await _client.PatchAsJsonAsync(
            $"/api/developers/{created.Id}/status",
            new UpdateDeveloperStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        var inactive = await deactivateResponse.Content.ReadFromJsonAsync<DeveloperResponse>();
        Assert.NotNull(inactive);
        Assert.False(inactive.IsActive);

        var selectableDevelopers = await _client.GetFromJsonAsync<List<DeveloperResponse>>("/api/developers");
        Assert.NotNull(selectableDevelopers);
        Assert.DoesNotContain(selectableDevelopers, developer => developer.Id == created.Id);
        var allDevelopers = await _client.GetFromJsonAsync<List<DeveloperResponse>>("/api/developers?includeInactive=true");
        Assert.Contains(allDevelopers!, developer => developer.Id == created.Id);
    }

    [Fact]
    public async Task GetProductOwners_ReturnsOkWithData()
    {
        var response = await _client.GetAsync("/api/product-owners");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var owners = await response.Content.ReadFromJsonAsync<List<ProductOwnerResponse>>();
        Assert.NotNull(owners);
        Assert.True(owners.Count >= 3);
    }

    [Fact]
    public async Task GetUserById_NonExisting_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/users/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
