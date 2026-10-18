# 🧪 UI & End-to-End (E2E) Testing Guide — Selenium WebDriver

**Course**: SE3112 — Microservice Testing Pipeline  
**Assigned Student**: Hiruna  
**Paradigm**: Automated UI & Multi-Microservice End-to-End (E2E) Testing  
**Primary Framework**: Selenium WebDriver (TypeScript / Node.js) with Page Object Model (POM)  
**Git Branch**: `feature/e2e-testing`

---

## 1. Architecture & Cross-Service Boundaries

E2E testing validates user journeys across the entire system topology, asserting that user actions initiated in the React frontend trigger and propagate state accurately across backend microservices:

```
                                  [ Selenium WebDriver ]
                               (Chrome Headless / Headed)
                                           │
                                           │ User Interactions (Clicks, Typing, DOM Assertions)
                                           ▼
                       ┌───────────────────────────────────────┐
                       │   ARENA Web Client (React 18 / Vite)  │
                       │             Port: 5173                │
                       └───────────────────┬───────────────────┘
                                           │
                    ┌──────────────────────┼──────────────────────┐
                    │ Reverse Proxy        │ Reverse Proxy        │ Reverse Proxy
                    ▼                      ▼                      ▼
        ┌──────────────────────┐ ┌────────────────────┐ ┌────────────────────┐
        │     User Service     │ │ Tournament Service │ │   Stream Service   │
        │      Port: 5168      │ │    Port: 8082      │ │    Port: 5167      │
        │   (Auth, RBAC, JWT)  │ │   (Tournaments)    │ │ (Match Scheduling) │
        └──────────┬───────────┘ └─────────┬──────────┘ └─────────┬──────────┘
                   │                       │                      │
                   ▼                       ▼                      ▼
             [ MySQL: User ]       [ MySQL: Tournament ]    [ MySQL: Stream ]
```

---

## 2. Design Pattern: Page Object Model (POM)

To maintain high code quality and prevent brittle tests, all UI interactions follow the **Page Object Model (POM)**:

| Component | Path | Responsibility |
| :--- | :--- | :--- |
| **`BasePage`** | `pages/BasePage.ts` | Encapsulates explicit wait synchronization (`until.elementLocated`, `until.elementIsVisible`, `until.urlContains`) and screenshot capture. |
| **`SignupPage`** | `pages/SignupPage.ts` | Encapsulates user registration inputs and validation error elements. |
| **`LoginPage`** | `pages/LoginPage.ts` | Encapsulates credentials form, submit action, and authentication error feedback. |
| **`TournamentsPage`** | `pages/TournamentsPage.ts` | Encapsulates tournament catalog, "Create Tournament" modal triggers, and card verification. |
| **`ProfileSettingsPage`** | `pages/ProfileSettingsPage.ts` | Encapsulates role elevation toggle (`Viewer` $\leftrightarrow$ `Organizer`). |
| **`ForbiddenPage`** | `pages/ForbiddenPage.ts` | Encapsulates `/403` Access Denied security view assertions. |

---

## 3. Test Scenarios Matrix

| Test ID | Test Scenario | Microservices Crossed | Expected Outcome |
| :--- | :--- | :--- | :--- |
| **`TC-E2E-01`** | **Multi-Service User Lifecycle**: Signup $\rightarrow$ Login $\rightarrow$ Upgrade Role $\rightarrow$ Create Tournament $\rightarrow$ Verify UI & DB | Frontend (`5173`) $\rightarrow$ User Service (`5168`) $\rightarrow$ Tournament Service (`8082`) | User registers, authenticates, switches to Organizer, creates tournament; state renders in UI and persists in DB. |
| **`TC-E2E-02`** | **RBAC Route Guard (Negative)**: Viewer denied access to `/organizer/matches/new` | Frontend (`5173`) $\rightarrow$ User Service (`5168`) | Access blocked by `ProtectedRoute`; redirected to `/403` "Access Denied". |
| **`TC-E2E-03`** | **Validation Conflict (Edge Case)**: Rejection of duplicate username registration | Frontend (`5173`) $\rightarrow$ User Service (`5168`) | Backend returns `409 Conflict`; UI displays warning toast/banner without unhandled exception. |
| **`TC-E2E-04`** | **State Teardown & Data Isolation**: Purge transient test accounts and entities | User Service (`5168`) API | Test account deleted via `DELETE /api/Users/me`; database left completely clean. |

---

## 4. Test Synchronization (Eliminating Flakiness)

