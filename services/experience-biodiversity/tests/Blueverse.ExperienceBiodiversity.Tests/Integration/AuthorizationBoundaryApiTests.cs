using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Blueverse.ExperienceBiodiversity.DTOs;
using Blueverse.ExperienceBiodiversity.Tests.Fixtures;
using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Integration;

public sealed class AuthorizationBoundaryApiTests : IClassFixture<TestWebApplicationFactory>
{
    private const string CatalogueRead = "experiences.catalogue.read";
    private const string CatalogueManage = "experiences.catalogue.manage";
    private const string SystemRoleManage = "auth.role.system.manage";

    private static readonly Guid ExistingRecordId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly ManagementRequest[] ManagementRequests =
    [
        new(HttpMethod.Post, "/api/experiences/destinations"),
        new(HttpMethod.Put, $"/api/experiences/destinations/{ExistingRecordId}"),
        new(HttpMethod.Delete, $"/api/experiences/destinations/{ExistingRecordId}"),
        new(HttpMethod.Post, $"/api/experiences/destinations/{ExistingRecordId}/publication-evaluations"),
        new(new HttpMethod("PATCH"), $"/api/experiences/destinations/{ExistingRecordId}/publication"),
        new(HttpMethod.Post, "/api/experiences/activities"),
        new(HttpMethod.Put, $"/api/experiences/activities/{ExistingRecordId}"),
        new(HttpMethod.Delete, $"/api/experiences/activities/{ExistingRecordId}"),
        new(HttpMethod.Post, $"/api/experiences/activities/{ExistingRecordId}/publication-evaluations"),
        new(new HttpMethod("PATCH"), $"/api/experiences/activities/{ExistingRecordId}/publication"),
        new(HttpMethod.Post, "/api/experiences/offerings"),
        new(HttpMethod.Put, $"/api/experiences/offerings/{ExistingRecordId}"),
        new(HttpMethod.Delete, $"/api/experiences/offerings/{ExistingRecordId}"),
        new(HttpMethod.Post, $"/api/experiences/offerings/{ExistingRecordId}/publication-evaluations"),
        new(new HttpMethod("PATCH"), $"/api/experiences/offerings/{ExistingRecordId}/publication"),
        new(HttpMethod.Post, $"/api/experiences/offerings/{ExistingRecordId}/schedules"),
        new(HttpMethod.Put, $"/api/experiences/offerings/{ExistingRecordId}/schedules/{ExistingRecordId}"),
        new(HttpMethod.Delete, $"/api/experiences/offerings/{ExistingRecordId}/schedules/{ExistingRecordId}")
    ];

    private readonly TestWebApplicationFactory _factory;

