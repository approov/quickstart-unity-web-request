using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Unity.VisualScripting;


namespace Approov {
    public class ApproovCertificateHandler : CertificateHandler {
        private static readonly string TAG = "ApproovCertificateHandler ";
        private ApproovWebRequest approovWebRequest = null;
        private CertificateHandler baseCertificateHandler = null;
        public ApproovCertificateHandler(ApproovWebRequest request) {
            this.approovWebRequest = request;
            this.baseCertificateHandler = request.certificateHandler;
        }

        protected override bool ValidateCertificate(byte[] certificateData)
        {
            // Extract the hostname from the URL
            Uri uri = new Uri(approovWebRequest.url);
            string hostname = uri.Host;
            Debug.Log(TAG + "ApproovCertificateHandler.ValidateCertificate: validating certificate for " + hostname);
            // Call bridging layer versions
            string result = ApproovBridge.ShouldProceedWithNetworkConnection(certificateData, hostname,ApproovBridge.kPinTypePublicKeySha256);
            // The bridging layer processes the return result from the native layer and returns null if the connection should be allowed
            if (result == null) {
                Debug.Log(TAG + "ApproovCertificateHandler.ValidateCertificate: will ALLOW connection to " + approovWebRequest.url);
                return true;
            }
            // Pr returns an eeror message if the connection should be denied
            Debug.Log(TAG + "ApproovCertificateHandler.ValidateCertificate: will DENY connection to " + approovWebRequest.url + " with error: " + result);
            return false;
        }
    }// ApproovCertificateHandler.class
} // namespace Approov