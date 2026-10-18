import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Counter, Rate } from 'k6/metrics';

// ── Custom Performance Metrics ─────────────────────────────────────────────
const authDuration = new Trend('auth_req_duration_ms');
const tournamentDuration = new Trend('tournaments_req_duration_ms');
const successfulRequests = new Counter('successful_requests');
const errorRate = new Rate('custom_error_rate');

// ── Configuration & Environment Defaults ───────────────────────────────────
const USER_SERVICE_URL = __ENV.USER_SERVICE_URL || 'http://localhost:5168';
const TOURNAMENT_SERVICE_URL = __ENV.TOURNAMENT_SERVICE_URL || 'http://localhost:8082';
const TARGET = __ENV.TARGET || 'all'; // 'auth', 'tournaments', or 'all'

// ── Load Scenario Configuration (as specified in load.md) ───────────────────
export const options = {
  stages: [
    // Phase 1: Ramp-Up: Gradually increase concurrent users (0 to 50 VUs over 20s)
    { duration: '20s', target: 50 },
    // Phase 2: Sustained Load: Hold peak load (50 VUs for 30s)
    { duration: '30s', target: 50 },
    // Phase 3: Ramp-Down: Step down to 0 VUs over 10s
    { duration: '10s', target: 0 },
  ],

  // Pass/Fail Criteria Thresholds
  thresholds: {
    // 95% of all requests must complete in under 500ms
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
    // HTTP failure rate must stay below 1% (0.01)
    http_req_failed: ['rate<0.01'],
    // Custom error rate tracking
    custom_error_rate: ['rate<0.01'],
  },

  // Summary display options
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

// ── Payload for Authentication ─────────────────────────────────────────────
const loginPayload = JSON.stringify({
  identifier: __ENV.AUTH_IDENTIFIER || 'organizer1',
  password: __ENV.AUTH_PASSWORD || 'Password123!',
});

const jsonHeaders = {
  'Content-Type': 'application/json',
  'Accept': 'application/json',
};

// ── Virtual User (VU) Execution Lifecycle ──────────────────────────────────
export default function () {
  // Scenario A: Tournament Catalog Query (High-traffic public read endpoint)
  if (TARGET === 'tournaments' || TARGET === 'all') {
    const tournamentRes = http.get(`${TOURNAMENT_SERVICE_URL}/api/tournaments`, {
      headers: jsonHeaders,
      tags: { endpoint: 'get_tournaments' },
    });

    tournamentDuration.add(tournamentRes.timings.duration);

    const tournamentSuccess = check(tournamentRes, {
      'Tournaments status is 200': (r) => r.status === 200,
      'Tournaments latency < 500ms': (r) => r.timings.duration < 500,
      'Tournaments body is JSON array': (r) => {
        try {
          const body = JSON.parse(r.body);
          return Array.isArray(body);
        } catch (_) {
          return false;
        }
      },
    });

    if (tournamentSuccess) {
      successfulRequests.add(1);
      errorRate.add(0);
    } else {
      errorRate.add(1);
    }
  }

  // Small pacing pause between user actions
  sleep(0.5);

  // Scenario B: User Authentication (High-resource POST endpoint: BCrypt + DB query + JWT)
  if (TARGET === 'auth' || TARGET === 'all') {
    const authRes = http.post(`${USER_SERVICE_URL}/api/auth/login`, loginPayload, {
      headers: jsonHeaders,
      tags: { endpoint: 'post_auth_login' },
      responseCallback: http.expectedStatuses(200, 401, 429),
    });

    authDuration.add(authRes.timings.duration);

    const authSuccess = check(authRes, {
      // 200 = Success, 401 = Bad credentials, 429 = Rate limited by ASP.NET Core IP limiter (expected security behavior)
      'Auth status is 200, 401, or 429': (r) => r.status === 200 || r.status === 401 || r.status === 429,
      'Auth latency < 500ms': (r) => r.timings.duration < 500,
      'Auth no 5xx server errors': (r) => r.status < 500,
    });

    if (authSuccess && authRes.status < 500) {
      successfulRequests.add(1);
      errorRate.add(0);
    } else {
      errorRate.add(1);
    }
  }

  // Pacing before next loop iteration to emulate real user behavior
  sleep(0.5);
}
