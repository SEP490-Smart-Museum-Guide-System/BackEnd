using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.Commerce;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Services;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
}
static bool Valid(object value) => Validator.TryValidateObject(value, new ValidationContext(value), [], true);
Check(!Valid(new ProductRequest { Name = "Test", Price = -1, ResourceIds = [1] }), "Reject negative prices.");
Check(!Valid(new ProductRequest { Name = "Test", Price = 10, Seats = 0, ResourceIds = [1] }), "Reject zero seats.");
Check(!Valid(new ProductRequest { Name = "Test", Price = 10, Kind = "subscription", ResourceIds = [1] }), "Reject subscription product kind.");
Check(!Valid(new RedeemKeyRequest { Key = "guess" }), "Reject short keys.");
foreach (var name in new[] { "ConfirmPayment", "CreateResource", "UpdateResource", "CreateProduct", "UpdateProduct", "Revoke", "Revenue", "ManageOrder" })
{
    var auth = typeof(CommerceController).GetMethod(name)!.GetCustomAttribute<AuthorizeAttribute>();
    Check(auth?.Roles == RoleNames.Admin + "," + RoleNames.MuseumStaff, name + " requires museum managers.");
}
foreach (var name in new[] { "Create", "Update", "Delete", "RegenerateQr" })
    Check(typeof(ArtifactsController).GetMethod(name)!.IsDefined(typeof(AuthorizeAttribute)) &&
        !typeof(ArtifactsController).GetMethod(name)!.IsDefined(typeof(AllowAnonymousAttribute)), "Artifact mutation cannot bypass premium configuration.");
Console.WriteLine("Commerce validation and authorization checks passed.");
if (!args.Contains("--database")) return;

var api = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SMGS.API"));
var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(api, "appsettings.json"))
    .AddJsonFile(Path.Combine(api, "appsettings.Development.json"), true)
    .AddUserSecrets("smgs-api-local-development").AddEnvironmentVariables().Build();
