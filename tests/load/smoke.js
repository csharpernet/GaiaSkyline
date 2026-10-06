import http from 'k6/http';
import { check, sleep } from 'k6';

// Smoke load test (Stage 8 Part E): a tiny, short run over the key public routes that fails the build if
// latency or error rate regresses under light concurrency. Not a stress test — it guards the happy path.
//
// Run locally against the dev server:
//   BASE_URL=https://localhost:7443 k6 run --insecure-skip-tls-verify tests/load/smoke.js
// CI runs it against the plain-HTTP app the browser-quality job already has running.

export const options = {
  vus: 5,
  duration: '30s',
  thresholds: {
    // Lenient on purpose — a smoke test guards against gross regressions, not CI-runner jitter.
    http_req_failed: ['rate<0.02'], // under 2% of requests may fail
    http_req_duration: ['p(95)<1500'], // 95th percentile under 1.5 s
  },
};

const BASE = __ENV.BASE_URL || 'http://localhost:5080';

// A representative slice: the localized home, the booking funnel entry, content pages and readiness.
const ROUTES = ['/en', '/en/book', '/en/gallery', '/en/stories', '/pt-pt', '/health/ready'];

export default function () {
  for (const route of ROUTES) {
    const res = http.get(`${BASE}${route}`);
    check(res, {
      'status is 200': (r) => r.status === 200,
    });
    sleep(0.5);
  }
}
