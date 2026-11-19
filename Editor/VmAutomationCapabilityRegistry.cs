using System;
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationCapabilityRegistry
    {
        static Dictionary<string, string> packageVersions;
        static readonly object packageLock = new();
        static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> loadedTypes = new(StringComparer.Ordinal);

        static VmAutomationCapabilityRegistry()
        {
            UnityEditor.PackageManager.Events.registeredPackages += _ =>
            {
                lock (packageLock) System.Threading.Volatile.Write(ref packageVersions, null);
            };
        }
        private sealed class Capability
        {
            internal string Name;
            internal string[] RoutePrefixes = Array.Empty<string>();
            internal string[] ExactRoutes = Array.Empty<string>();
            internal Func<bool> IsAvailable;
            internal string Requirement;
            internal string PackageName;
            internal string[] PackageNames = Array.Empty<string>();
            internal string MinimumVersion;
            internal bool UsesUnityVersion;

            internal bool Matches(string route)
            {
                foreach (string candidate in ExactRoutes)
                    if (string.Equals(candidate, route, StringComparison.Ordinal)) return true;
                foreach (string prefix in RoutePrefixes)
                    if (route.StartsWith(prefix, StringComparison.Ordinal)) return true;
                return false;
            }
        }

        private static readonly Capability[] OptionalCapabilities =
        {
            new Capability
            {
                Name = "vfxgraph",
                RoutePrefixes = new[] { "vfxgraph/" },
                ExactRoutes = new[] { "shadergraph/list-vfx", "shadergraph/open-vfx" },
                IsAvailable = VmAutomationShaderGraphCommands.IsVFXGraphInstalled,
                Requirement = "com.unity.visualeffectgraph",
                PackageName = "com.unity.visualeffectgraph"
            },
            new Capability
            {
                Name = "localization",
                RoutePrefixes = new[] { "localization/" },
                IsAvailable = () =>
                    VmAutomationBuiltInRouteDescriptorRegistry.Contains("localization/status"),
                Requirement = "com.unity.localization",
                PackageName = "com.unity.localization"
            },
            new Capability
            {
                Name = "shadergraph",
                RoutePrefixes = new[] { "shadergraph/" },
                IsAvailable = VmAutomationShaderGraphCommands.IsShaderGraphInstalled,
                Requirement = "com.unity.shadergraph or a render pipeline package that contains Shader Graph",
                PackageNames = new[]
                {
                    "com.unity.shadergraph",
                    "com.unity.render-pipelines.universal",
                    "com.unity.render-pipelines.high-definition",
                }
            },
            new Capability
            {
                Name = "addressables",
                RoutePrefixes = new[] { "addressables/" },
                IsAvailable = () => IsPackageInstalled("com.unity.addressables") &&
                                    TypeExists(
                                        "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject",
                                        "Unity.Addressables.Editor"),
                Requirement = "com.unity.addressables",
                PackageName = "com.unity.addressables"
            },
            new Capability
            {
                Name = "timeline",
                RoutePrefixes = new[] { "timeline/" },
                IsAvailable = () => IsPackageInstalled("com.unity.timeline") &&
                                    TypeExists("UnityEngine.Timeline.TimelineAsset",
                                        "Unity.Timeline"),
                Requirement = "com.unity.timeline",
                PackageName = "com.unity.timeline"
            },
            new Capability
            {
                Name = "cinemachine",
                RoutePrefixes = new[] { "cinemachine/" },
                IsAvailable = () => IsPackageInstalled("com.unity.cinemachine") &&
                                    (TypeExists("Unity.Cinemachine.CinemachineCamera",
                                         "Unity.Cinemachine") ||
                                     TypeExists("Cinemachine.CinemachineVirtualCameraBase",
                                         "Cinemachine")),
                Requirement = "com.unity.cinemachine",
                PackageName = "com.unity.cinemachine"
            },
            new Capability
            {
                Name = "build-profile",
                ExactRoutes = new[] { "build/profile" },
                IsAvailable = () => TypeExists("UnityEditor.Build.Profile.BuildProfile"),
                Requirement = "Unity 6 Build Profiles",
                MinimumVersion = "6000.0",
                UsesUnityVersion = true
            },
            new Capability
            {
                Name = "project-auditor",
                RoutePrefixes = new[] { "project-auditor/" },
                IsAvailable = () =>
                    TypeExists("Unity.ProjectAuditor.Editor.ProjectAuditor",
                        "UnityEditor.ProjectAuditorModule"),
                Requirement = "Unity 6.4+ Project Auditor",
                MinimumVersion = "6000.4",
                UsesUnityVersion = true
            }
        };

        internal static bool IsRouteAvailable(string route)
        {
            Capability capability = FindForRoute(route);
            return capability == null || SafeIsAvailable(capability);
        }

        internal static int AvailabilityMask
        {
            get
            {
                int mask = 0;
                for (int i = 0; i < OptionalCapabilities.Length; i++)
                    if (SafeIsAvailable(OptionalCapabilities[i])) mask |= 1 << i;
                return mask;
            }
        }

        internal static bool IsRouteAvailable(string route, int availabilityMask)
        {
            for (int i = 0; i < OptionalCapabilities.Length; i++)
                if (OptionalCapabilities[i].Matches(route)) return (availabilityMask & (1 << i)) != 0;
            return true;
        }

        internal static string GetCapabilityName(string route)
        {
            return FindForRoute(route)?.Name ?? "core";
        }

        internal static object GetCapabilities()
        {
            var optional = OptionalCapabilities.Select(capability => new Dictionary<string, object>
            {
                { "name", capability.Name },
                { "routePrefixes", capability.RoutePrefixes.ToList() },
                { "exactRoutes", capability.ExactRoutes.ToList() },
                { "available", SafeIsAvailable(capability) },
                { "requirement", capability.Requirement },
                { "version", GetDetectedVersion(capability) },
            }).ToList();

            foreach (var item in optional.Zip(OptionalCapabilities,
                         (dictionary, capability) => new { dictionary, capability }))
            {
                if (!string.IsNullOrEmpty(item.capability.PackageName))
                    item.dictionary["packageName"] = item.capability.PackageName;
                if (item.capability.PackageNames.Length > 0)
                {
                    item.dictionary["packageNames"] =
                        item.capability.PackageNames.ToList();
                    item.dictionary["detectedPackageName"] =
                        FindInstalledPackageName(item.capability.PackageNames);
                }
                if (!string.IsNullOrEmpty(item.capability.MinimumVersion))
                    item.dictionary["minimumVersion"] = item.capability.MinimumVersion;
            }

            return new Dictionary<string, object>
            {
                { "coreAvailable", true },
                { "optional", optional },
                { "availableOptional", optional.Where(item => Convert.ToBoolean(item["available"]))
                    .Select(item => item["name"]).ToList() },
                { "unavailableOptional", optional.Where(item => !Convert.ToBoolean(item["available"]))
                    .Select(item => item["name"]).ToList() }
            };
        }

        private static Capability FindForRoute(string route)
        {
            if (string.IsNullOrEmpty(route))
                return null;

            foreach (var capability in OptionalCapabilities)
                if (capability.Matches(route)) return capability;
            return null;
        }

        private static bool SafeIsAvailable(Capability capability)
        {
            try
            {
                return capability.IsAvailable();
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsCapabilityAvailable(string name)
        {
            foreach (var capability in OptionalCapabilities)
                if (string.Equals(capability.Name, name, StringComparison.Ordinal)) return SafeIsAvailable(capability);
            return false;
        }

        private static string GetDetectedVersion(Capability capability)
        {
            if (capability.UsesUnityVersion)
                return UnityEngine.Application.unityVersion ?? "";
            if (!string.IsNullOrEmpty(capability.PackageName))
                return FindPackageVersion(capability.PackageName);
            return FindPackageVersion(
                FindInstalledPackageName(capability.PackageNames));
        }

        private static string FindInstalledPackageName(string[] packageNames)
        {
            foreach (string name in packageNames ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(FindPackageVersion(name))) return name;
            return "";
        }

        private static bool IsPackageInstalled(string packageName)
        {
            return !string.IsNullOrEmpty(FindPackageVersion(packageName));
        }

        private static string FindPackageVersion(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return "";
            try
            {
                var snapshot = System.Threading.Volatile.Read(ref packageVersions);
                if (snapshot == null)
                {
                    lock (packageLock)
                    {
                        snapshot = packageVersions;
                        if (snapshot == null)
                        {
                            var registered = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
                            if (registered == null || registered.Length == 0) return "";
                            snapshot = new Dictionary<string, string>(registered.Length, StringComparer.Ordinal);
                            foreach (var package in registered)
                                if (package != null && !string.IsNullOrEmpty(package.name)) snapshot[package.name] = package.version;
                            System.Threading.Volatile.Write(ref packageVersions, snapshot);
                        }
                    }
                }
                return snapshot.TryGetValue(packageName, out string version) ? version ?? "" : "";
            }
            catch
            {
                return "";
            }
        }

        private static bool TypeExists(string fullName, string assemblyName = null)
        {
            if (string.IsNullOrEmpty(fullName))
                return false;
            if (loadedTypes.ContainsKey(fullName)) return true;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType(fullName, false) != null)
                    { loadedTypes.TryAdd(fullName, true); return true; }
                }
                catch
                {
                    // Optional package assemblies can be mid-reload. Treat them as unavailable.
                }
            }

            if (!string.IsNullOrEmpty(assemblyName))
            {
                try
                {
                    var assembly = LoadOptionalAssembly(assemblyName);
                    if (assembly?.GetType(fullName, false) != null)
                    { loadedTypes.TryAdd(fullName, true); return true; }
                }
                catch
                {
                    // Installed optional assemblies are resolved lazily and can be mid-reload.
                }
            }

            return false;
        }

        private static System.Reflection.Assembly LoadOptionalAssembly(string assemblyName)
        {
            try
            {
                return System.Reflection.Assembly.Load(assemblyName);
            }
            catch
            {
                string projectRoot = System.IO.Path.GetDirectoryName(
                    UnityEngine.Application.dataPath);
                string assemblyPath = System.IO.Path.Combine(projectRoot ?? "",
                    "Library", "ScriptAssemblies", assemblyName + ".dll");
                return System.IO.File.Exists(assemblyPath)
                    ? System.Reflection.Assembly.LoadFrom(assemblyPath)
                    : null;
            }
        }
    }
}
