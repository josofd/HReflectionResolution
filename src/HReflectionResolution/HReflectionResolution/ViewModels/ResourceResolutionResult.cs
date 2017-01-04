using System;
namespace HReflectionResolution.ViewModels
{
    [Serializable]
    public class ResourceResolutionResult : ResolutionResult
    {
        public ResourceResolutionResult(string location)
            : base(location)
        {
        }
    }
}
