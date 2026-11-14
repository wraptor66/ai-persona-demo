# Northline Retail — Backlog

Project key: **NL** · Board: Northline Retail · Sprint: Demo 1

---

## NL-1 · Add phone number to Customer with validation

| Field | Value |
|---|---|
| Type | Story |
| Priority | Medium |
| Story points | 3 |
| Component | API / Customers |
| Labels | customers, validation |

**Description**
As a customer service rep, I want each customer record to carry a phone number so that I can call a
customer about an order problem without leaving the system. The number is optional, but when present it
must be valid and stored in a single consistent format.

**Acceptance Criteria**
- AC-1: `Customer` has a nullable `PhoneNumber` property, max length 20, persisted to the database.
- AC-2: `POST /api/customers` and `PUT /api/customers/{id}` accept `phoneNumber`.
- AC-3: A phone number is valid when it is E.164 format (`+` followed by 8–15 digits). Spaces, dashes,
  dots and parentheses in the input are stripped before validation.
- AC-4: An invalid phone number returns `400 Bad Request` with a message naming the field.
- AC-5: A valid phone number is stored normalized (e.g. `(602) 555-0142` with default `+1` → `+16025550142`).
- AC-6: Omitting `phoneNumber` still succeeds; existing customers are unaffected.

**Definition of Done**
- Code meets `references/standards.md`.
- Unit tests cover valid, invalid, normalized and omitted cases.
- `dotnet build` and `dotnet test` pass in CI.
- Seed data includes a phone number on at least two customers.
- PR reviewed and merged to `main`.

---

## NL-2 · Add order status and filter orders by status

| Field | Value |
|---|---|
| Type | Story |
| Priority | High |
| Story points | 5 |
| Component | API / Orders |
| Labels | orders, filtering |

**Description**
As an operations lead, I want every order to carry a fulfilment status and to filter the order list by
that status, so that the warehouse can see what still needs to ship.

**Acceptance Criteria**
- AC-1: `Order` has a `Status` field backed by an enum with exactly three values: `Pending`, `Shipped`,
  `Cancelled`. Stored as a string in the database.
- AC-2: New orders default to `Pending`.
- AC-3: `GET /api/orders?status=Shipped` returns only orders with that status; matching is case-insensitive.
- AC-4: `GET /api/orders` with no `status` returns all orders (current behaviour preserved).
- AC-5: An unknown status value (e.g. `?status=Lost`) returns `400 Bad Request` listing the allowed values.
- AC-6: Seed data includes at least one order in each status.

**Definition of Done**
- Code meets `references/standards.md`.
- Unit tests cover each status filter, no filter, and the invalid value.
- `dotnet build` and `dotnet test` pass in CI.
- Frontend Orders tab shows the status column.
- PR reviewed and merged to `main`.

---

## NL-3 · Low-stock alert endpoint

| Field | Value |
|---|---|
| Type | Story |
| Priority | High |
| Story points | 3 |
| Component | API / Products |
| Labels | inventory, alerts |

**Description**
As an inventory planner, I want an endpoint that lists products running low on stock so that I can
reorder before we sell out. The threshold should default to a sensible value but be overridable per request.

**Acceptance Criteria**
- AC-1: `GET /api/products/low-stock` returns active products whose `StockQuantity` is strictly below the threshold.
- AC-2: The default threshold is 10, read from configuration key `Inventory:LowStockThreshold` (no magic number in code).
- AC-3: An optional query parameter `?threshold=n` overrides the default for that request.
- AC-4: A threshold less than 1 returns `400 Bad Request`.
- AC-5: Results are ordered by `StockQuantity` ascending, then `Name`.
- AC-6: Inactive products are excluded even if below the threshold.
- AC-7: Each item includes `id`, `sku`, `name`, `stockQuantity`, and `threshold`.

**Definition of Done**
- Code meets `references/standards.md`.
- Unit tests cover default threshold, override, invalid threshold, and inactive exclusion.
- `dotnet build` and `dotnet test` pass in CI.
- Endpoint visible in Swagger.
- PR reviewed and merged to `main`.

---

## NL-4 · Make checkout better

| Field | Value |
|---|---|
| Type | Story |
| Priority | Medium |
| Story points | — |
| Component | — |
| Labels | checkout |

**Description**
Checkout needs to be better for customers. Some people said it's confusing. Let's improve it.

**Acceptance Criteria**
_None provided._

**Definition of Done**
- PR reviewed and merged to `main`.

---

## NL-5 · Move Products business logic into a service class

| Field | Value |
|---|---|
| Type | Story (Tech debt) |
| Priority | Medium |
| Story points | 5 |
| Component | API / Products |
| Labels | refactor, standards |

**Description**
As a developer, I want `ProductsController` to contain only HTTP concerns so that pricing and stock rules
can be tested and reused without a controller. Today the controller validates price and stock, normalizes
SKUs, calculates discounts (with a hard-coded 50% cap) and adjusts stock inline, which violates STD-1 and
STD-4 in `references/standards.md`.

**Acceptance Criteria**
- AC-1: A new `IProductService` / `ProductService` in `src/Northline.Api/Services/` owns create validation,
  SKU normalization, discount calculation and stock adjustment.
- AC-2: The service is registered in DI and injected into `ProductsController`.
- AC-3: `ProductsController` contains no validation, calculation or EF Core queries; each action calls the
  service and maps the result to an `IActionResult`.
- AC-4: The maximum discount percentage is a named constant or configuration value, not a literal.
- AC-5: Every existing endpoint keeps the same route, request shape, response shape and status codes.
- AC-6: All existing tests pass unchanged.

**Definition of Done**
- Code meets `references/standards.md`.
- New unit tests target `ProductService` directly for each rule.
- `dotnet build` and `dotnet test` pass in CI.
- No behaviour change visible through the API.
- PR reviewed and merged to `main`.
