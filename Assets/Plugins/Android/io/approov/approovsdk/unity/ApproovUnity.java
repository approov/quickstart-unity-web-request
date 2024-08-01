package io.approov.approovsdk.unity;
import java.io.ByteArrayInputStream;
import java.io.Console;
import java.net.URI;
import java.net.URL;
import java.security.KeyStore;
import java.security.MessageDigest;
import java.security.PublicKey;
import java.security.cert.Certificate;
import java.security.cert.CertificateFactory;
import java.security.cert.X509Certificate;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Base64;
import java.util.Collection;
import java.util.Dictionary;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import com.criticalblue.approovsdk.Approov;

import javax.net.ssl.HttpsURLConnection;
import javax.net.ssl.TrustManager;
import javax.net.ssl.TrustManagerFactory;
import javax.net.ssl.X509TrustManager;
import java.util.concurrent.locks.ReadWriteLock;
import java.util.concurrent.locks.ReentrantReadWriteLock;

/**
 *  ApproovUnity handles certificate pinning check and validates the certificate chain
 *  for a specific  hostname. It uses the Approov SDK to get the set of pins for the host and
 *  checks if the pin value of at least one certificate is in that set.
 */
public class ApproovUnity {
    // TAG to be used when logging
    private static final String TAG = "ApproovUnity: ";
    // Connect timeout (in ms) for host certificate fetch
    private static final int FETCH_CERTIFICATES_TIMEOUT_MS = 3000;
    // Initial size of the Approov cert cache
    private static final int APPROOV_CERT_CACHE_SIZE = 10;
    // Cache of host certificate
    private static HashMap<String,byte[]>  approovCertCache = new HashMap<String,byte[]>(APPROOV_CERT_CACHE_SIZE);
    // Lock to protect the cache
    private static final ReadWriteLock certCacheLock = new ReentrantReadWriteLock();
    // Success string for the C# code
    private static final String SUCCESS = "SUCCESS";
    
    /**
     * Connect to hostname and fetch certificates.
     * Returns a list of certificates in byte array format
     *
     * @param hostname the hostname to connect to
     * @return a list of certificates in byte array format
    */
    private static List<byte[]> fetchHostCertificates(String hostname) {
        try {
            URI uri = new URI("https", hostname, null, null);
            URL url = uri.toURL();
            HttpsURLConnection connection = (HttpsURLConnection) url.openConnection();
            connection.setConnectTimeout(FETCH_CERTIFICATES_TIMEOUT_MS);
            connection.connect();
            Certificate[] certificates = connection.getServerCertificates();
            final List<byte[]> hostCertificates = new ArrayList<>(certificates.length);
            for (Certificate certificate: certificates) {
                hostCertificates.add(certificate.getEncoded());
            }
            connection.disconnect();
            return hostCertificates;
        } catch (Exception e) {
            return null;
        }
    }// fetchHostCertificates

