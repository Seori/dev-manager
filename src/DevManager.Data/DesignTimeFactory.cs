using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DevManager.Data;

// dotnet ef 마이그레이션 생성 전용. 런타임 접속 정보는 앱의 appsettings.json을 사용한다.
public class DesignTimeFactory : IDesignTimeDbContextFactory<DevManagerDbContext>
{
    public DevManagerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DevManagerDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new DevManagerDbContext(options);
    }
}
