using Application.Interfaces;
using Application.Services;
using Infrastructure.Persistence;
using Licensing.Services;
using Microsoft.Extensions.DependencyInjection;
using Security.Interfaces;
using Security.Services;

namespace Infrastructure;

public static class DependencyInjection
{
    public const string DefaultPostgreSqlConnectionString = 
        "Host=localhost;Port=5432;Database=offline_inventory_db;Username=postgres;Password=1234;Include Error Detail=true;";

    public static IServiceCollection AddCoreInfrastructureServices(
        this IServiceCollection services, 
        string? connectionString = null, 
        bool usePostgres = true)
    {
        string connStr = connectionString ?? DefaultPostgreSqlConnectionString;

        // Security Services
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IAesGcmService, AesGcmService>();
        services.AddSingleton<IRsaCryptoService, RsaCryptoService>();

        // Licensing Services
        services.AddSingleton<IProvisioningKeyService, ProvisioningKeyService>();
        services.AddSingleton<ILicenseManager, LicenseManager>();

        if (usePostgres)
        {
            // PostgreSQL Connection Factory & DB Initializer
            services.AddSingleton<IDbConnectionFactory>(new NpgsqlDbConnectionFactory(connStr));
            services.AddSingleton<DatabaseInitializer>();

            // PostgreSQL Repositories
            services.AddSingleton<ICompanyRepository, PostgresCompanyRepository>();
            services.AddSingleton<IUserRepository, PostgresUserRepository>();
            services.AddSingleton<ILicenseRepository, PostgresLicenseRepository>();
            services.AddSingleton<IAuditLogRepository, PostgresAuditLogRepository>();
            services.AddSingleton<IProductRepository, PostgresProductRepository>();
            services.AddSingleton<IInvoiceRepository, PostgresInvoiceRepository>();
        }
        else
        {
            // Local In-Memory Offline Storage (for isolated unit tests)
            services.AddSingleton<ICompanyRepository, LocalCompanyRepository>();
            services.AddSingleton<IUserRepository, LocalUserRepository>();
            services.AddSingleton<ILicenseRepository, LocalLicenseRepository>();
            services.AddSingleton<IAuditLogRepository, LocalAuditLogRepository>();
            services.AddSingleton<IProductRepository, LocalProductRepository>();
            services.AddSingleton<IInvoiceRepository, LocalInvoiceRepository>();
        }

        // Application Services
        services.AddTransient<IBootstrapService, BootstrapService>();
        services.AddTransient<IAuthenticationService, AuthenticationService>();

        return services;
    }
}
