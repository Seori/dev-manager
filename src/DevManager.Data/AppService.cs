using System.Text.Json;
using DevManager.Core;
using DevManager.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevManager.Data;

// 담당자 확인 상태: 미배정 / 미확인(배정 알림을 아직 안 열어봄) / 확인
public record RequirementRow(
    int Id, string DocNo, string Title, bool IsNew,
    string AssigneeName, bool IsUnassigned, bool IsUnconfirmed, bool IsConfirmed);

public record SeedAccount(string LoginId, string Name, string Password, string Role);

public record Option(int Id, string Name)
{
    public override string ToString() => Name;
}

public record RequirementInput(
    string Title, string Description, int CategoryId, int RequesterUserId,
    DateOnly? Due, DateOnly? Start, DateOnly? End, DateOnly? Deploy);

// 모든 권한 검사는 이 서비스에서 한다. 화면(ViewModel)은 판단하지 않는다.
public class AppService(DatabaseSettings settings, string baseDir)
{
    public const string LoginFailMessage = "아이디 또는 비밀번호가 올바르지 않습니다";

    private DevManagerDbContext Open() => DatabaseFactory.Create(settings, baseDir);

    public void Initialize()
    {
        using var db = Open();
        DatabaseFactory.Migrate(db);
        SeedCodes(db);
        if (!db.Projects.Any()) db.Projects.Add(new Project { Name = "기본 프로젝트", Status = ProjectStatus.InProgress });
        db.SaveChanges();
    }

    // 카테고리·직급은 코드값으로 관리한다. 코드가 없는 기존 행은 삭제하지 않고 비활성화한다.
    private static void SeedCodes(DevManagerDbContext db)
    {
        string[] cats = ["차세대 통합 플랫폼", "AI 업무 자동화", "데이터 허브 구축", "클라우드 전환", "보안 통합 관제"];
        for (var i = 0; i < cats.Length; i++)
        {
            var code = $"CAT_{(char)('A' + i)}";
            var c = db.Categories.FirstOrDefault(x => x.Code == code) ?? db.Categories.FirstOrDefault(x => x.Name == cats[i]);
            if (c == null) { c = new Category(); db.Categories.Add(c); }
            c.Code = code; c.Name = cats[i]; c.SortOrder = i; c.IsActive = true;
        }
        foreach (var old in db.Categories.Where(x => x.Code == null)) old.IsActive = false;

        string[] titles = ["사원", "주임", "대리", "과장", "차장", "부장", "이사", "상무", "전무", "대표이사"];
        for (var i = 0; i < titles.Length; i++)
        {
            var code = $"JT{i + 1:00}";
            var t = db.JobTitles.FirstOrDefault(x => x.Code == code) ?? db.JobTitles.FirstOrDefault(x => x.Name == titles[i]);
            if (t == null) { t = new JobTitle(); db.JobTitles.Add(t); }
            t.Code = code; t.Name = titles[i]; t.SortOrder = i; t.IsActive = true;
        }
        foreach (var old in db.JobTitles.Where(x => x.Code == null)) old.IsActive = false;
        db.SaveChanges();
    }

    public bool HasAdmin()
    {
        using var db = Open();
        return db.Users.Any(u => u.Role == GlobalRole.Admin);
    }

