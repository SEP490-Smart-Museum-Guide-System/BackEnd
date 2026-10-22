using SMGS.Domain.Entities;

namespace SMGS.Domain.Constants;

// Provincial codes, Decision 19/2025/QĐ-TTg, Appendix I (effective 01/07/2025).
public static class CityCatalog
{
    public static readonly IReadOnlyList<City> Items = new List<City>
    {
        new() { Code = "01", Name = "Hà Nội" },
        new() { Code = "04", Name = "Cao Bằng" },
        new() { Code = "08", Name = "Tuyên Quang" },
        new() { Code = "11", Name = "Điện Biên" },
        new() { Code = "12", Name = "Lai Châu" },
        new() { Code = "14", Name = "Sơn La" },
        new() { Code = "15", Name = "Lào Cai" },
        new() { Code = "19", Name = "Thái Nguyên" },
        new() { Code = "20", Name = "Lạng Sơn" },
        new() { Code = "22", Name = "Quảng Ninh" },
        new() { Code = "24", Name = "Bắc Ninh" },
        new() { Code = "25", Name = "Phú Thọ" },
        new() { Code = "31", Name = "Hải Phòng" },
        new() { Code = "33", Name = "Hưng Yên" },
        new() { Code = "37", Name = "Ninh Bình" },
        new() { Code = "38", Name = "Thanh Hóa" },
        new() { Code = "40", Name = "Nghệ An" },
        new() { Code = "42", Name = "Hà Tĩnh" },
        new() { Code = "44", Name = "Quảng Trị" },
        new() { Code = "46", Name = "Huế" },
        new() { Code = "48", Name = "Đà Nẵng" },
        new() { Code = "51", Name = "Quảng Ngãi" },
        new() { Code = "52", Name = "Gia Lai" },
        new() { Code = "56", Name = "Khánh Hòa" },
        new() { Code = "66", Name = "Đắk Lắk" },
        new() { Code = "68", Name = "Lâm Đồng" },
        new() { Code = "75", Name = "Đồng Nai" },
        new() { Code = "79", Name = "Hồ Chí Minh" },
        new() { Code = "80", Name = "Tây Ninh" },
        new() { Code = "82", Name = "Đồng Tháp" },
        new() { Code = "86", Name = "Vĩnh Long" },
        new() { Code = "91", Name = "An Giang" },
        new() { Code = "92", Name = "Cần Thơ" },
        new() { Code = "96", Name = "Cà Mau" }
    }.AsReadOnly();

    public static bool IsValid(string? code) => code is null || Items.Any(c => c.Code == code);
}
