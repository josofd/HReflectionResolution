using HReflectionResolution.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

namespace HReflectionResolution
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    internal partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Resolve()
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.DefaultExt = "dll";
            openDialog.Filter = "Assembly files (*.dll)|*.dll|Executable files (*.exe)|*.exe";
            openDialog.Title = "Choose file to resolution";
            var resultOpen = openDialog.ShowDialog();
            if (resultOpen == System.Windows.Forms.DialogResult.OK)
            {
                TargetAssemblyFileName.Text = openDialog.FileName;

                DataGridDependencies.ItemsSource = null;

                if (ToggleCache.IsChecked == false)
                {
                    ReflectionResolutor.DeleteFileCahe();
                }

                using (ReflectionResolutor resolutor = new ReflectionResolutor())
                {
                    resolutor.LoadCacheFile();

                    var result = resolutor.Resolve(openDialog.FileName);
                    var resolvedAssemblies = resolutor.GetResolvedAssembliesAndDependencies();

                    DataGridDependencies.ItemsSource = resolvedAssemblies;
                    TextBlockTotal.Text = string.Format("Resolved: {0}  |   Total: {1}  |   Resolved in {2} seconds",
                        result.Resolved,
                        resolvedAssemblies.Count,
                        result.TotalSeconds);

                    resolutor.WriteCacheFile();

                    if (ToggleGenerateManifest.IsChecked == true)
                    {
                        var attachedFiles = new List<string>();

                        openDialog = new OpenFileDialog();
                        openDialog.Filter = "Attached files (*.*)|*.*";
                        openDialog.Title = "Choose the attached files to include in manifest";
                        resultOpen = openDialog.ShowDialog();
                        if (resultOpen == System.Windows.Forms.DialogResult.OK)
                        {
                            attachedFiles = openDialog.FileNames.ToList();
                        }

                        resolutor.GenerateApplicationManifest(new GenerateApplicationManifestOptions()
                        {
                            Company = "Company",
                            Product = "Product",
                            AttachedFiles = attachedFiles
                        });

                        resolutor.GenerateDeploymentManifest(new GenerateManifestOptions()
                        {
                            Company = "Company",
                            Product = "Product"
                        });

                        System.Windows.MessageBox.Show("Resolution completed!", "Reflection Resolution");
                    }
                }
            }
        }

        private void TargetAssemblyFileName_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Resolve();
        }

        private void Row_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridRow row = sender as DataGridRow;
            var viewModel = row.DataContext as AssemblyResolution;
            if (viewModel.Dependencies.Count > 0)
            {
                DetailView detail = new DetailView(row.DataContext as AssemblyResolution);
                detail.ShowDialog();
            }
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }
    }
}
