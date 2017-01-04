using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace HReflectionResolution.ViewModels
{
    [Serializable]
    public class AssemblyResolution
    {
        public AssemblyName AssemblyName { get; set; }
        public string Location { get; internal set; }
        public bool GAC { get; internal set; }

        public List<AssemblyName> Dependencies { get; internal set; }
        public List<ResourceResolutionResult> Resources { get; internal set; }

        public AssemblyResolution(AssemblyName assemblyName)
        {
            AssemblyName = assemblyName;
            Dependencies = new List<AssemblyName>();
            Resources = new List<ResourceResolutionResult>();
        }

        public AssemblyResolution(Assembly assembly)
            : this(assembly.GetName())
        {
            Location = assembly.Location;
            GAC = assembly.GlobalAssemblyCache;
            Dependencies = assembly.GetReferencedAssemblies().ToList();

            if (!GAC) FindResourceAssemblies();
        }

        private void FindResourceAssemblies()
        {
            Directory.EnumerateFiles(new FileInfo(Location).Directory.FullName, string.Format("{0}.resources.dll", AssemblyName.Name), SearchOption.AllDirectories)
                .ToList()
                .ForEach(resourceFile => Resources.Add(new ResourceResolutionResult(resourceFile)));
        }

        public override string ToString()
        {
            return AssemblyName.ToString();
        }
    }
}
