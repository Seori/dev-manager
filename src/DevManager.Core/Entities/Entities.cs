namespace DevManager.Core.Entities;

public class JobTitle
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Category
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class User
{
    public int Id { get; set; }
    public string LoginId { get; set; } = "";
    public string Name { get; set; } = "";
    public int? JobTitleId { get; set; }
    public JobTitle? JobTitle { get; set; }
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = "";
    public GlobalRole Role { get; set; } = GlobalRole.Member;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public int FailedCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public List<ProjectMember> Members { get; set; } = [];
}

public class ProjectMember
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public ProjectRole Role { get; set; } = ProjectRole.Developer;
}

public class Milestone
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Name { get; set; } = "";
    public DateOnly DueDate { get; set; }
}

// 개발 등록건. 문서번호(DocNo)는 등록 후 변경하지 않는다.
public class Requirement
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string DocNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public Priority Priority { get; set; } = Priority.Normal;
    public RequirementStatus Status { get; set; } = RequirementStatus.Registered;

    public int? RequesterUserId { get; set; }
    public User? RequesterUser { get; set; }
    public string? RequesterName { get; set; }

    public DateOnly? DueDate { get; set; }
    public DateOnly? DevStartDate { get; set; }
    public DateOnly? DevEndDate { get; set; }
    public DateOnly? DeployDate { get; set; }

    // 담당자가 개발 완료 시 기록하는 완료 모듈 목록과 완료 시각
    public string? CompletedModules { get; set; }
    public DateTime? CompletedAt { get; set; }

    public int? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    // 동시 수정 감지용 낙관적 잠금 토큰. 저장 시 DbContext가 증가시킨다.
    public int Version { get; set; }

    public int CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

public class Assignment
{
    public int Id { get; set; }
    public int RequirementId { get; set; }
    public int? FromUserId { get; set; }
    public int? ToUserId { get; set; }
    public int AssignedById { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

// 문서번호 채번용. 연도별 마지막 번호를 쓰기 트랜잭션 안에서 증가시킨다.
public class DocSequence
{
    public int Year { get; set; }
    public int LastNo { get; set; }
}

public class WorkTask
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? RequirementId { get; set; }
    public int? ParentId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? AssigneeId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public WorkStatus Status { get; set; } = WorkStatus.Todo;
    public Priority Priority { get; set; } = Priority.Normal;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

public class Issue
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? TaskId { get; set; }
    public int? RequirementId { get; set; }
    public string Title { get; set; } = "";
    public string? Steps { get; set; }
    public Severity Severity { get; set; } = Severity.Minor;
    public int? AssigneeId { get; set; }
    public IssueStatus Status { get; set; } = IssueStatus.New;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

public class Document
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Version { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Comment
{
    public int Id { get; set; }
    public string Entity { get; set; } = "";
    public int EntityId { get; set; }
    public int AuthorId { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// 수신자(UserId) 기준으로 저장한다. 다른 사용자의 알림은 조회할 수 없어야 한다.
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public NotificationType Type { get; set; }
    public string Entity { get; set; } = "";
    public int EntityId { get; set; }
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public int? CreatedById { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public long Id { get; set; }
    public string Entity { get; set; } = "";
    public int EntityId { get; set; }
    public int? ActorId { get; set; }
    public string Action { get; set; } = "";
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
