using System;
using System.Threading.Tasks;

namespace Qlue.Pipeline.Component
{
    public interface IPipelineComponent
    {
        Task Execute(PipelineContext context);
    }
}
