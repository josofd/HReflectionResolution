using System.Windows;

namespace HReflectionResolution
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    internal partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length == 0)
            {
                new MainWindow().Show();
                return;
            }

            HReflectionResolutionLineArgs lineArgs = new HReflectionResolutionLineArgs(e.Args);
            if (lineArgs.ValidateArguments())
            {
                using (ReflectionResolutor resolutor = new ReflectionResolutor())
                {
                    if (!lineArgs.Cache) ReflectionResolutor.DeleteFileCahe();

                    if (lineArgs.Cache)
                        resolutor.LoadCacheFile();

                    resolutor.Resolve(lineArgs.TargetAssemblyPath);
                    resolutor.GenerateApplicationManifest(new ViewModels.GenerateApplicationManifestOptions()
                    {
                        AttachedFiles = lineArgs.AttachedFiles,
                        Company = lineArgs.Company,
                        Product = lineArgs.Product
                    });
                    resolutor.GenerateDeploymentManifest(new ViewModels.GenerateManifestOptions()
                    {
                        Company = lineArgs.Company,
                        Product = lineArgs.Product
                    });

                    if (lineArgs.Cache)
                        resolutor.WriteCacheFile();
                }
            }

            Application.Current.Shutdown();
        }
    }
}
