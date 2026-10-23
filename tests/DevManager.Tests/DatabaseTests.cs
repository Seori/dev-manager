using DevManager.Core.Entities;
using DevManager.Data;
using Microsoft.EntityFrameworkCore;

namespace DevManager.Tests;

public class DatabaseTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "devmanager-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private DevManagerDbContext NewDb()
    {
        var settings = new DatabaseSettings { Path = Path.Combine(_dir, "sub", "test.db") };
        var db = DatabaseFactory.Create(settings, _dir);
        DatabaseFactory.Migrate(db);
        return db;
    }

    private static (User admin, Category cat, Project project) Seed(DevManagerDbContext db)
    {
        var admin = new User { LoginId = "admin", Name = "관리자", PasswordHash = "x", Role = GlobalRole.Admin };
        var cat = new Category { Name = "기능개선" };
        var project = new Project { Name = "P1" };
        db.AddRange(admin, cat, project);
        db.SaveChanges();
        return (admin, cat, project);
    }

    private static Requirement NewReq(User u, Category c, Project p, string docNo) => new()
    {
        ProjectId = p.Id, DocNo = docNo, Title = "t", Description = "d", CategoryId = c.Id, CreatedById = u.Id
    };

    [Fact]
    public void Migrate_creates_database_at_configured_path()
    {
        using var db = NewDb();
        Assert.True(File.Exists(Path.Combine(_dir, "sub", "test.db")));
    }

    [Fact]
    public void DocNo_must_be_unique()
    {
        using var db = NewDb();
        var (u, c, p) = Seed(db);
        db.Requirements.Add(NewReq(u, c, p, "DEV-2026-0001"));
        db.SaveChanges();
        db.Requirements.Add(NewReq(u, c, p, "DEV-2026-0001"));
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void Concurrent_edit_is_detected_by_version()
    {
        using var db1 = NewDb();
        var (u, c, p) = Seed(db1);
        db1.Requirements.Add(NewReq(u, c, p, "DEV-2026-0002"));
        db1.SaveChanges();

        using var db2 = DatabaseFactory.Create(new DatabaseSettings { Path = Path.Combine(_dir, "sub", "test.db") }, _dir);
        var a = db1.Requirements.Single();
        var b = db2.Requirements.Single();

        a.Title = "first";
        db1.SaveChanges();

        b.Title = "second";
        Assert.Throws<DbUpdateConcurrencyException>(() => db2.SaveChanges());
        Assert.Equal(1, db1.Requirements.Single().Version);
    }

    [Fact]
    public void Soft_deleted_requirement_is_hidden()
    {
        using var db = NewDb();
        var (u, c, p) = Seed(db);
        var r = NewReq(u, c, p, "DEV-2026-0003");
        db.Requirements.Add(r);
        db.SaveChanges();
        r.DeletedAt = DateTime.UtcNow;
        db.SaveChanges();
        Assert.Empty(db.Requirements.ToList());
        Assert.Single(db.Requirements.IgnoreQueryFilters().ToList());
    }
}
