using Microsoft.EntityFrameworkCore;

namespace DevManager.Data;

public static class DatabaseFactory
{
    public static DevManagerDbContext Create(DatabaseSettings settings, string baseDirectory)
    {
        var path = settings.ResolvePath(baseDirectory);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var options = new DbContextOptionsBuilder<DevManagerDbContext>()
            .UseSqlite(settings.BuildConnectionString(baseDirectory))
            .Options;
        var db = new DevManagerDbContext(options);
        // EF Core 10은 새 DB를 WAL로 만들 수 있다. 공유 폴더 안전성을 위해 롤백 저널로 되돌린다.
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE;");
        return db;
    }

    // 앱 시작 시 호출. 스키마가 없거나 낮으면 마이그레이션을 적용한다. DB 삭제/재생성은 하지 않는다.
    public static void Migrate(DevManagerDbContext db) => db.Database.Migrate();
}
