# REVIEW — KAN-4: Add low-stock alert endpoint for Products

**Branch:** `feature/KAN-4-low-stock-endpoint` · **PR:** draft, see PR description · **Slice:** PR-1 of 1 · **Pass:** BATCH-20261115
**Flag:** none (`FEATURE_FLAGS = off`)
**Engine:** batch-remediate 4.0.0 · engagement `demo` · batch `demo1` · host Claude Code · lean pass (`MODES = off`, `GATES = none`)

## 0. Story review (Phase -1, `ENRICH_MODE = rfi`)

- **Readiness:** 14/20 as read. Outcome, AC testability and consistency are good. Gaps: R8 token issuer, R7 inactive products and bad thresholds, R6 out of scope, R5 side effects not stated (the endpoint is read-only).
- **Written to the story:** nothing (`FIELD_WRITEBACK = off`). The review-request comment **10000** @mentions the owner (reporter; the story has no assignee).
- **Prompts posted with proposed answers:**
  - R8 valid token for AC-4 → proposed **A**: JWT bearer from the configured issuer; 401 without a token or with a bad one. **Built to A.**
  - R7 inactive products → proposed **A**: excluded (consistent with `ProductsController.cs:25`). **Built to A.**
  - R7 bad threshold → proposed **A**: not a whole number → 400 (framework binding); zero or negative → 200 `[]`. **Built to A**, no extra code.
- **Sources:** `BACKLOG.md` NL-3 is a different draft of this story. It was not used; the Jira story is the specification of record.
- **Certification:** off for this engagement (`CERTIFICATION = off`).
- **Open warnings carried into implementation:** R5 and R6 were not stated. The implementation changes no data, and every other endpoint behaves as before (proven by `AC4_OtherProductEndpoints_StayAnonymous` and the existing suite).

## 0.5 Design agreement (Phase 0.25)

All three decisions were taken by **default (GATES=none)**. Each is recorded on the ticket with its alternatives and can be reversed with a reply.

- **D-KAN-4-1 (comment 10001)**: **A**, a new `LowStockService` injected with `[FromServices]` into a new `ProductsController` action. B (inline LINQ, which breaks STD-4) and C (a separate `InventoryController`) were rejected. Deciding rule: design-gate rule 2 (additive) plus STD-4.
- **D-KAN-4-2 (comment 10002)**: **A**, the JwtBearer scheme with `[Authorize]` on the low-stock action only. B (authorize the whole controller) would break existing endpoints; C (an API key) means a hand-written security handler.
- **D-KAN-4-3 (comment 10003)**: **A**, a `LowStockProduct(Id, Name, Quantity, UnitPrice)` DTO. B (return the entity) was rejected.
- **Capabilities:** `products.low-stock` (new) · `api.authentication` (new; opt-in per endpoint).
- **PR series:** one slice.

## 1. Specification source

- **Type:** Story (Jira `KAN-4`, live REST v3 intake, `source = jira`)
- **Spec of record:** the description's `Acceptance Criteria` section (`AC_SOURCE = description-heading`). The story has no DoD.
- **Engagement rule RULE-1:** unit tests use the EF Core InMemory provider and must not need the SQL Server container.

## 2. Acceptance criteria

| Req | Statement | Status | Proven by |
|----|-----------|--------|-----------|
| AC-1 | GET `/api/products/low-stock` takes an optional `threshold`, default 10 | Satisfied | `AC1_NoThresholdParameter_UsesDefaultOf10`, `AC1_ThresholdParameter_IsApplied` |
| AC-2 | JSON array of products with Quantity < threshold, each with id, name, quantity, unit price | Satisfied | `AC2_EachItem_HasExactlyIdNameQuantityUnitPrice`, `AC5_AC2_TwoOfFiveBelowThreshold_ReturnsThoseTwoWithIdNameQuantityUnitPrice` |
| AC-3 | None below → 200 with `[]` | Satisfied | `AC3_NoneBelowThreshold_Returns200WithEmptyArray`, `AC5_AC3_NoneBelowThreshold_ReturnsEmptyList` |
| AC-4 | Requires authentication; 401 without a valid token | Satisfied in code; issuer config open (RFI-KAN-4-1) | `AC4_NoToken_Returns401`, `AC4_TokenSignedWithWrongKey_Returns401`, `AC4_OtherProductEndpoints_StayAnonymous` |
| AC-5 | MSTest: 2 of 5 seeded below threshold; none below | Satisfied | `AC5_AC2_…`, `AC5_AC3_…` (five-product InMemory DB) |
| RULE-1 | Tests use EF Core InMemory, no SQL Server | Satisfied | Every new test uses `UseInMemoryDatabase`; the HTTP tests replace the SQL Server registration |
| R7a (proposed) | Inactive products excluded | Built, pending the owner's answer | `R7a_InactiveProductBelowThreshold_IsNotReturned` |
| R7b (proposed) | Non-numeric threshold → 400 | Built, pending the owner's answer | `R7b_NonNumericThreshold_Returns400` |

