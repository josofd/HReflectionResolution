using HReflectionResolution.ViewModels;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;

namespace HReflectionResolution
{
    public class ReflectionResolutor : IDisposable
    {
        public static readonly string CacheFilePath = Path.Combine(Directory.GetCurrentDirectory(), "cache.bin");
        public string DomainReflectionName = "Reflection Domain {0}";

        private AssemblyReflectionManager _manager = new AssemblyReflectionManager();
        private List<AssemblyResolution> _resolvedCache = new List<AssemblyResolution>();
        private List<AssemblyName> _dependenciesProcessed = new List<AssemblyName>();
        private List<AssemblyName> _unresolvedReferences = new List<AssemblyName>();
        private string _targetAssemblyPath;
        private List<DirectoryInfo> _probingDirectories;

        public ResolveResult Resolve(string targetAssemblyPath)
        {
            ResolveResult response = new ResolveResult();

            var watch = Stopwatch.StartNew();

            _targetAssemblyPath = targetAssemblyPath;

            // Get probing paths information by target assembly
            _probingDirectories = AssemblyReflectionManager.GetProbingPathsByTargetAssemblyFile(_targetAssemblyPath);

            // Include also target assembly directory in probing list because this will be use in dependencies search
            _probingDirectories.Add(new DirectoryInfo(targetAssemblyPath).Parent);

            // Load target assembly in random domain and get dependencies
            DomainReflectionName = string.Format(DomainReflectionName, Extensions.RandomNumberString(10));
            response.Resolved = _manager.LoadAssembly(targetAssemblyPath, DomainReflectionName);
            if (!response.Resolved)
            {
                watch.Stop();
                response.TotalSeconds = watch.Elapsed.TotalSeconds;
                return response;
            }

            var resultResolution = _manager.Reflect(targetAssemblyPath, (assembly) =>
            {
                var assemblyViewModel = new AssemblyResolution(assembly);
                return assemblyViewModel;
            });

            // If already contains the target assembly in cache remove it.
            var targetAssemblyInCache = _resolvedCache.FirstOrDefault(w => w.ToString() == resultResolution.ToString());
            if (targetAssemblyInCache != null)
                _resolvedCache.Remove(targetAssemblyInCache);

            UpdateCache(resultResolution);
            UpdateUnresolvedReferences(resultResolution);

            // Resolve each dependency and sub dependency
            while (_unresolvedReferences.Count > 0)
            {
                var nextResolution = _unresolvedReferences.First();

                // It is not necessary process two times the same dependency
                if (_dependenciesProcessed.Any(a => a.ToString() == nextResolution.ToString()))
                {
                    _unresolvedReferences.Remove(nextResolution);
                    continue;
                }

                // First find file locally
                var localAssemblyFileFound = FindFileByAssemblyNameInProbingDirectories(nextResolution);

                // Check the cache
                resultResolution = _resolvedCache.FirstOrDefault(a => a.ToString() == nextResolution.ToString());
                if (resultResolution != null)
                {
                    // If dependency is in cache and not exist locally, considere cache option
                    if (!string.IsNullOrWhiteSpace(resultResolution.Location) &&
                        File.Exists(resultResolution.Location) &&
                        localAssemblyFileFound == null)
                    {
                        UpdateUnresolvedReferences(resultResolution);
                        _dependenciesProcessed.Add(nextResolution);
                        _unresolvedReferences.Remove(nextResolution);
                        continue;
                    }
                }

                // Resolve assembly name (from GAC) or assembly path
                var assemblyToResolve = string.Empty;
                if (localAssemblyFileFound != null)
                {
                    _manager.LoadAssembly(localAssemblyFileFound.FullName, DomainReflectionName);
                    assemblyToResolve = localAssemblyFileFound.FullName;
                }
                else
                {
                    _manager.LoadAssembly(nextResolution, DomainReflectionName);
                    assemblyToResolve = nextResolution.ToString();
                }

                resultResolution = _manager.Reflect(assemblyToResolve, (assembly) =>
                {
                    AssemblyResolution assemblyViewModel = null;

                    if (assembly != null)
                    {
                        assemblyViewModel = new AssemblyResolution(assembly);
                    }

                    return assemblyViewModel;
                });

                // Not found dependency
                if (resultResolution == null)
                    resultResolution = new AssemblyResolution(nextResolution);

                // Update assembly name because .NET Framework load the last version
                // Ex: mscorlib.dll, Version 2.0.0.0
                //        - .NET Framework will loaded: mscorlib.dll, Version 4.0.0.0
                resultResolution.AssemblyName = nextResolution;

                UpdateCache(resultResolution);
                UpdateUnresolvedReferences(resultResolution);

                _dependenciesProcessed.Add(nextResolution);
                _unresolvedReferences.Remove(nextResolution);
            }

            watch.Stop();
            response.Resolved = true;
            response.TotalSeconds = watch.Elapsed.TotalSeconds;
            return response;
        }

