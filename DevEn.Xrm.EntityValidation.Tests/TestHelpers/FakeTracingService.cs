using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Tests.TestHelpers
{
    internal sealed class FakeTracingService : ITracingService
    {
        private readonly List<string> _messages = new List<string>();

        public IReadOnlyList<string> Messages => _messages;

        public void Trace(string format, params object[] args)
        {
            _messages.Add(args == null || args.Length == 0
                ? format
                : string.Format(CultureInfo.InvariantCulture, format, args));
        }
    }
}
