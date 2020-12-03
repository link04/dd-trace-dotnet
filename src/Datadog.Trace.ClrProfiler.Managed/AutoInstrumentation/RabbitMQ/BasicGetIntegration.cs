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
    /// RabbitMQ.Client BasicGet calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        Assembly = "RabbitMQ.Client",
        Type = "RabbitMQ.Client.Impl.ModelBase",
        Method = "BasicGet",
        ReturnTypeName = "RabbitMQ.Client.BasicGetResult",
        ParametersTypesNames = new[] { ClrNames.String, ClrNames.Bool },
        MinimumVersion = "3.6.9",
        MaximumVersion = "6.*.*",
        IntegrationName = IntegrationName)]
    public class BasicGetIntegration
    {
        private const string OperationName = "amqp.command";
        private const string IntegrationName = "RabbitMQ";
        private const string Command = "basic.get";
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.RabbitMQ));

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(BasicGetIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="queue">The queue name of the message</param>
        /// <param name="autoAck">The original autoAck argument</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string queue, bool autoAck)
        {
            Scope scope = null;
            RabbitMQTags tags = null;

            scope = CreateScope(Tracer.Instance, Command, queue, IntegrationName, IntegrationId, out tags);
            if (scope != null)
            {
                // add distributed tracing headers to the message
                // SpanContextPropagator.Instance.Inject(scope.Span.Context, new RabbitMQHeadersCollection(basicProperties.Headers));
            }

            return new CallTargetState(new IntegrationState(scope, tags));
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResult">Type of the BasicGetResult</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="basicGetResult">BasicGetResult instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A default CallTargetReturn to satisfy the CallTarget contract</returns>
        public static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult basicGetResult, Exception exception, CallTargetState state)
            where TResult : IBasicGetResult
        {
            IntegrationState integrationState = (IntegrationState)state.State;
            if (integrationState.Scope is null)
            {
                return new CallTargetReturn<TResult>(basicGetResult);
            }

            try
            {
                if (basicGetResult != null)
                {
                    var tags = integrationState.Tags;
                    tags.MessageSize = basicGetResult.Body.Length.ToString();
                }

                if (exception != null)
                {
                    integrationState.Scope.Span.SetException(exception);
                }
            }
            finally
            {
                integrationState.Scope.Dispose();
            }

            return new CallTargetReturn<TResult>(basicGetResult);
        }

        private static Scope CreateScope(Tracer tracer, string command, string queue, string integrationName, IntegrationInfo integrationId, out RabbitMQTags tags)
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
                scope = tracer.StartActiveWithTags(OperationName, tags: tags, serviceName: $"{tracer.DefaultServiceName}-{IntegrationName}");
                var span = scope.Span;

                string queueString = string.IsNullOrEmpty(queue) || !queue.StartsWith("amq.gen-") ? queue : "<generated>";

                span.Type = SpanTypes.MessageConsumer;
                span.ResourceName = $"{command} {queueString}";

                tags.Command = command;
                tags.Queue = queue;

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
