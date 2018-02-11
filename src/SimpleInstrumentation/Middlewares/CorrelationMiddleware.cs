﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SampleApi.Services;
using Serilog.Context;

namespace SimpleInstrumentation.Middlewares
{
    public class CorrelationMiddleware
    {
        private readonly RequestDelegate _next;

        public CorrelationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Based on:
        ///
        /// - https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/HttpCorrelationProtocol.md
        /// - https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/FlatRequestId.md
        /// - https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/HierarchicalRequestId.md
        /// </summary>
        /// <param name="context"></param>
        /// <param name="correlator"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context, ICorrelator correlator, ILogger<CorrelationMiddleware> logger)
        {
            if (context.Request.Headers.TryGetValue(correlator.HeaderName, out var correlationId))
            {
                logger.LogDebug("Read CorrelationId from header");

                context.TraceIdentifier = correlationId;
            }
            else
            {
                var firstDotIndex = context.TraceIdentifier.IndexOf('.');

                if (firstDotIndex > -1 && context.TraceIdentifier[0] == '|')
                {
                    context.TraceIdentifier = context.TraceIdentifier.Substring(1, firstDotIndex - 1);
                }

                correlationId = context.TraceIdentifier;
            }

            correlator.CorrelationId = correlationId;

            context.Response.OnStarting(() =>
            {
                context.Response.Headers.Add(correlator.HeaderName, new[] { context.TraceIdentifier });
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }
}