var connection = Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION") ?? config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Set SMGS_TEST_CONNECTION.");
var schema = "smgs_commerce_test_" + Guid.NewGuid().ToString("N");
var connectionBuilder = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
ApplicationDbContext Open() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionBuilder.ConnectionString).Options);
async Task<T> Run<T>(Func<CommerceService, Task<T>> action)
{
    await using var db = Open(); return await action(new CommerceService(db));
}
async Task Fails(Func<Task> action, int status)
{
    try { await action(); throw new Exception("Expected rejection " + status); }
    catch (CommerceException ex) { Check(ex.StatusCode == status, ex.Message); }
}
await using var setup = Open();
var createSchemaSql = $"CREATE SCHEMA \"{schema}\"";
var dropSchemaSql = $"DROP SCHEMA \"{schema}\" CASCADE";
await setup.Database.ExecuteSqlRawAsync(createSchemaSql);
try
{
    await setup.Database.ExecuteSqlRawAsync(setup.Database.GenerateCreateScript());
    var owner = Guid.NewGuid(); var staff = Guid.NewGuid(); var stranger = Guid.NewGuid();
    var members = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
    setup.Users.AddRange(new[] { owner, staff, stranger }.Concat(members).Select(id =>
        new ApplicationUser { Id = id, UserName = id.ToString(), NormalizedEmail = id + "@TEST" }));
    var museum = new Museum { Name = "Commerce checks" }; setup.Museums.Add(museum); await setup.SaveChangesAsync();
    setup.StaffAssignments.Add(new StaffAssignment { MuseumId = museum.Id, UserId = staff });
    var artifact = new Artifact { MuseumId = museum.Id, Name = "Premium artifact", AccessLevel = "premium" };
    setup.Artifacts.Add(artifact); await setup.SaveChangesAsync();
    var admission = await Run(s => s.ConfigureResource(museum.Id, null, new() { Name = "Entry", Kind = "admission", AccessLevel = "premium" }, staff, false));
    var digital = await Run(s => s.ConfigureResource(museum.Id, null, new() { Name = "Content", ArtifactId = artifact.Id, AccessLevel = "premium" }, staff, false));
    await Fails(async () => await Run(s => s.ConfigureResource(museum.Id, null, new() { Name = "No access" }, stranger, false)), 403);
    await Fails(async () => await Run(s => s.ConfigureProduct(museum.Id, null, new() { Name = "Unconfirmed", Price = 100, ResourceIds = [digital.Id], IsActive = true }, staff, false)), 400);
    await Fails(async () => await Run(s => s.ConfigureProduct(museum.Id, null, new() { Name = "Bad combo", Kind = "combo", Price = 100, ValidityHours = 24, ValidityStartsAt = "payment", ResourceIds = [digital.Id], IsActive = true }, staff, false)), 400);
    var product = await Run(s => s.ConfigureProduct(museum.Id, null, new() { Name = "Group combo", Kind = "combo", Price = 100,
        Seats = 3, ValidityHours = 24, ValidityStartsAt = "payment", ResourceIds = [admission.Id, digital.Id], IsActive = true }, staff, false));
    var order = await Run(s => s.CreateOrder(new() { ProductId = product.Id, BuyerName = "Buyer", BuyerDetails = new() { ["email"] = "buyer@example.test" } }, owner));
    Check(!await Run(s => s.CanReadArtifact(artifact.Id, owner)), "Unpaid order grants no access.");
    var first = await Run(s => s.StartPayment(order.Id, owner));
    Check((await Run(s => s.StartPayment(order.Id, owner))).Id == first.Id, "Repeated pending requests reuse attempt.");
    await Fails(async () => await Run(s => s.ConfirmPayment(first.Id, new() { Succeeded = true, Reference = "unauthorized" }, stranger, false)), 403);
    await Run(s => s.ConfirmPayment(first.Id, new() { FailureReason = "Declined" }, staff, false));
    var second = await Run(s => s.StartPayment(order.Id, owner));
    Check(second.Id != first.Id, "Retry must be a new transaction.");
    await Run(s => s.ConfigureProduct(museum.Id, product.Id, new() { Name = "Changed", Kind = "combo", Price = 999,
        Seats = 6, ValidityHours = 48, ValidityStartsAt = "payment", ResourceIds = [admission.Id, digital.Id], IsActive = true }, staff, false));
    var confirm = new ConfirmPaymentRequest { Succeeded = true, Reference = "manual-test-" + Guid.NewGuid() };
    await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Run(s => s.ConfirmPayment(second.Id, confirm, staff, false))));
    Check((await Run(s => s.Transactions(order.Id, owner))).Count == 2, "Preserve failed and succeeded attempts.");
    var invoice = await Run(s => s.Invoice(order.Id, owner));
    Check(invoice.Amount == 100 && invoice.ProductName == "Group combo" && invoice.Seats == 3, "Invoice uses purchase snapshot.");
    Check(await setup.PurchaseInvoices.CountAsync(i => i.OrderId == order.Id) == 1, "Concurrent confirmations create one invoice.");
    await Fails(async () => await Run(s => s.Invoice(order.Id, stranger)), 404);
    await Fails(async () => await Run(s => s.StartPayment(order.Id, owner)), 409);
    var oldKey = await Run(s => s.IssueGroupKey(order.Id, owner));
    var key = await Run(s => s.IssueGroupKey(order.Id, owner));
    await Fails(async () => await Run(s => s.Redeem(oldKey.Key, members[0])), 404);
    var redeemed = await Task.WhenAll(members.Select(async user =>
    {
        try { await Run(s => s.Redeem(key.Key, user)); return user; }
        catch (CommerceException e) when (e.StatusCode == 409) { return Guid.Empty; }
    }));
    Check(redeemed.Count(id => id != Guid.Empty) == 3, "Concurrent claims cannot exceed paid seats.");
    var member = redeemed.First(id => id != Guid.Empty);
    var pass = await Run(s => s.Redeem(key.Key, member));
    Check(await setup.MuseumPasses.CountAsync(p => p.OrderId == order.Id) == 3, "Repeated claim does not consume seats.");
    Check(await Run(s => s.CanReadArtifact(artifact.Id, member)), "Claimed pass opens purchased content.");
    Check(!await Run(s => s.CanReadArtifact(artifact.Id, stranger)), "Other accounts cannot use purchased content.");
    await using (var db = Open()) { await new CommerceService(db).RevokePass(pass.Id, staff, false); }
    Check(!await Run(s => s.CanReadArtifact(artifact.Id, member)), "Revoked pass cannot read content.");
    await Fails(async () => await Run(s => s.Redeem(key.Key, member)), 403);
    var report = await Run(s => s.Revenue(museum.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), staff, false));
    Check(report.Single() is { Revenue: 100, PaidOrders: 1, FailedAttempts: 1 }, "Revenue counts successful orders once and retains failures.");
    await using (var db = Open())
    {
        var o = await db.PurchaseOrders.SingleAsync(o => o.Id == order.Id); o.ExpiresAt = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
    }
    Check(!await Run(s => s.CanReadArtifact(artifact.Id, redeemed.Last(id => id != Guid.Empty))), "Expired group passes cannot read content.");
    await Run(s => s.ConfigureResource(museum.Id, digital.Id, new() { Name = "Now free", ArtifactId = artifact.Id, AccessLevel = "free" }, staff, false));
    Check(await Run(s => s.CanReadArtifact(artifact.Id, null)), "Free content works anonymously.");
    Console.WriteLine("Commerce PostgreSQL checks passed: retry, snapshots, concurrent payment/key claims, access, revocation, expiry, invoices and revenue.");
}
finally
{
    // Only the randomly named schema created above is removed.
    await setup.Database.ExecuteSqlRawAsync(dropSchemaSql);
}

