using IdeaBoard.App.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdeaBoard.App.Data;

/// <summary>
/// The single entry point to the database. Mapping lives here through the Fluent API and
/// not as annotations on the entities, so that the domain types carry no knowledge of
/// persistence.
///
/// Table and column names are the ones fixed in the data model document, because the
/// manual correction procedure and the success metrics are written against them verbatim.
/// </summary>
public class IdeaBoardDbContext : DbContext
{
    #region Constants

    /// <summary>
    /// Name of the unique index that enforces one vote per person per idea. It is named
    /// explicitly rather than left to convention because the vote path has to recognise a
    /// violation of this specific index.
    /// </summary>
    public const string VoteUniqueIndexName = "IX_Vote_ideaId_userId";

    #endregion


    #region Construction

    /// <summary>Creates the context with options supplied by dependency injection.</summary>
    public IdeaBoardDbContext(DbContextOptions<IdeaBoardDbContext> options)
        : base(options)
    {
    }

    #endregion


    #region Sets

    /// <summary>People who have signed in at least once.</summary>
    public DbSet<User> Users { get; set; } = null!;

    /// <summary>Every idea ever submitted. Nothing removes a row.</summary>
    public DbSet<Idea> Ideas { get; set; } = null!;

    /// <summary>Current votes. Withdrawing a vote deletes the row.</summary>
    public DbSet<Vote> Votes { get; set; } = null!;

    #endregion


    #region Model configuration

    /// <summary>Applies the Fluent API configuration for every mapped entity.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUser(modelBuilder);
        ConfigureIdea(modelBuilder);
        ConfigureVote(modelBuilder);
    }


    /// <summary>Maps <see cref="User"/>, keyed for matching by Entra object identifier.</summary>
    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<User> user = modelBuilder.Entity<User>();

        user.ToTable("User");
        user.HasKey(entity => entity.Id);

        user.Property(entity => entity.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        user.Property(entity => entity.EntraObjectId)
            .HasColumnName("entraObjectId")
            .IsRequired();

        user.Property(entity => entity.DisplayName)
            .HasColumnName("displayName")
            .IsRequired();

        // Optional, because a version 1.0 token carries no preferred_username and the
        // application tolerates that: the object identifier is what identity depends on.
        user.Property(entity => entity.Email)
            .HasColumnName("email");

        user.Property(entity => entity.FirstSeenAt)
            .HasColumnName("firstSeenAt")
            .IsRequired();

        user.HasIndex(entity => entity.EntraObjectId)
            .IsUnique()
            .HasDatabaseName("IX_User_entraObjectId");
    }


    /// <summary>
    /// Maps <see cref="Idea"/>. There is no state column, no category column and no vote
    /// count column: the first two because they do not exist in the product, the third
    /// because the count is aggregated at read time and never stored.
    /// </summary>
    private static void ConfigureIdea(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<Idea> idea = modelBuilder.Entity<Idea>();

        idea.ToTable("Idea");
        idea.HasKey(entity => entity.Id);

        idea.Property(entity => entity.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        idea.Property(entity => entity.Title)
            .HasColumnName("title")
            .HasMaxLength(IdeaLimits.TitleMaximumLength)
            .IsRequired();

        idea.Property(entity => entity.Description)
            .HasColumnName("description")
            .HasMaxLength(IdeaLimits.DescriptionMaximumLength)
            .IsRequired();

        idea.Property(entity => entity.AuthorUserId)
            .HasColumnName("authorUserId")
            .IsRequired();

        idea.Property(entity => entity.CreatedAt)
            .HasColumnName("createdAt")
            .IsRequired();

        idea.HasOne(entity => entity.Author)
            .WithMany()
            .HasForeignKey(entity => entity.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        idea.HasIndex(entity => entity.CreatedAt)
            .IsDescending(true)
            .HasDatabaseName("IX_Idea_createdAt");
    }


    /// <summary>
    /// Maps <see cref="Vote"/> and declares the index that is the centrepiece of the
    /// model: one vote per person per idea, enforced by the database rather than by a
    /// check in code that a race condition can slip past.
    /// </summary>
    private static void ConfigureVote(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<Vote> vote = modelBuilder.Entity<Vote>();

        vote.ToTable("Vote");
        vote.HasKey(entity => entity.Id);

        vote.Property(entity => entity.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        vote.Property(entity => entity.IdeaId)
            .HasColumnName("ideaId")
            .IsRequired();

        vote.Property(entity => entity.UserId)
            .HasColumnName("userId")
            .IsRequired();

        vote.Property(entity => entity.CreatedAt)
            .HasColumnName("createdAt")
            .IsRequired();

        vote.HasOne(entity => entity.Idea)
            .WithMany(entity => entity.Votes)
            .HasForeignKey(entity => entity.IdeaId)
            .OnDelete(DeleteBehavior.Restrict);

        vote.HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        vote.HasIndex(entity => new { entity.IdeaId, entity.UserId })
            .IsUnique()
            .HasDatabaseName(VoteUniqueIndexName);
    }

    #endregion
}
