using System.Collections.Generic;

namespace HReflectionResolution.ViewModels
{
    public class GenerateApplicationManifestOptions : GenerateManifestOptions
    {
        public GenerateApplicationManifestOptions()
        {
            AttachedFiles = new List<string>();
        }

        public List<string> AttachedFiles { get; set; }
    }
}
