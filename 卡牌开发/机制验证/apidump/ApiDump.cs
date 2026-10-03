using System;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class ApiDump
{
    private static string[] _searchDirs = Array.Empty<string>();

    private static int Main(string[] args)
    {
        // args: <searchDir1> <searchDir2> <dllFileName> <nameFilter1> [nameFilter2 ...]
        _searchDirs = new[] { args[0], args[1] };
        AppDomain.CurrentDomain.AssemblyResolve += OnResolve;

        var dllName = args[2];
        var filters = args.Skip(3).ToArray();
        var path = new[] { args[0], args[1] }.Select(d => Path.Combine(d, dllName)).FirstOrDefault(File.Exists);
        if (path == null)
        {
            Console.WriteLine("DLL not found: " + dllName);
            return 1;
        }

        var asm = Assembly.LoadFrom(path);
        Type[] types;
        try
        {
            types = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
        }

        foreach (var t in types.Where(t => t.FullName != null && filters.Any(f => t.FullName.Contains(f))))
        {
            Console.WriteLine("TYPE " + t.FullName + (t.IsPublic || t.IsNestedPublic ? " [public]" : " [internal]") +
                (t.BaseType != null ? " : " + t.BaseType.Name : ""));
            foreach (var c in t.GetConstructors())
                Console.WriteLine("  ctor(" + string.Join(", ", c.GetParameters().Select(P)) + ")");
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                Console.WriteLine("  " + (m.IsStatic ? "static " : "") + Sig(m.ReturnType) + " " + m.Name +
                    (m.IsGenericMethodDefinition ? "<" + string.Join(",", m.GetGenericArguments().Select(a => a.Name)) + ">" : "") +
                    "(" + string.Join(", ", m.GetParameters().Select(P)) + ")");
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                Console.WriteLine("  prop " + Sig(p.PropertyType) + " " + p.Name);
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                Console.WriteLine("  field " + Sig(f.FieldType) + " " + f.Name);
        }
        return 0;
    }

    private static string P(ParameterInfo p) => Sig(p.ParameterType) + " " + p.Name + (p.HasDefaultValue ? " = default" : "");

    private static string Sig(Type t)
    {
        if (t.IsByRef) return Sig(t.GetElementType()!) + "&";
        if (t.IsGenericType)
            return t.Name.Split('`')[0] + "<" + string.Join(",", t.GetGenericArguments().Select(Sig)) + ">";
        return t.Name;
    }

    private static Assembly? OnResolve(object? sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name + ".dll";
        foreach (var dir in _searchDirs)
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path)) return Assembly.LoadFrom(path);
        }
        return null;
    }
}
