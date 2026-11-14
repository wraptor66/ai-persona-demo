# Northline Coding Standards

Applies to all C# in `src/`. Reviewers and the batch-remediate skill check every change against these.

## STD-1 — No magic numbers
Numeric literals with business meaning (thresholds, limits, percentages, retry counts) live in a named
`const`, a static readonly field, or configuration. `0`, `1`, and `-1` used as plain arithmetic are exempt.

## STD-2 — Async all the way down
Any method that performs I/O (EF Core, HTTP, file) is `async`, returns `Task`/`Task<T>`, and is awaited
by its caller. No `.Result`, `.Wait()`, or `GetAwaiter().GetResult()`. Pass `CancellationToken` through.

## STD-3 — Controller actions return IActionResult
Every public controller action returns `Task<IActionResult>` (or `IActionResult` when synchronous), using
`Ok`, `NotFound`, `BadRequest`, `Conflict`, `CreatedAtAction`, or `NoContent`.

## STD-4 — No business logic in controllers
Controllers handle HTTP concerns only: binding, calling a service, mapping the outcome to a status code.
Validation rules, pricing, stock rules and calculations belong in a service class registered with DI.

## STD-5 — Every public method has XML docs
Every `public` method on a `public` type has a `/// <summary>` comment that states what it does.
