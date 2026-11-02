START TRANSACTION;

ALTER TABLE museums ADD "CityCode" character varying(2);

CREATE TABLE cities (
    "Code" character varying(2) NOT NULL,
    "Name" character varying(100) NOT NULL,
    CONSTRAINT "PK_cities" PRIMARY KEY ("Code")
);

INSERT INTO cities ("Code", "Name")
VALUES ('01', 'Hà Nội');
INSERT INTO cities ("Code", "Name")
VALUES ('04', 'Cao Bằng');
INSERT INTO cities ("Code", "Name")
VALUES ('08', 'Tuyên Quang');
INSERT INTO cities ("Code", "Name")
VALUES ('11', 'Điện Biên');
INSERT INTO cities ("Code", "Name")
VALUES ('12', 'Lai Châu');
INSERT INTO cities ("Code", "Name")
VALUES ('14', 'Sơn La');
INSERT INTO cities ("Code", "Name")
VALUES ('15', 'Lào Cai');
INSERT INTO cities ("Code", "Name")
VALUES ('19', 'Thái Nguyên');
INSERT INTO cities ("Code", "Name")
VALUES ('20', 'Lạng Sơn');
INSERT INTO cities ("Code", "Name")
VALUES ('22', 'Quảng Ninh');
INSERT INTO cities ("Code", "Name")
VALUES ('24', 'Bắc Ninh');
INSERT INTO cities ("Code", "Name")
VALUES ('25', 'Phú Thọ');
INSERT INTO cities ("Code", "Name")
VALUES ('31', 'Hải Phòng');
INSERT INTO cities ("Code", "Name")
VALUES ('33', 'Hưng Yên');
INSERT INTO cities ("Code", "Name")
VALUES ('37', 'Ninh Bình');
INSERT INTO cities ("Code", "Name")
VALUES ('38', 'Thanh Hóa');
INSERT INTO cities ("Code", "Name")
VALUES ('40', 'Nghệ An');
INSERT INTO cities ("Code", "Name")
VALUES ('42', 'Hà Tĩnh');
INSERT INTO cities ("Code", "Name")
VALUES ('44', 'Quảng Trị');
INSERT INTO cities ("Code", "Name")
VALUES ('46', 'Huế');
INSERT INTO cities ("Code", "Name")
VALUES ('48', 'Đà Nẵng');
INSERT INTO cities ("Code", "Name")
VALUES ('51', 'Quảng Ngãi');
INSERT INTO cities ("Code", "Name")
VALUES ('52', 'Gia Lai');
INSERT INTO cities ("Code", "Name")
VALUES ('56', 'Khánh Hòa');
INSERT INTO cities ("Code", "Name")
VALUES ('66', 'Đắk Lắk');
INSERT INTO cities ("Code", "Name")
VALUES ('68', 'Lâm Đồng');
INSERT INTO cities ("Code", "Name")
VALUES ('75', 'Đồng Nai');
INSERT INTO cities ("Code", "Name")
VALUES ('79', 'Hồ Chí Minh');
INSERT INTO cities ("Code", "Name")
VALUES ('80', 'Tây Ninh');
INSERT INTO cities ("Code", "Name")
VALUES ('82', 'Đồng Tháp');
INSERT INTO cities ("Code", "Name")
VALUES ('86', 'Vĩnh Long');
INSERT INTO cities ("Code", "Name")
VALUES ('91', 'An Giang');
INSERT INTO cities ("Code", "Name")
VALUES ('92', 'Cần Thơ');
INSERT INTO cities ("Code", "Name")
VALUES ('96', 'Cà Mau');

CREATE INDEX "IX_museums_CityCode" ON museums ("CityCode");

ALTER TABLE museums ADD CONSTRAINT "FK_museums_cities_CityCode" FOREIGN KEY ("CityCode") REFERENCES cities ("Code") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261021093939_AddMuseumDiscoveryCities', '8.0.0');

COMMIT;

