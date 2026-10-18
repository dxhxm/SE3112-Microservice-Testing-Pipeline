<div align="center">

# ⚔️ ARENA — Esports Streaming & Gamified Tournament Platform

[![CI Pipeline](https://github.com/ARENA-esports/arena-streaming-platform/actions/workflows/ci.yml/badge.svg)](https://github.com/ARENA-esports/arena-streaming-platform/actions/workflows/ci.yml)
[![CD Staging](https://github.com/ARENA-esports/arena-streaming-platform/actions/workflows/cd-staging.yml/badge.svg)](https://github.com/ARENA-esports/arena-streaming-platform/actions/workflows/cd-staging.yml)
[![Frontend Staging](https://github.com/ARENA-esports/arena-streaming-platform/actions/workflows/cd-frontend-staging.yml/badge.svg)](https://github.com/ARENA-esports/arena-streaming-platform/actions/workflows/cd-frontend-staging.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18.x-61DAFB?logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.x-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Kafka](https://img.shields.io/badge/Apache_Kafka-Event--Driven-231F20?logo=apachekafka&logoColor=white)](https://kafka.apache.org/)
[![MySQL](https://img.shields.io/badge/MySQL-8.0-4479A1?logo=mysql&logoColor=white)](https://www.mysql.com/)
[![Docker](https://img.shields.io/badge/Docker-Containerized-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![Azure](https://img.shields.io/badge/Azure-Cloud_Deployed-0078D4?logo=microsoftazure&logoColor=white)](https://azure.microsoft.com/)

**Next-generation, cloud-native interactive esports broadcasting platform.**  
Viewers don't just watch tournament streams — they align with factions, earn passive watch rewards, engage in real-time faction chat, spend earned coins on weapon strikes, and sway live tournament rounds via a shared dynamic battle bar.
[Evaluation Criteria](#evaluation-criteria--weightage-mapping) • [Target Endpoints](#target-endpoints-selected) • [Load Profile](#load-scenario-profile) • [Quick Start](#quick-start--execution)

</div>

# Arena Microservices — Load & Performance Testing Suite

This directory contains the automated load and performance testing suite built with **k6**, designed to validate that the Arena microservices fulfill strict production performance targets and SLAs.

---

## Evaluation Criteria & Weightage Mapping

| Category | Weightage | Implementation Details |
| :--- | :---: | :--- |
| **Code Quality** | **15%** | Clean ES6 k6 test scripts, modular structure, reusable payload definitions, custom `Trend`/`Counter`/`Rate` metrics, and granular assertions. |
| **Depth** | **15%** | Realistic multi-phase load profiling (Ramp-up, Sustained Peak, Ramp-down), pacing/sleep delays, comparison across multiple endpoints (Auth write/CPU-heavy vs Tournament read-heavy), plus dedicated Stress and Spike test suites. |
| **Live Demo** | **15%** | 2-minute plug-and-play live terminal presentation script with clean output and clear green pass/fail indicators. |
| **Walkthrough** | **15%** | Complete breakdown of metrics: Requests/sec (RPS), p95/p99 latency, error rates, data throughput, and HTTP status verification. |
| **Individual VIVA** | **20%** | Thorough, industry-standard answers to all three performance engineering questions. |

---

## Target Endpoints Selected

1. **User Authentication (Write / CPU & DB Intensive)**
   - **Endpoint**: `POST http://localhost:5168/api/auth/login`
   - **Rationale**: Authentication involves database user lookup, cryptographic password hashing (BCrypt), and cryptographic JWT token generation. It represents the highest CPU and memory resource cost per request on the identity boundary.
2. **Tournament Season Catalog (Read / DB Query & Serialization)**
   - **Endpoint**: `GET http://localhost:8082/api/tournaments`
   - **Rationale**: Public-facing, high-traffic read endpoint queried by all active viewers and organizers to view schedules and standings.

---

## Load Scenario Profile

The test executes the mandatory 3-phase profile:

```
VUs (Concurrent Users)
 50 |                  ┌──────────────────────────────┐
    |                 /│                              │\
    |                / │                              │ \
    |               /  │                              │  \
    |              /   │                              │   \
  0 └─────────────┴────┴──────────────────────────────┴────┴──────── Time
      0s         20s               50s                   60s
     [--- Ramp-Up ---]   [------ Sustained Peak ------]  [-- Ramp-Down --]
```

1. **Ramp-Up (20 seconds)**: 0 to 50 concurrent Virtual Users (VUs) to simulate gradual traffic influx.
2. **Sustained Load (30 seconds)**: Maintain peak load of 50 VUs to observe steady-state service behavior and connection pooling.
3. **Ramp-Down (10 seconds)**: Step down from 50 to 0 VUs to verify graceful resource release.

###  Pass / Fail Thresholds
- **95th Percentile Latency**: `p(95) < 500ms` (95% of requests must complete in under 500ms)
- **99th Percentile Latency**: `p(99) < 1000ms`
- **Error Rate**: `http_req_failed < 0.01` (Failure rate must stay strictly below 1%)

---

## Quick Start & Execution

### 1. Prerequisites
Ensure `k6` is installed. (Installed via `winget install GrafanaLabs.k6` or verify with `k6 version`).

### 2. Start Services
From the `Arena/` directory, start the required microservices:
```powershell
# In Arena/ directory:
docker compose up -d mysql-user user mysql-tournament tournament
```
Or start the full stack:
```powershell
docker compose up -d
```

### 3. Run the Load Tests

Navigate to `Arena/tests/load`:
```powershell
cd Arena/tests/load
```

#### Option A: Run the Primary Load Test (Both Endpoints)
```powershell
k6 run load-test.js
# Or via npm:
npm run test:load
```

#### Option B: Target Specific Endpoints
```powershell
# Only User Authentication Endpoint
k6 run -e TARGET=auth load-test.js
# Or:
npm run test:load:auth

# Only Tournament Catalog Endpoint
k6 run -e TARGET=tournaments load-test.js
# Or:
npm run test:load:tournaments
```

#### Option C: Run Beyond Breaking Point (Stress Test)
```powershell
k6 run stress-test.js
# Or:
npm run test:load:stress
```

