# Museum Discovery & Outdoor Navigation — kế hoạch FE/BE

Ngày: 21/10/2026. Kế hoạch đã được người dùng xác nhận và code FE/BE đã triển khai. Các mục bên dưới lưu phân tích trước triển khai; hợp đồng cuối ở `Museum_Discovery_API.md`, kết quả kiểm tra và các bước môi trường còn lại ở `Development_Log.md`. Migration đã tạo nhưng chưa apply vào DB ứng dụng.

## Kết quả đối chiếu source

- BE là ASP.NET Core, các lớp Domain/Application/Infrastructure/API; Museum ID là int. Response dùng `{statusCode,message,data}`; phân trang nằm trong `data.items`, kèm page/pageSize/totalCount/totalPages.
- Museum entity, EF mapping và `project-docs/smgs_schema.dbml` có latitude/longitude nullable, address, is_active; chưa có cityCode hay danh mục tỉnh/thành. Repository mới lọc keyword/isActive và phân trang; chưa có navigation service/controller.
- Museum GET hiện AllowAnonymous, không mặc định lọc active; GET detail cũng chưa chặn inactive. Giữ cấu hình authentication hiện có, áp dụng quy tắc active cho dữ liệu Visitor của chức năng này và giữ khả năng quản trị xem inactive theo quyền.
- FE chức năng Visitor nằm trong `smgs_mobile` (Flutter), không phải ứng dụng React quản trị `smgs_web`. Có http/geolocator/url_launcher/google_maps_flutter. Có discovery screen, API client, location service, route sheet và model để tái sử dụng.
- FE còn bản đồ vẽ mô phỏng và GoogleMap tùy chọn; API route lỗi thì tự tạo km/phút bằng đường thẳng; GPS bị từ chối thì trả vị trí mặc định TP.HCM. Những fallback này phải bỏ khỏi luồng dữ liệu thực.
- `docs/PLAN_MUSEUM_DISCOVERY_OUTDOOR_NAVIGATION.md` và `docs/intent/museum_discovery_intent.md` mô tả Google Routes, nhiều travel mode và BE đã hoàn thành; source BE hiện tại không có các API đó. Kế hoạch mới thay thế các giả định này.
- Đã đọc project context, DBML và tài liệu thay đổi UI `SMGS_UI_Changes_Latest_Merged.md`. Không tìm thấy development log riêng trong BE; sẽ tạo log triển khai tại project-docs. Hai working tree sạch tại thời điểm khảo sát.

## Contract thống nhất đề xuất

1. `GET /api/museums?keyword=&cityCode=&latitude=&longitude=&sortBy=distance&page=1&pageSize=20`. Giữ keyword và wrapper hiện có để tương thích caller. Có thể nhận search làm alias, không thay keyword. Mỗi item thêm cityCode, distanceKm nullable; giữ id int và các field hiện có. Kiểm tra cặp tọa độ, range và sortBy; distance sort bắt buộc GPS hợp lệ. Sort trước phân trang, tie-break bằng ID. Museum thiếu tọa độ vẫn xuất hiện trong danh sách, distance null xếp cuối, không vẽ marker giả tại 0,0.
2. `GET /api/museums/cities`: `data` là danh sách `{code,name}` từ danh mục chung BE, FE không có danh sách fallback riêng. Xác minh nguồn mã hành chính trước khi tạo danh mục; không lấy HCM hay danh mục cũ trong FE làm nguồn chính thức.
3. `POST /api/navigation/outdoor/routes`: body `{museumId: int, origin: {latitude,longitude}, travelMode: "DRIVE"}`. Chỉ DRIVE; từ chối mode khác. Đích lấy từ DB và chỉ route tới museum active có tọa độ hợp lệ.
4. Response route trong wrapper hiện có: `{museumId,destination:{latitude,longitude},travelMode,distanceMeters,durationSeconds,routePoints:[{latitude,longitude}]}`. Giá trị khoảng cách/thời gian là số, không làm tròn ở BE. Không dùng encodedPolyline. OSRM GeoJSON lon/lat chuyển đúng sang lat/lon.
5. Lỗi: 400 input/mode không hợp lệ hoặc thiếu tọa độ, 404 museum không khả dụng, 422 không tìm được tuyến, 429 quá giới hạn, 502 provider response lỗi, 504 timeout. Không đưa URL nội bộ hay payload provider ra client. FE luôn cho mở Google Maps khi có đích hợp lệ kể cả preview lỗi.

## Task BE