## 3. Diff summary

| File | Change | Satisfies |
|------|--------|-----------|
| `src/Northline.Api/Services/LowStockService.cs:7-53` (new) | `ILowStockService` + `LowStockService`: active products with `StockQuantity < threshold`, ordered by id, projected to the DTO; `DefaultThreshold = 10` const (STD-1); one log line | AC-1, AC-2, AC-3, R7a |
| `src/Northline.Api/Controllers/ProductsController.cs:36-48` | New `[Authorize] [HttpGet("low-stock")] GetLowStock` action; service injected with `[FromServices]` so the constructor and existing tests are unchanged | AC-1, AC-4 |
| `src/Northline.Api/Models/Dtos.cs:13-14` | `record LowStockProduct(Id, Name, Quantity, UnitPrice)` | AC-2 |
| `src/Northline.Api/Program.cs:13-18, 32-33` | DI registration; `AddAuthentication(JwtBearer).AddJwtBearer()`, `AddAuthorization()`; `UseAuthentication()` / `UseAuthorization()` | AC-4 |
| `src/Northline.Api/Northline.Api.csproj:13` | `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.11 | AC-4 |
| `tests/Northline.Api.Tests/Northline.Api.Tests.csproj:14` | `Microsoft.AspNetCore.Mvc.Testing` 8.0.11 (test only) | AC-1, AC-4 tests |
| `tests/Northline.Api.Tests/LowStockServiceTests.cs` (new) | 3 service tests on a five-product InMemory DB | AC-2, AC-3, AC-5, R7a |
| `tests/Northline.Api.Tests/LowStockEndpointTests.cs` (new) | 8 in-process HTTP tests (WebApplicationFactory, InMemory DB, locally signed tokens) | AC-1–AC-4, R7b |

Considered and **not** changed: no issuer or audience values in `appsettings*.json` (RFI-KAN-4-1; no secrets or tenant values guessed); existing products endpoints stay anonymous; no configuration key for the default threshold (AC-1 says "default of 10"; NL-3's config key is not in this story).

## 4. Tests added

| Test | Covers | Notes |
|------|--------|-------|
| `AC5_AC2_TwoOfFiveBelowThreshold_ReturnsThoseTwoWithIdNameQuantityUnitPrice` | AC-5, AC-2 | Quantities 3, 9, 10, 25, 140 at threshold 10 → ids 101, 102 only (10 is the boundary and is excluded) |
| `AC5_AC3_NoneBelowThreshold_ReturnsEmptyList` | AC-5, AC-3 | Same five at threshold 3 → empty |
| `R7a_InactiveProductBelowThreshold_IsNotReturned` | proposed R7a | Inactive product with quantity 1 is excluded |
| `AC1_NoThresholdParameter_UsesDefaultOf10` | AC-1 | Through HTTP binding: seed ids 2, 4, 6, 7 |
| `AC1_ThresholdParameter_IsApplied` | AC-1 | `?threshold=5` → 4, 6 |
| `AC2_EachItem_HasExactlyIdNameQuantityUnitPrice` | AC-2 | JSON property names are exactly `id, name, quantity, unitPrice` |
| `AC3_NoneBelowThreshold_Returns200WithEmptyArray` | AC-3 | `?threshold=0` → 200 `[]` |
| `AC4_NoToken_Returns401` | AC-4 | |
| `AC4_TokenSignedWithWrongKey_Returns401` | AC-4 | Token signed with a different key |
| `AC4_OtherProductEndpoints_StayAnonymous` | AC-4 regression, D-KAN-4-2 | `GET /api/products` without a token → 200 |
| `R7b_NonNumericThreshold_Returns400` | proposed R7b | `?threshold=abc` |

All 11 tests are in `[TestCategory("TICKET-KAN-4")]`. Run them with `dotnet test --filter "TestCategory=TICKET-KAN-4"`.
**Measured on this branch (head `c8eddba`):** KAN-4 tests 11/11 passed · full `dotnet test` 17/17 passed (6 existing + 11 new) · `dotnet build` 0 warnings, 0 errors.
One test was corrected during the pass: `AC2_EachItem_…` first used `threshold=4`, which also matches seed product 6 (quantity 0). The fix changed the test's input to `threshold=1`. The code and the AC-2 expectations did not change.

The HTTP tests sign tokens with a random key generated for each test run and validate them against that key, using `PostConfigure<JwtBearerOptions>`. Production validation settings are untouched. These tests do not prove that a token from the real identity provider is accepted; that needs RFI-KAN-4-1.

## 4.5 Consumer impact (Phase 1.5)

**Shared symbols modified:** none. `ProductsController` gains a member, and no existing member changes. `blast-radius.sh ProductsController` found 0 call sites and no loop-nested calls.
**Pipeline change:** `UseAuthentication()` now runs on every request. For anonymous endpoints the JwtBearer handler only reads the `Authorization` header and never challenges, so a request to an existing endpoint behaves as before (proven by `AC4_OtherProductEndpoints_StayAnonymous` and the 6 existing tests). **No contract change.**

## 5. Logging

- **Correlation ID:** `TICKET-KAN-4` · **EventIds reserved:** 4000–4009 (4000 `LowStockQueried` used)
- **Log points added:** the outcome of each query: threshold and returned count (`LowStockService.cs:47`, Information)
- **Sink, traced in code:** `LowStockService.GetLowStockAsync` → `ILogger<LowStockService>` (`LowStockService.cs:47`) → default providers from `WebApplication.CreateBuilder` (`Program.cs:6`; no custom provider registered) → Console provider → process stdout → the `api` container's log (`docker-compose.yml`, service `api`). Level: `Logging:LogLevel:Default = Information` (`appsettings.json`, `appsettings.Development.json`), so the row is emitted. **Not in:** any persistent store or Application Insights (none configured).
- **Verification query** (not yet run: the skill never runs docker compose):
  ```
  docker compose logs api --since {from} | grep "TICKET-KAN-4"
  ```
- **How to exercise:** `GET http://localhost:8080/api/products/low-stock?threshold=10` with a bearer token. A request without a token returns 401 before the service runs, so it logs nothing.
- **Not logged:** 401 rejections (the framework handles them before the action runs; no ticket row); who called (deliberately, so no identity is logged).

