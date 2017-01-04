using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Xml.Linq;

namespace HReflectionResolution
{
    /// <summary>
    /// Loading Assemblies from Anywhere into a New AppDomain
    /// http://www.codeproject.com/Articles/453778/Loading-Assemblies-from-Anywhere-into-a-New-AppDom
    /// </summary>
    internal class AssemblyReflectionManager : IDisposable
    {
        Dictionary<string, AppDomain> _mapDomains = new Dictionary<string, AppDomain>();
        Dictionary<string, AppDomain> _loadedAssemblies = new Dictionary<string, AppDomain>();
        Dictionary<string, AssemblyReflectionProxy> _proxies = new Dictionary<string, AssemblyReflectionProxy>();

        public bool LoadAssembly(AssemblyName assemblyName, string domainName)
        {
            // if the assembly was already loaded then fail
            if (_loadedAssemblies.ContainsKey(assemblyName.ToString()))
                return true;

            // check if the appdomain exists, and if not create a new one
            AppDomain appDomain = null;
            if (_mapDomains.ContainsKey(domainName))
            {
                appDomain = _mapDomains[domainName];
            }
            else
            {
                appDomain = CreateChildDomain(AppDomain.CurrentDomain, domainName, assemblyName.ToString());
                _mapDomains[domainName] = appDomain;
            }

            // load the assembly in the specified app domain
            try
            {
                Type proxyType = typeof(AssemblyReflectionProxy);
                if (proxyType.Assembly != null)
                {
                    var proxy = (AssemblyReflectionProxy)appDomain.
                        CreateInstanceFrom(proxyType.Assembly.Location, proxyType.FullName).Unwrap();

                    proxy.LoadAssembly(assemblyName);

                    _loadedAssemblies[assemblyName.ToString()] = appDomain;
                    _proxies[assemblyName.ToString()] = proxy;

                    return true;
                }
            }
            catch { }

            return false;
        }

        public bool LoadAssembly(string assemblyPath, string domainName)
        {
            // if the assembly was already loaded then fail
            if (_loadedAssemblies.ContainsKey(assemblyPath))
                return true;

            // check if the appdomain exists, and if not create a new one
            AppDomain appDomain = null;
            if (_mapDomains.ContainsKey(domainName))
            {
                appDomain = _mapDomains[domainName];
            }
            else
            {
                appDomain = CreateChildDomain(AppDomain.CurrentDomain, domainName, assemblyPath);
                _mapDomains[domainName] = appDomain;
            }

            // load the assembly in the specified app domain
            try
            {
                Type proxyType = typeof(AssemblyReflectionProxy);
                if (proxyType.Assembly != null)
                {
                    var proxy = (AssemblyReflectionProxy)appDomain.
                        CreateInstanceFrom(proxyType.Assembly.Location, proxyType.FullName).Unwrap();

                    proxy.LoadAssembly(assemblyPath);

                    _loadedAssemblies[assemblyPath] = appDomain;
                    _proxies[assemblyPath] = proxy;

                    return true;
                }
            }
            catch { }

            return false;
        }

        public bool UnloadAssembly(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
                return false;

            // check if the assembly is found in the internal dictionaries
            if (_loadedAssemblies.ContainsKey(assemblyPath) &&
               _proxies.ContainsKey(assemblyPath))
            {
                // check if there are more assemblies loaded in the same app domain; in this case fail
                AppDomain appDomain = _loadedAssemblies[assemblyPath];
                int count = _loadedAssemblies.Values.Count(a => a == appDomain);
                if (count != 1)
                    return false;

                try
                {
                    // remove the appdomain from the dictionary and unload it from the process
                    _mapDomains.Remove(appDomain.FriendlyName);
                    AppDomain.Unload(appDomain);

                    // remove the assembly from the dictionaries
                    _loadedAssemblies.Remove(assemblyPath);
                    _proxies.Remove(assemblyPath);

                    return true;
                }
                catch { }
            }

            return false;
        }

        public bool UnloadDomain(string domainName)
        {
            // check the appdomain name is valid
            if (string.IsNullOrEmpty(domainName))
                return false;

            // check we have an instance of the domain
            if (_mapDomains.ContainsKey(domainName))
            {
                try
                {
                    var appDomain = _mapDomains[domainName];

                    // check the assemblies that are loaded in this app domain
                    var assemblies = new List<string>();
                    foreach (var kvp in _loadedAssemblies)
                    {
                        if (kvp.Value == appDomain)
                            assemblies.Add(kvp.Key);
                    }

                    // remove these assemblies from the internal dictionaries
                    foreach (var assemblyName in assemblies)
                    {
                        _loadedAssemblies.Remove(assemblyName);
                        _proxies.Remove(assemblyName);
                    }

                    // remove the appdomain from the dictionary
                    _mapDomains.Remove(domainName);

                    // unload the appdomain
                    AppDomain.Unload(appDomain);

                    return true;
                }
                catch { }
            }

            return false;
        }

        public TResult Reflect<TResult>(string assemblyPath, Func<Assembly, TResult> func)
        {
            // check if the assembly is found in the internal dictionaries
            if (_loadedAssemblies.ContainsKey(assemblyPath) &&
               _proxies.ContainsKey(assemblyPath))
            {
                return _proxies[assemblyPath].Reflect(func);
            }

            return default(TResult);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AssemblyReflectionManager()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var appDomain in _mapDomains.Values)
                    AppDomain.Unload(appDomain);

                _loadedAssemblies.Clear();
                _proxies.Clear();
                _mapDomains.Clear();
            }
        }

        private AppDomain CreateChildDomain(AppDomain parentDomain, string domainName, string assemblyPath)
        {
            AppDomainSetup setup = parentDomain.SetupInformation;

            var probingDirectories = GetProbingPathsByTargetAssemblyFile(assemblyPath).Select(s => s.Name);
            if (probingDirectories.Count() > 0)
            {
                setup.PrivateBinPath = string.Join(";", probingDirectories);
            }

            Evidence evidence = new Evidence(parentDomain.Evidence);
            return AppDomain.CreateDomain(domainName, evidence, setup);
        }

        public static List<DirectoryInfo> GetProbingPathsByTargetAssemblyFile(string assemblyPath)
        {
            List<DirectoryInfo> directories = new List<DirectoryInfo>();
            var fileInfo = new FileInfo(assemblyPath);
            if (!fileInfo.Exists)
                return directories;

            string configFilePath = string.Format("{0}.config", assemblyPath);
            if (File.Exists(configFilePath))
            {
                XElement configFileElement = XElement.Load(configFilePath);

                var probingElement = (from runtime in configFileElement.Descendants("runtime")
                                      from assemblyBinding in runtime.Elements(XName.Get("assemblyBinding", "urn:schemas-microsoft-com:asm.v1"))
                                      from probing in assemblyBinding.Elements(XName.Get("probing", "urn:schemas-microsoft-com:asm.v1"))
                                      select probing).FirstOrDefault();

                if (probingElement != null)
                {
                    var privatePathAttribute = probingElement.Attribute(XName.Get("privatePath"));
                    if (privatePathAttribute != null)
                    {
                        var probing = privatePathAttribute.Value;
                        var probingPaths = probing.Split(';');
                        foreach (var probingPath in probingPaths)
                        {
                            var probingDirectoryInfo = new DirectoryInfo(Path.Combine(fileInfo.Directory.FullName, probingPath));
                            if (probingDirectoryInfo.Exists)
                                directories.Add(probingDirectoryInfo);
                        }
                    }
                }
            }

            return directories;
        }
    }
}
