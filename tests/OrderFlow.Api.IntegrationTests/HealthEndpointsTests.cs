using OrderFlow.Api.IntegrationTests.Infrastructure;

namespace OrderFlow.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class HealthEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_report_healthy(string path)
    {
        var response = await CreateClient().GetAsync(path, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }
}