1. Schema: đề xuất thêm `Museum.CityCode` nullable, ánh xạ EF, index và FK tới danh mục tỉnh/thành thống nhất. Tạo migration bổ sung, không dựng lại bảng hay sửa migration cũ. Giữ dữ liệu cũ với cityCode null; chỉ backfill khi có dữ liệu đã xác minh. Cập nhật DBML, create/update/response DTO cùng validation và mapping. Không tự apply migration vào database.
2. Discovery: sửa MuseumsController, IMuseumService/MuseumService, IMuseumRepository/MuseumRepository. Filter search/city/active; tính Haversine bằng query EF/PostgreSQL dịch được để sort trước Skip/Take, kiểm tra integration trên PostgreSQL. Giữ giới hạn pageSize hiện tại. Bổ sung danh mục cities và cập nhật các caller quản trị liên quan nếu hợp đồng cần thay đổi.
3. Routing: thêm DTO request/response, IOutdoorRoutingService và OSRM implementation trong Infrastructure; thêm navigation orchestration ở Application và NavigationController. Dùng HttpClientFactory, cấu hình base URL/timeout ở một chỗ, cancellation xuyên suốt, kiểm tra HTTP status/OSRM code/geometry hữu hạn và đủ điểm. Không retry vô hạn.
4. Vận hành: đăng ký service trong Infrastructure dependency injection; rate limiter riêng endpoint route và cấu hình middleware trong Program. Không cache hoặc persist GPS. Tắt log request URI của typed routing HttpClient vì URI chứa tọa độ; log loại lỗi/status có kiểm soát. Kiểm tra logging middleware có ghi body/query GPS không.
5. Tests: thêm `tests/SMGS.DiscoveryChecks` theo kiểu assert executable đang có. Fake HttpMessageHandler kiểm tra lon/lat, NoRoute, JSON hỏng, 429/5xx, timeout/cancel; test range, museum inactive/thiếu tọa độ, Haversine, sort/filter/pagination; kiểm tra query bằng PostgreSQL test DB khi có môi trường phù hợp.
6. Tài liệu: API contract, cấu hình environment, hướng dẫn migration/backfill, development log và giới hạn demo server.

## Task FE Flutter

1. Dependency: thay Google Maps trong module discovery bằng flutter_map/latlong2 với phiên bản phù hợp Flutter SDK. Kiểm tra mọi import google_maps_flutter trước khi gỡ package; giữ module ngoài phạm vi nếu còn sử dụng. Cập nhật pubspec/lock và platform config liên quan, không xóa key/config của module khác tùy tiện.
2. Model/API: sửa `models/museum.dart`, `models/outdoor_route.dart`, `services/discovery_api_service.dart`, `core/network/api_config.dart`. Giữ ID đúng từ BE, bỏ fallback museumId=1, parse tọa độ nullable thay vì 0,0; giữ metadata phân trang. Parse routePoints, xử lý lỗi HTTP/body không phải JSON, auth hết hạn và rate limit. Bỏ route/city mock fallback.
3. GPS: sửa `services/location_service.dart` và mọi caller để từ chối GPS không biến thành vị trí TP.HCM. Chỉ xin GPS khi Gần tôi hoặc preview. GPS lỗi chỉ vô hiệu chức năng cần vị trí, giữ search/city hoạt động. Giữ quyền Android/iOS phù hợp, không stream theo dõi liên tục.
4. Discovery: sửa `screens/explore/explore_screen.dart`, `museum_discovery_map_screen.dart`, `widgets/interactive_heritage_map.dart`, `museum_discovery_card.dart`, `city_filter_sheet.dart` khi cần. TileLayer/MarkerLayer, vị trí hiện tại, attribution có link; debounce search và chặn response cũ ghi đè filter mới; loading/error/empty/retry và tải trang tiếp. Marker/list dùng cùng dữ liệu đã lọc; fit bounds khi dữ liệu sẵn sàng.
5. Preview/detail: sửa `widgets/route_preview_sheet.dart`, `screens/museum/museum_detail_screen.dart`, bỏ decoder encoded polyline nếu không còn caller. Chỉ DRIVE, vẽ PolylineLayer và fit toàn tuyến; km/phút chỉ từ OSRM. Giữ CTA chính xác “Chỉ đường bằng Google Maps”, Maps URL driving + externalApplication và browser fallback; không cần GPS hay BE để mở đích. Không dùng two-wheeler URL như mode phổ quát.
6. Tests: parsing payload BE, tọa độ thiếu, HTTP lỗi không sinh route giả, GPS bị từ chối, filter/race/pagination, route error vẫn hiện CTA ngoài app. Chạy flutter analyze/test; kiểm tra Android build khi môi trường hỗ trợ. Smoke test GPS/Google Maps trên thiết bị cần thiết bị thật hoặc emulator có dịch vụ tương ứng.

## Quy tắc và rủi ro cần giữ

- Không chỉnh auth nền tảng, indoor navigation, tour hay lịch sử GPS. React quản trị chỉ đổi nếu phát sinh caller cần field cityCode trong phạm vi quản lý museum hiện có; không xây thêm dashboard.
- Tile provider có định danh ứng dụng, attribution, không offline/prefetch hàng loạt; kiểm tra chính sách chính thức và flutter_map docs khi triển khai. OSM public tiles và OSRM demo chỉ dùng thử tải thấp, production cần provider/self-host phù hợp, không có bảo đảm dịch vụ từ demo.
- OSRM chỉ có profile driving đã cấu hình; không giả walking/bicycle bằng driving. Không dùng Google API key/Billing cho preview.
- Quy tắc GET anonymous hiện tại khác tài liệu tổng quan yêu cầu đăng nhập; không mở rộng thay đổi auth trong task này. Cần giữ quyết định truy cập hiện hành và không để museum inactive rò qua luồng Visitor.
- Chạy build/tests phù hợp sau triển khai và ghi kết quả thực tế; không đánh dấu acceptance criteria đã đạt chỉ vì compile thành công.

## Thứ tự thực hiện sau xác nhận

Schema/contract → BE discovery/cities → OSRM route/error/tests → FE model/GPS → FlutterMap/discovery → preview/Google Maps ngoài → kiểm tra tích hợp, tài liệu và log.
