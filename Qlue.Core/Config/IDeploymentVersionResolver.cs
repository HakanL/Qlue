using System;
using System.Configuration;

namespace Qlue
{
    public interface IDeploymentVersionResolver
    {
        string GetLatestDeploymentVersion();
    }
}
