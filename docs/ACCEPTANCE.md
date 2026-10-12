# Local backend acceptance — 2026-10-10

Scope: a locally runnable portfolio backend with Stripe as the first provider, PostgreSQL, explicit Ledger accounting, durable workers/retries, reconciliation, local operator controls and reproducible documentation. Deployment, cloud rollout and frontend are excluded. Optional real external Stripe TEST-mode acceptance is explicitly unverified; no live charge or hosted system is claimed.

## Final checklist

| Requirement | Evidence |
| --- | --- |
| Actual starting state inspected | Clean `main` at `23b2fdcce39f74afcd799a591a4de5bc3eda36cc`, fetched remote matched, prior exact-head CI green; fresh 436-test baseline |
| Explicit account provisioning | HTTP RED404 then GREEN; caller account/ledger/currency, matching replay200, changed ledger/currency409, invalid ID400; concurrent initial replays in final acceptance |
| Provider receipt worker observability | Tag-free attempt/completion/deferral/failure/retry-write counters; real durable processor tests and cancellation/failing retry-write cases |
| Expired orphan safety | Durable immutable attempt + 23h guard; supplied original intent retrieved/correlated before attachment; mismatch409, valid/replay200, zero creation calls, unchanged timestamp, no local completion |
| Local sensitive endpoint access | Default-on operator key, HTTPS, fixed-time hashed comparison, missing/wrong/duplicate credentials401; invalid configuration fails closed; all module routes share policy |
| Neutral module boundaries | Stripe SDK references only Payments Infrastructure; neutral application/provider contracts; no creation-response completion |
| Signed durable completion | Invalid signature rejected, valid evidence persisted before acknowledgment, correlated completion atomically records success/intent/audit/receipt time |
| Complete local fake-transport Stripe acceptance | Actual SDK + three migrated PostgreSQL databases + authenticated provisioning/preparation/creation + separately signed public webhook + both workers + one exact balanced transaction + replay + authenticated reads/reconciliation |
| Migrations/readiness | CLI migration commands exercised for all three stores; EF reports no model drift in all modules; readiness200 after migration,503 after database stop; liveness200 throughout |
| Real local process demonstration | Kestrel HTTPS with ephemeral certificate/key; unauthorized401, explicit provisioning/replay, manual provider-disabled completion, Posted timestamp, exact1234/1200/34 entries and Matched reconciliation; temporary resources removed |
| Reproducible docs | README, architecture, operations, focused automated acceptance, interactive local demo and optional external TEST-mode instructions |
| Full Release verification | Restore/build/full test:454 passed,0 failed,0 skipped; build0 warnings/errors |
| Exact implementation GitHub CI | All six inspected/implementation slice runs succeeded; see exact-head links below |

## Separate slices and exact commits

| Slice | Exact commit | Local full test total | GitHub CI |
| --- | --- | ---: | --- |
| Baseline | `23b2fdcce39f74afcd799a591a4de5bc3eda36cc` |436| [38055377306](https://github.com/Suthankan1/SettleCore/actions/runs/38055377306), success |
| Account provisioning | `70715939cd9b2be6fbcd7acc9d765e16a62b9c38` |437| [38060173768](https://github.com/Suthankan1/SettleCore/actions/runs/38060173768), success |
| Receipt worker metrics | `16c6b2f8aa6a8ab0855b32ff21663044a19628f2` |439| [38060340876](https://github.com/Suthankan1/SettleCore/actions/runs/38060340876), success |
| Verified orphan recovery | `2cb09b80f6eea76c7d45d904b86ad950c70e9422` |440| [38060538501](https://github.com/Suthankan1/SettleCore/actions/runs/38060538501), success |
| Local operator protection | `39f28516397f532f6da67d0d06d31a44dc60d660` |454| [38060727901](https://github.com/Suthankan1/SettleCore/actions/runs/38060727901), success |
| Complete authenticated acceptance | `89ea789a8abd4bf9d4ea00517f1cdf150c472b50` |454| [38060908293](https://github.com/Suthankan1/SettleCore/actions/runs/38060908293), success |

Full verification breakdown: Payments unit146, Payments Infrastructure109, API integration130, Ledger unit34/Infrastructure11, Reconciliation unit20/Infrastructure4. The closing documentation commit changes no application or test behavior; its own exact-head CI must be green and `HEAD` must equal fresh `origin/main` with a clean tree before final handback. Its hash/run are reported in the final handback; a document cannot embed its own Git commit hash without changing that hash.

## What is and is not externally verified

Verified fake-transport cases use Stripe.net53.0.0, real SDK request serialization/status normalization, signed envelopes, PostgreSQL persistence and hosted workers. They require no Stripe account or live API credential. The CLI-backed local interactive flow uses real HTTPS/Kestrel and PostgreSQL but deliberately disables Stripe and uses the explicit manual demo success route.

No external Stripe test/sandbox API call, account-specific webhook forwarding or real confirmation has been performed during this completion. Follow [the optional test-mode runbook](LOCAL_DEMO.md#optional-external-stripe-test-mode-acceptance--not-yet-verified) to add that evidence later. These credentials are not a blocking requirement of the agreed fully local scope.

Reconciliation comparisons use caller-supplied observations; they do not import Stripe settlements. Original-intent recovery requires a known, verified candidate reference; it never treats a missing/ambiguous search result as permission to recreate. Local operator authentication intentionally serves one trusted operator, without customer tenancy. These are explicit scope boundaries, not hidden production-readiness claims.
