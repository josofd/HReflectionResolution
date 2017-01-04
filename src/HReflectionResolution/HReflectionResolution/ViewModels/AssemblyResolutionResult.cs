using System;
using System.Collections.Generic;
using System.Reflection;

namespace HReflectionResolution.ViewModels
{
    [Serializable]
    public class AssemblyResolutionResult : ResolutionResult
    {
        public AssemblyName AssemblyName { get; set; }

        public bool GAC { get; set; }

        public List<ResourceResolutionResult> Resources { get; set; }

        public AssemblyResolutionResult(AssemblyName assemblyName, string location, bool gac, List<ResourceResolutionResult> resources)
            : base(location)
        {
            AssemblyName = assemblyName;
            GAC = gac;
            Resources = resources;
        }

        public override string ToString()
        {
            return AssemblyName.ToString();
        }
    }
}
