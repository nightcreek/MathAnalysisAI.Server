namespace MathAnalysisAI.Server.Services.Courses;

public class CourseService
{
    private readonly ICoursePersistenceService _coursePersistenceService;

    public CourseService(ICoursePersistenceService coursePersistenceService)
    {
        _coursePersistenceService = coursePersistenceService;
    }

    public async Task<List<CourseListItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _coursePersistenceService.ListCoursesAsync(cancellationToken);
    }

    public async Task<List<ChapterListItemDto>?> GetChaptersAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var courseExists = await _coursePersistenceService.CourseExistsAsync(courseId, cancellationToken);

        if (!courseExists)
        {
            return null;
        }

        return await _coursePersistenceService.ListCourseChaptersAsync(courseId, cancellationToken);
    }
}

public class CourseListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int SubjectId { get; set; }
    public int ChapterCount { get; set; }
}

public class ChapterListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}
