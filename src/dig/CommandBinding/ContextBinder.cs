using System.CommandLine.Invocation;
using System.CommandLine;
using System.Reflection;

internal class ContextBinder(ILogger<ContextBinder> logger)
{
    private readonly ILogger<ContextBinder> _logger = logger;

    public void BindToContext(Command command, InvocationContext context, MethodInfo method, IServiceProvider services)
    {
        var type = method.DeclaringType ?? throw new InvalidOperationException($"Could not get declaring type for method {method.Name}");
        var target = Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Could not create instance of type {type.FullName}");

        // bind all the arguments to the instance
        foreach (var argument in command.Arguments)
        {
            var property = type.GetProperty(argument.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is not null)
            {
                var value = context.ParseResult.GetValueForArgument(argument);
                property.SetValue(target, value);
            }
        }

        // bind all the options to the instance
        foreach (var option in command.Options)
        {
            // option names might have a dash in them, but the property names do not
            var property = type.GetProperty(option.Name.Replace("-", ""), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is not null)
            {
                var value = context.ParseResult.GetValueForOption(option);
                property.SetValue(target, value);
            }
        }

        try
        {
            // now that all the target object's properties have been set, invoke the method
            var task = method.Invoke(target, GetAsArguments(services, method)) as Task<int> ?? throw new InvalidOperationException($"Could not invoke method {method.Name}");
            context.ExitCode = task.GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Message}", e.InnerException?.Message ?? e.Message);
            context.ExitCode = -1;
        }
    }

    private static object[] GetAsArguments(IServiceProvider services, MethodInfo method)
    {
        // for every parameter the target method has, get the service from the container with a matching type
        var parameters = method.GetParameters();
        var arguments = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            arguments[i] = services.GetService(parameter.ParameterType) ?? throw new InvalidOperationException($"Could not get service of type {parameter.ParameterType.FullName}");
        }

        return arguments;
    }
}
