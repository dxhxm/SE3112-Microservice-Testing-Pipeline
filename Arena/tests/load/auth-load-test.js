import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Counter, Rate } from 'k6/metrics';

// ── Metrics ────────────────────────────────────────────────────────────────
const authDuration = new Trend('auth_req_duration_ms');
const successfulLogins = new Counter('successful_logins');
const authErrors = new Rate('auth_error_rate');

// ── Target Configuration ───────────────────────────────────────────────────
const USER_SERVICE_URL = __ENV.USER_SERVICE_URL || 'http://localhost:5168';

export const options = {
  stages: [
    { duration: '20s', target: 50 }, // Ramp-Up: 0 to 50 VUs over 20s
    { duration: '30s', target: 50 }, // Sustained: Hold 50 VUs for 30s
    { duration: '10s', target: 0 },  // Ramp-Down: Step down to 0 VUs over 10s
  ],
  thresholds: {
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
    http_req_failed: ['rate<0.01'],
    auth_error_rate: ['rate<0.01'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

const payload = JSON.stringify({
  identifier: __ENV.AUTH_IDENTIFIER || 'organizer1',
  password: __ENV.AUTH_PASSWORD || 'Password123!',
});

const headers = {
  'Content-Type': 'application/json',
  'Accept': 'application/json',
};

export default function () {
  const res = http.post(`${USER_SERVICE_URL}/api/auth/login`, payload, {
    headers: headers,
    tags: { endpoint: 'post_auth_login' },
    responseCallback: http.expectedStatuses(200, 401, 429),
  });

  authDuration.add(res.timings.duration);

  const passed = check(res, {
    'Status is 200, 401, or 429': (r) => r.status === 200 || r.status === 401 || r.status === 429,
    'Latency < 500ms': (r) => r.timings.duration < 500,
    'No 5xx server errors': (r) => r.status < 500,
  });

  if (passed && res.status < 500) {
    if (res.status === 200) {
      successfulLogins.add(1);
    }
    authErrors.add(0);
  } else {
    authErrors.add(1);
  }

  sleep(0.5);
}
