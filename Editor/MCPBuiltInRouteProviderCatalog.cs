using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VMUnityAutomation.Editor
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class MCPBuiltInRouteProviderAttribute : Attribute
    {
        public MCPBuiltInRouteProviderAttribute(Type providerType)
        {
            ProviderType = providerType ?? throw new ArgumentNullException(nameof(providerType));
        }

        internal Type ProviderType { get; }
    }

    internal interface IMCPBuiltInRouteProvider
    {
        IReadOnlyList<MCPBuiltInRouteDescriptor> Descriptors { get; }

        string AuditedRouteManifestSha256 { get; }
    }

    /// <summary>
    /// Resolves compile-time-declared route providers from optional package assemblies
    /// and merges their typed descriptors with the core route set exactly once.
    /// </summary>
    internal static class MCPBuiltInRouteProviderCatalog
    {
        internal static MCPBuiltInRouteDescriptor[] Merge(
            IEnumerable<MCPBuiltInRouteDescriptor> coreDescriptors,
            string auditedCoreRouteManifestSha256)
        {
            MCPBuiltInRouteDescriptor[] core =
                (coreDescriptors ?? throw new ArgumentNullException(nameof(coreDescriptors)))
                .ToArray();
            ValidateManifest("core", core,
                auditedCoreRouteManifestSha256);
            var descriptors = new List<MCPBuiltInRouteDescriptor>(core);

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()
                         .OrderBy(item => item.FullName, StringComparer.Ordinal))
            {
                foreach (MCPBuiltInRouteProviderAttribute attribute in assembly
                             .GetCustomAttributes<MCPBuiltInRouteProviderAttribute>()
                             .OrderBy(item => item.ProviderType.FullName, StringComparer.Ordinal))
                {
                    IMCPBuiltInRouteProvider provider = CreateProvider(attribute);
                    ValidateManifest(attribute.ProviderType.FullName,
                        provider.Descriptors,
                        provider.AuditedRouteManifestSha256);
                    descriptors.AddRange(provider.Descriptors);
                }
            }

            string duplicateRoute = descriptors
                .Where(descriptor => descriptor != null)
                .GroupBy(descriptor => descriptor.Route, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(route => route, StringComparer.Ordinal)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(duplicateRoute))
            {
                throw new InvalidOperationException(
                    $"Duplicate built-in route descriptor '{duplicateRoute}'.");
            }
            if (descriptors.Any(descriptor => descriptor == null))
                throw new InvalidOperationException("A built-in route provider returned a null descriptor.");

            return descriptors.OrderBy(descriptor => descriptor.Route, StringComparer.Ordinal).ToArray();
        }

        internal static void ValidateManifest(string owner,
            IEnumerable<MCPBuiltInRouteDescriptor> descriptors,
            string auditedRouteManifestSha256)
        {
            MCPBuiltInRouteDescriptor[] manifest =
                (descriptors ?? throw new ArgumentNullException(nameof(descriptors)))
                .ToArray();
            if (manifest.Any(descriptor => descriptor == null))
            {
                throw new InvalidOperationException(
                    $"Built-in route manifest '{owner}' contains a null descriptor.");
            }

            string actual = MCPToolConfigurationPolicy.ComputeRouteManifestSha256(
                manifest.Select(descriptor => descriptor.Route));
            if (!string.Equals(actual, auditedRouteManifestSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Built-in route manifest '{owner}' has not passed the configuration audit. " +
                    $"Expected SHA-256 '{auditedRouteManifestSha256}', actual '{actual}'.");
            }
        }

        private static IMCPBuiltInRouteProvider CreateProvider(
            MCPBuiltInRouteProviderAttribute attribute)
        {
            Type providerType = attribute.ProviderType;
            if (!typeof(IMCPBuiltInRouteProvider).IsAssignableFrom(providerType) ||
                providerType.IsAbstract || providerType.IsInterface)
            {
                throw new InvalidOperationException(
                    $"Built-in route provider '{providerType.FullName}' must be a concrete " +
                    $"{nameof(IMCPBuiltInRouteProvider)} implementation.");
            }
            if (providerType.GetConstructor(BindingFlags.Instance | BindingFlags.Public |
                                            BindingFlags.NonPublic, null, Type.EmptyTypes, null) == null)
            {
                throw new InvalidOperationException(
                    $"Built-in route provider '{providerType.FullName}' requires a parameterless constructor.");
            }

            var provider = (IMCPBuiltInRouteProvider)Activator.CreateInstance(providerType, true);
            if (provider.Descriptors == null)
            {
                throw new InvalidOperationException(
                    $"Built-in route provider '{providerType.FullName}' returned no descriptor collection.");
            }
            return provider;
        }
    }
}
