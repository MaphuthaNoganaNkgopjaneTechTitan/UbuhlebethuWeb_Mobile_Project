using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using UbuhlebethuConnectPro.Core.Models.DTOs;

namespace UbuhlebethuConnectPro.WebApp.Handlers
{
    public class JwtHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly SemaphoreSlim _refreshLock = new(1, 1);

        public JwtHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            string? token = null;

            if (httpContext != null)
            {
                token = await httpContext.GetTokenAsync("jwt_token");
                if (string.IsNullOrEmpty(token))
                {
                    token = httpContext.Session.GetString("JwtToken");
                }

                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
            }

            HttpResponseMessage response = await SendWithStartupRetryAsync(request, cancellationToken);

            // If 401 Unauthorized occurs on an authenticated session, attempt refresh and retry
            var requestPath = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                !requestPath.Contains("/auth/login") &&
                !requestPath.Contains("/auth/refresh") &&
                httpContext != null)
            {
                var refreshToken = httpContext.Session.GetString("RefreshToken");
                if (!string.IsNullOrEmpty(refreshToken) && !string.IsNullOrEmpty(token))
                {
                    bool refreshed = false;
                    string? newToken = null;

                    await _refreshLock.WaitAsync(cancellationToken);
                    try
                    {
                        // Check if another concurrent request already refreshed the token
                        var currentSessionToken = httpContext.Session.GetString("JwtToken");
                        if (!string.IsNullOrEmpty(currentSessionToken) && currentSessionToken != token)
                        {
                            newToken = currentSessionToken;
                            refreshed = true;
                        }
                        else
                        {
                            // Call API refresh endpoint directly using an un-intercepted HttpClient
                            var baseUri = request.RequestUri != null
                                ? new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority))
                                : new Uri("http://localhost:5000");

                            using var directClient = new HttpClient { BaseAddress = baseUri };
                            var refreshPayload = new RefreshRequest { Token = token, RefreshToken = refreshToken };
                            var refreshResp = await directClient.PostAsJsonAsync("api/v1/auth/refresh", refreshPayload, cancellationToken);

                            if (refreshResp.IsSuccessStatusCode)
                            {
                                var result = await refreshResp.Content.ReadFromJsonAsync<RefreshResponse>(cancellationToken: cancellationToken);
                                if (result != null && !string.IsNullOrEmpty(result.Token))
                                {
                                    newToken = result.Token;
                                    httpContext.Session.SetString("JwtToken", result.Token);
                                    if (!string.IsNullOrEmpty(result.RefreshToken))
                                    {
                                        httpContext.Session.SetString("RefreshToken", result.RefreshToken);
                                    }
                                    refreshed = true;
                                }
                            }
                            else
                            {
                                // Refresh token is revoked or expired
                                httpContext.Session.Remove("JwtToken");
                                httpContext.Session.Remove("RefreshToken");
                            }
                        }
                    }
                    catch
                    {
                        // Refresh failed
                    }
                    finally
                    {
                        _refreshLock.Release();
                    }

                    if (refreshed && !string.IsNullOrEmpty(newToken))
                    {
                        // Clone request and retry with new bearer token
                        var retryRequest = await CloneHttpRequestMessageAsync(request);
                        retryRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
                        response.Dispose();
                        return await base.SendAsync(retryRequest, cancellationToken);
                    }
                }
            }

            return response;
        }

        private async Task<HttpResponseMessage> SendWithStartupRetryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await base.SendAsync(request, cancellationToken);
                }
                catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException && i < maxRetries - 1)
                {
                    // If the API is still starting up (EF migrations), wait 2 seconds and retry.
                    await Task.Delay(2000, cancellationToken);
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }

        private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri)
            {
                Version = req.Version
            };

            if (req.Content != null)
            {
                var ms = new System.IO.MemoryStream();
                await req.Content.CopyToAsync(ms);
                ms.Position = 0;
                clone.Content = new StreamContent(ms);

                foreach (var header in req.Content.Headers)
                {
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            foreach (var header in req.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}
