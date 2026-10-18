<div align="center">

# ⚔️ ARENA Esports — Microservice Testing Pipeline
### SE3112: Advanced Software Engineering
**BSc. (Hons) in Computer Science — Year 3, Semester 1**

[![Automated Testing Pipeline](https://github.com/ARENA-esports/arena-streaming-platform/actions/workflows/testing-pipeline.yml/badge.svg)](https://github.com/ARENA-esports/arena-streaming-platform/actions/workflows/testing-pipeline.yml)
[![CI Pipeline](https://github.com/ARENA-esports/arena-streaming-platform/actions/workflows/ci.yml/badge.svg)](https://github.com/ARENA-esports/arena-streaming-platform/actions/workflows/ci.yml)
[![CI Frontend](https://github.com/ARENA-esports/arena-streaming-platform/actions/workflows/ci-frontend.yml/badge.svg)](https://github.com/ARENA-esports/arena-streaming-platform/actions/workflows/ci-frontend.yml)
[![SonarCloud Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=arena-esports_arena-backend&metric=alert_status)](https://sonarcloud.io/dashboard?id=arena-esports_arena-backend)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React 18](https://img.shields.io/badge/React-18.x-61DAFB?logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.x-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![k6](https://img.shields.io/badge/k6-Load_Testing-7D64FF?logo=k6&logoColor=white)](https://k6.io/)
[![Selenium](https://img.shields.io/badge/Selenium-WebDriver-43B02A?logo=selenium&logoColor=white)](https://www.selenium.dev/)

<p align="center">
  A comprehensive, multi-layered automated testing pipeline implemented across the <b>ARENA Esports Streaming & Gamified Tournament Microservices Platform</b>. Built to demonstrate industry-standard testing paradigms, unified CI/CD automation, and software quality indicators.
</p>

[Team Allocation](#-team-member-allocation--role-division) • [Architecture](#-microservices--testing-architecture) • [Tool Setup](#-tool-setup--prerequisites) • [Pipeline Commands](#-pipeline-execution-commands) • [Testing Paradigms](#-deep-dive-testing-paradigms) • [Live Demo](#-12-minute-live-presentation-structure)

---

</div>

## 👥 Team Member Allocation & Role Division

In accordance with the assignment specifications, exactly 4 distinct testing paradigms were selected, with each student taking ownership of one specialized area:

| Student Name | Student IT Number | Assigned Testing Paradigm | Key Tools & Frameworks | Primary Scope & Deliverables |
| :--- | :---: | :--- | :--- | :--- |
| **Rupasinghe R.A.C.A.** | **IT24610794** | **Unit Testing & Mocking** | xUnit, Moq, Coverlet (.NET 10) | Parameterized cases (`[Theory]`, `[InlineData]`), Test Doubles (Mocks/Stubs) isolating service logic across all 6 microservices. |
| **Jayakody J.A.D.D.S.** | **IT24610791** | **Code Quality & Metrics** | SonarCloud, Roslyn Analyzers, SonarJS | Automated Static Code Analysis, Clean as You Code Quality Gate, SQALE technical debt calculation, code smells, vulnerabilities, and Cobertura/LCOV coverage ingestion. |
| **Weeramanthri R.D.** | **IT24610795** | **Load & Performance Testing** | k6, JavaScript (ES6) | Realistic 3-phase load curve (Ramp-up $\rightarrow$ Sustained Peak $\rightarrow$ Ramp-down), SLA threshold assertions (`p95 < 500ms`, `error rate < 1%`), and stress testing. |
| **Weerakoon R.A.D.H.M.** | **IT24610788** | **UI / E2E Testing** | Selenium WebDriver, TypeScript, Mocha, Chai | Automated cross-service end-to-end user journeys following the Page Object Model (POM), validating frontend state transitions across backend microservices. |

---

## 🏛️ Microservices & Testing Architecture

The base system consists of 6 backend microservices and 1 frontend client, fully containerized and orchestrated:

```mermaid
graph TD
    subgraph Client [Frontend Layer]
        WEB[ARENA Web Client<br/>React 18 / TypeScript / Vite<br/>Port: 5173]
    end

    subgraph Tests [Testing Pipeline Layers]
        E2E[4. UI / E2E Testing<br/>Selenium WebDriver + POM]
        LOAD[3. Load & Performance Testing<br/>k6 Scenario Profiling]
        SONAR[2. Code Quality & Metrics<br/>SonarCloud Static Analysis]
        UNIT[1. Unit Testing & Mocking<br/>xUnit + Moq + Coverlet]
    end

    subgraph Services [Backend Microservices (.NET 10)]
        USER[User Service<br/>Port: 5168 / MySQL]
        STREAM[Stream Service<br/>Port: 5167 / MySQL]
        TOUR[Tournament Service<br/>Port: 8082 / MySQL]
        CHAT[Chat Service<br/>WebSocket / In-Memory]
        BATTLE[Battle Economy Service<br/>Port: 8083 / MySQL]
        ANALYTICS[Analytics Service<br/>Kafka Event Consumer]
    end

    E2E -->|Automates User Journey| WEB
    WEB --> USER
    WEB --> TOUR
    WEB --> STREAM
    LOAD -->|Stress & Performance| USER
    LOAD -->|Read Catalog Concurrency| TOUR
    SONAR -->|Monorepo Static Analysis| Services
    SONAR -->|Client Static Analysis| WEB
    UNIT -->|Isolates Business Logic| Services
```

---

## 🛠️ Tool Setup & Prerequisites

### 1. General System Prerequisites
Ensure the following runtimes and tools are installed on your environment:
* **.NET 10 SDK** (`dotnet --version` $\ge$ 10.0)
* **Node.js** (`node --version` $\ge$ 20.x, recommended v22+)
* **Docker & Docker Compose** (for multi-service container dependencies)
* **Google Chrome** (for Selenium E2E execution)
* **k6** (for Load Testing execution):
  * *macOS:* `brew install k6`
  * *Windows:* `winget install GrafanaLabs.k6`
  * *Linux:* `sudo apt-get install k6`

### 2. Environment Initialization
Clone the repository and initialize project dependencies:
```bash
# Clone the repository
git clone https://github.com/ARENA-esports/arena-streaming-platform.git
cd arena-streaming-platform

# Restore .NET solution dependencies (all 6 microservices + test suites)
dotnet restore Arena/Arena.slnx

# Install root runner dependencies
npm install

# Install Frontend dependencies
cd Arena/client/arena-web && npm install && cd ../../

# Install E2E Selenium dependencies
cd Arena/tests/e2e-selenium && npm install && cd ../../
```

### 3. Spin Up Local Services (for Load & E2E Testing)
```bash
# Start all microservices and databases via Docker Compose
docker compose -f Arena/docker-compose.yml up -d
```

### 4. SonarCloud Integration Setup
* **Backend Project Key:** `arena-esports_arena-backend`
* **Frontend Project Key:** `arena-esports_arena-frontend`
* **Organization:** `arena-esports`
* In GitHub repository settings (`Settings -> Secrets and variables -> Actions`), configure the repository secret `SONAR_TOKEN`.

---

## 🚀 Pipeline Execution Commands

The testing pipeline can be executed either **systematically via unified automation** or **per individual paradigm**:

### Option A: Unified Automation Pipeline (Master Runner)
Run the complete, multi-stage automated testing pipeline from the project root:

```bash
# Run the entire pipeline across all 4 testing layers
npm test
# Or using the master shell runner:
bash scripts/run-pipeline.sh all
```

The unified runner executes each stage sequentially, records execution durations, and outputs a formatted summary table:

```text
╔═════════════════════════════════════════════════════════════════════════════╗
║              SE3112 AUTOMATED TESTING PIPELINE SUMMARY TABLE                ║
╠═════════════════════════════════════════════════════════════════════════════╣
║ Testing Paradigm & Assigned Role                 │ Status       │ Duration ║
╟──────────────────────────────────────────────────┼──────────────┼──────────╢
║ [1/4] Unit Testing & Mocking (xUnit / Moq)         │ PASSED       │     12s   ║
║ [2/4] Code Quality & Metrics (SonarCloud / Roslyn) │ PASSED       │     18s   ║
║ [3/4] Load & Performance Testing (k6)              │ PASSED       │     60s   ║
║ [4/4] UI & End-to-End Testing (Selenium WebDriver) │ PASSED       │     15s   ║
╠═════════════════════════════════════════════════════════════════════════════╣
║ Total Pipeline Execution Time: 105s                                         ║
╚═════════════════════════════════════════════════════════════════════════════╝
```

---

### Option B: Stage-by-Stage Individual Commands

#### 1. Unit Testing & Mocking (Rupasinghe R.A.C.A.)
```bash
# Execute unit tests across all 6 microservices
npm run test:unit
# Or via .NET CLI:
dotnet test Arena/Arena.slnx --configuration Release

# Execute unit tests with XPlat Cobertura code coverage collection
npm run test:unit:coverage
```

#### 2. Code Quality & Metrics (Jayakody J.A.D.D.S.)
```bash
# Validate solution compilation and Roslyn static analyzers locally
npm run test:quality

# Run frontend linting & static analysis
cd Arena/client/arena-web && npm run lint

# Trigger SonarCloud CI Scan via GitHub CLI
gh workflow run "CI" --ref main
gh workflow run "CI Frontend" --ref main
```

#### 3. Load & Performance Testing (Weeramanthri R.D.)
```bash
# Run primary 3-phase load test (both Auth and Tournament endpoints)
npm run test:load
# Or via k6 CLI:
k6 run Arena/tests/load/load-test.js

# Target specific endpoints:
npm run test:load:auth          # Only User Authentication (CPU/DB intensive)
npm run test:load:tournaments   # Only Tournament Catalog (Read concurrency)
npm run test:load:stress        # Breaking-point stress test
```

#### 4. UI & End-to-End Testing (Weerakoon R.A.D.H.M.)
```bash
# Run full Selenium E2E suite in Headless Chrome (CI-friendly)
npm run test:e2e

# Run Selenium E2E suite in Headed Chrome (visible browser for Live Demo)
npm run test:e2e:headed

# Open Mochawesome visual HTML test report
npm run test:e2e:report
```

---

## 🔬 Deep Dive: Testing Paradigms

### 1. Unit Testing & Mocking — Rupasinghe R.A.C.A. (IT24610794)
* **Frameworks:** `xUnit 2.x`, `Moq 4.x`, `FluentAssertions`, `Coverlet`
* **Implementation Highlights:**
  * **Parameterized Testing:** Utilizes `[Theory]` and `[InlineData]` / `[MemberData]` annotations to validate edge cases (boundary conditions, malformed tokens, boundary coin balances) without duplicate test logic.
  * **Test Doubles (Mocks):** Implements `Mock<IRepository>`, `Mock<IKafkaProducer>`, and `Mock<ILogger>` to isolate pure domain logic from database and network boundaries.
  * **Microservices Tested:** `UserService.Tests`, `TournamentService.Tests`, `StreamService.Tests`, `ChatService.Tests`, `BattleEconomyService.Tests`, `AnalyticsService.Tests`.

### 2. Code Quality & Metrics — Jayakody J.A.D.D.S. (IT24610791)
* **Frameworks:** `SonarCloud`, `dotnet-sonarscanner`, `Roslyn Analyzers`, `SonarJS`
* **Implementation Highlights:**
  * **Static Application Security Testing (SAST):** Scans C# and TypeScript code for injection vulnerabilities, insecure token handling, and information leakage.
  * **Clean as You Code (CaYC):** Quality Gates enforce strict criteria on New Code (0 New Bugs, 0 Vulnerabilities, 0 Security Hotspots, Duplication $<3\%$).
  * **Technical Debt & Maintainability:** Measures SQALE index rating (Grade A) to ensure low remediation time.
  * **Coverage Ingestion:** Automatically ingests Coverlet Cobertura (`coverage.cobertura.xml`) for .NET and Jest LCOV (`coverage/lcov.info`) for React.
  * *Note on JaCoCo:* JaCoCo is strictly a JVM bytecode instrumentation library for Java. Because this platform is built on C# (.NET 10) and TypeScript (React), SonarCloud is the architecturally appropriate, industry-standard solution.

### 3. Load & Performance Testing — Weeramanthri R.D. (IT24610795)
* **Frameworks:** `k6` (Grafana Labs)
* **Implementation Highlights:**
  * **3-Phase Load Profile:** 20s gradual Ramp-Up ($0 \rightarrow 50$ VUs) $\rightarrow$ 30s Sustained Peak load (50 VUs) $\rightarrow$ 10s Ramp-Down ($50 \rightarrow 0$ VUs).
  * **Enforced SLA Thresholds:**
    * 95th Percentile Latency: `p(95) < 500ms`
    * 99th Percentile Latency: `p(99) < 1000ms`
    * Failure Rate: `http_req_failed < 0.01` ($<1\%$ errors)
  * **Target Endpoints:**
    * `POST /api/auth/login` (CPU/crypto-heavy password verification & JWT generation)
    * `GET /api/tournaments` (High-concurrency database read & serialization)

### 4. UI & End-to-End Testing — Weerakoon R.A.D.H.M. (IT24610788)
* **Frameworks:** `Selenium WebDriver`, `TypeScript`, `Mocha`, `Chai`, `Mochawesome`
* **Implementation Highlights:**
  * **Page Object Model (POM):** Decouples test specifications from UI locators (`BasePage`, `SignupPage`, `LoginPage`, `TournamentsPage`, `ProfileSettingsPage`).
  * **Cross-Service Workflows:** Validates multi-service journey from registration in `UserService` to role elevation to tournament creation in `TournamentService`.
  * **Robust Synchronization:** Eliminates flaky tests by relying exclusively on explicit waits (`until.elementLocated`, `until.elementIsVisible`) rather than arbitrary sleep delays.

---

## 📊 CI/CD Automation & GitHub Actions Integration

The repository includes automated CI/CD workflows under `.github/workflows/`:

| Workflow File | Trigger Events | Jobs Executed |
| :--- | :--- | :--- |
| **`testing-pipeline.yml`** | `push` (`main`, `dev`, `feat/**`), `pull_request`, `workflow_dispatch` | Orchestrates all 4 testing stages in parallel and publishes the unified Markdown summary table in `$GITHUB_STEP_SUMMARY`. |
| **`ci.yml`** | `push` (`dev`, `feat/**`), `pull_request` (`main`, `dev`) | Builds microservices, executes unit tests with coverage, and triggers SonarCloud Backend Analysis. |
| **`ci-frontend.yml`** | `push` (`dev`, `feat/**`), `pull_request` (`main`, `dev`) | Lints React client, executes frontend tests with LCOV coverage, compiles Vite build, and runs SonarCloud Frontend Analysis. |

---