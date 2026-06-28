# CrudDatastore

CrudDatastore is a lightweight ORM-style data access library built around CRUD operations, specifications, and unit-of-work patterns.

## Packages

The v2 public packaging scaffold currently produces target-specific artifacts for:

- `netstandard2.0`
- `net481`
- `net8.0`
- `net10.0`

## Core concepts

- `DataQuery` for read/query operations
- `DataStore` for CRUD operations
- `Specification<T>` for query composition
- `DataContextBase` and `UnitOfWorkBase` for richer data-context patterns

## Repository split

The public `CrudDatastore` repository is intended to contain only the ORM library and release assets.

Internal tests and the temporary Blazor harness are planned for separate private repositories:

- `CrudDatastore.Private`
- `CrudDatastore.BlazorHarness`

## Release

The public repo is designed to publish NuGet packages from GitHub Actions on version tags.

## Status

Current v2 public solution is `CrudDatastore.sln`.