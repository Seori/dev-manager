using DevManager.Core.Entities;
using DevManager.Data;

namespace DevManager.Tests;

public class AppServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "devmanager-svc-" + Guid.NewGuid().ToString("N"));
    private readonly AppService _svc;

    public AppServiceTests()
    {
        _svc = new AppService(new DatabaseSettings { Path = "svc.db" }, _dir);
        _svc.Initialize();
        _svc.SeedAccounts("""[{"LoginId":"boss","Name":"마스터","Password":"short1!","Role":"admin"},{"LoginId":"dev1","Name":"개발자","Password":"short1!","Role":"member"}]""");
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static RequirementInput Input(string title = "제목") =>
        new(title, "내용", 1, 1, null, null, null, null);

    [Fact]
    public void Login_succeeds_and_fails_with_same_message()
    {
        Assert.NotNull(_svc.Login("boss", "short1!").User);
        Assert.Equal(AppService.LoginFailMessage, _svc.Login("boss", "wrong").Error);
        Assert.Equal(AppService.LoginFailMessage, _svc.Login("nobody", "x").Error);
    }

    [Fact]
    public void Register_numbers_documents_sequentially()
    {
        var boss = _svc.Login("boss", "short1!").User!;
        var a = _svc.Register(boss.Id, Input("a")).Result!;
        var b = _svc.Register(boss.Id, Input("b")).Result!;
        var year = DateTime.Now.Year;
        Assert.Equal($"DEV-{year}-0001", a.DocNo);
        Assert.Equal($"DEV-{year}-0002", b.DocNo);
    }

    [Fact]
    public void Register_rejects_missing_fields_and_bad_dates()
    {
        var boss = _svc.Login("boss", "short1!").User!;
        Assert.NotNull(_svc.Register(boss.Id, Input("")).Error);
        var bad = Input() with { Start = new DateOnly(2026, 11, 5), End = new DateOnly(2026, 11, 1) };
        Assert.NotNull(_svc.Register(boss.Id, bad).Error);
    }

    [Fact]
    public void Only_admin_can_assign_and_assignee_gets_notification()
    {
        var boss = _svc.Login("boss", "short1!").User!;
        var dev = _svc.Login("dev1", "short1!").User!;
        var r = _svc.Register(dev.Id, Input()).Result!;

        Assert.NotNull(_svc.Assign(dev.Id, r.Id, dev.Id));
        Assert.Empty(_svc.List(dev.Id, false));

        Assert.Null(_svc.Assign(boss.Id, r.Id, dev.Id));
        Assert.Single(_svc.List(dev.Id, false));
        Assert.True(_svc.List(dev.Id, false)[0].IsNew);
        Assert.Equal(1, _svc.UnreadCount(dev.Id));
        Assert.Equal(0, _svc.UnreadCount(boss.Id));

        _svc.MarkRead(dev.Id, r.Id);
        Assert.Equal(0, _svc.UnreadCount(dev.Id));
    }

    [Fact]
    public void List_shows_assignee_and_confirmation_state()
    {
        var boss = _svc.Login("boss", "short1!").User!;
        var dev = _svc.Login("dev1", "short1!").User!;
        var r = _svc.Register(boss.Id, Input()).Result!;

        Assert.True(_svc.List(boss.Id, true).Single().IsUnassigned);

        _svc.Assign(boss.Id, r.Id, dev.Id);
        var row = _svc.List(boss.Id, true).Single();
        Assert.Equal("개발자", row.AssigneeName);
        Assert.True(row.IsUnconfirmed);

        _svc.MarkRead(dev.Id, r.Id);
        Assert.True(_svc.List(boss.Id, true).Single().IsConfirmed);
    }

    [Fact]
    public void Dev_dates_are_assignee_only_and_deploy_date_is_master_only()
    {
        var boss = _svc.Login("boss", "short1!").User!;
        var dev = _svc.Login("dev1", "short1!").User!;
        var dates = Input() with { Start = new DateOnly(2026, 11, 1), End = new DateOnly(2026, 11, 3), Deploy = new DateOnly(2026, 11, 5) };

        // 등록 시점에는 담당자가 없으므로 개발 일정은 무시되고, 배포일은 마스터만 반영된다.
        var byBoss = _svc.Register(boss.Id, dates).Result!;
        Assert.Null(byBoss.DevStartDate);
        Assert.Equal(new DateOnly(2026, 11, 5), byBoss.DeployDate);
        Assert.Null(_svc.Register(dev.Id, dates).Result!.DeployDate);

        _svc.Assign(boss.Id, byBoss.Id, dev.Id);

        // 마스터는 개발 시작일·종료일을 입력할 수 없다.
        Assert.Null(_svc.Update(boss.Id, byBoss.Id, _svc.Detail(boss.Id, byBoss.Id)!.Version, dates));
        Assert.Null(_svc.Detail(boss.Id, byBoss.Id)!.DevStartDate);

        // 배정받은 사용자는 입력할 수 있다.
        Assert.Null(_svc.Update(dev.Id, byBoss.Id, _svc.Detail(dev.Id, byBoss.Id)!.Version, dates));
        var saved = _svc.Detail(dev.Id, byBoss.Id)!;
        Assert.Equal(new DateOnly(2026, 11, 1), saved.DevStartDate);
        Assert.Equal(new DateOnly(2026, 11, 3), saved.DevEndDate);

        Assert.NotNull(_svc.Register(boss.Id, Input() with { RequesterUserId = 0 }).Error);
    }

    [Fact]
    public void Title_and_category_are_master_only_on_edit()
    {
        var boss = _svc.Login("boss", "short1!").User!;
        var dev = _svc.Login("dev1", "short1!").User!;
        var r = _svc.Register(boss.Id, Input("원래 제목")).Result!;
        _svc.Assign(boss.Id, r.Id, dev.Id);

        var changed = Input("바뀐 제목") with { CategoryId = 2 };
        Assert.Null(_svc.Update(dev.Id, r.Id, _svc.Detail(dev.Id, r.Id)!.Version, changed));
        var afterDev = _svc.Detail(dev.Id, r.Id)!;
        Assert.Equal("원래 제목", afterDev.Title);
        Assert.Equal(1, afterDev.CategoryId);

        Assert.Null(_svc.Update(boss.Id, r.Id, afterDev.Version, changed));
        var afterBoss = _svc.Detail(boss.Id, r.Id)!;
        Assert.Equal("바뀐 제목", afterBoss.Title);
        Assert.Equal(2, afterBoss.CategoryId);
    }

    [Fact]
    public void Assignee_records_completed_modules_and_master_is_notified()
    {
        var boss = _svc.Login("boss", "short1!").User!;
        var dev = _svc.Login("dev1", "short1!").User!;
        var r = _svc.Register(boss.Id, Input()).Result!;
        _svc.Assign(boss.Id, r.Id, dev.Id);

        Assert.NotNull(_svc.Complete(boss.Id, r.Id, _svc.Detail(boss.Id, r.Id)!.Version, "모듈A"));
        Assert.NotNull(_svc.Complete(dev.Id, r.Id, _svc.Detail(dev.Id, r.Id)!.Version, "  "));

        Assert.Null(_svc.Complete(dev.Id, r.Id, _svc.Detail(dev.Id, r.Id)!.Version, "로그인 모듈\n알림 모듈"));
        var done = _svc.Detail(boss.Id, r.Id)!;
        Assert.Equal(DevManager.Core.Entities.RequirementStatus.Done, done.Status);
        Assert.Contains("알림 모듈", done.CompletedModules);
        Assert.NotNull(done.CompletedAt);
        Assert.Equal(1, _svc.UnreadCount(boss.Id));
    }

    [Fact]
    public void Non_admin_cannot_create_users_or_see_others_items()
    {
        var boss = _svc.Login("boss", "short1!").User!;
        var dev = _svc.Login("dev1", "short1!").User!;
        Assert.NotNull(_svc.CreateUser(dev.Id, "x1", "x", null, "abcdefghij1"));
        var r = _svc.Register(boss.Id, Input()).Result!;
        Assert.Null(_svc.Detail(dev.Id, r.Id));
        Assert.NotNull(_svc.Detail(boss.Id, r.Id));
    }
}
