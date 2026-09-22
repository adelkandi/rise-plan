using backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> Users => Set<UserProfile>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Commitment> Commitments => Set<Commitment>();
    public DbSet<DailyPlan> DailyPlans => Set<DailyPlan>();
    public DbSet<PlanItem> PlanItems => Set<PlanItem>();
    public DbSet<UserPlanningContext> UserPlanningContexts => Set<UserPlanningContext>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasMaxLength(200);
            entity.Property(user => user.DisplayName).HasMaxLength(200);
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(conversation => conversation.Id);
            entity.Property(conversation => conversation.Title).HasMaxLength(200);
            entity.HasIndex(conversation => new { conversation.UserId, conversation.UpdatedAt });
            entity.HasOne(conversation => conversation.User)
                .WithMany(user => user.Conversations)
                .HasForeignKey(conversation => conversation.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Role).HasConversion<string>().HasMaxLength(30);
            entity.Property(message => message.Content).HasMaxLength(20_000);
            entity.Property(message => message.Metadata).HasColumnType("jsonb");
            entity.HasIndex(message => new { message.ConversationId, message.CreatedAt });
            entity.HasOne(message => message.Conversation)
                .WithMany(conversation => conversation.Messages)
                .HasForeignKey(message => message.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(task => task.Id);
            entity.Property(task => task.Title).HasMaxLength(300);
            entity.Property(task => task.Description).HasMaxLength(5_000);
            entity.Property(task => task.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(task => task.Priority).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(task => new { task.UserId, task.Status, task.DueDate });
            entity.HasOne(task => task.User)
                .WithMany(user => user.Tasks)
                .HasForeignKey(task => task.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Commitment>(entity =>
        {
            entity.HasKey(commitment => commitment.Id);
            entity.Property(commitment => commitment.Title).HasMaxLength(300);
            entity.Property(commitment => commitment.Description).HasMaxLength(5_000);
            entity.Property(commitment => commitment.Location).HasMaxLength(500);
            entity.HasIndex(commitment => new { commitment.UserId, commitment.StartTime });
            entity.HasOne(commitment => commitment.User)
                .WithMany(user => user.Commitments)
                .HasForeignKey(commitment => commitment.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DailyPlan>(entity =>
        {
            entity.HasKey(plan => plan.Id);
            entity.Property(plan => plan.Summary).HasMaxLength(5_000);
            entity.HasIndex(plan => new { plan.UserId, plan.Date }).IsUnique();
            entity.HasOne(plan => plan.User)
                .WithMany(user => user.DailyPlans)
                .HasForeignKey(plan => plan.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlanItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(300);
            entity.Property(item => item.Description).HasMaxLength(5_000);
            entity.Property(item => item.Type).HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.Priority).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(item => new { item.DailyPlanId, item.StartTime });
            entity.HasOne(item => item.DailyPlan)
                .WithMany(plan => plan.Items)
                .HasForeignKey(item => item.DailyPlanId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Task)
                .WithMany()
                .HasForeignKey(item => item.TaskId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.Commitment)
                .WithMany()
                .HasForeignKey(item => item.CommitmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserPlanningContext>(entity =>
        {
            entity.HasKey(context => context.UserId);
            entity.Property(context => context.TimeZone).HasMaxLength(100);
            entity.Property(context => context.PreferredWorkingHours).HasMaxLength(200);
            entity.Property(context => context.Preferences).HasColumnType("jsonb");
            entity.HasOne(context => context.User)
                .WithOne(user => user.PlanningContext)
                .HasForeignKey<UserPlanningContext>(context => context.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
