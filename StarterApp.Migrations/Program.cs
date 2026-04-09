using Microsoft.EntityFrameworkCore;
using StarterApp.Database.Data;

Console.WriteLine("Running migrations...");
using var context = new AppDbContext();
await LegacySchemaRepair.NormalizeAsync(context);
await context.Database.MigrateAsync();
Console.WriteLine("Migrations complete.");
