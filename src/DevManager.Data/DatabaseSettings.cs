namespace DevManager.Data;

// appsettings.json의 "Database" 섹션. DB를 다른 폴더/공유 폴더로 옮길 때는 Path만 바꾼다.
public class DatabaseSettings
{
    public const string SectionName = "Database";

    public string Path { get; set; } = "data/devmanager.db";

    // 공유 폴더에서 다른 PC가 쓰기 중일 때 잠금 해제를 기다리는 시간(초)
    public int BusyTimeoutSeconds { get; set; } = 5;

    public string ResolvePath(string baseDirectory) =>
        System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(Path) ? Path : System.IO.Path.Combine(baseDirectory, Path));

    // WAL은 네트워크 공유에서 안전하지 않으므로 기본 롤백 저널(DELETE) 모드를 쓴다.
    public string BuildConnectionString(string baseDirectory) =>
        $"Data Source={ResolvePath(baseDirectory)};Foreign Keys=True;Default Timeout={BusyTimeoutSeconds}";
}
