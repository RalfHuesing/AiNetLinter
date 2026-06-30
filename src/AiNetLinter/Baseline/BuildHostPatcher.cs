using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Reflection;
using Microsoft.Build.Locator;

namespace AiNetLinter.Baseline;

/// <summary>
/// Patches the out-of-process BuildHost.exe folder and configuration for VS 2026/MSBuild 18+ compatibility.
/// </summary>
public static class BuildHostPatcher
{
    public static void PatchBuildHostForVs2026()
    {
        try
        {
            var exeDir = AppContext.BaseDirectory;
            var buildHostDir = Path.Combine(exeDir, "BuildHost-net472");
            if (!Directory.Exists(buildHostDir)) return;

            string? vsDir = FindVs2026MsBuildDir();
            if (vsDir == null) return;

            var configPath = Path.Combine(buildHostDir, "Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.exe.config");
            if (!File.Exists(configPath)) return;

            PatchConfiguration(buildHostDir, vsDir, configPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN]: Failed to patch BuildHost for VS 2026 compatibility: {ex.Message}");
        }
    }

    private static string? FindVs2026MsBuildDir()
    {
        var locatorPath = FindVs2026MsBuildDirViaLocator();
        if (locatorPath != null) return locatorPath;

        return FindVs2026MsBuildDirViaManualScan();
    }

    private static string? FindVs2026MsBuildDirViaLocator()
    {
        try
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances();
            foreach (var instance in instances)
            {
                if (instance.Version != null && instance.Version.Major >= 18)
                {
                    var msbuildBinDir = instance.MSBuildPath;
                    if (Directory.Exists(msbuildBinDir) && File.Exists(Path.Combine(msbuildBinDir, "System.Collections.Immutable.dll")))
                    {
                        return msbuildBinDir;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN]: Error querying Visual Studio instances: {ex.Message}");
        }
        return null;
    }

    private static string? FindVs2026MsBuildDirViaManualScan()
    {
        try
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string vsRoot = Path.Combine(programFiles, "Microsoft Visual Studio");
            if (!Directory.Exists(vsRoot)) return null;

            foreach (var vsVerDir in Directory.GetDirectories(vsRoot))
            {
                var msbuildPath = CheckVsVersionDir(vsVerDir);
                if (msbuildPath != null) return msbuildPath;
            }
        }
        catch (Exception ignored)
        {
            _ = ignored;
        }
        return null;
    }

    private static string? CheckVsVersionDir(string vsVerDir)
    {
        var folderName = Path.GetFileName(vsVerDir);
        if (int.TryParse(folderName, out int majorVer) && majorVer >= 18)
        {
            foreach (var editionDir in Directory.GetDirectories(vsVerDir))
            {
                var msbuildBinDir = Path.Combine(editionDir, @"MSBuild\Current\Bin");
                if (File.Exists(Path.Combine(msbuildBinDir, "System.Collections.Immutable.dll")))
                {
                    return msbuildBinDir;
                }
            }
        }
        return null;
    }

    private static void PatchConfiguration(string buildHostDir, string vsDir, string configPath)
    {
        var doc = new XmlDocument();
        doc.Load(configPath);
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("asm", "urn:schemas-microsoft-com:asm.v1");

        bool modified = false;
        var assemblies = new[]
        {
            "System.Collections.Immutable",
            "System.Text.Json",
            "System.Runtime.CompilerServices.Unsafe",
            "System.Memory",
            "System.Buffers",
            "System.Numerics.Vectors",
            "System.Threading.Tasks.Extensions",
            "System.Text.Encodings.Web",
            "Microsoft.Bcl.AsyncInterfaces",
            "System.IO.Pipelines"
        };

        foreach (var asmName in assemblies)
        {
            if (PatchAssembly(buildHostDir, vsDir, asmName, doc, nsmgr))
            {
                modified = true;
            }
        }

        if (modified)
        {
            doc.Save(configPath);
        }
    }

    private static bool PatchAssembly(string buildHostDir, string vsDir, string asmName, XmlDocument doc, XmlNamespaceManager nsmgr)
    {
        var sourceDll = Path.Combine(vsDir, asmName + ".dll");
        if (!File.Exists(sourceDll)) return false;

        var targetDll = Path.Combine(buildHostDir, asmName + ".dll");
        var vsAssemblyName = AssemblyName.GetAssemblyName(sourceDll);
        var vsVer = vsAssemblyName.Version;
        if (vsVer == null) return false;

        if (File.Exists(targetDll))
        {
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetDll);
            if (targetAssemblyName.Version == vsVer)
            {
                return false;
            }
        }

        File.Copy(sourceDll, targetDll, true);
        return UpdateBindingRedirect(doc, nsmgr, asmName, vsAssemblyName, vsVer);
    }

    private static bool UpdateBindingRedirect(XmlDocument doc, XmlNamespaceManager nsmgr, string asmName, AssemblyName vsAssemblyName, Version vsVer)
    {
        var query = $"/configuration/runtime/asm:assemblyBinding/asm:dependentAssembly[asm:assemblyIdentity[@name='{asmName}']]";
        var depNode = doc.SelectSingleNode(query, nsmgr);
        var newVerStr = vsVer.ToString();

        if (depNode != null)
        {
            var redirectNode = depNode.SelectSingleNode("asm:bindingRedirect", nsmgr) as XmlElement;
            if (redirectNode != null)
            {
                redirectNode.SetAttribute("oldVersion", $"0.0.0.0-{newVerStr}");
                redirectNode.SetAttribute("newVersion", newVerStr);
                return true;
            }
            return false;
        }

        var token = vsAssemblyName.GetPublicKeyToken();
        var tokenStr = "";
        if (token != null && token.Length > 0)
        {
            var sb = new StringBuilder();
            foreach (var b in token) sb.AppendFormat("{0:x2}", b);
            tokenStr = sb.ToString();
        }

        var newDepNode = doc.CreateElement("dependentAssembly", "urn:schemas-microsoft-com:asm.v1");
        
        var identityNode = doc.CreateElement("assemblyIdentity", "urn:schemas-microsoft-com:asm.v1");
        identityNode.SetAttribute("name", asmName);
        identityNode.SetAttribute("publicKeyToken", tokenStr);
        identityNode.SetAttribute("culture", "neutral");
        newDepNode.AppendChild(identityNode);

        var newRedirectNode = doc.CreateElement("bindingRedirect", "urn:schemas-microsoft-com:asm.v1");
        newRedirectNode.SetAttribute("oldVersion", $"0.0.0.0-{newVerStr}");
        newRedirectNode.SetAttribute("newVersion", newVerStr);
        newDepNode.AppendChild(newRedirectNode);

        var bindingRoot = doc.SelectSingleNode("/configuration/runtime/asm:assemblyBinding", nsmgr);
        if (bindingRoot != null)
        {
            bindingRoot.AppendChild(newDepNode);
            return true;
        }
        return false;
    }
}
