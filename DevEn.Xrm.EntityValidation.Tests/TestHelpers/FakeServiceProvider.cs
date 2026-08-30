using System;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.TestHelpers
{
    /// <summary>
    /// Minimal <see cref="IServiceProvider"/> implementation to run <see cref="Plugin.GenericValidationPlugin"/>
    /// in tests without depending on the plugin-execution APIs of a specific FakeXrmEasy version.
    /// </summary>
    internal sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly IPluginExecutionContext _pluginExecutionContext;
        private readonly IOrganizationService _organizationService;
        private readonly ITracingService _tracingService;

        public FakeServiceProvider(IPluginExecutionContext pluginExecutionContext, IOrganizationService organizationService, ITracingService tracingService)
        {
            _pluginExecutionContext = pluginExecutionContext;
            _organizationService = organizationService;
            _tracingService = tracingService;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IPluginExecutionContext))
            {
                return _pluginExecutionContext;
            }

            if (serviceType == typeof(ITracingService))
            {
                return _tracingService;
            }

            if (serviceType == typeof(IOrganizationServiceFactory))
            {
                return new FakeOrganizationServiceFactory(_organizationService);
            }

            throw new NotSupportedException($"Service not handled by the fake: {serviceType.FullName}");
        }
    }
}
