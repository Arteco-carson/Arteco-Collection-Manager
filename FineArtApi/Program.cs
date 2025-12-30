using Microsoft.EntityFrameworkCore;
using FineArtApi.Data;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// --- 1. AUTHENTICATION SERVICES (ISO27001 Compliance) ---
builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        // Ensure this matches AuthController SecretKey
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("Your_Super_Secret_Key_At_Least_32_Chars_Long")),
        ValidateIssuer = false,
        ValidateAudience = false,
        //ClockSkew = TimeSpan.Zero 
    };

    // IT Operations Diagnostics: Log authentication failures for governance oversight
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("Governance/Auth Failure: " + context.Exception.Message);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddCors(options => {
    options.AddPolicy("AllowMobile", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddControllers().AddJsonOptions(options => {
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; 
});

builder.Services.AddDbContext<ArtContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(); // Serve files from wwwroot
app.UseCors("AllowMobile");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();