using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.TestHelpers
{
    internal sealed class FakeTracingService : ITracingService
    {
        public void Trace(string format, params object[] args)
        {
            // No need to assert on tracing in tests: just don't throw exceptions during plugin execution.
        }
    }
}
