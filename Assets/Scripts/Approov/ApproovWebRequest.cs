
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;


namespace Approov {
    public class ApproovWebRequest: UnityWebRequest {
        // The log tag
        public static readonly string TAG = "ApproovWebRequest ";
        /* Approov token default header */
        public static string ApproovTokenHeader = "Approov-Token";
        /* Approov token custom prefix: any prefix to be added such as "Bearer " */
        public static string ApproovTokenPrefix = "";
        /* Lock object for the above string variables */
        protected static readonly object HeaderAndPrefixLock = new object();
        /* true if the connection should proceed on network failures and not add an Approov token */
        protected static bool ProceedOnNetworkFail = false;
        /* Lock object for the above boolean variable*/
        protected static readonly object ProceedOnNetworkFailLock = new();
        /* Any header to be used for binding in Approov tokens or null if not set */
        protected static string BindingHeader = null;
        /* Lock object */
        protected static readonly object BindingHeaderLock = new();
        /* map of headers that should have their values substituted for secure strings, mapped to their
            required prefixes */
        protected static Dictionary<string, string> SubstitutionHeaders = new Dictionary<string, string>();
        /* Lock object for the above Set*/
        protected static readonly object SubstitutionHeadersLock = new();
        /* set of URL regexs that should be excluded from any Approov protection */
        protected static HashSet<Regex> ExclusionURLRegexs = new HashSet<Regex>();
        /* Lock object for the above Set*/
        protected static readonly object ExclusionURLRegexsLock = new();
        /*  Set of query parameters that may be substituted, specified by the key name */
        protected static HashSet<string> SubstitutionQueryParams = new HashSet<string>();
        /* Lock object for the above Set*/
        protected static readonly object SubstitutionQueryParamsLock = new();
        // Constructors: https://docs.unity3d.com/ScriptReference/Networking.UnityWebRequest-ctor.html
        public ApproovWebRequest() : base() {
            this.certificateHandler = new ApproovCertificateHandler(this);
        }
        public ApproovWebRequest(string url) : base(url) {
            this.certificateHandler = new ApproovCertificateHandler(this);
        }
        public ApproovWebRequest(Uri uri) : base(uri) {
            this.certificateHandler = new ApproovCertificateHandler(this);
        }
        public ApproovWebRequest(string url, string method) : base(url, method) {
            this.certificateHandler = new ApproovCertificateHandler(this);
        }
        public ApproovWebRequest(Uri uri, string method) : base(uri, method) {
            this.certificateHandler = new ApproovCertificateHandler(this);
        }
        public ApproovWebRequest(string url, string method, DownloadHandler downloadHandler, UploadHandler uploadHandler) : base(url, method, downloadHandler, uploadHandler) {
            this.certificateHandler = new ApproovCertificateHandler(this);
        }
        public ApproovWebRequest(Uri uri, string method, DownloadHandler downloadHandler, UploadHandler uploadHandler) : base(uri, method, downloadHandler, uploadHandler) {
            this.certificateHandler = new ApproovCertificateHandler(this);
        }

        // MARK: Static methods from https://docs.unity3d.com/ScriptReference/Networking.UnityWebRequest.html
        public new static void ClearCookieCache() {
            UnityWebRequest.ClearCookieCache();
        }

        public new static ApproovWebRequest Delete(string uri) {
            return new ApproovWebRequest(uri, "DELETE");
        }

        public new static ApproovWebRequest Get(string uri) {
            return new ApproovWebRequest(uri, "GET");
        }
        public new static ApproovWebRequest Get(Uri uri) {
            return new ApproovWebRequest(uri, "GET");
        }

        public new static ApproovWebRequest Head(string uri) {
            return new ApproovWebRequest(uri, "HEAD");
        }
        public new static ApproovWebRequest Head(Uri uri) {
            return new ApproovWebRequest(uri, "HEAD");
        }

        public static ApproovWebRequest Post(string uri) {
            return new ApproovWebRequest(uri, "POST");
        }
        public static ApproovWebRequest Post(Uri uri) {
            return new ApproovWebRequest(uri, "POST");
        }

