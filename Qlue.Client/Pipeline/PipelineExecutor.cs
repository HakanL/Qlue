using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Qlue.Pipeline.Component;

namespace Qlue.Pipeline
{
    public class PipelineExecutor : IDisposable
    {
        private List<IPipelineComponent> components;

        public PipelineExecutor(params IPipelineComponent[] components)
        {
            this.components = new List<IPipelineComponent>(components);
        }

        public async Task Execute(PipelineContext context)
        {
            foreach (var component in this.components)
            {
                await component.Execute(context).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.components != null)
                {
                    foreach (var component in this.components)
                        if (component is IDisposable)
                            ((IDisposable)component).Dispose();

                    this.components = null;
                }
            }
        }
    }
}
