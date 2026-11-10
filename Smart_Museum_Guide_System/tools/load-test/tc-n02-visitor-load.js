import http from "k6/http";
import { check, sleep } from "k6";

/**
 * TC-N02 (N02 - Lê Quốc Minh):
 * Đo thời gian phản hồi P95 cho các request xem nội dung đã xuất bản & xem/gợi ý tour công khai
 * ở mức tải 50 người dùng đồng thời (50 concurrent VUs).
 * Ngưỡng cam kết (Threshold): P95 <= 2000 ms (2 giây) và tỷ lệ lỗi < 1%.
 *
 * Cách chạy:
 *   k6 run -e BASE_URL=http://localhost:5000 -e MUSEUM_ID=1 -e TOUR_ID=1 -e ARTIFACT_ID=1 tools/load-test/tc-n02-visitor-load.js
 */

const BASE_URL = __ENV.BASE_URL || "http://localhost:5000";
const MUSEUM_ID = __ENV.MUSEUM_ID || "1";
const TOUR_ID = __ENV.TOUR_ID || "1";
const ARTIFACT_ID = __ENV.ARTIFACT_ID || "1";
const TOPIC_ID = Number(__ENV.TOPIC_ID || "1");

export const options = {
  scenarios: {
    visitor_concurrent_50: {
      executor: "ramping-vus",
      startVUs: 0,
      stages: [
        { duration: "15s", target: 50 }, // Tăng tải lên 50 người dùng đồng thời
        { duration: "60s", target: 50 }, // Giữ ổn định 50 người dùng đồng thời
        { duration: "10s", target: 0 },  // Hạ tải
      ],
    },
  },
  thresholds: {
    http_req_duration: ["p(95)<=2000"], // Cam kết N02: P95 <= 2 giây
    http_req_failed: ["rate<0.01"],     // Tỷ lệ lỗi < 1%
  },
};

export default function () {
  // 1. Khách xem danh sách tour công khai của bảo tàng
  const listRes = http.get(`${BASE_URL}/api/tours?museumId=${MUSEUM_ID}&page=1&pageSize=20`);
  check(listRes, {
    "list public tours 200": (r) => r.status === 200,
  });

  // 2. Khách xem phiên bản công khai hiện tại của tour
  const verRes = http.get(`${BASE_URL}/api/tours/${TOUR_ID}/public-version`);
  check(verRes, {
    "get public tour version 200": (r) => r.status === 200,
  });

  // 3. Khách yêu cầu gợi ý tour theo sở thích + thời gian (F05)
  const recPayload = JSON.stringify({
    topicIds: [TOPIC_ID],
    availableMinutes: 60,
  });
  const recRes = http.post(
    `${BASE_URL}/api/tours/recommendations?museumId=${MUSEUM_ID}`,
    recPayload,
    { headers: { "Content-Type": "application/json" } }
  );
  check(recRes, {
    "recommend tours 200": (r) => r.status === 200,
  });

  // 4. Khách xem thông tin hiện vật công khai
  const artRes = http.get(`${BASE_URL}/api/artifacts/${ARTIFACT_ID}`);
  check(artRes, {
    "get published artifact 200": (r) => r.status === 200,
  });

  sleep(0.5);
}