        public new static ApproovWebRequest PostWwwForm(string uri, string form) {
            ApproovWebRequest request = new ApproovWebRequest(uri, "POST");
            byte[] formData = System.Text.Encoding.UTF8.GetBytes(form);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(formData);
            request.uploadHandler.contentType = "application/x-www-form-urlencoded";
            return request;
        }
        public new static ApproovWebRequest PostWwwForm(Uri uri, string form) {
            ApproovWebRequest request = new ApproovWebRequest(uri, "POST");
            byte[] formData = System.Text.Encoding.UTF8.GetBytes(form);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(formData);
            request.uploadHandler.contentType = "application/x-www-form-urlencoded";
            return request;
        }

        public static ApproovWebRequest Put(string uri) {
            return new ApproovWebRequest(uri, "PUT");
        }
        public static ApproovWebRequest Put(Uri uri) {
            return new ApproovWebRequest(uri, "PUT");
        }

        // MARK: Override SendWebRequest method
        public new UnityWebRequestAsyncOperation SendWebRequest() {
            // Modify the request headers
            UpdateRequestHeadersWithApproov();
            // New download handler
            this.downloadHandler = new DownloadHandlerBuffer();
            return base.SendWebRequest();
        }

        // MARK: Regexp/Query param methods

        /*
        * Sets a flag indicating if the network interceptor should proceed anyway if it is
        * not possible to obtain an Approov token due to a networking failure. If this is set
        * then your backend API can receive calls without the expected Approov token header
        * being added, or without header/query parameter substitutions being made. Note that
        * this should be used with caution because it may allow a connection to be established
        * before any dynamic pins have been received via Approov, thus potentially opening the channel to a MitM.
        *
        * @param proceed is true if Approov networking fails should allow continuation
        */
        public static void SetProceedOnNetworkFailure(bool proceed)
        {
            lock (ProceedOnNetworkFailLock)
            {
                ProceedOnNetworkFail = proceed;
                Console.WriteLine(TAG + "SetProceedOnNetworkFailure " + proceed.ToString());
            }
        }

        /*
        * Gets a flag indicating if the network interceptor should proceed anyway if it is
        * not possible to obtain an Approov token due to a networking failure. If this is set
        * then your backend API can receive calls without the expected Approov token header
        * being added, or without header/query parameter substitutions being made. Note that
        * this should be used with caution because it may allow a connection to be established
        * before any dynamic pins have been received via Approov, thus potentially opening the channel to a MitM.
        *
        * @return boolean true if Approov networking fails should allow continuation
        */
        public static bool GetProceedOnNetworkFailure()
        {
            lock (ProceedOnNetworkFailLock)
            {
                return ProceedOnNetworkFail;
            }
        }

        /*  Sets the Approov Header and optional prefix. By default, those values are "Approov-Token"
        *  for the header and the prefix is an empty string. If you wish to use "Authorization Bearer .."
        *  for example, the header should be set to "Authorization " and the prefix to "Bearer"
        *  
        *  @param  header the header to use
        *  @param  prefix optional prefix, can be an empty string if not needed
        */
        public static void SetTokenHeaderAndPrefix(string header, string prefix)
        {
            lock (HeaderAndPrefixLock)
            {
                if (header != null) ApproovTokenHeader = header;
                if (prefix != null) ApproovTokenPrefix = prefix;
                Console.WriteLine(TAG + "SetTokenHeaderAndPrefix header: " + header + " prefix: " + prefix);
            }
        }

        /*
        * Adds the name of a header which should be subject to secure strings substitution. This
        * means that if the header is present then the value will be used as a key to look up a
        * secure string value which will be substituted into the header value instead. This allows
        * easy migration to the use of secure strings. A required prefix may be specified to deal
        * with cases such as the use of "Bearer " prefixed before values in an authorization header.
        *
        * @param header is the header to be marked for substitution
        * @param requiredPrefix is any required prefix to the value being substituted or nil if not required
        */
        public static void AddSubstitutionHeader(string header, string requiredPrefix)
        {
            if (ApproovService.IsSDKInitialized())
            {
                lock (SubstitutionHeadersLock)
                {
                    if (requiredPrefix == null)
                    {
                        SubstitutionHeaders.Add(header, "");
                    }
                    else
                    {
                        SubstitutionHeaders.Add(header, requiredPrefix);
                    }
                    Console.WriteLine(TAG + "AddSubstitutionHeader header: " + header + " requiredPrefix: " + requiredPrefix);
                }
            }
        }

