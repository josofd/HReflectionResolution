using HReflectionResolution.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace HReflectionResolution
{
    /// <summary>
    /// Interaction logic for DetailView.xaml
    /// </summary>
    internal partial class DetailView : Window
    {
        public DetailView(AssemblyResolution viewModel)
        {
            InitializeComponent();

            Title = "Visualizing dependencies of " + viewModel.ToString();
            DataGridDependencies.ItemsSource = viewModel.Dependencies;
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }
    }
}
