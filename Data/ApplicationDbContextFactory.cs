using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MathAnalysisAI.Server.Data;

namespace MathAnalysisAI.Server.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=MathAnalysisAI;Trusted_Connection=True;MultipleActiveResultSets=true"
            );
            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}