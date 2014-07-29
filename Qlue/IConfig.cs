using System;

namespace Qlue
{
    public interface IConfig
    {
        ICloudCredentials GetCloudCredentials();

        string GetConfigSetting(string key);

        string QueuePrefix { get; }

        string DeploymentVersion { get; }

        string DeploymentEnvironment { get; }
    }
}