        /*
            * Removes a header previously added using addSubstitutionHeader.
            *
            * @param header is the header to be removed for substitution
            */
        public static void RemoveSubstitutionHeader(string header)
        {
            if (ApproovService.IsSDKInitialized())
            {
                lock (SubstitutionHeadersLock)
                {
                    if (SubstitutionHeaders.ContainsKey(header))
                    {
                        SubstitutionHeaders.Remove(header);
                        Console.WriteLine(TAG + "RemoveSubstitutionHeader " + header);

                    }
                }
            }
        }

        /**
            * Adds a key name for a query parameter that should be subject to secure strings substitution.
            * This means that if the query parameter is present in a URL then the value will be used as a
            * key to look up a secure string value which will be substituted as the query parameter value
            * instead. This allows easy migration to the use of secure strings.
            *
            * @param key is the query parameter key name to be added for substitution
            */
        public static void AddSubstitutionQueryParam(string key)
        {
            if (ApproovService.IsSDKInitialized())
            {
                lock (SubstitutionQueryParamsLock)
                {
                    SubstitutionQueryParams.Add(key);
                    Console.WriteLine(TAG + "AddSubstitutionQueryParam " + key);
                }
            }
        }

        /**
        * Removes a query parameter key name previously added using addSubstitutionQueryParam.
        * @param key is the query parameter key name to be removed for substitution
        */
        public static void RemoveSubstitutionQueryParam(string key)
        {
            if (ApproovService.IsSDKInitialized())
            {
                lock (SubstitutionQueryParamsLock)
                {
                    if (SubstitutionQueryParams.Contains(key))
                    {
                        SubstitutionQueryParams.Remove(key);
                        Console.WriteLine(TAG + "RemoveSubstitutionQueryParam " + key);
                    }
                }
            }
        }

        /**
        * Adds an exclusion URL regular expression. If a URL for a request matches this regular expression
        * then it will not be subject to any Approov protection. Note that this facility must be used with
        * EXTREME CAUTION due to the impact of dynamic pinning. Pinning may be applied to all domains added
        * using Approov, and updates to the pins are received when an Approov fetch is performed. If you
        * exclude some URLs on domains that are protected with Approov, then these will be protected with
        * Approov pins but without a path to update the pins until a URL is used that is not excluded. Thus
        * you are responsible for ensuring that there is always a possibility of calling a non-excluded
        * URL, or you should make an explicit call to fetchToken if there are persistent pinning failures.
        * Conversely, use of those option may allow a connection to be established before any dynamic pins
        * have been received via Approov, thus potentially opening the channel to a MitM.
        *
        * @param urlRegex is the regular expression that will be compared against URLs to exclude them
        * @throws ArgumentException if urlRegex is malformed
        */
        public static void AddExclusionURLRegex(string urlRegex)
        {
            if (ApproovService.IsSDKInitialized())
            {
                lock (ExclusionURLRegexsLock)
                {
                    if (urlRegex != null)
                    {
                        try {
                            Regex reg = new Regex(urlRegex);
                            ExclusionURLRegexs.Add(reg);
                            Console.WriteLine(TAG + "AddExclusionURLRegex " + urlRegex);
                        } catch (ArgumentException e) {
                            Console.WriteLine(TAG + "AddExclusionURLRegex: " + e.Message);
                        }
                    }
                }
            }
        }

        /**
        * Removes an exclusion URL regular expression previously added using addExclusionURLRegex.
        * @param urlRegex is the regular expression that will be compared against URLs to exclude them
        * @throws ArgumentException if urlRegex is malformed
        */
        public static void RemoveExclusionURLRegex(string urlRegex)
        {
            if (ApproovService.IsSDKInitialized())
            {
                lock (ExclusionURLRegexsLock)
                {
                    if (urlRegex != null)
                    {
                        try {
                            Regex reg = new Regex(urlRegex);
                            ExclusionURLRegexs.Remove(reg);
                            Console.WriteLine(TAG + "RemoveExclusionURLRegex " + urlRegex);
                        } catch (ArgumentException e) {
                            Console.WriteLine(TAG + "RemoveExclusionURLRegex: " + e.Message);
                        }
                    }
                }
            }
        }

