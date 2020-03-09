﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Hyperledger.TestHarness.Mock
{
    public class InProcMessageHandler : HttpMessageHandler
    {
        public InProcAgent TargetAgent { get; set; }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (TargetAgent is InProcMediatorAgent mediatorAgent && request.Method == HttpMethod.Get)
            {
                var discoveryConfiguration = await mediatorAgent.HandleDiscoveryAsync();

                var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
                responseMessage.Content = new StringContent(discoveryConfiguration.ToJson());
                responseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                return responseMessage;
            }
            else
            {
                var response = await TargetAgent.HandleAsync(await request.Content?.ReadAsByteArrayAsync());
                var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

                if (response != null)
                {
                    responseMessage.Content = new ByteArrayContent(response.Payload);
                    responseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(DefaultMessageService.AgentWireMessageMimeType);
                }

                return responseMessage;
            }
        }
    }
}