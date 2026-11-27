namespace SMGS.Domain.Entities;

public class VisitorHome
{
    public int Id { get; set; }
    public Guid Revision { get; set; }
    public string Title { get; set; } = "";
    public string SlidesJson { get; set; } = "[]";
}
