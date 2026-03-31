using System.Reflection;
using Fluxorq.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxorq.DependencyInjection.Internal;

/// <summary>
/// Scans assemblies for <see cref="IRequestHandler{TRequest,TResponse}"/> and
/// <see cref="IValidator{T}"/> implementations and registers them in the DI container.
/// </summary>
internal static class HandlerRegistrar
{
    private static readonly Type _handlerOpenType = typeof(IRequestHandler<,>);
    private static readonly Type _validatorOpenType = typeof(IValidator<>);

    internal static void Register(IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            RegisterHandlers(services, assembly);
            RegisterValidators(services, assembly);
        }
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly assembly)
    {
        var candidates = GetConcreteTypes(assembly);

        foreach (var type in candidates)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType) continue;
                if (iface.GetGenericTypeDefinition() != _handlerOpenType) continue;

                services.AddTransient(iface, type);
            }
        }
    }

    private static void RegisterValidators(IServiceCollection services, Assembly assembly)
    {
        var candidates = GetConcreteTypes(assembly);

        foreach (var type in candidates)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType) continue;
                if (iface.GetGenericTypeDefinition() != _validatorOpenType) continue;

                services.AddTransient(iface, type);
            }
        }
    }

    private static IEnumerable<Type> GetConcreteTypes(Assembly assembly)
        => assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition && t.IsPublic);
}
