using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Payments.Application.CreatePayment;
using SettleCore.Modules.Payments.Application.GetPaymentLedgerPosting;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Application.GetPaymentById;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using SettleCore.Modules.Payments.Application.GetPaymentByProviderReference;

namespace SettleCore.IntegrationTests;

public sealed class PaymentsPostgreSqlEndToEndTests
{
    [Fact]
    public async Task PostThenGetPaymentRoundTripsThroughPostgreSql()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("settlecore_test")
            .WithUsername("settlecore")
            .WithPassword("settlecore")
            .Build();

        await postgres.StartAsync();

        using var factory =
            new PaymentsApiFactory(postgres.GetConnectionString());

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

            await dbContext.Database.MigrateAsync();
        }

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var createResponse = await client.PostAsJsonAsync(
            "/payments",
            new
            {
                amount = 321.45m,
                currency = "sgd"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<CreatePaymentResult>();

        var createdPayment =
            Assert.IsType<CreatePaymentResult>(created);

        Assert.NotEqual(
            Guid.Empty,
            createdPayment.PaymentId);

        Assert.Equal(
            321.45m,
            createdPayment.Amount);

        Assert.Equal(
            "SGD",
            createdPayment.Currency);

        Assert.Equal(
            "Pending",
            createdPayment.Status);

        var getResponse = await client.GetAsync(
            $"/payments/{createdPayment.PaymentId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        var fetched =
            await getResponse.Content
                .ReadFromJsonAsync<GetPaymentByIdResult>();

        var fetchedPayment =
            Assert.IsType<GetPaymentByIdResult>(fetched);

        Assert.Equal(
            createdPayment.PaymentId,
            fetchedPayment.PaymentId);

        Assert.Equal(
            createdPayment.Amount,
            fetchedPayment.Amount);

        Assert.Equal(
            createdPayment.Currency,
            fetchedPayment.Currency);

        Assert.Equal(
            createdPayment.Status,
            fetchedPayment.Status);
    }

    [Fact]
    public async Task SucceedPaymentPersistsSucceededStatusThroughPostgreSql()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("settlecore_test")
            .WithUsername("settlecore")
            .WithPassword("settlecore")
            .Build();

        await postgres.StartAsync();

        using var factory =
            new PaymentsApiFactory(postgres.GetConnectionString());

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

            await dbContext.Database.MigrateAsync();
        }

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var createResponse = await client.PostAsJsonAsync(
            "/payments",
            new
            {
                amount = 500.00m,
                currency = "sgd"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<CreatePaymentResult>();

        var createdPayment =
            Assert.IsType<CreatePaymentResult>(created);

        Assert.Equal(
            "Pending",
            createdPayment.Status);

        var succeedResponse = await client.PostAsJsonAsync(
            $"/payments/{createdPayment.PaymentId}/succeed",
            new PaymentLedgerPostingInput(Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 300));

        Assert.Equal(
            HttpStatusCode.OK,
            succeedResponse.StatusCode);

        var succeeded =
            await succeedResponse.Content
                .ReadFromJsonAsync<MarkPaymentSucceededResult>();

        var succeededPayment =
            Assert.IsType<MarkPaymentSucceededResult>(succeeded);

        Assert.Equal(
            createdPayment.PaymentId,
            succeededPayment.PaymentId);

        Assert.Equal(
            500.00m,
            succeededPayment.Amount);

        Assert.Equal(
            "SGD",
            succeededPayment.Currency);

        Assert.Equal(
            "Succeeded",
            succeededPayment.Status);

        var getResponse = await client.GetAsync(
            $"/payments/{createdPayment.PaymentId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        var fetched =
            await getResponse.Content
                .ReadFromJsonAsync<GetPaymentByIdResult>();

        var fetchedPayment =
            Assert.IsType<GetPaymentByIdResult>(fetched);

        Assert.Equal(
            createdPayment.PaymentId,
            fetchedPayment.PaymentId);

        Assert.Equal(
            500.00m,
            fetchedPayment.Amount);

        Assert.Equal(
            "SGD",
            fetchedPayment.Currency);

        Assert.Equal(
            "Succeeded",
            fetchedPayment.Status);
    }

    [Fact]
    public async Task SuccessRequiresExplicitInputAndPersistsRetrySafeIntent()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        using var factory = new PaymentsApiFactory(postgres.GetConnectionString());
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        var payment = SettleCore.Modules.Payments.Domain.Payment.Create(100m, "SGD");
        await using (var seed = new PaymentsDbContext(options))
        {
            await seed.Database.MigrateAsync();
            seed.Payments.Add(payment);
            await seed.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        var url = $"/payments/{payment.Id.Value}/succeed";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(url, null)).StatusCode);
        var input = new PaymentLedgerPostingInput(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 300);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url,
            input with { FeeAmountMinorUnits = 0 })).StatusCode);
        await using (var before = new PaymentsDbContext(options))
        {
            Assert.Equal(SettleCore.Modules.Payments.Domain.PaymentStatus.Pending,
                (await before.Payments.SingleAsync()).Status);
            Assert.Empty(await before.PaymentLedgerPostingIntents.ToListAsync());
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            $"/payments/{Guid.NewGuid()}/succeed", input)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(url, input)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(url, input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(url,
            input with { TransactionId = Guid.NewGuid() })).StatusCode);
        await using var reader = new PaymentsDbContext(options);
        Assert.Equal(SettleCore.Modules.Payments.Domain.PaymentStatus.Succeeded,
            (await reader.Payments.SingleAsync()).Status);
        var stored = Assert.Single(await reader.PaymentLedgerPostingIntents.ToListAsync());
        Assert.Equal(payment.Id, stored.PaymentId);
        Assert.Equal(input.TransactionId, stored.TransactionId);
        Assert.Equal(input.LedgerId, stored.LedgerId);
        Assert.Equal(input.ProcessorReceivableAccountId, stored.ProcessorReceivableAccountId);
        Assert.Equal(input.MerchantPayableAccountId, stored.MerchantPayableAccountId);
        Assert.Equal(input.PlatformRevenueAccountId, stored.PlatformRevenueAccountId);
        Assert.Equal("SGD", stored.Currency);
        Assert.Equal(10_000, stored.GrossAmountMinorUnits);
        Assert.Equal(300, stored.FeeAmountMinorUnits);
        Assert.Equal(SettleCore.Modules.Payments.Domain.PaymentLedgerPostingIntentStatus.Pending, stored.Status);
    }

    [Fact]
    public async Task PostingStatusLookupReflectsDurableLifecycleWithoutWrites()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        await using (var setup = new PaymentsDbContext(options))
        {
            await setup.Database.MigrateAsync();
        }
        using var factory = new PaymentsApiFactory(postgres.GetConnectionString());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        var created = Assert.IsType<CreatePaymentResult>(await (await client.PostAsJsonAsync(
            "/payments", new { amount = 100m, currency = "SGD" })).Content.ReadFromJsonAsync<CreatePaymentResult>());
        var url = $"/payments/{created.PaymentId}/ledger-posting";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
            $"/payments/{Guid.NewGuid()}/ledger-posting")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(
            $"/payments/{Guid.Empty}/ledger-posting")).StatusCode);
        var input = new PaymentLedgerPostingInput(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 300);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            $"/payments/{created.PaymentId}/succeed", input)).StatusCode);
        var pendingResponse = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        var pending = Assert.IsType<GetPaymentLedgerPostingResult>(
            await pendingResponse.Content.ReadFromJsonAsync<GetPaymentLedgerPostingResult>());
        Assert.Equal(created.PaymentId, pending.PaymentId);
        Assert.Equal(input.TransactionId, pending.TransactionId);
        Assert.Equal("Pending", pending.Status);
        Assert.Null(pending.NextAttemptAt);
        Assert.Null(pending.PostedAt);
        var due = new DateTimeOffset(2026, 10, 9, 1, 2, 3, TimeSpan.Zero);
        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IPaymentLedgerPostingIntentRepository>();
            Assert.True(await repository.ScheduleRetryAsync(
                PaymentId.From(created.PaymentId), due));
        }
        var scheduled = await client.GetFromJsonAsync<GetPaymentLedgerPostingResult>(url);
        Assert.NotNull(scheduled);
        Assert.Equal("Pending", scheduled.Status);
        Assert.Equal(due, scheduled.NextAttemptAt);
        Assert.Null(scheduled.PostedAt);
        using (var scope = factory.Services.CreateScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IPaymentLedgerPostingIntentRepository>()
                .MarkPostedAsync(PaymentId.From(created.PaymentId)));
        }
        var posted = await client.GetFromJsonAsync<GetPaymentLedgerPostingResult>(url);
        Assert.NotNull(posted);
        Assert.Equal("Posted", posted.Status);
        Assert.Equal(input.TransactionId, posted.TransactionId);
        await using var reader = new PaymentsDbContext(options);
        var stored = Assert.Single(await reader.PaymentLedgerPostingIntents.ToListAsync());
        Assert.Equal(SettleCore.Modules.Payments.Domain.PaymentLedgerPostingIntentStatus.Posted, stored.Status);
        Assert.Equal(stored.NextAttemptAt, posted.NextAttemptAt);
        Assert.NotNull(posted.PostedAt);
        Assert.Equal(stored.PostedAt, posted.PostedAt);
        var events = await reader.PaymentLedgerPostingEvents.OrderBy(x => x.OccurredAt).ToListAsync();
        Assert.Equal(new[] { PaymentLedgerPostingEventKind.IntentRecorded,
            PaymentLedgerPostingEventKind.RetryScheduled,
            PaymentLedgerPostingEventKind.PostingAcknowledged }, events.Select(x => x.Kind));
        Assert.All(events, audit => {
            Assert.Equal(created.PaymentId, audit.PaymentId.Value);
            Assert.Equal(input.TransactionId, audit.TransactionId);
        });
        Assert.Equal(due, events[1].NextAttemptAt);
        Assert.Equal(posted.PostedAt, events[2].OccurredAt);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(3, await reader.PaymentLedgerPostingEvents.CountAsync());
        Assert.Equal(300, stored.FeeAmountMinorUnits);
        Assert.Equal(10_000, stored.GrossAmountMinorUnits);
    }

    private sealed class PaymentsApiFactory(
        string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<
                    IDbContextOptionsConfiguration<PaymentsDbContext>>();

                services.RemoveAll<
                    DbContextOptions<PaymentsDbContext>>();

                services.RemoveAll<
                    PaymentsDbContext>();

                services.AddDbContext<PaymentsDbContext>(
                    options =>
                        options.UseNpgsql(connectionString));
            });
        }
    }

    [Fact]
    public async Task DuplicateProviderReferenceReturnsConflict()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("settlecore_test")
            .WithUsername("settlecore")
            .WithPassword("settlecore")
            .Build();

        await postgres.StartAsync();

        using var factory =
            new PaymentsApiFactory(postgres.GetConnectionString());

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

            await dbContext.Database.MigrateAsync();
        }

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var firstCreateResponse = await client.PostAsJsonAsync(
            "/payments",
            new
            {
                amount = 100.00m,
                currency = "SGD"
            });

        var firstPayment =
            Assert.IsType<CreatePaymentResult>(
                await firstCreateResponse.Content
                    .ReadFromJsonAsync<CreatePaymentResult>());

        var secondCreateResponse = await client.PostAsJsonAsync(
            "/payments",
            new
            {
                amount = 200.00m,
                currency = "SGD"
            });

        var secondPayment =
            Assert.IsType<CreatePaymentResult>(
                await secondCreateResponse.Content
                    .ReadFromJsonAsync<CreatePaymentResult>());

        var firstAttachResponse = await client.PostAsJsonAsync(
            $"/payments/{firstPayment.PaymentId}/provider-reference",
            new
            {
                provider = "stripe",
                reference = "pi_duplicate"
            });

        Assert.Equal(
            HttpStatusCode.OK,
            firstAttachResponse.StatusCode);

        var secondAttachResponse = await client.PostAsJsonAsync(
            $"/payments/{secondPayment.PaymentId}/provider-reference",
            new
            {
                provider = "stripe",
                reference = "pi_duplicate"
            });

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondAttachResponse.StatusCode);
    }

    [Fact]
    public async Task AttachedProviderReferenceRoundTripsThroughPostgreSql()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("settlecore_test")
            .WithUsername("settlecore")
            .WithPassword("settlecore")
            .Build();

        await postgres.StartAsync();

        using var factory =
            new PaymentsApiFactory(postgres.GetConnectionString());

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

            await dbContext.Database.MigrateAsync();
        }

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var createResponse = await client.PostAsJsonAsync(
            "/payments",
            new
            {
                amount = 250.00m,
                currency = "SGD"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            Assert.IsType<CreatePaymentResult>(
                await createResponse.Content
                    .ReadFromJsonAsync<CreatePaymentResult>());

        var attachResponse = await client.PostAsJsonAsync(
            $"/payments/{created.PaymentId}/provider-reference",
            new
            {
                provider = "stripe",
                reference = "pi_roundtrip"
            });

        Assert.Equal(
            HttpStatusCode.OK,
            attachResponse.StatusCode);

        var getResponse = await client.GetAsync(
            $"/payments/{created.PaymentId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        var fetched =
            Assert.IsType<GetPaymentByIdResult>(
                await getResponse.Content
                    .ReadFromJsonAsync<GetPaymentByIdResult>());

        Assert.Equal(
            created.PaymentId,
            fetched.PaymentId);

        Assert.Equal(
            "stripe",
            fetched.Provider);

        Assert.Equal(
            "pi_roundtrip",
            fetched.ProviderReference);
    }

    [Fact]
    public async Task LookupByProviderReferenceReturnsPersistedPayment()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("settlecore_test")
            .WithUsername("settlecore")
            .WithPassword("settlecore")
            .Build();

        await postgres.StartAsync();

        using var factory =
            new PaymentsApiFactory(postgres.GetConnectionString());

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

            await dbContext.Database.MigrateAsync();
        }

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var createResponse = await client.PostAsJsonAsync(
            "/payments",
            new
            {
                amount = 425.50m,
                currency = "SGD"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            Assert.IsType<CreatePaymentResult>(
                await createResponse.Content
                    .ReadFromJsonAsync<CreatePaymentResult>());

        var attachResponse = await client.PostAsJsonAsync(
            $"/payments/{created.PaymentId}/provider-reference",
            new
            {
                provider = "stripe",
                reference = "pi_lookup_e2e"
            });

        Assert.Equal(
            HttpStatusCode.OK,
            attachResponse.StatusCode);

        var lookupResponse = await client.GetAsync(
            "/payments/by-provider-reference" +
            "?provider=stripe" +
            "&reference=pi_lookup_e2e");

        Assert.Equal(
            HttpStatusCode.OK,
            lookupResponse.StatusCode);

        var found =
            Assert.IsType<GetPaymentByProviderReferenceResult>(
                await lookupResponse.Content
                    .ReadFromJsonAsync<GetPaymentByProviderReferenceResult>());

        Assert.Equal(
            created.PaymentId,
            found.PaymentId);

        Assert.Equal(
            425.50m,
            found.Amount);

        Assert.Equal(
            "SGD",
            found.Currency);

        Assert.Equal(
            "Pending",
            found.Status);

        Assert.Equal(
            "stripe",
            found.Provider);

        Assert.Equal(
            "pi_lookup_e2e",
            found.ProviderReference);
    }
}
