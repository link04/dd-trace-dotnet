using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// RabbitMQ.Client BasicPublish calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        Assembly = "RabbitMQ.Client",
        Type = "RabbitMQ.Client.Framing.Impl.Model",
        Method = "_Private_BasicPublish",
        ReturnTypeName = ClrNames.Void,
        ParametersTypesNames = new[] { ClrNames.String, ClrNames.String, ClrNames.Bool, ClrNames.Ignore, ClrNames.Ignore },
        MinimumVersion = "3.6.9",
        MaximumVersion = "6.*.*",
        IntegrationName = IntegrationName)]
    public class BasicPublishIntegration
    {
        private const string OperationName = "amqp.command";
        private const string IntegrationName = "RabbitMQ";
        private const string Command = "basic.publish";
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.RabbitMQ));

        private static readonly string[] DeliveryModeStrings = { null, "1", "2" };

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(BasicPublishIntegration));

        private static Action<IDictionary<string, object>, string, string> setter = ((carrier, key, value) =>
        {
            carrier.Add(key, Encoding.UTF8.GetBytes(value));
        });

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TBasicProperties">Type of the message properties</typeparam>
        /// <typeparam name="TBody">Type of the message body</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exchange">Name of the exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="mandatory">The mandatory routing flag.</param>
        /// <param name="basicProperties">The message properties.</param>
        /// <param name="body">The message body.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TBasicProperties, TBody>(TTarget instance, string exchange, string routingKey, bool mandatory, TBasicProperties basicProperties, TBody body)
            where TBasicProperties : IBasicProperties, IDuckType
            where TBody : IBody // ReadOnlyMemory<byte> body in 6.0.0
        {
            Scope scope = null;
            RabbitMQTags tags = null;

            scope = CreateScope(Tracer.Instance, Command, exchange, routingKey, IntegrationName, IntegrationId, out tags);
            if (scope != null)
            {
                tags.MessageSize = body.Length.ToString();

                if (basicProperties.Instance != null && basicProperties.IsDeliveryModePresent())
                {
                    tags.DeliveryMode = DeliveryModeStrings[0x3 & basicProperties.DeliveryMode];
                }

                // add distributed tracing headers to the message
                if (basicProperties.Headers == null)
                {
                    basicProperties.Headers = new Dictionary<string, object>();
                }

                SpanContextPropagator.Instance.Inject(scope.Span.Context, basicProperties.Headers, setter);
            }

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

        private static Scope CreateScope(Tracer tracer, string command, string exchange, string routingKey, string integrationName, IntegrationInfo integrationId, out RabbitMQTags tags)
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

                tags = new RabbitMQProducerTags();
                scope = tracer.StartActiveWithTags(OperationName, tags: tags, serviceName: $"{tracer.DefaultServiceName}-{IntegrationName}");
                var span = scope.Span;

                string exchangeString = string.IsNullOrEmpty(exchange) ? "<default>" : exchange;
                string routingKeyString = string.IsNullOrEmpty(routingKey) ? "<all>" : routingKey.StartsWith("amq.gen-") ? "<generated>" : routingKey;

                span.Type = SpanTypes.MessageProducer;
                span.ResourceName = $"{command} {exchangeString} -> {routingKeyString}";

                tags.Command = command;
                tags.Exchange = exchangeString;
                tags.RoutingKey = routingKey;

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
