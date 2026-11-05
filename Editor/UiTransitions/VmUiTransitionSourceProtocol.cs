using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace VMUnityAutomation.Editor
{
    public sealed class VmUiTransitionSourceProtocol
    {
        public string Owner { get; set; }
        public string AssemblyMvid { get; set; }
        public string AssemblyName { get; set; }
        public Arc[] Visibility { get; set; } = Array.Empty<Arc>();
        public Arc[] Content { get; set; } = Array.Empty<Arc>();
        public sealed class Arc
        {
            public string From { get; set; }
            public string To { get; set; }
        }

        internal static VmUiTransitionSourceProtocol Capture()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (assemblies.Length > 512) throw new InvalidOperationException("UI protocol discovery exceeds512 loaded assemblies.");
            var owners = assemblies.Select(assembly => assembly.GetType("VM233.UIElements.UiTransitionProtocol", false))
                .Where(type => type != null).ToArray();
            if (owners.Length > 1) throw new InvalidOperationException("The UI phase protocol has multiple loaded authorities.");
            if (owners.Length == 0) return new VmUiTransitionSourceProtocol();
            Type owner = owners[0];
            return new VmUiTransitionSourceProtocol
            {
                Owner = owner.FullName, AssemblyMvid = owner.Module.ModuleVersionId.ToString(), AssemblyName = owner.Assembly.GetName().Name,
                Visibility = Read(owner, "Visibility"), Content = Read(owner, "Content")
            };
        }

        private static Arc[] Read(Type owner, string member)
        {
            var property = owner.GetProperty(member, BindingFlags.Public | BindingFlags.Static);
            if (property == null) throw new InvalidOperationException("The loaded UI protocol has no " + member + " authority.");
            var values = ((IEnumerable)property.GetValue(null)).Cast<object>().Take(33).ToArray();
            if (values.Length is 0 or > 32) throw new InvalidOperationException("The loaded UI protocol requires1..32 arcs per family.");
            return values.Select(value => new Arc
            {
                From = VmAutomationCSharpSyntax.Property(value, "From").ToString(),
                To = VmAutomationCSharpSyntax.Property(value, "To").ToString()
            }).ToArray();
        }
    }
}
