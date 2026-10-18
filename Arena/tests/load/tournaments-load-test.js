import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Counter, Rate } from 'k6/metrics';

// ── Metrics ────────────────────────────────────────────────────────────────
const queryDuration = new Trend('tournaments_req_duration_ms');
const successfulQueries = new Counter('successful_tournament_queries');
const queryErrors = new Rate('query_error_rate');

// ── Target Configuration ───────────────────────────────────────────────────
const TOURNAMENT_SERVICE_URL = __ENV.TOURNAMENT_SERVICE_URL || 'http://localhost:8082';

export const options = {
  stages: [
    { duration: '20s', target: 50 }, // Ramp-Up: 0 to 50 VUs over 20s
    { duration: '30s', target: 50 }, // Sustained: Hold 50 VUs for 30s
    { duration: '10s', target: 0 },  // Ramp-Down: Step down to 0 VUs over 10s
  ],
  thresholds: {
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
    http_req_failed: ['rate<0.01'],
    query_error_rate: ['rate<0.01'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

const headers = {
  'Accept': 'application/json',
};

export default function () {
  const res = http.get(`${TOURNAMENT_SERVICE_URL}/api/tournaments`, {
    headers: headers,
    tags: { endpoint: 'get_tournaments' },
  });

  queryDuration.add(res.timings.duration);

  const passed = check(res, {
    'Status is 200': (r) => r.status === 200,
    'Latency < 500ms': (r) => r.timings.duration < 500,
    'Response is JSON': (r) => {
      try {
        JSON.parse(r.body);
        return true;
      } catch (_) {
        return false;
      }
    },
  });

  if (passed) {
    successfulQueries.add(1);
    queryErrors.add(0);
  } else {
    queryErrors.add(1);
  }

  sleep(0.5);
}
