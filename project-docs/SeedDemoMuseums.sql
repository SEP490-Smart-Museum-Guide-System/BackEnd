-- Fictional museums in central HCMC for discovery/GPS testing, not real venues.
-- Repeatable: existing museums with these names are left unchanged.
BEGIN;
INSERT INTO museums ("Name", "Description", "Address", "CityCode", "Latitude", "Longitude", "IsActive", "CreatedAt")
SELECT v.name, 'Dữ liệu giả lập để kiểm thử tìm kiếm, GPS và chỉ đường; không phải bảo tàng thật.',
       v.address, '79', v.latitude, v.longitude, TRUE, NOW()
FROM (VALUES
  ('Bảo tàng mẫu Sài Gòn A', 'Điểm thử A, khu vực trung tâm TP. Hồ Chí Minh', 10.779800, 106.699000),
  ('Bảo tàng mẫu Sài Gòn B', 'Điểm thử B, khu vực trung tâm TP. Hồ Chí Minh', 10.770000, 106.704000)
) AS v(name, address, latitude, longitude)
WHERE NOT EXISTS (SELECT 1 FROM museums m WHERE m."Name" = v.name);
COMMIT;
