namespace HReflectionResolution.ViewModels
{
    public class ResolveResult
    {
        public bool Resolved { get; set; }
        public double TotalSeconds { get; set; }

        public override string ToString()
        {
            return string.Format("Resolved: {0} in {2} seconds", Resolved, TotalSeconds);
        }
    }
}
