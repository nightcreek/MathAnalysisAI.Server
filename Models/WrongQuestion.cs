using System;
using System.ComponentModel.DataAnnotations;

namespace MathAnalysisAI.Server.Models   // 必须和上面的引用一致
{
    public class WrongQuestion
    {
        [Key]
        public int Id { get; set; }
        public string ImagePath { get; set; }
        public string RawLatex { get; set; }
        public string CleanLatex { get; set; }
        public string StudentAnswer { get; set; }
        public string OverallEvaluation { get; set; }
        public string ErrorAnalysis { get; set; }
        public string ImprovementSuggestion { get; set; }
        public string StandardSolution { get; set; }
        public string KnowledgePoint { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}