    /**
     * Check if the connection should proceed with the given certificate
     *
     * @param cert the leaf certificate presented to us by the server
     * @param hostname the hostname of the server
     * @param pinType the type of pin used to check certificates
     * @return "SUCCESS" if connection should proceed or error message otherwise
     */
    public static String shouldProceedWithConnection(byte[] cert, String hostname, String pinType) {
        // Get the pinning information from the certificate
        String pinningString = getCertPinForPinType(cert, pinType);
        if (pinningString == null) {
            String errorMessage = TAG + "Unable to extract pin value from certificate for host " + hostname;
            System.out.println(errorMessage);
            return errorMessage;
        }
        // Check if the pinning string is in the set of pins present in Approov:
        // We do this step early ONLY to allow the connection to proceed even if the cert is invalid/self-signed/etc
        // since is common to use this option during testing! It will only work if the HOSTNAME is set as protected by Approov
        if (checkPinForHostIsSetInApproov(hostname, pinningString, getPinsForHost(hostname, pinType))) {
            // Perhaps change to "pinned to leaf cert". Is this going to be annoying in the logs?
            System.out.println(TAG + "Leaf cert pin, connection allowed for host " + hostname);
            return SUCCESS;
        }
        // The pinning info from leaf certificate is not in the Approov SDK, so we query the cache
        byte[] cachedCert = getFromCache(hostname);
        if(cachedCert != null) {
            if (Arrays.equals(cachedCert, cert)) {
                // we have a match to the leaf cert, it has been checked before so allow connection
                System.out.println(TAG + "Cached cert match, connection allowed for host " + hostname);
                return SUCCESS;
            } else {
                /* The leaf certificate is NOT present in cache: We DELETE the cached entry for host */
                System.out.println(TAG + "Cached cert mismatch, deleting entry for host " + hostname);
                removeFromCache(hostname);
            } // if Arrays.equals(cachedCert, cert)
        }// if cachedCert != null

        /* TLS lookup is needed now: we are either not pinning to leaf certificate,
        *   the leaf cert is not found in the cache, or the host is not pinned by Approov.
        *   We need to fetch the certificate chain and check the pins for the host
        */
        // Get certificate chain for the host
        List<byte[]> hostCertificates = fetchHostCertificates(hostname);
        if (hostCertificates == null) {
            String errorMessage = TAG + "Unable to fetch certificates for host " + hostname;
            System.out.println(errorMessage);
            return errorMessage;
        }
        // We need to have a leaf cert and at least one intermediate/root cert because we have already checked the leaf.
        if (hostCertificates.size() < 2) {
            String errorMessage = TAG + "Certificate chain too small for host " + hostname;
            System.out.println(errorMessage);
            return errorMessage;
        }
        // Check if the leaf certificate obtained matches the input certificate
        if (!Arrays.equals(cert, hostCertificates.get(0))) {
            String errorMessage = TAG + "Leaf certificate presented does not match the one fetched for host " + hostname;
            System.out.println(errorMessage);
            return errorMessage;
        }

        // Check pinning status
        String resultMessage = approovPinsValidation(hostCertificates, hostname, pinType);
        if (resultMessage == null) {
            System.out.println(TAG + "New cert chain verified and cached, connection allowed for host " + hostname);
            // Add the chain to cache
            addToCache(hostname, hostCertificates.get(0));
            return SUCCESS;
        }
        // We return the error message to C# land
        return resultMessage;
    }// validateLeafCertificate

    /**
     * Iterates over certificate chain and attempts to match pins to the ones in the Approov SDK
     *
     * @param hostCertificates the certificate chain to validate
     * @param hostname the hostname of the server
     * @param pinType the type of pinning we are using to pin our connections
     * @return null if the connection is pinned by approov or is not protected by Approov, an error message otherwise
    */
    private static String approovPinsValidation(List<byte[]> hostCertificates, String hostname, String pinType) {
        // Only check the intermediate and root certificates because we know the pin is not to the leaf cert
        int startIndex = 1;
        // The list of pins for current host
        List<String> allPinsForHost = getPinsForHost(hostname, pinType);
        // if there are no pins for the domain (but the host is present) then use any managed trust roots instead
        if ((allPinsForHost != null) && (allPinsForHost.size() == 0))
        {
            // Get the wildcard pins for managed trust roots
            allPinsForHost = getPinsForHost("*", pinType);
            // managed trust roots only ever pin to the root certificate
            startIndex = hostCertificates.size() - 1;
        }
        // if we are not pinning then we consider this level of trust to be acceptable
        if ((allPinsForHost == null) || (allPinsForHost.size() == 0))
        {
            // Host is not being pinned and we have successfully checked certificate chain as valid
            System.out.println(TAG + "Host not pinned " + hostname);
            return null;
        }

        // Iterate over the intermediate/root certificates, extract pinning information and compare with Approov pins
        for (int i = startIndex; i < hostCertificates.size(); i++) {
            byte[] certificate = hostCertificates.get(i);
            String evaluatedPin = getCertPinForPinType(certificate, pinType);
            if (evaluatedPin == null) {
                String errorMessage = TAG + "Unable to extract pin value for intermediate/root cert for host " + hostname;
                System.out.println(errorMessage);
                return errorMessage;
            }
            if (checkPinForHostIsSetInApproov(hostname, evaluatedPin, allPinsForHost)) {
                System.out.println(TAG + "Intermediate/root cert pin, connection allowed for host " + hostname);
                return null;
            }
        }
        // No pin matched the pins and we have checked the whole chain
        String errorMessage = TAG + "No matching Intermediate/root cert pins for host " + hostname;
        System.out.println(errorMessage);
        return errorMessage;
    }

