namespace DevManager.Core.Entities;

public enum GlobalRole { Member = 0, Admin = 1 }

public enum ProjectRole { Developer = 0, Pm = 1, Viewer = 2 }

public enum ProjectStatus { Planned = 0, InProgress = 1, Done = 2, OnHold = 3 }

public enum RequirementStatus { Registered = 0, InProgress = 1, Review = 2, Done = 3, Closed = 4 }

public enum WorkStatus { Todo = 0, InProgress = 1, Review = 2, Done = 3 }

public enum Priority { Low = 0, Normal = 1, High = 2, Urgent = 3 }

public enum IssueStatus { New = 0, InProgress = 1, Resolved = 2, Closed = 3 }

public enum Severity { Minor = 0, Major = 1, Critical = 2 }

public enum NotificationType { Assigned = 0, Unassigned = 1, RequirementUpdated = 2, StatusChanged = 3, Commented = 4 }