        /**
        * Checks if the url matches one of the exclusion regexs defined in exclusionURLRegexs
        * @param   url is the URL for which the check is performed
        * @return  Bool true if url matches preset pattern in Dictionary
        */
        public static bool CheckURLIsExcluded(string url)
        {
            // obtain a copy of the exclusion URL regular expressions in a thread safe way
            int elementCount;
            Regex[] exclusionURLs;
            lock (ExclusionURLRegexsLock)
            {
                elementCount = ExclusionURLRegexs.Count;
                if (elementCount == 0) return false;
                exclusionURLs = new Regex[elementCount];
                ExclusionURLRegexs.CopyTo(exclusionURLs);
            }

            foreach (Regex pattern in exclusionURLs)
            {
                Match match = pattern.Match(url, 0, url.Length);
                if (match.Length > 0)
                {
                    Console.WriteLine(TAG + "CheckURLIsExcluded match for " + url);
                    return true;
                }
            }
            return false;
        }

        /*
        *  Adds Approov to the given request by adding the Approov token in a header. If a binding header has been specified
        *  then this should be available. If it is not currently possible to fetch an Approov token (typically due to no or
        *  poor network) then an NetworkingErrorException is thrown and a later retry should be made. Other failures will
        *  result in an ApproovException. Note that if substitution headers have been setup then this method also examines
        *  the headers and remaps them to the substituted value if they correspond to a secure string set in Approov. Note that
        *  in this  case it is possible for the method to fail with a RejectionException, which may provide additional
        *  information about the reason for the rejection.
        */
        protected void UpdateRequestHeadersWithApproov() 
        {
            // Check if we have initialized the SDK
            if (!ApproovService.IsSDKInitialized()) {
                Debug.LogError(TAG + "Approov SDK not initialized");
                return;
            }
            
            // Build the final url (we have to use to call FetchApproovToken)
            string urlWithBaseAddress = this.uri != null ? this.uri.AbsoluteUri : this.url;
            // Parse the URL and obtain the hostname
            Uri uri = new Uri(urlWithBaseAddress);
            string hostname = uri.Host;
            // Check if the URL matches one of the exclusion regexs and just return if it does
            if (CheckURLIsExcluded(hostname))
            {
                Console.WriteLine(TAG + "UpdateRequestHeadersWithApproov excluded url " + hostname);
                return;
            }

            // Check if Bind Header is set to a non empty String
            lock (BindingHeaderLock)
            {
                if (BindingHeader != null)
                {
                    if (this.GetRequestHeader(BindingHeader) != null)
                    {
                        // Returns all header values for a specified header stored in the Headers collection.
                        string headerValue = this.GetRequestHeader(BindingHeader);
                        ApproovService.SetDataHashInToken(headerValue);
                        // Log
                        Console.WriteLine(TAG + "bindheader set: " + headerValue);
                    }
                    else
                    {
                        throw new ConfigurationFailureException(TAG + "Missing token binding header: " + BindingHeader);
                    }
                }
            }// lock
            
            // Invoke fetch token sync from the ApproovBridge since we need to get the result
            ApproovTokenFetchResult approovResult = ApproovBridge.FetchApproovTokenAndWait(hostname);
            // Log result
            Console.WriteLine(TAG + "Approov token for " + hostname + " : " + approovResult.loggableToken);

            // if there was a configuration change we clear it by fetching the new config and clearing
            // all the cached certificates which will force re-evaluation for new connections
            if (approovResult.isConfigChanged)
            {
                // Clear the certificate cache
                ApproovBridge.ClearCertificateCache();
                // Fetch the new configuration
                ApproovService.FetchConfig();
                Console.WriteLine(TAG + "updateRequest, dynamic configuration update");
            }

            // if a pin update is forced then this indicates the pins have been updated since the last time they
            // where read, or that we never had any valid pins when the pinned client was created so we cannot allow
            // the update to complete as this could leak an Approov token via an unpinned connection
            if (approovResult.isForceApplyPins)
                throw new NetworkingErrorException(TAG + "Forced pin update required");

            ApproovTokenFetchStatus aCurrentFetchStatus = approovResult.status;
            // Process the result
            Console.WriteLine(TAG + "FetchToken: " + url + " " + ApproovService.ApproovTokenFetchStatusToString(aCurrentFetchStatus));
            

            // Check the status of the Approov token fetch
            if (aCurrentFetchStatus == ApproovTokenFetchStatus.Success)
            {
                // we successfully obtained a token so add it to the header
                this.SetRequestHeader(ApproovTokenHeader, ApproovTokenPrefix + approovResult.token);
            }
            else if ((aCurrentFetchStatus == ApproovTokenFetchStatus.NoNetwork) ||
                    (aCurrentFetchStatus == ApproovTokenFetchStatus.PoorNetwork) ||
                    (aCurrentFetchStatus == ApproovTokenFetchStatus.MITMDetected))
            {
                /* We are unable to get the approov token due to network conditions so the request can
                *  be retried by the user later
                */
                if (!ProceedOnNetworkFail)
                {
                    // Must not proceed with network request and inform user a retry is needed
                    throw new NetworkingErrorException(TAG + "Retry attempt needed. " + approovResult.loggableToken, true);
                }
            }
            else if ((aCurrentFetchStatus == ApproovTokenFetchStatus.UnknownURL) ||
                    (aCurrentFetchStatus == ApproovTokenFetchStatus.UnprotectedURL) ||
                    (aCurrentFetchStatus == ApproovTokenFetchStatus.NoApproovService))
            {
                Console.WriteLine(TAG + "Will continue without Approov-Token");
            }
            else
            {
                throw new PermanentException("Unknown approov token fetch result " + ApproovService.ApproovTokenFetchStatusToString(aCurrentFetchStatus));
            }
            
            /* We only continue additional processing if we had a valid status from Approov, to prevent additional delays
            * by trying to fetch from Approov again and this also protects against header substitutions in domains not
            * protected by Approov and therefore are potentially subject to a MitM.
            */
            if ((aCurrentFetchStatus != ApproovTokenFetchStatus.Success) &&
                (aCurrentFetchStatus != ApproovTokenFetchStatus.UnprotectedURL))
            {
                // We return
                return;
            }
            /* We now have to deal with any substitution headers */
            // Make a copy of original dictionary
            Dictionary<string, string> originalSubstitutionHeaders;
            lock (SubstitutionHeadersLock)
            {
                originalSubstitutionHeaders = new Dictionary<string, string>(SubstitutionHeaders);
            }
            // Iterate over the copied dictionary
            foreach (KeyValuePair<string, string> entry in originalSubstitutionHeaders)
            {
                string header = entry.Key;
                string prefix = entry.Value; // can be null
                // Check if prefix for a given header is not null
                if (prefix == null) prefix = "";
                string value = this.GetRequestHeader(header);
                // The request headers do NOT contain the header needing replaced
                if (value == null) continue;    // None of the available headers contain the value
                // Check if the request contains the header we want to replace
                if (value.StartsWith(prefix) && (value.Length > prefix.Length))
                {
                    // We have a match
                    string stringValue = value.Substring(prefix.Length);
                    // Fetch Secure String
                    ApproovTokenFetchResult secStringResult = ApproovBridge.FetchSecureStringAndWait(stringValue, null);
                    ApproovTokenFetchStatus fetchStatus = secStringResult.status;
                    // Check the status
                    Console.WriteLine(TAG + "Substituting header: " + header + ", " + ApproovService.ApproovTokenFetchStatusToString(fetchStatus));
                    
                    // Process the result of the token fetch operation
                    if (fetchStatus == ApproovTokenFetchStatus.Success)
                    {
                        // We add the modified header to the request headers 
                        // Call getSecureString() method on fetch result
                        String secureString = secStringResult.secureString;
                        if (secureString != null)
                        {
                            // We add the modified header to the request headers; this shouldd also override the original
                            this.SetRequestHeader(header, prefix + secureString);
                        }
                        else
                        {
                            // Secure string is null
                            throw new ApproovException(TAG + "UpdateRequestHeadersWithApproov null return from secure message fetch");
                        }
                    }
                    else if (fetchStatus == ApproovTokenFetchStatus.Rejected)
                    {
                        // if the request is rejected then we provide a special exception with additional information
                        string localARC = secStringResult.ARC;
                        string localReasons = secStringResult.rejectionReasons;
                        throw new RejectionException(TAG + "secure message rejected", arc: localARC, rejectionReasons: localReasons);
                    }
                    else if (fetchStatus == ApproovTokenFetchStatus.NoNetwork ||
                            fetchStatus == ApproovTokenFetchStatus.PoorNetwork ||
                            fetchStatus == ApproovTokenFetchStatus.MITMDetected)
                    {
                        /* We are unable to get the secure string due to network conditions so the request can
                        *  be retried by the user later
                        *  We are unable to get the secure string due to network conditions, so - unless this is
                        *  overridden - we must not proceed. The request can be retried by the user later.
                        */
                        if (!ProceedOnNetworkFail)
                        {
                            // We throw
                            throw new NetworkingErrorException(TAG + "Header substitution: network issue, retry needed");
                        }
                    }
                    else if (fetchStatus != ApproovTokenFetchStatus.UnknownKey)
                    {
                        // we have failed to get a secure string with a more serious permanent error
                        throw new PermanentException(TAG + "Header substitution: " + ApproovService.ApproovTokenFetchStatusToString(fetchStatus));
                    }
                } // if (value.StartsWith ...
            }

            /* Finally, we deal with any query parameter substitutions, which may require further fetches but these
            * should be using cached results */
            // Make a copy of original substitutionQuery set
            HashSet<string> originalQueryParams;
            lock (SubstitutionQueryParamsLock)
            {
                originalQueryParams = new HashSet<string>(SubstitutionQueryParams);
            }
            string urlString = urlWithBaseAddress;
            foreach (string entry in originalQueryParams)
            {
                string pattern = entry;
                Regex regex = new Regex(pattern, RegexOptions.ECMAScript);
                // See if there is any match
                MatchCollection matchedPatterns = regex.Matches(urlString);
                // We skip Group at index 0 as this is the match (e.g. ?Api-Key=api_key_placeholder) for the whole
                // regex, but we only want to replace the query parameter value part (e.g. api_key_placeholder)
                for (int count = 0; count < matchedPatterns.Count; count++)
                {
                    // We must have 2 Groups, the first being the full pattern and the second one the query parameter
                    if (matchedPatterns[count].Groups.Count != 2) continue;
                    string matchedText = matchedPatterns[count].Groups[1].Value;
                    // We fetch a secure string again, which should just return the old one from the cache
                    ApproovTokenFetchResult secStringResult = ApproovBridge.FetchSecureStringAndWait(matchedText, null);
                    ApproovTokenFetchStatus fetchStatus = secStringResult.status;

                    // Check the status
                    if (fetchStatus == ApproovTokenFetchStatus.Success)
                    {
                        // we successfully obtained a secure string so replace the query parameter value
                        // Call getSecureString() method on fetch result
                        String secureString = secStringResult.secureString;
                        // Replace the ocureences and modify the URL
                        string newURL = urlString.Replace(matchedText, secureString);
                        // we log
                        Console.WriteLine(TAG + "replacing url with " + newURL);
                        this.uri = new Uri(newURL);
                    }
                    else if (fetchStatus == ApproovTokenFetchStatus.Rejected)
                    {
                        // if the request is rejected then we provide a special exception with additional information
                        string localARC = secStringResult.ARC;
                        string localReasons = secStringResult.rejectionReasons;
                        throw new RejectionException(TAG + "UpdateRequestHeadersWithApproov secure message rejected", arc: localARC, rejectionReasons: localReasons);
                    }
                    else if (fetchStatus == ApproovTokenFetchStatus.NoNetwork ||
                            fetchStatus == ApproovTokenFetchStatus.PoorNetwork ||
                            fetchStatus == ApproovTokenFetchStatus.MITMDetected)
                    {
                        /* We are unable to get the secure string due to network conditions so the request can
                        *  be retried by the user later
                        *  We are unable to get the secure string due to network conditions, so - unless this is
                        *  overridden - we must not proceed. The request can be retried by the user later.
                        */
                        if (!ProceedOnNetworkFail)
                        {
                            // We throw
                            throw new NetworkingErrorException(TAG + "Query parameter substitution: network issue, retry needed");
                        }
                    }
                    else if (fetchStatus != ApproovTokenFetchStatus.UnknownKey)
                    {
                        // we have failed to get a secure string with a more serious permanent error
                        throw new PermanentException(TAG + "Query parameter substitution error: " + ApproovService.ApproovTokenFetchStatusToString(fetchStatus));
                    }
                }
            }// foreach
        }//UpdateRequestHeadersWithApproov
    }// ApproovWebRequest class
}// namespace Approov