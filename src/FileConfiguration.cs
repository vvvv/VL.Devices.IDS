using peak.core;
using Path = VL.Lib.IO.Path;

namespace VL.Devices.IDS
{
    [ProcessNode(Name = "FromFile")]
    public class FileConfigurationNode
    {
        IConfiguration? configuration;
        Path? file;

        public IConfiguration Update(Path file)
        {
            if (file != this.file)
            {
                this.file = file;
                configuration = new FileConfiguration(file);

            }
            return configuration!;
        }
    }

    class FileConfiguration : IConfiguration
    {
        public Path File { get; }
        public FileConfiguration(Path file)
        {
            File = file;
        }

        public void Configure(NodeMap nodeMap)
        {
            if (File.Exists)
            {
                //nodeMap.LoadFromFile(File.ToString());
                nodeMap.FindNodeString("UEyeParametersetPath").SetValue(File.ToString());
                nodeMap.FindNodeCommand("UEyeParametersetLoad").Execute();
            }
        }
    }
}
