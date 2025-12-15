using Microsoft.Extensions.DependencyInjection;
using SoftwareCenter.Core.Errors;
using System;

namespace SoftwareCenter.Kernel.Services
{
    public class ErrorHandlerProvider : IErrorHandlerProvider
    {
        private readonly IServiceProvider _serviceProvider;

        public ErrorHandlerProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IErrorHandler GetHandler()
        {
            return _serviceProvider.GetRequiredService<IErrorHandler>();
        }
    }
}
