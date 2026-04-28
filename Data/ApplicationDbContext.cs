using Microsoft.EntityFrameworkCore;
using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<WrongQuestion> WrongQuestions { get; set; }
    }
}