    public List<Option> JobTitles()
    {
        using var db = Open();
        return db.JobTitles.Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => new Option(x.Id, x.Name)).ToList();
    }

    public List<Option> Categories()
    {
        using var db = Open();
        return db.Categories.Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => new Option(x.Id, x.Name)).ToList();
    }

    public List<Option> ActiveUsers()
    {
        using var db = Open();
        return db.Users.Include(u => u.JobTitle).Where(u => u.IsActive).OrderBy(u => u.Name)
            .Select(u => new Option(u.Id, u.JobTitle != null ? u.Name + " " + u.JobTitle.Name : u.Name)).ToList();
    }

    public string? CreateAdmin(string loginId, string name, int? jobTitleId, string password, string confirm)
    {
        using var db = Open();
        if (db.Users.Any(u => u.Role == GlobalRole.Admin)) return "이미 관리자 계정이 있습니다";
        return AddUser(db, loginId, name, jobTitleId, password, confirm, GlobalRole.Admin);
    }

    public string? CreateUser(int actorId, string loginId, string name, int? jobTitleId, string password)
    {
        using var db = Open();
        if (!IsAdmin(db, actorId)) return "권한이 없습니다";
        return AddUser(db, loginId, name, jobTitleId, password, password, GlobalRole.Member);
    }

    private static string? AddUser(DevManagerDbContext db, string loginId, string name, int? jobTitleId, string pw, string confirm, GlobalRole role)
    {
        loginId = loginId.Trim();
        name = name.Trim();
        if (loginId.Length == 0 || name.Length == 0) return "아이디와 이름을 입력하세요";
        if (pw != confirm) return "비밀번호 확인이 일치하지 않습니다";
        if (PasswordHasher.Validate(pw, loginId) is { } policy) return policy;
        if (db.Users.Any(u => u.LoginId == loginId)) return "이미 사용 중인 아이디입니다";
        db.Users.Add(new User
        {
            LoginId = loginId, Name = name, JobTitleId = jobTitleId, Role = role,
            PasswordHash = PasswordHasher.Hash(pw)
        });
        db.SaveChanges();
        return null;
    }

    // 소유자가 지정한 초기 계정을 로컬 파일(seed.local.json)에서 한 번만 만든다. 비밀번호 정책 검사는 생략하며, 처리 후 호출 측이 파일을 지운다.
    public int SeedAccounts(string json)
    {
        using var db = Open();
        var created = 0;
        foreach (var s in JsonSerializer.Deserialize<List<SeedAccount>>(json) ?? [])
        {
            if (db.Users.Any(u => u.LoginId == s.LoginId)) continue;
            db.Users.Add(new User
            {
                LoginId = s.LoginId, Name = s.Name, PasswordHash = PasswordHasher.Hash(s.Password),
                Role = s.Role == "admin" ? GlobalRole.Admin : GlobalRole.Member
            });
            created++;
        }
        db.SaveChanges();
        return created;
    }

    public (User? User, string? Error) Login(string loginId, string password)
    {
        using var db = Open();
        var u = db.Users.Include(x => x.JobTitle).FirstOrDefault(x => x.LoginId == loginId.Trim());
        if (u == null || !u.IsActive) return (null, LoginFailMessage);
        if (u.LockedUntil > DateTime.UtcNow) return (null, "로그인 시도가 너무 많습니다. 잠시 후 다시 시도하세요");
        if (!PasswordHasher.Verify(password, u.PasswordHash))
        {
            u.FailedCount++;
            if (u.FailedCount >= 5) { u.LockedUntil = DateTime.UtcNow.AddMinutes(15); u.FailedCount = 0; }
            db.SaveChanges();
            return (null, LoginFailMessage);
        }
        u.FailedCount = 0;
        u.LockedUntil = null;
        u.LastLoginAt = DateTime.UtcNow;
        db.SaveChanges();
        return (u, null);
    }

    public (Requirement? Result, string? Error) Register(int actorId, RequirementInput input)
    {
        if (Validate(input) is { } err) return (null, err);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var db = Open();
            var year = DateTime.Now.Year;
            var seq = db.DocSequences.Find(year);
            if (seq == null) { seq = new DocSequence { Year = year }; db.DocSequences.Add(seq); }
            seq.LastNo++;
            var r = new Requirement
            {
                ProjectId = db.Projects.OrderBy(p => p.Id).Select(p => p.Id).First(),
                DocNo = $"DEV-{year}-{seq.LastNo:0000}",
                CreatedById = actorId
            };
            if (!db.Users.Any(u => u.Id == input.RequesterUserId && u.IsActive)) return (null, "요청자를 선택하세요");
            Apply(r, input, false, IsAdmin(db, actorId));
            db.Requirements.Add(r);
            try
            {
                db.SaveChanges();
                db.AuditLogs.Add(new AuditLog { Entity = "Requirement", EntityId = r.Id, ActorId = actorId, Action = "create", AfterJson = Snapshot(r) });
                db.SaveChanges();
                return (r, null);
            }
            catch (DbUpdateException) when (attempt < 2) { }
        }
        return (null, "문서번호 채번에 실패했습니다. 다시 시도하세요");
    }

    public string? Update(int actorId, int id, int version, RequirementInput input)
    {
        if (Validate(input) is { } err) return err;
        using var db = Open();
        var r = db.Requirements.Find(id);
        if (r == null) return "개발 등록건을 찾을 수 없습니다";
        if (!(IsAdmin(db, actorId) || r.CreatedById == actorId || r.AssigneeId == actorId)) return "수정 권한이 없습니다";
        db.Entry(r).Property(x => x.Version).OriginalValue = version;
        if (!db.Users.Any(u => u.Id == input.RequesterUserId && u.IsActive)) return "요청자를 선택하세요";
        var before = Snapshot(r);
        Apply(r, input, r.AssigneeId == actorId, IsAdmin(db, actorId), canEditCore: IsAdmin(db, actorId));
        db.AuditLogs.Add(new AuditLog { Entity = "Requirement", EntityId = r.Id, ActorId = actorId, Action = "update", BeforeJson = before, AfterJson = Snapshot(r) });
        if (r.AssigneeId is { } a && a != actorId)
            db.Notifications.Add(Notify(a, NotificationType.RequirementUpdated, r, actorId));
        try { db.SaveChanges(); }
        catch (DbUpdateConcurrencyException) { return "다른 사용자가 먼저 수정했습니다. 새로고침한 뒤 다시 시도하세요"; }
        return null;
    }

    // 개발 완료는 배정받은 담당자만 기록한다. 완료한 모듈을 반드시 입력하고, 마스터에게 알림을 보낸다.
    public string? Complete(int actorId, int id, int version, string modules)
    {
        if (string.IsNullOrWhiteSpace(modules)) return "개발완료 모듈을 입력하세요";
        using var db = Open();
        var r = db.Requirements.Find(id);
        if (r == null) return "개발 등록건을 찾을 수 없습니다";
        if (r.AssigneeId != actorId) return "배정받은 담당자만 개발 완료를 기록할 수 있습니다";
        db.Entry(r).Property(x => x.Version).OriginalValue = version;
        var before = r.Status.ToString();
        r.Status = RequirementStatus.Done;
        r.CompletedAt = DateTime.UtcNow;
        r.CompletedModules = modules.Trim();
        foreach (var adminId in db.Users.Where(u => u.Role == GlobalRole.Admin && u.IsActive && u.Id != actorId).Select(u => u.Id).ToList())
            db.Notifications.Add(Notify(adminId, NotificationType.StatusChanged, r, actorId));
        db.AuditLogs.Add(new AuditLog { Entity = "Requirement", EntityId = r.Id, ActorId = actorId, Action = "complete", BeforeJson = before, AfterJson = r.CompletedModules });
        try { db.SaveChanges(); }
        catch (DbUpdateConcurrencyException) { return "다른 사용자가 먼저 수정했습니다. 새로고침한 뒤 다시 시도하세요"; }
        return null;
    }

    // 담당자 배정·변경·해제는 마스터(관리자)만 가능하다.
    public string? Assign(int actorId, int requirementId, int? toUserId)
    {
        using var db = Open();
        if (!IsAdmin(db, actorId)) return "담당자 배정은 마스터만 할 수 있습니다";
        var r = db.Requirements.Find(requirementId);
        if (r == null) return "개발 등록건을 찾을 수 없습니다";
        if (r.AssigneeId == toUserId) return null;
        var old = r.AssigneeId;
        r.AssigneeId = toUserId;
        db.Assignments.Add(new Assignment { RequirementId = r.Id, FromUserId = old, ToUserId = toUserId, AssignedById = actorId });
        if (toUserId is { } to) db.Notifications.Add(Notify(to, NotificationType.Assigned, r, actorId));
        if (old is { } o) db.Notifications.Add(Notify(o, NotificationType.Unassigned, r, actorId));
        db.AuditLogs.Add(new AuditLog { Entity = "Requirement", EntityId = r.Id, ActorId = actorId, Action = "assign", BeforeJson = old?.ToString(), AfterJson = toUserId?.ToString() });
        try { db.SaveChanges(); }
        catch (DbUpdateConcurrencyException) { return "다른 사용자가 먼저 수정했습니다. 새로고침한 뒤 다시 시도하세요"; }
        return null;
    }

    public List<RequirementRow> List(int userId, bool all)
    {
        using var db = Open();
        var unread = db.Notifications.Where(n => n.UserId == userId && !n.IsRead && n.Entity == "Requirement")
            .Select(n => n.EntityId).ToHashSet();
        var q = db.Requirements.AsNoTracking().Include(r => r.Assignee).ThenInclude(u => u!.JobTitle).AsQueryable();
        if (!all || !IsAdmin(db, userId)) q = q.Where(r => r.AssigneeId == userId);
        var rows = q.OrderByDescending(r => r.Id).Select(r => new
        {
            r.Id, r.DocNo, r.Title, r.AssigneeId,
            Assignee = r.Assignee == null ? null : r.Assignee.Name + (r.Assignee.JobTitle == null ? "" : " " + r.Assignee.JobTitle.Name)
        }).ToList();
        var ids = rows.Select(r => r.Id).ToList();
        var acks = db.Notifications
            .Where(n => n.Type == NotificationType.Assigned && n.Entity == "Requirement" && ids.Contains(n.EntityId))
            .Select(n => new { n.Id, n.EntityId, n.UserId, n.IsRead }).ToList();
        return rows.Select(r =>
        {
            var unassigned = r.AssigneeId == null;
            var latest = unassigned ? null : acks.Where(a => a.EntityId == r.Id && a.UserId == r.AssigneeId).OrderByDescending(a => a.Id).FirstOrDefault();
            var confirmed = latest?.IsRead ?? false;
            return new RequirementRow(r.Id, r.DocNo, r.Title, unread.Contains(r.Id),
                r.Assignee ?? "", unassigned, !unassigned && !confirmed, !unassigned && confirmed);
        }).ToList();
    }

    public Requirement? Detail(int userId, int id)
    {
        using var db = Open();
        var r = db.Requirements.AsNoTracking().Include(x => x.Category).Include(x => x.Assignee)
            .Include(x => x.RequesterUser).FirstOrDefault(x => x.Id == id);
        if (r == null) return null;
        return IsAdmin(db, userId) || r.AssigneeId == userId || r.CreatedById == userId ? r : null;
    }

    public int UnreadCount(int userId)
    {
        using var db = Open();
        return db.Notifications.Count(n => n.UserId == userId && !n.IsRead);
    }

    public void MarkRead(int userId, int requirementId)
    {
        using var db = Open();
        db.Notifications.Where(n => n.UserId == userId && n.Entity == "Requirement" && n.EntityId == requirementId && !n.IsRead)
            .ExecuteUpdate(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }

    private static bool IsAdmin(DevManagerDbContext db, int userId) =>
        db.Users.Any(u => u.Id == userId && u.Role == GlobalRole.Admin && u.IsActive);

    private static string? Validate(RequirementInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Title)) return "개발 제목을 입력하세요";
        if (i.Title.Length > 200) return "제목은 200자 이내로 입력하세요";
        if (string.IsNullOrWhiteSpace(i.Description)) return "내용을 입력하세요";
        if (i.CategoryId <= 0) return "카테고리를 선택하세요";
        if (i.Start is { } s && i.End is { } e && e < s) return "개발 종료일은 시작일보다 앞설 수 없습니다";
        return null;
    }

    // 개발 시작일·종료일은 배정받은 담당자만, 배포일은 마스터만 입력할 수 있다. 권한이 없으면 기존 값을 유지한다.
    private static void Apply(Requirement r, RequirementInput i, bool canDevDates, bool canDeploy, bool canEditCore = true)
    {
        // 제목·카테고리는 마스터만 수정할 수 있다(신규 등록 시에는 입력).
        if (canEditCore)
        {
            r.Title = i.Title.Trim();
            r.CategoryId = i.CategoryId;
        }
        r.Description = i.Description.Trim();
        r.RequesterUserId = i.RequesterUserId;
        r.DueDate = i.Due;
        if (canDevDates)
        {
            r.DevStartDate = i.Start;
            r.DevEndDate = i.End;
        }
        if (canDeploy) r.DeployDate = i.Deploy;
    }

    private static string Snapshot(Requirement r) => JsonSerializer.Serialize(new
    {
        r.Title, r.Description, r.CategoryId, r.RequesterUserId, r.DueDate, r.DevStartDate, r.DevEndDate, r.DeployDate, r.AssigneeId
    });

    private static Notification Notify(int userId, NotificationType type, Requirement r, int actorId) => new()
    {
        UserId = userId, Type = type, Entity = "Requirement", EntityId = r.Id,
        Title = $"{r.DocNo} {r.Title}", CreatedById = actorId
    };
}
