
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;


namespace Approov {
    public class ApproovWebRequest: UnityWebRequest {
        // The log tag
        public static readonly string TAG = "ApproovWebRequest ";
    
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
            if (ApproovService.CheckURLIsExcluded(hostname))
            {
                Console.WriteLine(TAG + "UpdateRequestHeadersWithApproov excluded url " + hostname);
                return;
            }
            string bindingHeader = ApproovService.GetBindingHeader();
            // Check if Bind Header is set to a non empty String
            if (bindingHeader != null)
            {
                if (this.GetRequestHeader(bindingHeader) != null)
                {
                    // Returns all header values for a specified header stored in the Headers collection.
                    string headerValue = this.GetRequestHeader(bindingHeader);
                    ApproovService.SetDataHashInToken(headerValue);
                    // Log
                    Console.WriteLine(TAG + "bindheader set: " + headerValue);
                }
                else
                {
                    throw new ConfigurationFailureException(TAG + "Missing token binding header: " + bindingHeader);
                }
            }
            
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
                this.SetRequestHeader(ApproovService.GetTokenHeader(), ApproovService.GetTokenPrefix() + approovResult.token);
            }
            else if ((aCurrentFetchStatus == ApproovTokenFetchStatus.NoNetwork) ||
                    (aCurrentFetchStatus == ApproovTokenFetchStatus.PoorNetwork) ||
                    (aCurrentFetchStatus == ApproovTokenFetchStatus.MITMDetected))
            {
                /* We are unable to get the approov token due to network conditions so the request can
                *  be retried by the user later
                */
                if (!ApproovService.GetProceedOnNetworkFailure())
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
            // Get a copy of original dictionary
            Dictionary<string, string> originalSubstitutionHeaders = ApproovService.GetSubstitutionHeaders();
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
                        if (!ApproovService.GetProceedOnNetworkFailure())
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
            // Get a copy of original substitutionQuery set
            HashSet<string> originalQueryParams = ApproovService.GetSubstitutionQueryParams();
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
                        if (!ApproovService.GetProceedOnNetworkFailure())
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