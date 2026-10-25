using Meridian.Api.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Meridian.Api.IntegrationTests;

public sealed class DatabaseConfigurationTests
{
    [Fact]
    public async Task ConnectionsDoNotProbeForKerberosEncryption()
    {
        // The chiseled runtime image has no libgssapi; Neon doesn't use GSS. Probing only costs a round trip and logs an error.
        await using var factory = MeridianApiFactory.WithUnreachableDatabase();

        var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();

        new NpgsqlConnectionStringBuilder(dataSource.ConnectionString).GssEncryptionMode.ShouldBe(GssEncryptionMode.Disable);
    }
}