        public void GenerateApplicationManifest(GenerateApplicationManifestOptions options)
        {
            var targetAssemblyDirectory = string.Format(@"{0}\", new DirectoryInfo(_targetAssemblyPath).Parent.FullName);

            ApplicationManifest appManifest = new ApplicationManifest();
            appManifest.AssemblyIdentity = AssemblyIdentity.FromFile(_targetAssemblyPath);
            appManifest.EntryPoint = new AssemblyReference(_targetAssemblyPath);
            appManifest.EntryPoint.AssemblyIdentity = AssemblyIdentity.FromFile(_targetAssemblyPath);
            appManifest.TrustInfo = new TrustInfo();
            appManifest.TrustInfo.SameSiteAccess = "site";
            appManifest.TrustInfo.PermissionSet = new System.Security.PermissionSet(System.Security.Permissions.PermissionState.Unrestricted);
            appManifest.Product = string.IsNullOrWhiteSpace(options.Product) ? string.Empty : options.Product;
            appManifest.Publisher = string.IsNullOrWhiteSpace(options.Company) ? string.Empty : options.Company;

            // Include founded references
            var resolvedAssemblies = GetResolvedAssemblies().Where(w => !w.GAC && !string.IsNullOrWhiteSpace(w.Location));
            foreach (var resolved in resolvedAssemblies)
            {
                var assemblyReference = new AssemblyReference(resolved.Location);
                assemblyReference.AssemblyIdentity = AssemblyIdentity.FromFile(resolved.Location);
                assemblyReference.Size = resolved.Size;
                assemblyReference.Hash = resolved.Hash;
                assemblyReference.TargetPath = resolved.Location.Replace(targetAssemblyDirectory, string.Empty);
                appManifest.AssemblyReferences.Add(assemblyReference);

                var fileReference = new FileReference(resolved.Location);
                fileReference.Size = resolved.Size;
                fileReference.Hash = resolved.Hash;
                fileReference.TargetPath = resolved.Location.Replace(targetAssemblyDirectory, string.Empty);
                appManifest.FileReferences.Add(fileReference);

                foreach (var resolvedResource in resolved.Resources)
                {
                    var fileResource = new FileReference(resolvedResource.Location);
                    fileResource.Size = resolvedResource.Size;
                    fileResource.Hash = resolvedResource.Hash;
                    fileResource.TargetPath = resolvedResource.Location.Replace(targetAssemblyDirectory, string.Empty);
                    appManifest.FileReferences.Add(fileResource);
                }
            }

            // Include target assembly configuration file
            var configFile = string.Format("{0}.config", _targetAssemblyPath);
            if (File.Exists(configFile))
            {
                appManifest.ConfigFile = configFile;
                appManifest.FileReferences.Add(new FileReference(configFile)
                {
                    Hash = AssemblyResolutionResult.CalculateHash(configFile),
                    Size = AssemblyResolutionResult.CalculateSize(configFile),
                    TargetPath = configFile.Replace(targetAssemblyDirectory, string.Empty)
                });
            }

            // Include target assembly
            appManifest.FileReferences.Add(new FileReference(_targetAssemblyPath)
            {
                Hash = AssemblyResolutionResult.CalculateHash(_targetAssemblyPath),
                Size = AssemblyResolutionResult.CalculateSize(_targetAssemblyPath),
                TargetPath = _targetAssemblyPath.Replace(targetAssemblyDirectory, string.Empty)
            });

            // Include resource files from target assembly
            foreach (var resolvedResource in _resolvedCache.First(w => w.Location == _targetAssemblyPath).Resources)
            {
                appManifest.FileReferences.Add(new FileReference(resolvedResource.Location)
                {
                    Hash = resolvedResource.Hash,
                    Size = resolvedResource.Size,
                    TargetPath = resolvedResource.Location.Replace(targetAssemblyDirectory, string.Empty)
                });
            }

            // Include if exist configuration files
            foreach (var configItem in options.AttachedFiles)
            {
                if (File.Exists(configItem))
                {
                    appManifest.FileReferences.Add(new FileReference(configItem)
                    {
                        Hash = AssemblyResolutionResult.CalculateHash(configItem),
                        Size = AssemblyResolutionResult.CalculateSize(configItem),
                        TargetPath = configItem.Replace(targetAssemblyDirectory, string.Empty)
                    });
                }
            }

            appManifest.Validate();

            ManifestWriter.WriteManifest(appManifest, string.Format("{0}.manifest", _targetAssemblyPath));
        }

        public void GenerateDeploymentManifest(GenerateManifestOptions options)
        {
            var targetAssemblyDirectory = string.Format(@"{0}\", new DirectoryInfo(_targetAssemblyPath).Parent.FullName);
            var manifestFile = string.Format("{0}.manifest", _targetAssemblyPath);

            DeployManifest deployManifest = new DeployManifest();
            deployManifest.AssemblyIdentity = AssemblyIdentity.FromFile(_targetAssemblyPath);
            deployManifest.Product = string.IsNullOrWhiteSpace(options.Product) ? string.Empty : options.Product;
            deployManifest.Publisher = string.IsNullOrWhiteSpace(options.Company) ? string.Empty : options.Company;

            var assemblyReference = new AssemblyReference(manifestFile);
            assemblyReference.AssemblyIdentity = AssemblyIdentity.FromFile(manifestFile);
            assemblyReference.Size = AssemblyResolutionResult.CalculateSize(manifestFile);
            assemblyReference.Hash = AssemblyResolutionResult.CalculateHash(manifestFile);
            deployManifest.AssemblyReferences.Add(assemblyReference);

            deployManifest.Validate();

            var targetAssenblyPath = string.Format("{0}.application", _targetAssemblyPath);
            targetAssenblyPath = targetAssenblyPath.Replace(".exe", string.Empty);

            ManifestWriter.WriteManifest(deployManifest, targetAssenblyPath);
        }

        public void LoadCacheFile()
        {
            if (!File.Exists(CacheFilePath))
                return;

            try
            {
                using (Stream stream = File.Open(CacheFilePath, FileMode.Open))
                {
                    BinaryFormatter bin = new BinaryFormatter();

                    _resolvedCache = (List<AssemblyResolution>)bin.Deserialize(stream);
                }
            }
            catch { }
        }

        internal List<AssemblyResolution> GetResolvedAssembliesAndDependencies()
        {
            List<AssemblyResolution> result = new List<AssemblyResolution>();

            foreach (var item in _dependenciesProcessed)
            {
                var found = _resolvedCache.FirstOrDefault(w => w.ToString() == item.ToString());
                result.Add(found);
            }

            return result.OrderBy(o => o.GAC).ToList();
        }

        public void WriteCacheFile()
        {
            try
            {
                using (Stream stream = File.Open(CacheFilePath, FileMode.Create))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    bin.Serialize(stream, _resolvedCache);
                }
            }
            catch { }
        }

        public static void DeleteFileCahe()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                    File.Delete(CacheFilePath);
            }
            catch { }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private FileInfo FindFileByAssemblyNameInProbingDirectories(AssemblyName unresolvedReference)
        {
            FileInfo found = null;

            for (int i = 0; i < _probingDirectories.Count; i++)
            {
                found = FindFileByVersionInProbingDirectory(_probingDirectories[i], unresolvedReference.Name + ".dll");
                if (found == null)
                    found = FindFileByVersionInProbingDirectory(_probingDirectories[i], unresolvedReference.Name + ".exe");

                if (found != null)
                    break;
            }

            return found;
        }

