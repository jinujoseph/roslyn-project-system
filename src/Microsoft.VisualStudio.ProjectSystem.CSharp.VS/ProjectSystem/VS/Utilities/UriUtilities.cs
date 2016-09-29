//--------------------------------------------------------------------------------------------
// UriUtilities
//
// Helper functions for urls
//
// Copyright(c) 2015 Microsoft Corporation
//--------------------------------------------------------------------------------------------

namespace Microsoft.VisualStudio.ProjectSystem.DotNet.Utilities
{
    using System;

    internal static class UriUtilities
    {
        /// <summary>
        /// Converts the given httpUrl to an https url with the port specified. Note that it will
        /// throw Uri exceptions if the httpUrl is not valid
        /// </summary>
        public static string MakeSecureUrl(string httpUrl, int sslPort)
        {
            UriBuilder uriBuilder = new UriBuilder(httpUrl);
            uriBuilder.Scheme = Uri.UriSchemeHttps;
            uriBuilder.Port = sslPort;
            return uriBuilder.Uri.AbsoluteUri;
        }

        /// <summary>
        /// Extension method to return whether a uri is a secure one or not
        /// </summary>
        public static bool IsSSLUri(this Uri uri)
        {
            return uri.Scheme.Equals(Uri.UriSchemeHttps);
        }
    }
}
