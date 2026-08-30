# C# Coding Standard

Reference standard for all backend code in this project (ASP.NET Core microservices, .NET 10). Feed this file to your AI IDE as context before generating or reviewing any service code — it's written rule-first so it works as a system prompt, not just documentation.

## 1. Scope & Philosophy
- Every service is a standalone ASP.NET Core Web API. These rules apply uniformly across all of them.
- Prefer clarity over cleverness. Prefer explicit over implicit.
- A rule with no exception listed has no exception — flag it in code review instead of silently deviating.

## 2. Project Structure (per service)
```
{service-name}/
├── {Service}.Api/            # Controllers/endpoints, DI wiring, Program.cs
├── {Service}.Application/    # Use cases, DTOs, validators, interfaces
├── {Service}.Domain/         # Entities, value objects, domain logic — no framework references
├── {Service}.Infrastructure/ # EF Core / Dapper, external clients, repository implementations
└── {Service}.Tests/
```
- One class per file. File name matches the class name exactly.
- `Domain` never references `Infrastructure`. Dependencies point inward.

## 3. Naming Conventions
| Element | Convention | Example |
|---|---|---|
| Class, Record, Interface (public API) | PascalCase | `SubscriptionPlan`, `IPaymentGateway` |
| Interface prefix | `I` + PascalCase | `ICatalogRepository` |
| Method | PascalCase, verb-led | `GetActiveSubscription()` |
| Async method | PascalCase + `Async` suffix | `GetActiveSubscriptionAsync()` |
| Private field | `_camelCase` | `_dbContext` |
| Local variable, parameter | camelCase | `subscriptionId` |
| Constant | PascalCase | `MaxRetryCount` |
| Boolean | Question form | `IsActive`, `HasExpired` |

- No abbreviations unless universally understood (`Id`, `Http`, `Url` are fine; `Sub`, `Cfg` are not).
- No Hungarian notation.

## 4. Formatting
- File-scoped namespaces (`namespace Catalog.Api;`), not block-scoped.
- Braces on every `if`/`for`/`while`, even single-line bodies.
- One statement per line. No inline multi-statement one-liners beyond simple expression bodies.
- `var` when the type is obvious from the right-hand side; explicit type otherwise.
- Usings sorted, `System.*` first, no unused usings.

## 5. SOLID
- **Single Responsibility** — a class has one reason to change. If a class both validates and persists, split it.
- **Open/Closed** — extend via a new implementation of an interface, not by adding `if` branches to existing classes for new cases.
- **Liskov Substitution** — a derived/implementing type must be usable anywhere the base/interface is expected, with no surprising behavior changes.
- **Interface Segregation** — many small interfaces over one large one. `IReadRepository<T>` / `IWriteRepository<T>` over one bloated `IRepository<T>`.
- **Dependency Inversion** — depend on interfaces defined in `Application`, implemented in `Infrastructure`. Nothing in `Application` or `Domain` references `Infrastructure` directly.

## 6. Design Patterns
- **Repository** — one repository interface per aggregate root, implemented in `Infrastructure`. Repositories return domain types, never leak `IQueryable`/EF Core out of the layer.
- **Options pattern** — bind configuration sections to strongly-typed classes (`IOptions<T>`); never read `IConfiguration["Key"]` directly outside `Program.cs`.
- **Mediator (CQRS-style)** — for services with meaningfully different read/write shapes (Catalog is the clear case here), route commands and queries through a mediator rather than calling application services directly from controllers.
- **Circuit breaker / retry (Polly)** — wrap outbound HTTP calls to other services, not raw `HttpClient` calls.
- Don't reach for a pattern because it's "proper" — a plain service class is correct when there's no real variation to abstract over.

## 7. Dependency Injection
- Constructor injection only. No service locator, no `IServiceProvider.GetService` calls inside business logic.
- Lifetimes: `DbContext` and repositories → **Scoped**. Stateless services/clients → **Singleton**. Anything holding per-request state → **Scoped**, never Singleton.
- Register services in a single `{Service}.Api/DependencyInjection.cs` extension method per project, not scattered across `Program.cs`.

## 8. Async/Await
- Async all the way — an async method's caller is async too, up to the controller/endpoint.
- Never `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on a Task. Hard rule, not a style preference — it causes deadlocks.
- Every async method that can be cancelled accepts a `CancellationToken` and passes it down.
- `ConfigureAwait(false)` in `Infrastructure`/`Application` library code.

## 9. Data Access
- Parameterized queries only. No string-concatenated SQL, ever — non-negotiable regardless of how "trusted" the input is.
- Writes go through EF Core; high-traffic reads go through Dapper with a hand-written query — match this to whichever pattern the service actually needs, don't default to one everywhere.
- One database per service. No service reaches into another service's database directly — go through its API or an event.
- Migrations are checked into source control and applied via a pipeline step, never run manually against a shared environment.

## 10. Error Handling
- Exceptions represent the unexpected, not control flow. Expected failure paths (validation errors, "not found") return a typed result/response, not a thrown exception.
- One global exception-handling middleware per service, mapping exceptions to a consistent problem-details response.
- Never `catch (Exception)` and swallow. Catch specific exception types; if you must catch broadly, log with full context and rethrow or return a typed failure.

## 11. API Design
- Controllers/endpoints are thin — map HTTP to a use case call and back. No business logic in a controller method.
- Request/response DTOs only at the boundary. Never return an EF Core entity directly from an endpoint.
- Every service exposes `GET /health` returning 200 when ready to serve traffic — this is what the Kubernetes readiness probe checks.

## 12. Security
- No secrets, connection strings, or keys in source. Configuration comes from environment variables or a secret store, injected at runtime.
- Every incoming request DTO is validated (FluentValidation or DataAnnotations) before it reaches application logic.
- Validate JWTs against issuer, audience, and expiry on every request through the gateway — don't trust a token just because it parses.

## 13. Logging
- Structured logging (Serilog), not string-interpolated messages — log values as parameters so they stay queryable.
- Every request carries a correlation ID, propagated across service calls, included in every log line for that request.
- Never log secrets, tokens, passwords, or full payment details, even at debug level.

## 14. Testing
- Test method names describe behavior: `MethodName_Scenario_ExpectedResult`.
- Arrange/Act/Assert, one logical assertion focus per test.
- Domain and Application layers get unit tests with mocked dependencies. Infrastructure gets integration tests against a real (containerized) database, not mocks.

## 15. Comments & Documentation
- XML doc comments on every public interface member — this is what surfaces in Swagger/IntelliSense.
- Comments explain *why*, not *what* — if a comment just restates the code, delete it.
- No commented-out code left in a commit. Delete it; source control remembers it.

## 16. Forbidden, no exceptions
- Dynamic SQL / string-concatenated queries
- Business logic inside a controller/endpoint method
- Static mutable state shared across requests
- `catch (Exception)` with no rethrow and no logging
- Magic strings/numbers where a named constant or enum belongs
- A class or method doing more than one job because splitting it "felt like overkill"