    public AuthorizationBoundaryApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("CaseId", "EXP-API-AUTHZ-001")]
    public async Task PublicCatalogueAndHealthReads_AreAvailableWithoutCredentials()
    {
        using var client = _factory.CreateAnonymousClient();

        foreach (var path in new[]
        {
            "/api/experiences/destinations",
            "/api/experiences/activities",
            "/api/experiences/offerings",
            "/api/experiences/map/config",
            "/api/experiences/health"
        })
        {
            using var response = await client.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"Expected an anonymous public read at {path}, received {(int)response.StatusCode}.");
            Assert.NotEmpty(await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    [Trait("CaseId", "EXP-API-AUTHZ-002")]
    public async Task EveryCatalogueMutation_RejectsAnonymousReadOnlyAndRoleOnlyActorsWithoutChangingData()
    {
        using var anonymous = _factory.CreateClient();
        using var readOnly = _factory.CreateAuthenticatedClient(Guid.NewGuid(), CatalogueRead);
        using var roleOnlyAdmin = _factory.CreateRoleOnlyClient(Guid.NewGuid(), "Admin");

        var initialCounts = await _factory.GetCatalogueCountsAsync();
        foreach (var endpoint in ManagementRequests)
        {
            await AssertRejectedAsync(anonymous, endpoint, HttpStatusCode.Unauthorized);
            await AssertRejectedAsync(readOnly, endpoint, HttpStatusCode.Forbidden);
            await AssertRejectedAsync(roleOnlyAdmin, endpoint, HttpStatusCode.Forbidden);
        }

        Assert.Equal(initialCounts, await _factory.GetCatalogueCountsAsync());
    }

    [Fact]
    [Trait("CaseId", "EXP-API-AUTHZ-005")]
    public async Task SystemRoleManagerPermission_GrantsCatalogueMutationAndDraftReadAccess()
    {
        using var systemRoleManager = _factory.CreateAuthenticatedClient(Guid.NewGuid(), SystemRoleManage);
        var slug = $"system-manager-{Guid.NewGuid():N}";
        var request = new CreateDestinationRequest(
            $"System Role Managed {slug}", slug, "Created with the system role permission", "Eastern Province", 8.4, 81.2);

        using var createResponse = await systemRoleManager.PostAsJsonAsync("/api/experiences/destinations", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(created);
        Assert.Equal("DRAFT", created.Status);

        using var draftListResponse = await systemRoleManager.GetAsync("/api/experiences/destinations?status=DRAFT");
        Assert.Equal(HttpStatusCode.OK, draftListResponse.StatusCode);
        using var draftListJson = JsonDocument.Parse(await draftListResponse.Content.ReadAsStringAsync());
        Assert.Contains(
            draftListJson.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == created.Id);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-AUTHZ-003")]
    public async Task DraftCatalogueRecords_AreHiddenFromGuestsAndVisibleToCatalogueReaders()
    {
        using var manager = _factory.CreateAuthenticatedClient(Guid.NewGuid(), CatalogueManage);
        using var reader = _factory.CreateAuthenticatedClient(Guid.NewGuid(), CatalogueRead);
        using var anonymous = _factory.CreateAnonymousClient();

        var slug = $"draft-{Guid.NewGuid():N}";
        var createRequest = new CreateDestinationRequest(
            $"Private Draft {slug}", slug, "A private draft coast record", "Southern Province", 6.1, 80.2);
        using var createResponse = await manager.PostAsJsonAsync("/api/experiences/destinations", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(created);
        Assert.Equal("DRAFT", created.Status);

        using var guestDetail = await anonymous.GetAsync($"/api/experiences/destinations/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, guestDetail.StatusCode);
        var guestBody = await guestDetail.Content.ReadAsStringAsync();
        using var notFoundDocument = JsonDocument.Parse(guestBody);
        var notFound = notFoundDocument.RootElement;
        Assert.Equal("Destination Not Found", notFound.GetProperty("title").GetString());
        Assert.DoesNotContain(createRequest.Name, guestBody);

        using var readerDetail = await reader.GetAsync($"/api/experiences/destinations/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, readerDetail.StatusCode);
        var visibleDraft = await readerDetail.Content.ReadFromJsonAsync<DestinationDto>();
        Assert.NotNull(visibleDraft);
        Assert.Equal(created.Id, visibleDraft.Id);
        Assert.Equal("DRAFT", visibleDraft.Status);

        using var guestList = await anonymous.GetAsync("/api/experiences/destinations");
        Assert.Equal(HttpStatusCode.OK, guestList.StatusCode);
        using var guestListJson = JsonDocument.Parse(await guestList.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            guestListJson.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == created.Id);

        using var readerDraftList = await reader.GetAsync("/api/experiences/destinations?status=DRAFT");
        Assert.Equal(HttpStatusCode.OK, readerDraftList.StatusCode);
        using var readerDraftListJson = JsonDocument.Parse(await readerDraftList.Content.ReadAsStringAsync());
        Assert.Contains(
            readerDraftListJson.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == created.Id);
    }

    [Fact]
    [Trait("CaseId", "EXP-API-AUTHZ-004")]
    public async Task UserAndReadEndpoints_UseTheirSpecificAuthenticationRequirements()
    {
        using var anonymous = _factory.CreateAnonymousClient();
        using var signedIn = _factory.CreateAuthenticatedClient(Guid.NewGuid());
        using var reader = _factory.CreateAuthenticatedClient(Guid.NewGuid(), CatalogueRead);

        using var anonymousFavourites = await anonymous.GetAsync("/api/experiences/favourites");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousFavourites.StatusCode);
        using var userFavourites = await signedIn.GetAsync("/api/experiences/favourites");
        Assert.Equal(HttpStatusCode.OK, userFavourites.StatusCode);
        Assert.Empty(await userFavourites.Content.ReadFromJsonAsync<List<JsonElement>>() ?? []);

        var availabilityRequest = new
        {
            offeringId = Guid.NewGuid(),
            startsAt = DateTimeOffset.UtcNow,
            endsAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        using var anonymousAvailability = await anonymous.PostAsJsonAsync(
            "/api/experiences/availability/evaluations", availabilityRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousAvailability.StatusCode);
        using var forbiddenAvailability = await signedIn.PostAsJsonAsync(
            "/api/experiences/availability/evaluations", availabilityRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenAvailability.StatusCode);
        using var allowedAvailability = await reader.PostAsJsonAsync(
            "/api/experiences/availability/evaluations", availabilityRequest);
        Assert.Equal(HttpStatusCode.OK, allowedAvailability.StatusCode);
        var availability = await allowedAvailability.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(availability.TryGetProperty("status", out _));

        using var anonymousAgentContext = await anonymous.GetAsync("/api/experiences/agent/context");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousAgentContext.StatusCode);
        using var signedInAgentContext = await signedIn.GetAsync("/api/experiences/agent/context");
        Assert.Equal(HttpStatusCode.OK, signedInAgentContext.StatusCode);
        var agent = await signedInAgentContext.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_connected", agent.GetProperty("status").GetString());
    }

    private static async Task AssertRejectedAsync(
        HttpClient client,
        ManagementRequest endpoint,
        HttpStatusCode expectedStatus)
    {
        using var request = new HttpRequestMessage(endpoint.Method, endpoint.Path);
        if (endpoint.Method != HttpMethod.Delete)
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == expectedStatus,
            $"{endpoint.Method} {endpoint.Path} expected {(int)expectedStatus}, received {(int)response.StatusCode}.");
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    private sealed record ManagementRequest(HttpMethod Method, string Path);
}
