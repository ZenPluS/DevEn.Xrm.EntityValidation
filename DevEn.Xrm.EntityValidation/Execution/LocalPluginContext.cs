using System;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Execution
{
    /// <summary>
    /// Extracts and groups the services a Dataverse plugin typically needs from the platform-provided
    /// <see cref="IServiceProvider"/>. Exposes two distinct <see cref="IOrganizationService"/> instances:
    /// one in the calling user's context (for operations that must respect the user's security) and one
    /// with system privileges (for the plugin's internal configuration reads).
    /// </summary>
    internal sealed class LocalPluginContext
    {
        public LocalPluginContext(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            PluginExecutionContext = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            TracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            UserOrganizationService = serviceFactory.CreateOrganizationService(PluginExecutionContext.UserId);
            SystemOrganizationService = serviceFactory.CreateOrganizationService(null);
        }

        public IPluginExecutionContext PluginExecutionContext { get; }

        public ITracingService TracingService { get; }

        public IOrganizationService UserOrganizationService { get; }

        public IOrganizationService SystemOrganizationService { get; }
    }
}
