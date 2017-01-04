using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace HReflectionResolution
{
    internal class HReflectionResolutionLineArgs
    {
        public string TargetAssemblyPath { get; set; }
        public string Company { get; set; }
        public string Product { get; set; }
        public List<string> AttachedFiles { get; set; }
        public bool Cache { get; set; }

        public HReflectionResolutionLineArgs(string[] args)
        {
            try
            {
                TargetAssemblyPath = args[0];
                Product = args[1];
                Company = args[2];
                Cache = Convert.ToBoolean(args[3]);

                if (args.Length > 4)
                    AttachedFiles = args[4].Split(',').Select(s => s.Trim()).ToList();
                else
                    AttachedFiles = new List<string>();
            }
            catch
            {
                TargetAssemblyPath = "help";
            }
        }

        public string GetLineArgsExample()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("The arguments is not valid");
            sb.AppendLine("Example: ");
            sb.AppendLine(@"    TargetAssemblyPath: C:\temp\MyAssembly.dll");
            sb.AppendLine(@"    Product: Reflection Resolutor");
            sb.AppendLine(@"    Company: Resolutor LTDA.");
            sb.AppendLine(@"    Cache: True");
            sb.AppendLine(@"    Attached Files: C:\temp\config1.config, C:\temp\config2.config");
            sb.AppendLine();
            sb.AppendLine("Parameters: ");
            sb.AppendLine(@"HReflectionResolution.exe C:\temp\MyAssembly.dll ""Reflection Resolutor"" ""Resolutor LTDA."" True ""C:\temp\config1.config, C:\temp\config2.config""");

            Clipboard.SetText(@"HReflectionResolution.exe C:\temp\MyAssembly.dll ""Reflection Resolutor"" ""Resolutor LTDA."" True ""C:\temp\config1.config, C:\temp\config2.config""");

            return sb.ToString();
        }

        public bool ValidateArguments()
        {
            if (TargetAssemblyPath == "h" || TargetAssemblyPath == "help")
            {
                MessageBox.Show(GetLineArgsExample());
                return false;
            }

            if (string.IsNullOrWhiteSpace(TargetAssemblyPath) || !File.Exists(TargetAssemblyPath))
            {
                MessageBox.Show("Target Assembly Path is not valid");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Company))
            {
                MessageBox.Show("Company is not valid");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Product))
            {
                MessageBox.Show("Product is not valid");
                return false;
            }

            foreach (var att in AttachedFiles)
            {
                if (!File.Exists(att))
                {
                    MessageBox.Show("The attached file is not valid: " + att);
                    return false;
                }
            }

            return true;
        }
    }
}
