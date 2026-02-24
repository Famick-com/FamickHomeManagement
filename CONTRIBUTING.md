# Contributing to Famick Home Management

Thank you for your interest in contributing to Famick Home Management! This document provides guidelines and information for contributors.

## Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for everyone.

## How to Contribute

### Reporting Issues

- Search existing issues before creating a new one
- Use the issue templates when available
- Provide clear reproduction steps for bugs
- Include relevant environment details (OS, .NET version, browser)

### Submitting Changes

1. Fork the repository
2. Create a feature branch from `main`
3. Make your changes following the coding standards below
4. Write or update tests for your changes
5. Ensure all tests pass
6. Submit a pull request

### Pull Request Process

1. **Reference the GitHub issue** - PR title or description must include the issue number (e.g., `#123`)
2. Update documentation if needed
3. Add a clear description of changes
4. Wait for code review and address feedback
5. Maintainers will merge approved PRs

PRs without a linked issue may be closed. Create an issue first if one doesn't exist.

## Coding Standards

### C# Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Namespaces | PascalCase | `Famick.HomeManagement.Core` |
| Classes | PascalCase | `StockService` |
| Interfaces | PascalCase with I prefix | `IStockService` |
| Methods | PascalCase | `GetStockItems()` |
| Public Properties | PascalCase | `ItemName` |
| Private Fields | camelCase with underscore prefix | `_itemRepository` |
| Local Variables | camelCase | `stockItem` |
| Parameters | camelCase | `itemId` |
| Constants | PascalCase | `MaxRetryCount` |
| Enums | PascalCase (singular) | `StockStatus` |
| Enum Values | PascalCase | `InStock`, `OutOfStock` |
| Async Methods | PascalCase with Async suffix | `GetStockItemsAsync()` |
| Generic Type Parameters | T prefix | `TEntity`, `TResult` |

### Code Style

```csharp
// Use explicit types for clarity in public APIs
public StockItem GetItem(Guid itemId)

// Use var when the type is obvious
var items = new List<StockItem>();

// Use expression-bodied members for simple methods
public string FullName => $"{FirstName} {LastName}";

// Use meaningful names over abbreviations
// Good: customerRepository, stockItem
// Bad: custRepo, si

// Use async/await consistently
public async Task<StockItem> GetItemAsync(Guid id, CancellationToken ct = default)
{
    return await _repository.GetByIdAsync(id, ct);
}
```

### File Organization

- One class per file (except for small related types)
- File name matches class name
- Organize using statements: System, Microsoft, third-party, project
- Use file-scoped namespaces

```csharp
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Famick.HomeManagement.Domain;

namespace Famick.HomeManagement.Core.Services;

public class StockService : IStockService
{
    // Implementation
}
```

### Project Structure

- Follow Clean Architecture principles
- Domain entities have no external dependencies
- Core services depend only on Domain
- Infrastructure implements interfaces from Core
- Web/UI depends on all layers

## Testing Requirements

### All Code Must Have Unit Tests

Every pull request must include appropriate test coverage:

- **New features**: Write tests covering the main functionality and edge cases
- **Bug fixes**: Write a test that reproduces the bug before fixing it
- **Refactoring**: Ensure existing tests still pass

### Test Naming Convention

```csharp
[Fact]
public void MethodName_StateUnderTest_ExpectedBehavior()
{
    // Example:
    // GetStockItem_WithValidId_ReturnsItem
    // CreateStock_WithNullName_ThrowsValidationException
}
```

### Test Structure (Arrange-Act-Assert)

```csharp
[Fact]
public async Task GetStockItemAsync_WithValidId_ReturnsItem()
{
    // Arrange
    var itemId = Guid.NewGuid();
    var expectedItem = new StockItem { Id = itemId, Name = "Test Item" };
    _mockRepository.Setup(r => r.GetByIdAsync(itemId, default))
        .ReturnsAsync(expectedItem);

    // Act
    var result = await _service.GetStockItemAsync(itemId);

    // Assert
    result.Should().NotBeNull();
    result.Id.Should().Be(itemId);
    result.Name.Should().Be("Test Item");
}
```

### Test Libraries

- **xUnit** - Test framework
- **FluentAssertions** - Assertion library
- **Moq** - Mocking framework

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/Famick.HomeManagement.Tests.Unit

# Run tests matching a filter
dotnet test --filter "FullyQualifiedName~StockService"
```

## Development Setup

1. Install .NET 10.0 SDK
2. Install PostgreSQL 16+ (or use Docker)
3. Clone the repository:
   ```bash
   git clone https://github.com/Famick-com/FamickHomeManagement.git
   cd FamickHomeManagement
   ```
4. Start the development database:
   ```bash
   docker compose -f docker/docker-compose.dev.yml up -d
   ```
5. Build and run:
   ```bash
   dotnet build Famick.sln
   dotnet run --project src/Famick.HomeManagement.Web
   ```

## Commit Messages

All commits must reference a GitHub issue number. Use clear, descriptive commit messages:

```text
feat(#123): add barcode scanning to stock entry
fix(#456): resolve null reference in recipe calculation
docs(#789): update API documentation for stock endpoints
test(#101): add unit tests for ShoppingListService
refactor(#102): extract validation logic to separate class
```

Format: `type(#issue): description`

Types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `style`, `perf`

## Questions?

- Open a GitHub Discussion for general questions
- Create an issue for bugs or feature requests

Thank you for contributing!
