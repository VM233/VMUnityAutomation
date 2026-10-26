using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VMUnityAutomation.Editor
{
    public static class VmFolderOrganizationPolicy
    {
        public static VmFolderOrganizationResult Review(string projectRoot, string[] paths = null, int maxIssues = 200)
        {
            string config = Path.Combine(projectRoot, VmFolderOrganizationReview.ConfigurationPath);
            bool authored = File.Exists(config);
            if (authored && new FileInfo(config).Length > VmFolderOrganizationReview.MaximumConfigurationBytes)
                throw new InvalidDataException("Folder policy must be at most 64 KiB.");
            string policy = authored ? File.ReadAllText(config) : CreateDefaults(projectRoot).ToString(Formatting.None);
            var result = VmFolderOrganizationReview.Review(projectRoot, policy, paths, maxIssues);
            result.ConfigurationSource = authored ? "project" : "defaults";
            return result;
        }

        private static JObject CreateDefaults(string projectRoot)
        {
            var folders = new JArray(Folder("Assets", 8, 16, false));
            foreach (string path in new[] { "Assets/Scripts", "Assets/Editor", "Assets/Resources",
                         "Assets/GameResources", "Assets/Game Resources" })
                if (Directory.Exists(Path.Combine(projectRoot, path)))
                    folders.Add(Folder(path, path.EndsWith("Scripts") || path.EndsWith("Editor") ? 24 : 64,
                        path.EndsWith("Editor") ? 24 : 32, true));
            return new JObject { ["schemaVersion"] = 1, ["folders"] = folders, ["excludePaths"] = new JArray() };
        }

        private static JObject Folder(string path, int files, int directories, bool recursive) => new JObject
        {
            ["path"] = path, ["maxDirectFiles"] = files, ["maxDirectSubfolders"] = directories,
            ["recursive"] = recursive
        };
    }
}
