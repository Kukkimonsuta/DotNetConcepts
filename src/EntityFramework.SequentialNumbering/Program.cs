using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetConcepts.EntityFramework.SequentialNumbering
{
    class Order
    {
        public int Id { get; set; }

        public DateTime Created { get; set; }
        public decimal Number { get; set; }
    }

    class SeqNumContext : DbContext
    {
        public DbSet<Order> Orders { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("Server=.;Initial Catalog=seqnum.local;Integrated Security=True");

            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Order>()
                .HasIndex(x => x.Number)
                .IsUnique()
                .HasFilter("[Number] != 0");
        }
    }

    class Program
    {
        static async Task<int> CreateOrder()
        {
            using (var context = new SeqNumContext())
            {
                Order order;

                using (var transaction = await context.Database.BeginTransactionAsync())
                {
                    order = new Order()
                    {
                        Created = DateTime.UtcNow,
                    };

                    context.Orders.Add(order);

                    await context.SaveChangesAsync();
                    
                    transaction.Commit();
                }

                var firstDailyOrder =
                    ((order.Created.Year % 100) * 100000000) +
                    order.Created.Month * 1000000 +
                    order.Created.Day * 10000 +
                    1;

                await context.Database.ExecuteSqlCommandAsync($"update oo set oo.[Number] = {firstDailyOrder} + (select count(Id) from [Orders] io where io.[Number] >= {firstDailyOrder}) from [Orders] oo (tablock) where oo.[Id] = {order.Id}");
                
                return order.Id;
            }
        }

        static async Task Main(string[] args)
        {
            var numberOfOrders = 10000;

            Console.WriteLine("Preparing database..");

            using (var context = new SeqNumContext())
            {
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
            }

            Console.WriteLine("Inserting data..");

            var runningTasks = new Dictionary<int, Task>();

            using (var throttle = new SemaphoreSlim(20, 20))
            {
                for (var i = 0; i < numberOfOrders; i++)
                {
                    Console.Title = $"SeqNum {i + 1}/{numberOfOrders}";

                    await throttle.WaitAsync();

                    Task task = null;
                    task = Task.Run(async () =>
                    {
                        try
                        {
                            await CreateOrder();
                        }
                        finally
                        {
                            lock (runningTasks)
                            {
                                runningTasks.Remove(task.Id);
                            }

                            throttle.Release();
                        }
                    });

                    lock (runningTasks)
                    {
                        runningTasks.Add(task.Id, task);
                    }
                }

                await Task.WhenAll(runningTasks.Values);
            }

            Console.WriteLine("Printing statistics..");

            using (var context = new SeqNumContext())
            {
                Console.WriteLine(" - created {0} orders", await context.Orders.CountAsync());
                Console.WriteLine(" - created {0} numbers", await context.Orders.Select(x => x.Number).Distinct().CountAsync());
            }

            Console.ReadKey();
        }
    }
}