    /**
     * Computes a base64 string based on the pinType variable. This could be a public key or certificate.
     *
     * @param cert the certificate to operate on
     * @param pinType the type of pinning to obtain
     * @return the base64 string of the computed pin or null if the pinning type is not recognized
    */
    private static String getCertPinForPinType(byte[] cert, String pinType) {
        X509Certificate x509Certificate;
        try {
            CertificateFactory cf = CertificateFactory.getInstance("X.509");
            x509Certificate = (X509Certificate) cf.generateCertificate(new ByteArrayInputStream(cert));
        } catch (Exception e) {
            System.out.println(TAG + "Unable to generate certificate: " + e.getMessage());
            return null;
        }
        if (pinType.equals("public-key-sha256")) {
            return publicKeyWithHeader(x509Certificate);
        } else {
            // Not implemented
            System.out.println(TAG + "Unrecognized pinType: " + pinType);
            return null;
        }
    }

    /**
     * Extract a public key pin from certificate and returns it (in Android this includes the header)
     *
     * @param cert the certification from which to extract the public key pin
     * @return nil if the key type in the certificate can not be recognized/extracted
     */
    private static String publicKeyWithHeader(X509Certificate cert){
        try {
            // Get the public key
            PublicKey publicKey = cert.getPublicKey();

            // Get the encoded form of the public key
            byte[] encoded = publicKey.getEncoded();
            if (encoded == null) {
                // Return null and let caller throw an exception with hostname
                return null;
            }

            // SHA-256 and Base64 encode the public key
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            byte[] hash = digest.digest(encoded);
            String encodedString = Base64.getEncoder().encodeToString(hash);

            return encodedString;
        } catch (Exception e) {
            System.out.println(TAG + "Unable to generate certificate: " + e.getMessage());
            return null;
        }
    }

    /**
     * Check if the Approov SDK contains the specified pin for host and pin type
     * 
     * @param host the host to check
     * @param targetPin the pin to check
     * @param pinsForHost list of pins for the host
     * @return true if the pin is in the Approov SDK, false otherwise
     */
    private static boolean checkPinForHostIsSetInApproov(String host, String targetPin, List<String> pinsForHost) {
        // Check if the pin is in the list of pins for the host
        if ((pinsForHost == null) || (pinsForHost.size() == 0)) {
            System.out.println(TAG + "Pin set is empty for host " + host);
            return false;
        }
        if (pinsForHost.contains(targetPin)) {
            System.out.println(TAG + "Pin " + targetPin + " found in Approov SDK pins for host " + host);
                return true;
            }
        System.out.println(TAG + "Pin " + targetPin + " NOT found in Approov SDK pins for host " + host);
        return false;
    }

    /**
     * Add a certificate to the cache
     *
     * @param key the key to use for the cache
     * @param value the value to store in the cache
     */
    private static void addToCache(String key, byte[] value) {
        certCacheLock.writeLock().lock();
        try {
            approovCertCache.put(key, value);
        } finally {
            certCacheLock.writeLock().unlock();
        }
    }

    /**
     * Get a value from the cache
     *
     * @param key the key to use for the cache
     * @return the value stored in the cache or null if the key is not found
     */
    private static byte[] getFromCache(String key) {
        certCacheLock.readLock().lock();
        try {
            return approovCertCache.get(key);
        } finally {
            certCacheLock.readLock().unlock();
        }
    }

    /**
     * Remove a certificate from the cache
     *
     * @param key the key to use for the cache
     */
    private static void removeFromCache(String key) {
        certCacheLock.writeLock().lock();
        try {
            approovCertCache.remove(key);
        } finally {
            certCacheLock.writeLock().unlock();
        }
    }

    /**
     * Clear the cache
     */
    public static void clearCertCache() {
        certCacheLock.writeLock().lock();
        try {
            approovCertCache.clear();
        } finally {
            certCacheLock.writeLock().unlock();
        }
    }

    /**
     * Get the list of pins for a given hostname from the Approov SDK
     * @param hostname the hostname to get the pins for
     * @param pinType the type of pin to get
     * @return the list of pins for the hostname or null if the pins are not found
     */
    public static List<String> getPinsForHost(String hostname, String pinType) {
        Map<String,List<String>> allPins = Approov.getPins(pinType);
        if(allPins == null) {
            return null;
        }
        return allPins.get(hostname);
    }
}
