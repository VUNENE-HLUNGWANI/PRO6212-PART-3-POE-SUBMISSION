# PRO6212-PART-3-POE-SUBMISSION

RaceDay is a sporting event management web app for running, walking and cycling events. Organisers manage events, categories and results, and participants enrol and track finish times. It has an ASP.NET Core 8 Web API with SQL Server, an MVC front end, Azure Blob Storage for images, and Docker. Tests and CI are included.

**Video presentation:** [Watch the RaceDay Application Demonstration](https://youtu.be/CPYWL6qtDr0)

## Contents

- [Features](#features)
- [Technologies](#technologies)
- [Architecture](#architecture)
- [Project structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Running with Docker](#running-with-docker)
- [Running locally with Visual Studio](#running-locally-with-visual-studio)
- [API documentation](#api-documentation)
- [Running the tests](#running-the-tests)
- [Continuous integration](#continuous-integration)

## Features

**Organisers**
- Dashboard with total enrolments and the next event
- Create, edit and manage events, including banner upload
- Create and edit categories for each event
- View registrations and update enrolment status (for example, Pending to Confirmed)
- Enter results: finish time and position, or DNF, DNS or DSQ

**Participants**
- Browse upcoming events and view event details
- Enrol in an event and choose a category
- My enrolments, showing each event's status
- My results, showing finish times and positions
- Profile with picture upload

**Accounts**
- Register as a Participant or an Organiser (Organisers need the organiser code)
- Session-based login with role-based navigation
- Logout asks for confirmation
- Unauthenticated users are redirected to the login page

## Technologies

- ASP.NET Core 8 (MVC and Web API)
- Entity Framework Core with SQL Server
- Azure Blob Storage for event banners and profile pictures
- Bootstrap for the front-end layout and theme
- xUnit and Moq for API unit tests
- Docker and Docker Compose
- GitHub Actions for continuous integration

## Architecture

The solution has two projects that run as separate containers.

- **RaceDayAPI** is a RESTful Web API. It owns the data and business rules, connects to SQL Server through Entity Framework Core, and uploads images to Azure Blob Storage. The MVC app never talks to Azure or the database directly.
- **RaceDayMVC** is the front end. Controllers call the API through `ApiClient`, and Razor views render the pages with Bootstrap. The user's session is stored by the MVC app.

Image uploads follow one path: the browser posts the file to the MVC app, the MVC app forwards it to the API, and the API stores it in Azure Blob Storage and saves the returned URL in the database.

## Project structure

```text
RaceDayAPI.slnx            Solution file (opens both projects and the tests)
docker-compose.yml         Starts the API and MVC containers together
.env.example               Template for the secrets the containers need
RaceDayAPI/                Web API: owns the data, business rules and Blob Storage
  Controllers/             Endpoints for auth, events, categories, registrations, results and profile
  Data/                    EF Core database context
  Filters/                 SessionAuthorizeAttribute, which protects endpoints by role
  Migrations/              Database schema history
  Models/                  Entities (User, Event, Category, Result and others)
  Services/                BlobStorageService (uploads) and PasswordHasher
  Dockerfile
RaceDayMVC/                MVC front end: pages for organisers and participants
  Controllers/             Page logic. Calls the API through ApiClient, never the database
  Helpers/                 Status badges, enrolment status and JSON helpers
  Models/                  View models for forms and pages
  Services/                ApiClient, the only class that calls the API
  Views/                   Razor pages, grouped by feature, plus Shared/_Layout.cshtml
  wwwroot/                 Bootstrap theme (css/site.css), jQuery and site.js
  Dockerfile
RaceDayAPI.Tests/          xUnit and Moq unit tests for the API controllers and filter
scripts/                   Run-RaceDayTests.ps1, an end-to-end test of the running app
.github/workflows/         raceday-ci.yml, the CI pipeline
```

### Where to find each requirement

| Requirement | Location |
|---|---|
| Register and login, session and role | API `Controllers/AuthController.cs`, `Filters/SessionAuthorizeAttribute.cs`; MVC `Controllers/AuthController.cs`, `Views/Auth/` |
| Role-based navigation | MVC `Views/Shared/_Layout.cshtml` |
| Organiser pages | MVC `Controllers/OrganiserController.cs`, `CategoriesController.cs`, `ResultsController.cs`; views in `Views/Organiser/`, `Views/Categories/`, `Views/Results/` |
| Participant pages | MVC `Controllers/EventsController.cs`, `MyEnrolmentsController.cs`, `MyResultsController.cs`, `ProfileController.cs`; views in `Views/Events/`, `Views/MyEnrolments/`, `Views/MyResults/`, `Views/Profile/` |
| Blob Storage upload | MVC `Services/ApiClient.cs` sends the file; API `Controllers/EventsController.cs` (`UploadBanner`) and `Controllers/ProfileController.cs` (`UploadPicture`) call `Services/BlobStorageService.cs` |
| Docker | `RaceDayAPI/Dockerfile`, `RaceDayMVC/Dockerfile`, `docker-compose.yml` |
| UI design | MVC `wwwroot/css/site.css`, `Views/Shared/_Layout.cshtml` |
| Automated tests | `RaceDayAPI.Tests/`, `scripts/Run-RaceDayTests.ps1` |
| CI pipeline | `.github/workflows/raceday-ci.yml` |

## Prerequisites

- Docker Desktop, for the Docker steps
- .NET 8 SDK and Visual Studio, for running locally
- A SQL Server instance (local or Azure) and an Azure Storage account with a blob container named `event-banners`

## Running with Docker

1. Copy `.env.example` to `.env` in the repository root and fill in your values:
   - `RACEDAY_SQL_CONNECTION`: SQL Server connection string
   - `AZURE_BLOB_CONNECTION`: Azure Storage connection string
   - `ORGANISER_CODE`: the code required to register as an Organiser

2. Make sure the database schema exists. Run the migrations once against your SQL Server database, using the same connection string as in `.env` (for example with `dotnet ef database update --project RaceDayAPI`).

3. From the repository root, the folder containing `docker-compose.yml`, run:

```bash
docker compose up --build
```

4. Open the app:
   - MVC front end: http://localhost:8080
   - API: http://localhost:5049

To stop the containers, press `Ctrl+C`, then run `docker compose down`.

## Running locally with Visual Studio

1. Open `RaceDayAPI.slnx`.
2. Add your secrets to the `RaceDayAPI` project with **Manage User Secrets**, using the same keys as `.env`: `ConnectionStrings:DefaultConnection`, `AzureBlobStorage:ConnectionString` and `Registration:OrganiserCode`. Secrets are stored outside the repository.
3. Run the database migrations from the Package Manager Console with `Update-Database`, with `RaceDayAPI` as the default project.
4. Set `RaceDayMVC` as the startup project and run it. Keep `RaceDayAPI` running alongside it, because the MVC app calls the API at `http://localhost:5049`.

## API documentation

The API exposes Swagger UI when it is running. With Docker, open http://localhost:5049/swagger. Running from Visual Studio, use the same path on the port shown in the launch profile.

## Running the tests

Unit tests for the API:

```bash
dotnet test RaceDayAPI.Tests/RaceDayAPI.Tests.csproj
```

End-to-end tests for the running app (start the containers first):

```powershell
.\scripts\Run-RaceDayTests.ps1
```

The script reads `ORGANISER_CODE` from `.env`. It creates its own test accounts and events, so it can be run repeatedly.

## Continuous integration

The workflow in `.github/workflows/raceday-ci.yml` runs on every push and pull request to `main`.

1. **build-and-test** checks that the required files are present, restores and builds the solution (API, MVC and tests), then runs the API unit tests.
2. **docker-build** runs after the first job passes and builds the API and MVC Docker images with Docker Compose.

### CI result

The Actions tab shows the **RaceDay CI** workflow passing on `main`. Each run builds the solution and runs the API tests, then builds the Docker images. Runs #2 and #3 are green.

<img width="1887" height="867" alt="image" src="https://github.com/user-attachments/assets/6a614086-96a7-4ec4-b5db-a0003ec5710f" />
