namespace MathAnalysisAI.Server.DTOs.Resources
{
    public class NetworkResourceListItemDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Link { get; set; }
        public int SortOrder { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class NetworkResourceCreateDto
    {
        public int CourseId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Link { get; set; }
        public int SortOrder { get; set; }
    }

    public class NetworkResourceUpdateDto
    {
        public string? Category { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Link { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsEnabled { get; set; }
    }
}
