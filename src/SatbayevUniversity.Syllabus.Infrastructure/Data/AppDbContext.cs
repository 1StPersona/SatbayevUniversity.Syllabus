using SatbayevUniversity.Syllabus.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SatbayevUniversity.Syllabus.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Institute> Institutes => Set<Institute>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<EducationalProgram> EducationalPrograms => Set<EducationalProgram>();
    public DbSet<Discipline> Disciplines => Set<Discipline>();
    public DbSet<CourseSyllabus> Syllabi => Set<CourseSyllabus>();
    public DbSet<SyllabusProgram> SyllabusPrograms => Set<SyllabusProgram>();
    public DbSet<SyllabusRequisite> Requisites => Set<SyllabusRequisite>();
    public DbSet<CalendarRow> CalendarRows => Set<CalendarRow>();
    public DbSet<LiteratureItem> Literature => Set<LiteratureItem>();
    public DbSet<CompetencyCell> Competencies => Set<CompetencyCell>();
    public DbSet<AssessmentItem> Assessments => Set<AssessmentItem>();
    public DbSet<ApprovalLogEntry> ApprovalLog => Set<ApprovalLogEntry>();
    public DbSet<UmkdComponentType> UmkdComponentTypes => Set<UmkdComponentType>();
    public DbSet<UmkdMaterial> UmkdMaterials => Set<UmkdMaterial>();
    public DbSet<TemplateSettings> TemplateSettings => Set<TemplateSettings>();
    public DbSet<GradeScaleRow> GradeScale => Set<GradeScaleRow>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Department).WithMany().HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(u => u.Institute).WithMany().HasForeignKey(u => u.InstituteId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(u => u.EducationalProgram).WithMany().HasForeignKey(u => u.EducationalProgramId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Department>().HasOne(d => d.Institute).WithMany(i => i.Departments)
            .HasForeignKey(d => d.InstituteId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EducationalProgram>().HasOne(p => p.Department).WithMany()
            .HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EducationalProgram>().HasIndex(p => p.Code).IsUnique();
        b.Entity<EducationalProgram>().Property(p => p.Code).HasMaxLength(32);
        b.Entity<EducationalProgram>().Property(p => p.Name).HasMaxLength(300);
        b.Entity<Institute>().Property(p => p.Name).HasMaxLength(300);
        b.Entity<Institute>().Property(p => p.ShortName).HasMaxLength(50);
        b.Entity<Institute>().Property(p => p.DirectorName).HasMaxLength(200);
        b.Entity<Department>().Property(p => p.Name).HasMaxLength(300);
        b.Entity<Department>().Property(p => p.HeadName).HasMaxLength(200);
        b.Entity<Department>().Property(p => p.HeadTitle).HasMaxLength(100);
        b.Entity<ApplicationUser>().Property(p => p.FullName).HasMaxLength(200);
        b.Entity<ApplicationUser>().Property(p => p.Regalia).HasMaxLength(300);

        b.Entity<Discipline>(e =>
        {
            e.HasOne(d => d.Department).WithMany().HasForeignKey(d => d.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(d => d.Code).IsUnique();
            e.Property(d => d.Code).HasMaxLength(32);
            e.Property(d => d.Name).HasMaxLength(300);
            e.Property(d => d.CreditStructure).HasMaxLength(32);
        });

        b.Entity<CourseSyllabus>(e =>
        {
            e.HasOne(s => s.Discipline).WithMany().HasForeignKey(s => s.DisciplineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Author).WithMany().HasForeignKey(s => s.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.OwnsOne(s => s.Lecturer, o => ConfigureTeacher(o, "Lecturer"));
            e.OwnsOne(s => s.PracticeTeacher, o => ConfigureTeacher(o, "PracticeTeacher"));
            e.HasMany(s => s.Requisites).WithOne().HasForeignKey(x => x.SyllabusId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.CalendarRows).WithOne().HasForeignKey(x => x.SyllabusId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Literature).WithOne().HasForeignKey(x => x.SyllabusId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Competencies).WithOne().HasForeignKey(x => x.SyllabusId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Assessments).WithOne().HasForeignKey(x => x.SyllabusId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.History).WithOne().HasForeignKey(x => x.SyllabusId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Materials).WithOne(m => m.Syllabus).HasForeignKey(x => x.SyllabusId).OnDelete(DeleteBehavior.Cascade);
            e.Property(s => s.AcademicYear).HasMaxLength(16);
            e.Property(s => s.StudyForm).HasMaxLength(100);
            e.Property(s => s.Language).HasMaxLength(50);
            e.Property(s => s.City).HasMaxLength(100);
            e.Property(s => s.ProtocolNumber).HasMaxLength(32);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(s => new { s.DisciplineId, s.AcademicYear });
            e.HasIndex(s => s.Status);
        });

        b.Entity<SyllabusProgram>(e =>
        {
            e.HasKey(x => new { x.SyllabusId, x.EducationalProgramId });
            e.HasOne(x => x.Syllabus).WithMany(s => s.Programs).HasForeignKey(x => x.SyllabusId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.EducationalProgram).WithMany().HasForeignKey(x => x.EducationalProgramId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<UmkdMaterial>(e =>
        {
            e.HasOne(m => m.ComponentType).WithMany().HasForeignKey(m => m.ComponentTypeId).OnDelete(DeleteBehavior.Restrict);
            e.Property(m => m.Title).HasMaxLength(500);
            e.Property(m => m.OriginalFileName).HasMaxLength(260);
            e.Property(m => m.StoredFileName).HasMaxLength(64);
            e.Property(m => m.ContentType).HasMaxLength(200);
            e.Property(m => m.ExternalUrl).HasMaxLength(2000);
            e.Property(m => m.ReviewStatus).HasConversion<string>().HasMaxLength(32);
        });
        b.Entity<ApprovalLogEntry>(e =>
        {
            e.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Action).HasMaxLength(200);
            e.Property(x => x.UserName).HasMaxLength(200);
        });
        b.Entity<UmkdComponentType>().Property(x => x.Name).HasMaxLength(300);

        b.Entity<GradeScaleRow>().Property(x => x.Gpa).HasPrecision(4, 2);
        b.Entity<AssessmentItem>().Property(x => x.FinalPoints).HasPrecision(6, 2);
    }

    private static void ConfigureTeacher<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.OwnedNavigationBuilder<T, TeacherInfo> o, string prefix)
        where T : class
    {
        o.Property(p => p.FullName).HasColumnName(prefix + "FullName");
        o.Property(p => p.Regalia).HasColumnName(prefix + "Regalia");
        o.Property(p => p.Office).HasColumnName(prefix + "Office");
        o.Property(p => p.OfficeHours).HasColumnName(prefix + "OfficeHours");
        o.Property(p => p.Email).HasColumnName(prefix + "Email");
    }
}
