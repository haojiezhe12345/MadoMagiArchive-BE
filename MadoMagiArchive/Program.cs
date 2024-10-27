using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;

using MadoMagiArchive.CoreServices;
using MadoMagiArchive.DataServices;
using MadoMagiArchive.FileServices;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("token", new OpenApiSecurityScheme
    {
        Name = "token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new() { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "token" } },
        Array.Empty<string>()
    }});
});


{
    builder.Services.AddHttpClient();
    builder.Services.AddMemoryCache();
    builder.Services.AddSignalR();

    builder.Services.AddDbContext<CoreDbContext>();
    builder.Services.AddScoped<CoreService>();
    builder.Services.AddScoped<UserContext>();
    builder.Services.AddScoped<UserService>();

    builder.Services.AddDbContext<DataDbContext>();

    builder.Services.AddSingleton<StorageService>();
    builder.Services.AddSingleton<FileScanService>();

    Directory.CreateDirectory("Databases");
}


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


{
    using (var scope = app.Services.CreateScope())
    {
        using (var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>())
        {
            context.Database.Migrate();
            context.Database.EnsureCreated();
            context.SeedData();
        }
        using (var context = scope.ServiceProvider.GetRequiredService<DataDbContext>())
        {
            context.Database.Migrate();
            context.Database.EnsureCreated();
            context.SeedData();
        }
    }

    app.UseMiddleware<UserAuthMiddleware>();
    app.UseMiddleware<PermissionCheckMiddleware>();

    app.MapHub<FileScanHub>("/files/scan");
}


app.UseAuthorization();

app.MapControllers();

app.Run();
