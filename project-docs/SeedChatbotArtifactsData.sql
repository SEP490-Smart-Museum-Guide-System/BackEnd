-- Seed dữ liệu Hiện vật & Nội dung thuyết minh chi tiết cho Chatbot (Artifact Guide) & Nhận diện QR/Detect
-- Có thể chạy lại nhiều lần (Idempotent): Tự động thêm mới hoặc cập nhật nội dung tiếng Việt đã xuất bản (Published).
-- Lưu ý: Cần chạy API ít nhất 1 lần trước (hoặc dotnet run --project SMGS.API -- --seed) để có tài khoản mặc định trong bảng "Users".

BEGIN;

DO $$
DECLARE
    v_staff_id uuid;
    v_curator_id uuid;
    v_museum_id integer;
    v_building_id integer;
    v_floor1_id integer;
    v_floor2_id integer;
    v_gallery1_id integer;
    v_gallery2_id integer;
    v_topic_national_id integer;
    v_topic_dongson_id integer;
    v_topic_ceramics_id integer;
    r record;
    v_artifact_id integer;
    v_content_id integer;
BEGIN
    -- 1. Lấy tài khoản nhân viên tạo và Curator duyệt
    SELECT "Id" INTO v_staff_id FROM "Users" WHERE "Email" = 'staff@museum.vn' LIMIT 1;
    IF v_staff_id IS NULL THEN
        SELECT "Id" INTO v_staff_id FROM "Users" ORDER BY "CreatedAt" LIMIT 1;
    END IF;

    SELECT "Id" INTO v_curator_id FROM "Users" WHERE "Email" = 'curator@museum.vn' LIMIT 1;
    IF v_curator_id IS NULL THEN
        v_curator_id := v_staff_id;
    END IF;

    IF v_staff_id IS NULL THEN
        RAISE EXCEPTION 'Chưa có tài khoản trong bảng "Users". Hãy chạy: dotnet run --project Smart_Museum_Guide_System/SMGS.API -- --seed trước.';
    END IF;

    -- 2. Đảm bảo Bảo tàng Lịch sử Quốc gia tồn tại
    SELECT "Id" INTO v_museum_id FROM museums WHERE "Name" = 'Bảo tàng Lịch sử Quốc gia' LIMIT 1;
    IF v_museum_id IS NULL THEN
        INSERT INTO museums ("Name", "Description", "Address", "CityCode", "Latitude", "Longitude", "OpeningHours", "IndoorLayout", "IsActive", "CreatedAt")
        VALUES (
            'Bảo tàng Lịch sử Quốc gia',
            'Bảo tàng lưu giữ và trưng bày các di sản văn hóa, lịch sử tiêu biểu của Việt Nam từ thời tiền sử đến ngày nay.',
            'Số 1 Tràng Tiền, Hoàn Kiếm, Hà Nội',
            '01',
            21.024500,
            105.859500,
            '08:00 - 17:00 (Thứ Ba - Chủ Nhật)',
            'floors',
            TRUE,
            NOW()
        )
        RETURNING "Id" INTO v_museum_id;
    END IF;

    -- Phân công nhân viên bảo tàng nếu chưa có
    IF NOT EXISTS (SELECT 1 FROM staff_assignments WHERE "UserId" = v_staff_id AND "MuseumId" = v_museum_id AND "GalleryId" IS NULL AND "ArtifactId" IS NULL) THEN
        INSERT INTO staff_assignments ("UserId", "MuseumId", "AssignedAt")
        VALUES (v_staff_id, v_museum_id, NOW());
    END IF;

    -- 3. Đảm bảo Building -> Floor -> Gallery tồn tại
    SELECT "Id" INTO v_building_id FROM buildings WHERE "MuseumId" = v_museum_id AND "Name" = 'Tòa nhà Trưng bày Chính' LIMIT 1;
    IF v_building_id IS NULL THEN
        INSERT INTO buildings ("MuseumId", "Name")
        VALUES (v_museum_id, 'Tòa nhà Trưng bày Chính')
        RETURNING "Id" INTO v_building_id;
    END IF;

    SELECT "Id" INTO v_floor1_id FROM floors WHERE "BuildingId" = v_building_id AND "LevelNo" = 1 LIMIT 1;
    IF v_floor1_id IS NULL THEN
        INSERT INTO floors ("BuildingId", "LevelNo", "Name")
        VALUES (v_building_id, 1, 'Tầng 1 - Thời kỳ Tiền sử & Sơ sử')
        RETURNING "Id" INTO v_floor1_id;
    END IF;

    SELECT "Id" INTO v_floor2_id FROM floors WHERE "BuildingId" = v_building_id AND "LevelNo" = 2 LIMIT 1;
    IF v_floor2_id IS NULL THEN
        INSERT INTO floors ("BuildingId", "LevelNo", "Name")
        VALUES (v_building_id, 2, 'Tầng 2 - Thời kỳ Phong kiến')
        RETURNING "Id" INTO v_floor2_id;
    END IF;

    SELECT "Id" INTO v_gallery1_id FROM galleries WHERE "FloorId" = v_floor1_id AND "Name" = 'Phòng Văn hóa Đông Sơn' LIMIT 1;
    IF v_gallery1_id IS NULL THEN
        INSERT INTO galleries ("FloorId", "Name", "Description")
        VALUES (v_floor1_id, 'Phòng Văn hóa Đông Sơn', 'Không gian trưng bày trống đồng và hiện vật tiêu biểu nền văn hóa Đông Sơn.')
        RETURNING "Id" INTO v_gallery1_id;
    END IF;

    SELECT "Id" INTO v_gallery2_id FROM galleries WHERE "FloorId" = v_floor2_id AND "Name" = 'Phòng Triều đại Lý - Trần' LIMIT 1;
    IF v_gallery2_id IS NULL THEN
        INSERT INTO galleries ("FloorId", "Name", "Description")
        VALUES (v_floor2_id, 'Phòng Triều đại Lý - Trần', 'Không gian trưng bày gốm sứ, kiến trúc và cổ vật thời Lý - Trần.')
        RETURNING "Id" INTO v_gallery2_id;
    END IF;

    -- 4. Đảm bảo Chủ đề (Topics) tồn tại
    SELECT "Id" INTO v_topic_national_id FROM topics WHERE "MuseumId" = v_museum_id AND "Name" = 'Bảo vật Quốc gia' LIMIT 1;
    IF v_topic_national_id IS NULL THEN
        INSERT INTO topics ("MuseumId", "Name", "Description", "IsActive", "CreatedAt")
        VALUES (v_museum_id, 'Bảo vật Quốc gia', 'Các hiện vật đặc biệt quý hiếm được công nhận là Bảo vật Quốc gia.', TRUE, NOW())
        RETURNING "Id" INTO v_topic_national_id;
    END IF;

    SELECT "Id" INTO v_topic_dongson_id FROM topics WHERE "MuseumId" = v_museum_id AND "Name" = 'Văn hóa Đông Sơn' LIMIT 1;
    IF v_topic_dongson_id IS NULL THEN
        INSERT INTO topics ("MuseumId", "Name", "Description", "IsActive", "CreatedAt")
        VALUES (v_museum_id, 'Văn hóa Đông Sơn', 'Hiện vật thuộc nền văn minh sông Hồng thời đại đồ đồng.', TRUE, NOW())
        RETURNING "Id" INTO v_topic_dongson_id;
    END IF;

    SELECT "Id" INTO v_topic_ceramics_id FROM topics WHERE "MuseumId" = v_museum_id AND "Name" = 'Gốm sứ cổ' LIMIT 1;
    IF v_topic_ceramics_id IS NULL THEN
        INSERT INTO topics ("MuseumId", "Name", "Description", "IsActive", "CreatedAt")
        VALUES (v_museum_id, 'Gốm sứ cổ', 'Bộ sưu tập gốm men ngọc, men hoa nâu qua các triều đại.', TRUE, NOW())
        RETURNING "Id" INTO v_topic_ceramics_id;
    END IF;

    -- 5. Upsert 8 Hiện vật & Nội dung tiếng Việt đã xuất bản (Published) cho Chatbot
    FOR r IN
        SELECT * FROM (VALUES
            (
                'HV-001',
                'Trống đồng Ngọc Lũ',
                'SMGS-NGOCLU001',
                'free',
                v_gallery1_id,
                'Trống đồng Ngọc Lũ - Bảo vật Quốc gia thời kỳ Đông Sơn',
                'Trống đồng Ngọc Lũ thuộc nền Văn hóa Đông Sơn, có niên đại khoảng 2.500 năm cách ngày nay (thế kỷ VII - VI trước Công nguyên). Hiện vật được chế tác bằng chất liệu đồng thau, có đường kính mặt trống 79 cm, chiều cao 63 cm và nặng 86 kg. Trống được phát hiện vào năm 1893 tại xã Như Trác, huyện Nam Xang (nay thuộc huyện Bình Lục, tỉnh Hà Nam) và được công nhận là Bảo vật Quốc gia đợt 1 năm 2012. Trống gồm ba phần rõ rệt: tang trống phình rộng, thân trống hình trụ thẳng và chân trống loe hình nón cụt, có bốn đôi quai kép trang trí văn bện thừng.',
                'Xin chào quý khách. Trước mắt quý khách là Trống đồng Ngọc Lũ, biểu tượng đỉnh cao của nghệ thuật đúc đồng thời kỳ Văn hóa Đông Sơn cách đây khoảng 2.500 năm. Chính giữa mặt trống là hình ngôi sao nổi 14 cánh tượng trưng cho mặt trời. Xung quanh ngôi sao là 16 vòng hoa văn đồng tâm, nổi bật với cảnh sinh hoạt của cư dân Việt cổ như giã gạo, múa hóa trang lông chim, đánh trống đồng, đua thuyền, cùng hình đàn hươu và chim Lạc bay ngược chiều kim đồng hồ. Trống đồng Ngọc Lũ được dùng làm nhạc khí trong các nghi lễ cầu mùa, lễ hội cộng đồng và là biểu tượng quyền lực của thủ lĩnh bộ lạc thời Hùng Vương dựng nước.'
            ),
            (
                'HV-002',
                'Thạp đồng Đào Thịnh',
                'SMGS-DAOTHINH002',
                'free',
                v_gallery1_id,
                'Thạp đồng Đào Thịnh - Bảo vật Quốc gia Văn hóa Đông Sơn',
                'Thạp đồng Đào Thịnh thuộc nền Văn hóa Đông Sơn, có niên đại khoảng 2.500 - 2.000 năm cách ngày nay (thế kỷ V - III trước Công nguyên). Hiện vật làm bằng đồng thau, có kích thước lớn nhất trong các thạp đồng Đông Sơn từng được tìm thấy với chiều cao 98 cm, đường kính miệng 61 cm, đường kính đáy 60 cm và nặng 74 kg. Thạp được người dân phát hiện vào ngày 14 tháng 9 năm 1961 bên bờ sông Hồng thuộc xã Đào Thịnh, huyện Trấn Yên, tỉnh Yên Bái. Hiện vật được công nhận là Bảo vật Quốc gia đợt 1 năm 2012.',
                'Xin mời quý khách chiêm ngưỡng Thạp đồng Đào Thịnh. Điểm độc đáo nhất của chiếc thạp nằm ở phần nắp: chính giữa là ngôi sao 12 cánh, xung quanh rìa nắp đúc nổi bốn cặp tượng nam nữ trong tư thế giao phối, thể hiện tín ngưỡng phồn thực cầu mong sự sinh sôi nảy nở của cư dân nông nghiệp trồng lúa nước. Trên thân thạp khắc họa 6 chiếc thuyền nối đuôi nhau cùng hình chim Lạc và vũ công hóa trang lông chim. Thạp ban đầu dùng để đựng lương thực, thóc giống hoặc rượu trong nghi lễ, và khi phát hiện có chứa di cốt cùng một thạp nhỏ bên trong, cho thấy nó còn được dùng làm quan tài táng người quyền quý.'
            ),
            (
                'HV-003',
                'Tượng đồng người cõng nhau thổi khèn',
                'SMGS-TUONGDONG003',
                'free',
                v_gallery1_id,
                'Tượng đồng người cõng nhau thổi khèn',
                'Tượng đồng người cõng nhau thổi khèn là hiện vật điêu khắc tròn thuộc nền Văn hóa Đông Sơn, có niên đại khoảng 2.500 - 2.000 năm cách ngày nay. Tượng được chế tác bằng đồng thau với chiều cao khoảng 9,5 cm, được tìm thấy tại di chỉ khảo cổ Đông Sơn (tỉnh Thanh Hóa). Hiện vật mô tả sinh động hình ảnh hai người đàn ông mặc khố, đầu vấn khăn: người dưới khỏe mạnh cõng người trên lưng và bước đi nhịp nhàng theo điệu múa, trong khi người ngồi trên ngả lưng ra sau và hai tay nâng cây khèn bầu lên miệng thổi.',
                'Tượng đồng người cõng nhau thổi khèn là minh chứng sống động cho đời sống tinh thần và nghệ thuật âm nhạc phong phú của người Việt cổ thời Đông Sơn. Cây khèn bầu trên tay nhân vật có cấu tạo giống với khèn của nhiều dân tộc vùng núi phía Bắc và Tây Nguyên ngày nay. Tác phẩm thường được gắn làm phần trang trí trên cán dao găm hoặc đồ dùng nghi lễ của tầng lớp quý tộc, thể hiện không khí lễ hội vui tươi sau mùa thu hoạch.'
            ),
            (
                'HV-004',
                'Đèn đồng hình người quỳ',
                'SMGS-DENDONG004',
                'free',
                v_gallery1_id,
                'Đèn đồng hình người quỳ - Bảo vật Quốc gia Lạch Trường',
                'Đèn đồng hình người quỳ (còn gọi là cây đèn hình người quỳ Lạch Trường) có niên đại khoảng 2.000 - 1.700 năm cách ngày nay (thế kỷ I - III sau Công nguyên), thuộc giai đoạn muộn của Văn hóa Đông Sơn tiếp xúc văn hóa Hán. Đèn được làm bằng chất liệu đồng thau, cao 40 cm, rộng 27 cm, nặng khoảng 1,8 kg. Hiện vật được nhà khảo cổ Olov Janse phát hiện năm 1935 trong ngôi mộ gạch số 3 tại Lạch Trường, huyện Hoằng Hóa, tỉnh Thanh Hóa và được công nhận là Bảo vật Quốc gia năm 2012.',
                'Trước mắt quý khách là cây Đèn đồng hình người quỳ nổi tiếng. Phần thân đèn là tượng một người đàn ông cởi trần, đóng khố, quỳ hai gối trên đế vuông, trên lưng và hai vai có gắn ba nhánh đèn hình chữ S đỡ các đĩa đựng dầu thắp sáng. Trên các nhánh đèn có đúc nổi hình 10 nhạc công đang thổi sáo và 4 con linh thú có cánh. Hiện vật dùng để thắp sáng trong không gian nghi lễ và tùy táng trong mộ quý tộc, thể hiện kỹ thuật đúc đồng ghép khối vô cùng tinh xảo.'
            ),
            (
                'HV-005',
                'Đầu rồng đất nung thời Lý',
                'SMGS-DAURONG005',
                'free',
                v_gallery2_id,
                'Đầu rồng đất nung thời Lý - Kiến trúc Hoàng thành Thăng Long',
                'Đầu rồng đất nung thời Lý có niên đại thế kỷ XI - XII (triều đại nhà Lý, từ năm 1009 đến năm 1225). Hiện vật được chế tác bằng chất liệu đất nung màu đỏ gạch độ bền cao, chiều cao khoảng 55 cm, chiều rộng 38 cm, được tìm thấy tại khu di tích Hoàng thành Thăng Long (Hà Nội). Đầu rồng là cấu kiện trang trí kiến trúc gắn trên bờ nóc và góc mái của các cung điện, chùa tháp thời Lý.',
                'Đầu rồng đất nung thời Lý mang đầy đủ những nét đặc trưng nhất của hình tượng rồng thời Lý: thân uốn lượn mềm mại, đầu ngẩng cao, bờm bay ngược ra sau như những làn sóng, miệng há rộng ngậm viên ngọc quý (minh châu), mào lửa vươn cao trên mũi và trán có khắc chữ Vương. Khác với vẻ dữ tợn của rồng phong kiến phương Bắc, rồng thời Lý có nét thanh thoát, hiền hòa, gắn liền với tín ngưỡng cầu mưa của cư dân nông nghiệp và tinh thần từ bi của Phật giáo thời Lý.'
            ),
            (
                'HV-006',
                'Thạp gốm hoa nâu thời Trần',
                'SMGS-THAPGOM006',
                'free',
                v_gallery2_id,
                'Thạp gốm hoa nâu thời Trần',
                'Thạp gốm hoa nâu thời Trần có niên đại thế kỷ XIII - XIV (triều đại nhà Trần, từ năm 1225 đến năm 1400). Hiện vật được chế tác bằng gốm sành phủ men trắng ngà và vẽ hoa văn màu nâu sắt (oxit sắt), chiều cao 64 cm, đường kính miệng 34 cm, được phát hiện tại khu vực Nam Định - quê hương của vương triều Trần. Gốm hoa nâu là dòng gốm độc đáo chỉ có ở Việt Nam vào thời Lý - Trần.',
                'Thạp gốm hoa nâu thời Trần có dáng hình trụ khỏe khoắn, vai phình nhẹ với 6 tai gắn ngang trên vai. Nghệ nhân thời Trần đã dùng kỹ thuật khắc chìm đường viền rồi tô màu nâu sắt lên nền men trắng trước khi nung ở nhiệt độ khoảng 1.200 độ C. Hoa văn trên thân thạp thể hiện hình các chiến binh cầm giáo và khiên tập võ nghệ xen kẽ họa tiết hoa sen dây cách điệu, phản ánh hào khí Đông A thượng võ của dân tộc Đại Việt trong ba lần kháng chiến chống quân Nguyên Mông. Thạp được dùng để đựng nước sạch, rượu quý hoặc vật phẩm trong cung đình và chùa chiền.'
            ),
            (
                'HV-007',
                'Chuông đồng Vân Bản',
                'SMGS-CHUONGVANBAN007',
                'premium',
                v_gallery2_id,
                'Chuông đồng Vân Bản - Bảo vật Quốc gia thời Trần',
                'Chuông đồng Vân Bản (còn gọi là chuông chùa Vân Bản) có niên đại từ thời nhà Trần (thế kỷ XIII - XIV). Chuông được đúc bằng đồng thau, cao 125 cm, đường kính miệng 80 cm và nặng khoảng 300 kg. Hiện vật được ngư dân phát hiện năm 1958 dưới bãi biển Đồ Sơn (thành phố Hải Phòng), nơi xưa kia từng có tháp Tường Long và chùa Vân Bản, và được công nhận là Bảo vật Quốc gia năm 2013.',
                'Đây là nội dung thuyết minh chuyên sâu (Premium) về Chuông đồng Vân Bản. Quai chuông được tạo hình đôi rồng đấu lưng vào nhau (lưỡng long) với thân mập khỏe, chân có bốn móng sắc nhọn đặc trưng mỹ thuật thời Trần. Trên thân chuông chia làm bốn ô lớn, khắc bài minh văn chữ Hán và các băng hoa văn cánh sen kép tinh xảo. Chuông Vân Bản không chỉ là pháp khí Phật giáo quan trọng của vùng biển Đông Bắc thời Trần mà còn là tư liệu quý giá về nghệ thuật đúc đồng và lịch sử Phật giáo Trúc Lâm Yên Tử.'
            ),
            (
                'HV-008',
                'Ấn vàng Sắc mệnh chi bảo',
                'SMGS-ANVANG008',
                'premium',
                v_gallery2_id,
                'Ấn vàng Sắc mệnh chi bảo - Bảo vật Quốc gia triều Nguyễn',
                'Ấn vàng Sắc mệnh chi bảo được đúc vào tháng 10 năm Minh Mạng thứ 8 (năm 1827) dưới triều đại nhà Nguyễn. Ấn được chế tác bằng vàng ròng nguyên chất (vàng 10 tuổi), có chiều cao 11 cm, mặt ấn hình vuông kích thước 14 cm x 14 cm, dày 2,5 cm và nặng 8,3 kg (tương đương khoảng 221 lượng vàng), trở thành chiếc kim bảo lớn nhất và nặng nhất của vương triều Nguyễn. Hiện vật được công nhận là Bảo vật Quốc gia năm 2015.',
                'Chào mừng quý khách đến với nội dung thuyết minh chuyên sâu (Premium) về Ấn vàng Sắc mệnh chi bảo. Quai ấn được đúc thành hình rồng uốn khúc năm móng đầu ngẩng cao uy nghiêm, đuôi xòe 9 dải lửa. Mặt dưới của ấn khắc nổi bốn chữ Hán Sắc mệnh chi bảo, còn mặt lưng khắc năm đúc 1827 cùng trọng lượng của ấn. Chiếc bảo ấn này được các hoàng đế triều Nguyễn sử dụng để đóng trên các đạo sắc phong thần, phong tước cho các bậc công thần, tướng lĩnh văn võ và ban cấp sắc mệnh trọng đại của quốc gia.'
            )
        ) AS t(code, name, qr_code, access_level, gallery_id, title, description, narration_text)
    LOOP
        SELECT "Id" INTO v_artifact_id FROM artifacts WHERE "MuseumId" = v_museum_id AND "Code" = r.code LIMIT 1;
        IF v_artifact_id IS NULL THEN
            INSERT INTO artifacts ("MuseumId", "GalleryId", "Code", "Name", "QrCode", "AccessLevel", "IsActive", "CreatedAt")
            VALUES (v_museum_id, r.gallery_id, r.code, r.name, r.qr_code, r.access_level, TRUE, NOW())
            RETURNING "Id" INTO v_artifact_id;
        ELSE
            UPDATE artifacts
            SET "Name" = r.name,
                "QrCode" = r.qr_code,
                "AccessLevel" = r.access_level,
                "GalleryId" = COALESCE("GalleryId", r.gallery_id),
                "IsActive" = TRUE
            WHERE "Id" = v_artifact_id;
        END IF;

        -- Gán chủ đề như DataSeeder.cs để cá nhân hóa có metadata nhất quán.
        IF r.code IN ('HV-001', 'HV-002', 'HV-004', 'HV-007', 'HV-008') THEN
            INSERT INTO artifact_topics ("ArtifactId", "TopicId", "AssignedAt")
            VALUES (v_artifact_id, v_topic_national_id, NOW()) ON CONFLICT DO NOTHING;
        END IF;
        IF r.code IN ('HV-001', 'HV-002', 'HV-003', 'HV-004') THEN
            INSERT INTO artifact_topics ("ArtifactId", "TopicId", "AssignedAt")
            VALUES (v_artifact_id, v_topic_dongson_id, NOW()) ON CONFLICT DO NOTHING;
        END IF;
        IF r.code IN ('HV-005', 'HV-006') THEN
            INSERT INTO artifact_topics ("ArtifactId", "TopicId", "AssignedAt")
            VALUES (v_artifact_id, v_topic_ceramics_id, NOW()) ON CONFLICT DO NOTHING;
        END IF;

        -- Đảm bảo bản ghi artifact_contents tiếng Việt ('vi') tồn tại
        SELECT "Id" INTO v_content_id FROM artifact_contents WHERE "ArtifactId" = v_artifact_id AND "LanguageCode" = 'vi' LIMIT 1;
        IF v_content_id IS NULL THEN
            INSERT INTO artifact_contents ("ArtifactId", "LanguageCode", "Revision")
            VALUES (v_artifact_id, 'vi', gen_random_uuid())
            RETURNING "Id" INTO v_content_id;
        END IF;

        -- Nếu đã có phiên bản tiếng Việt hiện hành (IsCurrent = TRUE), cập nhật nội dung chi tiết; nếu chưa có thì thêm mới hoặc nâng cấp bản v1
        IF EXISTS (SELECT 1 FROM artifact_content_versions WHERE "ArtifactContentId" = v_content_id AND "IsCurrent" = TRUE) THEN
            UPDATE artifact_content_versions
            SET "Title" = r.title,
                "Description" = r.description,
                "NarrationText" = r.narration_text,
                "Status" = 'published',
                "ReviewedBy" = COALESCE("ReviewedBy", v_curator_id),
                "ReviewedAt" = COALESCE("ReviewedAt", NOW()),
                "PublishedAt" = COALESCE("PublishedAt", NOW())
            WHERE "ArtifactContentId" = v_content_id AND "IsCurrent" = TRUE;
        ELSIF EXISTS (SELECT 1 FROM artifact_content_versions WHERE "ArtifactContentId" = v_content_id AND "VersionNo" = 1) THEN
            UPDATE artifact_content_versions
            SET "Title" = r.title,
                "Description" = r.description,
                "NarrationText" = r.narration_text,
                "Status" = 'published',
                "IsCurrent" = TRUE,
                "ReviewedBy" = v_curator_id,
                "ReviewedAt" = NOW(),
                "PublishedAt" = NOW(),
                "ReviewNote" = 'Nội dung thuyết minh chuẩn xác phục vụ Chatbot.'
            WHERE "ArtifactContentId" = v_content_id AND "VersionNo" = 1;
        ELSE
            INSERT INTO artifact_content_versions (
                "ArtifactContentId", "VersionNo", "Title", "Description", "NarrationText",
                "IsAiGenerated", "Status", "IsCurrent", "CreatedBy", "ReviewedBy", "ReviewedAt", "PublishedAt", "ReviewNote", "CreatedAt"
            )
            VALUES (
                v_content_id, 1, r.title, r.description, r.narration_text,
                FALSE, 'published', TRUE, v_staff_id, v_curator_id, NOW(), NOW(),
                'Nội dung thuyết minh chuẩn xác phục vụ Chatbot.', NOW()
            );
        END IF;
    END LOOP;
END $$;

COMMIT;

