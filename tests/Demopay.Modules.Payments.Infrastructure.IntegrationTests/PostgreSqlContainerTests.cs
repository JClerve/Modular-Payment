using System.Data;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Demopay.Modules.Payments.Infrastructure.IntegrationTests;

public sealed class PostgreSqlContainerTests
{
    [Fact]
    public async Task PostgreSqlContainerStartsAndAcceptsConnections()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("Demopay_test")
            .WithUsername("Demopay")
            .WithPassword("Demopay")
            .Build();

        await postgres.StartAsync();

        await using var connection =
            new NpgsqlConnection(postgres.GetConnectionString());

        await connection.OpenAsync();

        Assert.Equal(ConnectionState.Open, connection.State);
    }
}