namespace MathAnalysisAI.Server.Services.Knowledge;

public interface ILearningPathPersistenceService
{
    Task<LearningPathSnapshot> GetLearningPathSnapshotAsync(int courseId, int userId, CancellationToken cancellationToken);
}

public sealed class LearningPathSnapshot
{
    public string? CourseName { get; init; }
    public List<LearningPathKnowledgePointRecord> KnowledgePoints { get; init; } = new();
    public List<LearningPathDependencyRecord> Dependencies { get; init; } = new();
    public List<LearningPathUserStateRecord> UserStates { get; init; } = new();
    public List<LearningPathMistakeRecord> MistakeStats { get; init; } = new();
}

public sealed class LearningPathKnowledgePointRecord
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? ChapterName { get; init; }
}

public sealed class LearningPathDependencyRecord
{
    public int FromKnowledgePointId { get; init; }
    public int ToKnowledgePointId { get; init; }
    public string? DependencyType { get; init; }
}

public sealed class LearningPathUserStateRecord
{
    public int KnowledgePointId { get; init; }
    public int MasteryLevel { get; init; }
    public int PracticeCount { get; init; }
    public int CorrectCount { get; init; }
}

public sealed class LearningPathMistakeRecord
{
    public int? KnowledgePointId { get; init; }
    public int MistakeCount { get; init; }
    public int SeveritySum { get; init; }
    public string? MostCommonMistakeTag { get; init; }
}
