using Microsoft.Extensions.DependencyInjection;
using Richie.Application.Abstractions;
using Richie.Application.Assets;
using Richie.Application.Authentication;
using Richie.Application.Expenses;
using Richie.Application.Notifications;
using Richie.Application.Security;
using Richie.Application.Storage;
using Richie.Application.Vault;
using Richie.Infrastructure.Assets;
using Richie.Infrastructure.Expenses;
using Richie.Infrastructure.Authentication;
using Richie.Infrastructure.Notifications;
using Richie.Infrastructure.Persistence;
using Richie.Infrastructure.Security;
using Richie.Infrastructure.Storage;
using Richie.Infrastructure.Vault;

namespace Richie.Infrastructure;

/// <summary>
/// Registers Infrastructure services (crypto + persistence) with the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IKeyProtector, DpapiKeyProtector>();
        services.AddSingleton<IKeyDerivation, Pbkdf2KeyDerivation>();
        services.AddSingleton<IFieldCipher, AesGcmFieldCipher>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();

        services.AddSingleton<IDatabaseKeyProvider, DpapiDatabaseKeyProvider>();
        services.AddSingleton<IAppDbContextFactory, SqlCipherDbContextFactory>();
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IUserSession, UserSession>();
        services.AddSingleton<IAuthService, AuthService>();

        services.AddSingleton<IValuationService, ValuationService>();
        services.AddSingleton<IAssetService, AssetService>();
        services.AddSingleton<ISipService, SipService>();
        services.AddSingleton<IGoalService, GoalService>();
        services.AddSingleton<IFileVault, FileVault>();
        services.AddSingleton<IAssetDocumentService, AssetDocumentService>();
        services.AddSingleton<IAssetImportService, AssetImportService>();
        services.AddSingleton<IExpenseService, ExpenseService>();
        services.AddSingleton<IExpenseRecurringService, ExpenseRecurringService>();
        services.AddSingleton<IExpenseBudgetService, ExpenseBudgetService>();
        services.AddSingleton<IExpenseAnalyticsService, ExpenseAnalyticsService>();
        services.AddSingleton<IExpenseImportService, ExpenseImportService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IVaultGate, VaultGate>();
        services.AddSingleton<IVaultService, VaultService>();
        services.AddSingleton<IVaultHealthService, VaultHealthService>();
        services.AddSingleton<IVaultImportService, VaultImportService>();
        return services;
    }
}
