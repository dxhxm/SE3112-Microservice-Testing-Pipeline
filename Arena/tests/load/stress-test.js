import http from 'k6/http';
import { check, sleep } from 'k6';

// Stress testing pushes the system beyond normal operational capacity to find its breaking point
const USER_SERVICE_URL = __ENV.USER_SERVICE_URL || 'http://localhost:5168';
const TOURNAMENT_SERVICE_URL = __ENV.TOURNAMENT_SERVICE_URL || 'http://localhost:8082';

export const options = {
  stages: [
    { duration: '30s', target: 50 },  // Below normal load
    { duration: '30s', target: 100 }, // Normal load
    { duration: '30s', target: 150 }, // Breaking point stress
    { duration: '30s', target: 200 }, // Maximum saturation
    { duration: '20s', target: 0 },   // Recovery ramp down
  ],
  thresholds: {
    // Under extreme stress we allow looser bounds to observe degradation
    http_req_duration: ['p(95)<1500'],
    http_req_failed: ['rate<0.05'], // Allow up to 5% failure during breaking point testing
  },
};

export default function () {
  http.get(`${TOURNAMENT_SERVICE_URL}/api/tournaments`);
  sleep(0.2);
}
