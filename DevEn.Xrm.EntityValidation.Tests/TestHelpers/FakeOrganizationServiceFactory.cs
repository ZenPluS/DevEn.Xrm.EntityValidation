using System;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.TestHelpers
{
    internal sealed class FakeOrganizationServiceFactory : IOrganizationServiceFactory
    {
        private readonly IOrganizationService _organizationService;

        public FakeOrganizationServiceFactory(IOrganizationService organizationService)
        {
            _organizationService = organizationService;
        }

        public IOrganizationService CreateOrganizationService(Guid? userId)
        {
            return _organizationService;
        }
    }
}