        private FileInfo FindFileByVersionInProbingDirectory(DirectoryInfo directory, string fileName)
        {
            FileInfo found = directory
                    .GetFiles(fileName, SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();

            return found;
        }

        private void UpdateCache(AssemblyResolution assemblyViewModel)
        {
            var cache = _resolvedCache.FirstOrDefault(w => w.ToString() == assemblyViewModel.ToString());
            if (cache == null)
            {
                _resolvedCache.Add(assemblyViewModel);
            }
            else
            {
                cache.Dependencies = assemblyViewModel.Dependencies;
                cache.GAC = assemblyViewModel.GAC;
                cache.Location = assemblyViewModel.Location;
            }
        }

        private void UpdateUnresolvedReferences(AssemblyResolution assemblyViewModel)
        {
            if (assemblyViewModel.GAC)
                return;

            _unresolvedReferences.AddRange(assemblyViewModel.Dependencies);

            _unresolvedReferences = _unresolvedReferences
                .GroupBy(g => g.ToString())
                .Select(s => s.First())
                .ToList();
        }

        public List<AssemblyResolutionResult> GetResolvedAssemblies()
        {
            List<AssemblyResolutionResult> result = new List<AssemblyResolutionResult>();

            foreach (var item in _dependenciesProcessed)
            {
                var found = _resolvedCache.FirstOrDefault(w => w.ToString() == item.ToString());
                result.Add(new AssemblyResolutionResult(found.AssemblyName, found.Location, found.GAC, found.Resources));
            }

            return result.OrderBy(o => o.GAC).ToList();
        }

        ~ReflectionResolutor()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _manager.Dispose();
                _resolvedCache.Clear();
                _unresolvedReferences.Clear();
            }
        }
    }
}
