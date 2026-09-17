using System.Reflection;
using FluentValidation;
using InventStock.Application.Interfaces;
using InventStock.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace InventStock.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);

        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPartService, PartService>();
        services.AddScoped<IStockMovementService, StockMovementService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
