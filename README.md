# Famick Home Management

Self-hosted household management application built with .NET 10, Blazor, and PostgreSQL. Source code available under the Elastic License 2.0.

## Quick Start

### Self-Hosted (Docker Compose)

```bash
git clone https://github.com/Famick-com/FamickHomeManagement.git
cd FamickHomeManagement/docker
./setup.sh
docker compose up -d
```

Visit `http://localhost:5000` to get started. See [docker/README.md](docker/README.md) for full configuration options.

### Development

```bash
git clone https://github.com/Famick-com/FamickHomeManagement.git
cd FamickHomeManagement

# Start dev database
docker compose -f docker/docker-compose.dev.yml up -d

# Run the app
dotnet run --project src/Famick.HomeManagement.Web
```

## Project Structure

```
FamickHomeManagement/
├── src/
│   ├── Famick.HomeManagement.Domain/           # Entities, enums
│   ├── Famick.HomeManagement.Core/             # Interfaces, DTOs, validators
│   ├── Famick.HomeManagement.Infrastructure/   # EF Core, service implementations
│   ├── Famick.HomeManagement.Web.Shared/       # Shared API controllers
│   ├── Famick.HomeManagement.UI/               # Blazor components (Razor Class Library)
│   ├── Famick.HomeManagement.Shared/           # Shared utilities
│   ├── Famick.HomeManagement.Web/              # Self-hosted web application
│   ├── Famick.HomeManagement.Web.Client/       # Blazor WebAssembly client
│   └── Famick.HomeManagement.Mobile/           # .NET MAUI native mobile app
│   └── Famick.HomeManagement.Messaging/    # Unified messaging (email, SMS, push, in-app)
├── tests/                                       # Unit and integration tests
├── docker/                                      # Docker development files
├── scripts/                                     # Setup and maintenance scripts
├── docs/                                        # Architecture documentation
├── docker-compose.yml                           # Self-hosted quick start
├── Dockerfile                                   # Production Docker image
└── Famick.sln                                   # Solution file
```

## Features

- Inventory & stock management with barcode scanning
- Recipe management
- Shopping lists with store integrations (Kroger, USDA, OpenFoodFacts)
- Equipment & vehicle tracking with maintenance schedules
- Contact management
- Chores & todo lists
- Multi-user support with roles and permissions
- Plugin system for extensibility
- Mobile app (iOS & Android)

## Technology Stack

- **.NET 10** / ASP.NET Core / Blazor
- **PostgreSQL** with EF Core
- **.NET MAUI** native mobile app
- **MudBlazor** component library

## Cloud Version

A hosted cloud version is available at [app.famick.com](https://app.famick.com) with multi-tenant support, managed infrastructure, and additional features (Google/Apple Sign-In, cloud backups).

The cloud-specific code lives in a private submodule (`homemanagement-cloud/`) and is not included in the public distribution.

## Email Templates

All email and notification content is rendered from Mustache templates in the `Famick.HomeManagement.Messaging` library. Templates live in `src/Famick.HomeManagement.Messaging/Templates/` and are embedded in the assembly at build time.

### Preview Templates Locally

Generate all 9 email templates as HTML files with sample data:

```bash
dotnet run --project scripts/PreviewEmails
```

Or preview a single message type:

```bash
dotnet run --project scripts/PreviewEmails Expiry
```

HTML files are written to `email-previews/` and the index page opens automatically in your browser. Available types: `EmailVerification`, `PasswordReset`, `PasswordChanged`, `Welcome`, `Expiry`, `LowStock`, `TaskSummary`, `CalendarReminder`, `NewFeatures`.

### Test with Local SMTP

To test actual email delivery locally, run smtp4dev and configure the app to use it:

```bash
docker run --rm -p 3000:80 -p 2525:25 rnwood/smtp4dev
```

Then in `appsettings.Development.json`:

```json
{
  "Email": {
    "Provider": "Smtp",
    "FromEmail": "notifications@localhost",
    "FromName": "Famick",
    "Smtp": {
      "Host": "localhost",
      "Port": 2525,
      "EnableSsl": false
    }
  }
}
```

Open `http://localhost:3000` to view captured emails with full HTML rendering, headers, and plain text alternatives.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines, coding standards, and how to submit changes.

## License

This project is licensed under the [Elastic License 2.0 (ELv2)](LICENSE). You may use, modify, and self-host the software freely. You may **not** provide it to third parties as a hosted or managed service.

The `homemanagement-cloud/` subdirectory (if present) contains proprietary code not covered by the Elastic License.

## Support

- **Bug Reports**: [Open an issue](https://github.com/Famick-com/FamickHomeManagement/issues)
- **Security Issues**: security@famick.com (private disclosure)
- **Documentation**: See [docs/](docs/) and [CLAUDE.md](CLAUDE.md)