## 6. Security review (OWASP)

| Item | Outcome | Note |
|------|---------|------|
| Broken access control | Addressed | `[Authorize]` enforced server-side by the JwtBearer handler; fails closed (no issuer configured → every token rejected → 401) |
| Injection | Addressed | EF Core LINQ, parameterized; `threshold` is bound as `int` |
| XSS (front end) | N/A | No front-end change |
| Sensitive data exposure | Addressed | Response has only id, name, quantity and price; the log has threshold and count only; no tokens logged |
| Security misconfiguration | Addressed | No existing control weakened; the tests use a test-only key through `PostConfigure` and do not relax production validation |
| CSRF / state-changing endpoints | N/A | Read-only GET |
| SSRF / deserialization | N/A | No outbound calls. In production, JwtBearer fetches OIDC metadata from the configured authority, which is operator-set, not user-controlled |
| Dependencies | Addressed | `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.11 (first-party, matches the EF 8.0.11 line; needed for AC-4) · `Microsoft.AspNetCore.Mvc.Testing` 8.0.11 (test project only; the in-process host that proves the 401) |

No risk raised.

## 7. Residual risk

- **Until RFI-KAN-4-1 is answered, the endpoint returns 401 to every caller in every environment.** No authority or audience is configured, so no real token validates. This fails closed by design. Configure `Authentication:Schemes:Bearer:Authority` and `ValidAudiences` per environment (or use `dotnet user-jwts create` locally) to use it.
- The React frontend does not call this endpoint and sends no token. Any future client must send one.
- Proposed answers R7a and R7b are built, but the owner has not confirmed them.

## 8. Follow-ups

- **RFI-KAN-4-1** (owner: story reporter): which identity provider issues tokens for this API, and the authority and audience for each environment.
- **RF-KAN-4-1** in `batches/REFACTOR-OPPORTUNITIES.md`: fold `LowStockService` into the `ProductService` NL-5 introduces.
