using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetConcepts.EntityFramework.TagFiltering
{
    enum Relationship
    {
        Neutral = 0,
        Like = 1,
        Dislike = 2,
    }

    class User
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public ICollection<Tag> Tags { get; set; }
    }

    class Card
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public ICollection<Tag> Tags { get; set; }
    }

    class Tag
    {
        public int User_Id { get; set; }
        public User User { get; set; }

        public int Card_Id { get; set; }
        public Card Card { get; set; }

        public Relationship Relationship { get; set; }

        public ICollection<Tag> Tags { get; set; }
    }

    class TagFilteringContext : DbContext
    {
        public TagFilteringContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Card> Cards { get; set; }
        public DbSet<Tag> Tags { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasMany(t => t.Tags)
                    .WithOne(t => t.User)
                    .HasForeignKey(t => t.User_Id)
                    .IsRequired();
            });

            modelBuilder.Entity<Card>(entity =>
            {
                entity.HasMany(t => t.Tags)
                    .WithOne(t => t.Card)
                    .HasForeignKey(t => t.Card_Id)
                    .IsRequired();
            });

            modelBuilder.Entity<Tag>(entity =>
            {
                entity.HasKey(x => new { x.User_Id, x.Card_Id });

                entity.HasOne(t => t.User)
                    .WithMany(t => t.Tags)
                    .HasForeignKey(t => t.User_Id)
                    .IsRequired();

                entity.HasOne(t => t.Card)
                    .WithMany(t => t.Tags)
                    .HasForeignKey(t => t.Card_Id)
                    .IsRequired();
            });
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole();

            var optionsBuilder = new DbContextOptionsBuilder<TagFilteringContext>();
            optionsBuilder.UseLoggerFactory(loggerFactory);
            optionsBuilder.UseSqlServer("Server=.;Initial Catalog=concepts.tagfiltering.local;Integrated Security=True");
            optionsBuilder.ConfigureWarnings(options => options.Throw(RelationalEventId.QueryClientEvaluationWarning));

            var logger = loggerFactory.CreateLogger<Program>();

            logger.LogInformation("Preparing database..");

            using (var context = new TagFilteringContext(optionsBuilder.Options))
            {
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
            }

            logger.LogInformation("Inserting data..");

            using (var context = new TagFilteringContext(optionsBuilder.Options))
            using (var transaction = await context.Database.BeginTransactionAsync())
            {
                Card ragnaros, sylvanas, kelthuzad;

                context.Cards.Add(ragnaros = new Card()
                {
                    Name = "Ragnaros",
                });
                context.Cards.Add(sylvanas = new Card()
                {
                    Name = "Sylvanas Windrunner",
                });
                context.Cards.Add(kelthuzad = new Card()
                {
                    Name = "Kel'Thuzad",
                });

                User lukas, martin, tomas;

                context.Users.Add(lukas = new User()
                {
                    Name = "Lukas",
                });
                context.Users.Add(martin = new User()
                {
                    Name = "Martin",
                });
                context.Users.Add(tomas = new User()
                {
                    Name = "Tomas",
                });

                await context.SaveChangesAsync();

                context.Tags.Add(new Tag()
                {
                    Card_Id = sylvanas.Id,
                    User_Id = lukas.Id,
                    Relationship = Relationship.Like,
                });
                context.Tags.Add(new Tag()
                {
                    Card_Id = kelthuzad.Id,
                    User_Id = lukas.Id,
                    Relationship = Relationship.Dislike,
                });

                context.Tags.Add(new Tag()
                {
                    Card_Id = sylvanas.Id,
                    User_Id = martin.Id,
                    Relationship = Relationship.Dislike,
                });

                await context.SaveChangesAsync();

                transaction.Commit();
            }

            logger.LogInformation("Printing filtered cards..");

            using (var context = new TagFilteringContext(optionsBuilder.Options))
            {
                foreach (var user in context.Users.ToArray())
                {
                    var cards = context.Cards
                        .Where(c => !c.Tags.Any(t => t.User_Id == user.Id && t.Relationship == Relationship.Dislike))
                        .ToArray();

                    var messageBuilder = new StringBuilder();
                    messageBuilder.AppendLine($"{user.Name}:");
                    foreach (var pair in cards)
                    {
                        messageBuilder.AppendLine($"\t{pair.Name}");
                    }
                    logger.LogInformation(messageBuilder.ToString());
                }
            }

            Console.ReadKey();
        }
    }
}
