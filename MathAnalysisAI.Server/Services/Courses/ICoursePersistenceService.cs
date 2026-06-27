namespace MathAnalysisAI.Server.Services.Courses;

public interface ICoursePersistenceService
{
    Task<List<CourseListItemDto>> ListCoursesAsync(CancellationToken cancellationToken);
    Task<bool> CourseExistsAsync(int courseId, CancellationToken cancellationToken);
    Task<List<ChapterListItemDto>> ListCourseChaptersAsync(int courseId, CancellationToken cancellationToken);
}
