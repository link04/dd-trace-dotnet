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
    /// RabbitMQ.Client BasicDeliver calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        Assembly = "RabbitMQ.Client",
        Type = "RabbitMQ.Client.Events.EventingBasicConsumer",
        Method = "HandleBasicDeliver",
        ReturnTypeName = ClrNames.Void,
        ParametersTypesNames = new[] { ClrNames.String, ClrNames.UInt64, ClrNames.Bool, ClrNames.String, ClrNames.String, ClrNames.Ignore, ClrNames.Ignore },
        MinimumVersion = "3.6.9",
        MaximumVersion = "6.*.*",
        IntegrationName = IntegrationName)]
    public class BasicDeliverIntegration
    {
        private const string OperationName = "amqp.command";
        private const string IntegrationName = "RabbitMQ";
        private const string Command = "basic.deliver";
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.RabbitMQ));

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(BasicPublishIntegration));

        private static Func<IDictionary<string, object>, string, IEnumerable<string>> getter = ((carrier, key) =>
        {
            if (carrier.TryGetValue(key, out object value) && value is byte[] bytes)
            {
                return new[] { Encoding.UTF8.GetString(bytes) };
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        });

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TBasicProperties">Type of the message properties</typeparam>
        /// <typeparam name="TBody">Type of the message body</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="consumerTag">The original consumerTag argument</param>
        /// <param name="deliveryTag">The original deliveryTag argument</param>
        /// <param name="redelivered">The original redelivered argument</param>
        /// <param name="exchange">Name of the exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="basicProperties">The message properties.</param>
        /// <param name="body">The message body.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TBasicProperties, TBody>(TTarget instance, string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, TBasicProperties basicProperties, TBody body)
            where TBasicProperties : IBasicProperties
            where TBody : IBody // ReadOnlyMemory<byte> body in 6.0.0
        {
            SpanContext propagatedContext = null;

            // try to extract propagated context values from http headers
            if (basicProperties.Headers != null)
            {
                try
                {
                    propagatedContext = SpanContextPropagator.Instance.Extract(basicProperties.Headers, getter);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting propagated HTTP headers.");
                }
            }

            Scope scope = null;
            RabbitMQTags tags = null;

            scope = CreateScope(Tracer.Instance, propagatedContext, Command, exchange, routingKey, IntegrationName, IntegrationId, out tags);
            if (scope != null)
            {
                tags.MessageSize = body.Length.ToString();
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

        private static Scope CreateScope(Tracer tracer, ISpanContext parentContext, string command, string exchange, string routingKey, string integrationName, IntegrationInfo integrationId, out RabbitMQTags tags)
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

                tags = new RabbitMQConsumerTags();
                scope = tracer.StartActiveWithTags(OperationName, parentContext, tags: tags, serviceName: $"{tracer.DefaultServiceName}-{IntegrationName}");
                var span = scope.Span;

                // JAVA persists the queue name to set the resource to "basic.deliver {queueName}" or "basic.deliver <default>" or "basic.deliver <generated>"
                // string queueString = string.IsNullOrEmpty(queue) ? "<default>" : queue.StartsWith("amq.gen-") ? "<generated>" : queue;

                span.Type = SpanTypes.MessageConsumer;
                span.ResourceName = $"{command}";

                tags.Command = command;
                tags.Exchange = exchange;
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