Arbitrary sleep statements (`Thread.sleep`) are strictly avoided. Instead, synchronization is achieved using **explicit wait conditions**:
```typescript
// Example from BasePage: Synchronizing on element readiness
public async waitForClickable(locator: By, timeoutMs: number = this.timeout): Promise<WebElement> {
  const el = await this.waitForVisible(locator, timeoutMs);
  await this.driver.wait(until.elementIsEnabled(el), timeoutMs, `Element not clickable: ${locator.toString()}`);
  return el;
}
```

---

## 5. Execution Commands

### Prerequisites
Ensure microservices and frontend client are running:
```bash
# Option A: Run complete stack via Docker Compose
cd Arena
docker compose up -d

# Option B: Run locally
# Start User Service (:5168), Tournament Service (:8082), Stream Service (:5167)
# Start Frontend Client (:5173) in Arena/client/arena-web: npm run dev
```

### Running E2E Tests
From the workspace root:
```bash
# 1. Run Headless E2E Tests (Default, CI-Ready)
npm run test:e2e

# 2. Run Headed E2E Tests (Opens visible Chrome browser for Live Demo)
npm run test:e2e:headed

# 3. View Interactive Mochawesome HTML Report
npm run test:e2e:report
```

Or from the test directory (`Arena/tests/e2e-selenium`):
```bash
cd Arena/tests/e2e-selenium
npm test
```

---

## 6. Reporting & Artifacts

1. **Terminal Step Logs**: Custom timestamped, color-coded logging shows every microservice boundary crossed during live execution.
2. **Interactive HTML Report**: Generated at `Arena/tests/e2e-selenium/mochawesome-report/e2e-report.html` containing charts, timings, and pass/fail indicators.
3. **JUnit XML Output**: Generated at `Arena/tests/e2e-selenium/test-results/junit.xml` for integration with Daham's unified pipeline and SonarQube test execution metrics.

---

## 7. Live Demo Walkthrough (Strict 2-Minute Timebox: 06:00 – 08:00)

| Time | Target Action | Speaking Script |
| :--- | :--- | :--- |
| **06:00 – 06:20** | Introduction & Architecture Context | *"I am Hiruna, and I implemented the End-to-End and UI Testing framework using Selenium WebDriver with the Page Object Model. E2E tests validate complete user journeys across microservice boundaries."* |
| **06:20 – 07:15** | Live Execution (`npm run test:e2e:headed` or `npm run test:e2e`) | *"Triggering the suite live. Notice Step 1: User registers in User Service on port 5168. Step 2: Role is upgraded to Organizer. Step 3: Navigates to Tournament Service on port 8082 to create a tournament. The state instantly renders in the React UI."* |
| **07:15 – 07:45** | Negative RBAC & Teardown Verification | *"Next, TC-E2E-02 verifies our RBAC route guard: when a Viewer attempts to access an organizer route, they are blocked and redirected to 403 Forbidden. Immediately after, our automated teardown purges the test user via REST API, preventing database pollution."* |
| **07:45 – 08:00** | HTML Report & Handover | *"All tests executed with 100% pass rate. Here is our Mochawesome HTML report. Handing over to Daham for Code Quality."* |

---

## 8. VIVA Preparation & Defense

### Q1: Why did you choose the Page Object Model (POM) pattern?
> **Answer**: *"POM decouples UI locators and DOM actions from test specifications. If a button ID or CSS class changes, we update a single locator in `TournamentsPage` rather than modifying dozens of test files, ensuring high maintainability and adhering to the Single Responsibility Principle."*

### Q2: How did you prevent test flakiness in Selenium?
> **Answer**: *"We strictly avoided hardcoded pauses (`sleep`). Instead, we implemented explicit waits using `WebDriverWait` with conditions such as `until.elementLocated`, `until.elementIsVisible`, and `until.urlContains`. This synchronizes execution directly with the frontend's asynchronous React state updates and network responses."*

### Q3: How do you handle test data isolation across microservice databases?
> **Answer**: *"We dynamically generate unique timestamps for usernames (`e2e_user_<timestamp>`) and entities so parallel or successive test runs never collide. Additionally, our test lifecycle features an automated `after` teardown hook that invokes the backend deletion endpoint (`DELETE /api/Users/me`) using the acquired JWT token, ensuring clean database state."*

### Q4: Why are E2E tests at the top of the Test Pyramid while Unit tests are at the bottom?
> **Answer**: *"Unit tests at the base isolate individual methods in memory and execute in milliseconds without external dependencies. E2E tests sit at the apex because they require the entire microservices topology, databases, and browser engine running, making them the most expensive and slowest to run. However, they provide the highest real-world fidelity by verifying that all distributed services correctly collaborate from the end user's perspective."*
