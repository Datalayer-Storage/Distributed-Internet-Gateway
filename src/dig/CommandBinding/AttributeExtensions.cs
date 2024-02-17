using System.CommandLine;
using System.CommandLine.Builder;
using System.Reflection;
namespace dig;


internal static class AttributeExtensions
{
    /// <summary>
    /// This will load up all the commands from the assembly and add them to the root command.
    /// It does this by looking at types decorated with the <see cref="CommandAttribute"/> attribute.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="services"></param>
    /// <returns></returns>
    public static CommandLineBuilder UseAttributes(this CommandLineBuilder builder, IServiceProvider services)
    {
        // this assumes we're running in the assembly with all the commands
        var assembly = Assembly.GetExecutingAssembly();
        var types = from type in assembly.GetTypes()
                    let attr = type.GetCustomAttribute<CommandAttribute>()
                    where attr is not null and { Hidden: false }
                    orderby attr.Name
                    select (type, attr);

        foreach (var (type, attr) in types)
        {
            builder.Command.AddCommands(type, attr, services);
        }

        return builder;
    }

    private static void AddCommands(this Command parent, Type type, CommandAttribute commandAttr, IServiceProvider services)
    {
        var command = new Command(commandAttr.Name, commandAttr.Description);

        //
        // this is where all of the attribute values are bound to the command object instance
        //

        // get all the options for the command
        foreach (var (property, optionAttribute) in type.GetAttributedProperties<OptionAttribute>())
        {
            var aliases = new List<string>();
            if (!string.IsNullOrEmpty(optionAttribute.ShortName))
            {
                aliases.Add($"-{optionAttribute.ShortName}");
            }

            if (!string.IsNullOrEmpty(optionAttribute.LongName))
            {
                aliases.Add($"--{optionAttribute.LongName}");
            }

            Type t = typeof(Option<>).MakeGenericType(property.PropertyType);
            var option = Activator.CreateInstance(t, aliases.ToArray(), optionAttribute.Description) as Option ?? throw new InvalidOperationException($"Could not create argument of type {t.FullName}");
            option.IsRequired = optionAttribute.IsRequired;
            option.IsHidden = optionAttribute.IsHidden;
            option.ArgumentHelpName = optionAttribute.ArgumentHelpName ?? property.Name;
            if (optionAttribute.Default is not null)
            {
                option.SetDefaultValue(optionAttribute.Default);
            }

            command.AddOption(option);
        }

        // add required arguments
        foreach (var (property, argAttribute) in type.GetAttributedProperties<ArgumentAttribute>().OrderBy(tuple => tuple.Attribute.Index))
        {
            Type t = typeof(Argument<>).MakeGenericType(property.PropertyType);
            var argument = Activator.CreateInstance(t, argAttribute.Name, argAttribute.Description) as Argument ?? throw new InvalidOperationException($"Could not create argument of type {t.FullName}");
            if (argAttribute.Default is not null)
            {
                argument.SetDefaultValue(argAttribute.Default);
            }
            command.AddArgument(argument);
        }

        // and recurse to add subcommands
        foreach (var (property, subcommand) in type.GetAttributedProperties<CommandAttribute>())
        {
            command.AddCommands(property.PropertyType, subcommand, services);
        }

        var target = type.GetCommandTarget();
        if (target is not null)
        {
            var binder = services.GetRequiredService<ContextBinder>();
            Handler.SetHandler(command, (context) => binder.BindToContext(command, context, target));
        }

        parent.AddCommand(command);
    }

    /// <summary>
    /// Gets the method that is decorated with the <see cref="CommandTargetAttribute"/> attribute.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static MethodInfo? GetCommandTarget(this Type type)
    {
        var targets = from method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                      let attr = method.GetCustomAttribute<CommandTargetAttribute>(true)
                      where attr is not null
                      select method;

        return targets.FirstOrDefault();
    }

    private static IEnumerable<(PropertyInfo Property, T Attribute)> GetAttributedProperties<T>(this Type type) where T : Attribute
    {
        return from property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
               let attr = property.GetCustomAttribute<T>()
               where attr is not null
               select (property, attr);
    }
}
