using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Profile.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddProfileServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register repositories, application services, validators, etc. here.
        // Bind config sections with IOptions<T> here — never read
        // IConfiguration[""] outside this file.
        return services;
    }
}
