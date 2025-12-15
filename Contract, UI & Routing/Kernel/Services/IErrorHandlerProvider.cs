using SoftwareCenter.Core.Errors;

namespace SoftwareCenter.Kernel.Services
{
    public interface IErrorHandlerProvider
    {
        IErrorHandler GetHandler();
    }
}
