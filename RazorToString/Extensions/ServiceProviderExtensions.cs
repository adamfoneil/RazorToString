﻿using RazorToStringServices.Models;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RazorToStringServices.Extensions
{
    public static class ServiceProviderExtensions
    {
        public const string EmailToken = "email-token";

        /// <summary>
        /// thanks to https://stackoverflow.com/a/68760353/2023653
        /// </summary>
        public static ICollection<string> GetBaseUrls(this IServiceProvider services)
        {
            var server = services.GetService<IServer>();
            var addresses = server?.Features.Get<IServerAddressesFeature>();
            return addresses?.Addresses ?? Array.Empty<string>();
        }

        public static string GetBaseUrl(this IServiceProvider services, Func<string, bool>? predicate = null)
        {
            Func<string, bool> defaultPredicate = (url) => url.StartsWith("https://");
            var usePredicate = predicate ?? defaultPredicate;

            var urls = GetBaseUrls(services);
            var result = urls.FirstOrDefault(usePredicate) ?? urls.FirstOrDefault(defaultPredicate) ?? urls.First(url => url.StartsWith("http://"));
            return result.RemovePort(443);
        }

        public static string GetHttpsUrl(this IServiceProvider services) => GetBaseUrls(services)
            .First(url => url.StartsWith("https://"))
            .RemovePort(443);

        public static string RemovePort(this string url, int port)
        {
            var uri = new Uri(url);
            return (uri.Port == port) ? new Uri(uri.Scheme + "://" + uri.Host + uri.PathAndQuery).ToString() : url;
        }

        /// <summary>
        /// builds a URL to a resource in this application.        
        /// </summary>
        public static string BuildUrl(this IServiceProvider services, string path, Func<string, bool>? predicate = null) => PathUtil.Combine(GetBaseUrl(services, predicate), path);

        public static async Task<string> RenderPageAsync(this IServiceProvider services, string path, ILogger? logger = null)
        {
            // when starting with a full url, use that. Otherwise extract from server environment
            var url = (path.ToLower().StartsWith("http")) ? path : BuildUrl(services, path);

            logger?.LogDebug("RenderPageAsync: {url}", url);

            var http = services.GetRequiredService<HttpClient>();
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add(EmailToken, BuildEmailToken(services, path));

            return await http.GetStringAsync(url);
        }

        public static string BuildEmailToken(this IServiceProvider services, string path)
        {
            var options = services.GetService<IOptions<AuthorizeEmailOptions>>();
            return HashString((options?.Value.HashSalt ?? string.Empty) + path);
        }

        private static string HashString(string input)
        {
            using var md5 = MD5.Create();
            return Convert.ToBase64String(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
        }

    }
}
