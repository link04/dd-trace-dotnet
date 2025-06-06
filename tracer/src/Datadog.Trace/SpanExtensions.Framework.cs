// <copyright file="SpanExtensions.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;

namespace Datadog.Trace
{
    /// <summary>
    /// Extension methods for the <see cref="ISpan"/> interface
    /// </summary>
    public static partial class SpanExtensions
    {
        private static void RunBlockingCheck(Span span, string userId, string userSessionId)
        {
            var security = Security.Instance;

            if (security.AppsecEnabled)
            {
                var securityCoordinator = SecurityCoordinator.TryGet(Security.Instance, span);
                if (securityCoordinator is null)
                {
                    return;
                }

                var result = securityCoordinator.Value.RunWafForUser(userId, userSessionId, fromSdk: true);
                securityCoordinator.Value.BlockAndReport(result);
            }
        }
    }
}
#endif
