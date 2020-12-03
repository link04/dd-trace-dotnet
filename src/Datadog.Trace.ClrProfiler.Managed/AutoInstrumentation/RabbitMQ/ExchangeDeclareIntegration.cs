using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// RabbitMQ.Client ExchangeDeclare calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        Assembly = "RabbitMQ.Client",
        Type = "RabbitMQ.Client.Framing.Impl.Model",
        Method = "_Private_ExchangeDeclare",
        ReturnTypeName = ClrNames.Void,
        ParametersTypesNames = new[] { ClrNames.String, ClrNames.String, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, ClrNames.Ignore },
        MinimumVersion = "3.6.9",
        MaximumVersion = "6.*.*",
        IntegrationName = IntegrationName)]
    public class ExchangeDeclareIntegration
    {
        private const string OperationName = "amqp.command";
        private const string IntegrationName = "RabbitMQ";
        private const string Command = "exchange.declare";
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.RabbitMQ));

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(ExchangeDeclareIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exchange">Name of the exchange.</param>
        /// <param name="type">Type of the exchange.</param>
        /// <param name="passive">The original passive setting</param>
        /// <param name="durable">The original duable setting</param>
        /// <param name="autoDelete">The original autoDelete setting</param>
        /// <param name="internal">The original internal setting</param>
        /// <param name="nowait">The original nowait setting</param>
        /// <param name="arguments">The original arguments setting</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string exchange, string type, bool passive, bool durable, bool autoDelete, bool @internal, bool nowait, IDictionary<string, object> arguments)
        {
            Scope scope = null;
            RabbitMQTags tags = null;

            scope = CreateScope(Tracer.Instance, Command, exchange, IntegrationName, IntegrationId, out tags);

            return new CallTargetState(new IntegrationState(scope, tags));
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A default CallTargetReturn to satisfy the CallTarget contract</returns>
        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            IntegrationState integrationState = (IntegrationState)state.State;
            if (integrationState.Scope is null)
            {
                return default;
            }

            try
            {
                if (exception != null)
                {
                    integrationState.Scope.Span.SetException(exception);
                }
            }
            finally
            {
                integrationState.Scope.Dispose();
            }

            return default;
        }

        private static Scope CreateScope(Tracer tracer, string command, string exchange, string integrationName, IntegrationInfo integrationId, out RabbitMQTags tags)
        {
            tags = null;

            if (!tracer.Settings.IsIntegrationEnabled(integrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                Span parent = tracer.ActiveScope?.Span;

                tags = new RabbitMQTags();
                scope = tracer.StartActiveWithTags(OperationName, tags: tags, serviceName: $"{tracer.DefaultServiceName}-{IntegrationName}");
                var span = scope.Span;

                span.Type = SpanTypes.MessageProducer;
                span.ResourceName = command;

                tags.Command = command;
                tags.Exchange = exchange;

                tags.InstrumentationName = integrationName;
                tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }

        private readonly struct IntegrationState
        {
            public readonly Scope Scope;
            public readonly RabbitMQTags Tags;

            public IntegrationState(Scope scope, RabbitMQTags tags)
            {
                Scope = scope;
                Tags = tags;
            }
        }
    }
}
