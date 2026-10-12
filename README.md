# SettleCore

A locally runnable .NET modular payment backend built as a portfolio project. Payments, Ledger and Reconciliation have separate PostgreSQL persistence contexts. Stripe PaymentIntents are the first provider; payment completion is authorized by authenticated webhook evidence rather than a creation response.

The agreed deliverable is the **local backend**: no frontend, hosting or deployment is required. Automated acceptance uses the actual Stripe SDK with an isolated fake HTTP transport and real local PostgreSQL. No live external Stripe acceptance is claimed.

## Run the verification

Prerequisites: the SDK selected by `global.json` (currently .NET 10.0.401), a running Docker daemon, and permission to start PostgreSQL containers. Restore and first image pulls require network access; the payment acceptance itself makes no external provider requests.

```sh
dotnet restore SettleCore.slnx
dotnet build SettleCore.slnx --configuration Release --no-restore
dotnet test SettleCore.slnx --configuration Release --no-build --verbosity minimal
```

See [the local demo runbook](docs/LOCAL_DEMO.md) for a focused, fully local Stripe flow and an interactive operator API demo. [Operations](OPERATIONS.md) covers configuration, migrations, metrics and safe orphan recovery. [Architecture](docs/ARCHITECTURE.md) explains module boundaries and recovery guarantees. [Acceptance evidence](docs/ACCEPTANCE.md) records exact verified commits and limits; [the checkpoint](PROJECT_CHECKPOINT.md) preserves continuation history.

## Backend capabilities

- Explicit Ledger account provisioning with idempotent replay and conflicting identity/currency rejection; balanced, immutable transaction posting and concurrent replay protection.
- Payment-derived provider idempotency, persisted preparation and first-attempt timestamps, conservative creation retry limits, verified original-intent recovery and response-only client secrets.
- Durable signed receipt ingestion, authoritative correlated completion, atomic success/posting-intent/audit recording, and separately retrying receipt and Ledger posting workers.
- Default-on local operator authentication over HTTPS, public dependency-free liveness, all-module database/schema readiness, and tag-free worker metrics.

Accounting inputs are always explicit: transaction, ledger and account IDs, gross amount/currency and chosen fee. The project does not implement merchant routing, fee policy, multi-tenant customer authentication, refunds or payouts. Worker and provider integrations are opt-in. Do not use the local demo key model as a public customer API.

## API overview

All routes below require `X-SettleCore-Operator-Key` over HTTPS unless marked public. Configure a randomly generated `LocalApiAccess__OperatorKey` of at least 32 characters before starting the host; access is enabled by default.

| Route | Purpose |
| --- | --- |
| `POST /payments` | Create a pending local payment |
| `GET /payments/{id}` | Inspect local status/reference |
| `POST /ledger/accounts` | Provision explicit account identity/ledger/currency |
| `POST /payments/{id}/ledger-posting/preparation` | Persist immutable explicit posting inputs |
| `POST /payments/{id}/provider-payment` | Create the provider intent inside the retry window |
| `GET /payments/{id}/provider-payment` | Retrieve and correlate the stored provider intent |
| `POST /payments/{id}/provider-reference` | Recover a supplied original intent; verified lookup when Stripe is enabled |
| `POST /payments/webhooks/stripe` | Public, signature-authenticated durable receipt ingress |
| `GET /payments/{id}/ledger-posting` | Inspect posting status/retry/acknowledgment |
| `POST /ledger/transactions`, `GET /ledger/transactions/{id}` | Explicit balanced posting and retrieval |
| `POST /reconciliations`, `GET /reconciliations/{id}` | Store/inspect caller-supplied expected-versus-actual comparisons |
| `POST /payments/{id}/succeed` | Provider-disabled local demonstration path only |
| `GET /health/live`, `GET /health/ready` | Public liveness and dependency/schema readiness |
