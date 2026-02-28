import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: Number(__ENV.VUS || 40),
  duration: __ENV.DURATION || '60s'
};

const baseUrl = __ENV.BASE_URL || 'http://localhost:8082';
const apiKey = __ENV.API_KEY || 'dev-ingestion-key';
const payloadPadding = 'x'.repeat(Number(__ENV.PAYLOAD_PADDING_BYTES || 300));

export default function () {
  const payload = JSON.stringify({
    timestampUtc: new Date().toISOString(),
    level: 'Error',
    message: 'k6 ingestion test',
    serviceName: 'Perf',
    environment: 'Test',
    traceId: `T-${__VU}-${__ITER}`,
    statusCode: 500,
    propertiesJson: JSON.stringify({ pad: payloadPadding })
  });

  const res = http.post(`${baseUrl}/api/v1.0/logs`, payload, {
    headers: {
      'Content-Type': 'application/json',
      'X-Api-Key': apiKey
    }
  });

  check(res, {
    'accepted or throttled': r => r.status === 202 || r.status === 429
  });

  sleep(0.1);
}
