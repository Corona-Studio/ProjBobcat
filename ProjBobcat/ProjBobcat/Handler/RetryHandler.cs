﻿using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ProjBobcat.Handler
{
    public class RetryHandler : DelegatingHandler
    {
        private readonly int _maxRetries = 5;

        public RetryHandler()
        {
        }

        public RetryHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        public RetryHandler(HttpMessageHandler innerHandler, int maxRetries) : base(innerHandler)
        {
            _maxRetries = maxRetries;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;

            for (var i = 0; i < _maxRetries; i++)
            {
                try
                {
                    response = await base.SendAsync(request, cancellationToken);
                }
                catch (Exception e) when (IsNetworkError(e))
                {
                    Trace.WriteLine($"重试 <{i}>");
                    Trace.WriteLine(e);

                    continue;
                }

                return response;
            }

            return response;
        }

        private static bool IsNetworkError(Exception ex)
        {
            while (true)
            {
                if (ex is SocketException) return true;
                if (ex.InnerException == null) return false;

                ex = ex.InnerException;
            }
        }
    }
}